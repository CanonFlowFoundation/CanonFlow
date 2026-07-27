namespace CanonFlow.Evaluator

open System
open System.IO
open CanonFlow.Assurance
open FsToolkit.ErrorHandling
open CanonFlow.Assurance.Signing
open CanonFlow.Assurance.Verification

type EvaluationRun = {
    Receipt: CanonFlowEvidenceReceipt
    CanonicalReceipt: string
    ReceiptDigest: string
    ExitCode: int
}

module Pipeline =
    let verdictOf assessment =
        match assessment.Health, assessment.Compliance with
        | EvidenceHealth.Broken _, _ -> Verdict.ToolFailure
        | _, Compliance.NonConformant _ -> Verdict.Fail
        | EvidenceHealth.Complete, Compliance.Conformant
            when assessment.ApplicableRules > 0
                 && assessment.EvaluatedRules = assessment.ApplicableRules -> Verdict.Pass
        | _ -> Verdict.Inconclusive

    let aggregate assessments =
        assessments
        |> List.map verdictOf
        |> function
            | [] -> Verdict.Inconclusive
            | head :: tail -> tail |> List.fold Verdict.join head

    let exitCode = function
        | Verdict.Pass -> 0
        | Verdict.Fail -> 1
        | Verdict.Inconclusive -> 2
        | Verdict.ToolFailure -> 3

    let private validateSubjectRoot root =
        let fullPath = Path.GetFullPath(root)
        if Directory.Exists(fullPath) then Ok fullPath
        else Error $"Subject root does not exist: {root}"

    let private validateArtifacts (root: string) (artifacts: string list) =
        let rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar) + string Path.DirectorySeparatorChar
        let validate (artifact: string) =
            let candidate =
                if Path.IsPathRooted(artifact) then Path.GetFullPath(artifact)
                else Path.GetFullPath(Path.Combine(root, artifact))
            if not (candidate.StartsWith(rootWithSeparator, StringComparison.Ordinal)) then
                Error $"Artifact escapes the subject root: {artifact}"
            elif File.Exists(candidate) then
                let info = FileInfo(candidate)
                let resolved =
                    if isNull info.LinkTarget then candidate
                    else info.ResolveLinkTarget(true).FullName |> Path.GetFullPath
                if resolved.StartsWith(rootWithSeparator, StringComparison.Ordinal) then Ok candidate
                else Error $"Artifact symlink escapes the subject root: {artifact}"
            else
                Error $"Subject artifact does not exist: {artifact}"
        artifacts
        |> List.map validate
        |> List.fold (fun state next ->
            match state, next with
            | Ok values, Ok value -> Ok (value :: values)
            | Error error, _ | _, Error error -> Error error) (Ok [])
        |> Result.map List.rev

    let evaluate manifestPath =
        result {
            let manifestFullPath = Path.GetFullPath(manifestPath)
            let manifestDirectory = Path.GetDirectoryName(manifestFullPath)
            let! json =
                try Ok (File.ReadAllText(manifestFullPath))
                with ex -> Error $"Cannot read evaluation manifest: {ex.Message}"
            let! manifest =
                ManifestParser.parse json
                |> Result.mapError (fun error -> $"Invalid evaluation manifest: {error}")
            let budget = manifest.Budget |> Option.defaultValue Budgets.Default
            let root =
                if Path.IsPathRooted(manifest.Subject.Root) then manifest.Subject.Root
                else Path.Combine(manifestDirectory, manifest.Subject.Root)
            let! subjectRoot = validateSubjectRoot root
            let! artifacts = validateArtifacts subjectRoot manifest.Subject.Artifacts
            if artifacts.Length > budget.MaxFiles then
                return! Error $"Subject contains {artifacts.Length} declared artifacts; budget permits {budget.MaxFiles}."
            let inputBytes = artifacts |> List.sumBy (fun artifact -> FileInfo(artifact).Length)
            if inputBytes > budget.MaxInputBytes then
                return! Error $"Subject contains {inputBytes} bytes; budget permits {budget.MaxInputBytes}."
            let manifestDigest = "sha256:" + Hash.computeSha256 json
            let artifactRecords =
                artifacts
                |> List.map (fun artifact ->
                    let relativePath =
                        Path.GetRelativePath(subjectRoot, artifact)
                            .Replace(Path.DirectorySeparatorChar, '/')
                    let digest =
                        File.ReadAllBytes(artifact)
                        |> Hash.computeSha256Bytes
                        |> Convert.ToHexString
                        |> fun value -> "sha256:" + value.ToLowerInvariant()
                    { Path = relativePath; Digest = digest })
            let replayIdentity =
                JObject [
                    "manifestDigest", JString manifestDigest
                    "artifacts", JArray (
                        artifactRecords
                        |> List.map (fun artifact ->
                            JObject [
                                "path", JString artifact.Path
                                "digest", JString artifact.Digest
                            ]))
                    "instant", JString manifest.EvaluationContext.Instant
                    "profiles", JArray (manifest.Profiles |> List.map JString)
                ]
                |> CanonicalReceiptJson.serialize
                |> Hash.computeSha256
                |> fun value -> "sha256:" + value
            let componentManifest = {
                manifest with
                    Subject = { manifest.Subject with Root = subjectRoot; Artifacts = artifacts }
            }

            let! assessments =
                manifest.Profiles
                |> List.map (fun profile ->
                    match profile with
                    | "fsassay-production-v1" ->
                        FsAssayRunner.run componentManifest budget
                        |> Async.RunSynchronously
                        |> Ok
                    | "ondc-retail-1.2.0-preview" ->
                        OndcRunner.run componentManifest budget
                    | unknown ->
                        Error $"Profile is not installed: {unknown}")
                |> List.fold (fun state next ->
                    match state, next with
                    | Ok accumulated, Ok assessment -> Ok (assessment :: accumulated)
                    | Error error, _ | _, Error error -> Error error) (Ok [])
                |> Result.map List.rev

            let verdict = aggregate assessments
            let unsignedReceipt = {
                SchemaVersion = "1.0"
                ReceiptType = "CanonFlowEvidenceReceipt"
                ReplayIdentity = replayIdentity
                Subject = {
                    Root = "."
                    Schema = manifest.Subject.Artifacts |> List.tryHead |> Option.defaultValue ""
                    SourceDirectories = manifest.Subject.Artifacts |> List.skip (min 1 manifest.Subject.Artifacts.Length)
                    ManifestDigest = Some manifestDigest
                    Artifacts = artifactRecords
                }
                Evaluator = { EngineId = "CanonFlow.Evaluator"; EngineVersion = "0.1.0-alpha" }
                Context = {
                    Instant = manifest.EvaluationContext.Instant
                    TimeProvenance = manifest.EvaluationContext.TimeProvenance
                    Locale = manifest.EvaluationContext.Locale
                    NetworkPolicy = manifest.EvaluationContext.Network
                }
                Assessments = assessments
                Verdict = verdict
                Seal = Some (Seal.createUnsigned ())
            }
            let! receipt =
                match manifest.Configuration |> Option.bind (fun configuration -> configuration.SealKeyPath) with
                | None -> Ok unsignedReceipt
                | Some keyPath ->
                    try
                        let resolvedKeyPath =
                            if Path.IsPathRooted(keyPath) then keyPath
                            else Path.Combine(manifestDirectory, keyPath)
                        let keyText = File.ReadAllText(resolvedKeyPath).Trim()
                        let keyBytes = Convert.FromHexString(keyText)
                        match PrivateKey.create keyBytes with
                        | Error error -> Error error
                        | Ok privateKey ->
                            let keyId =
                                manifest.Configuration
                                |> Option.bind (fun configuration -> configuration.SealKeyId)
                                |> Option.defaultValue "local:unspecified"
                            Ok (ReceiptVerifier.signReceipt keyId privateKey unsignedReceipt)
                    with ex -> Error $"Cannot seal receipt: {ex.Message}"
            let canonicalPayload =
                match receipt.Seal with
                | Some seal when seal.Status = SealStatus.Signed -> CanonicalReceiptJson.serializeSigningPayload receipt
                | _ -> CanonicalReceiptJson.serializeEnvelope receipt
            let envelope = CanonicalReceiptJson.serializeEnvelope receipt
            return {
                Receipt = receipt
                CanonicalReceipt = envelope
                ReceiptDigest = "sha256:" + Hash.computeSha256 canonicalPayload
                ExitCode = exitCode verdict
            }
        }
