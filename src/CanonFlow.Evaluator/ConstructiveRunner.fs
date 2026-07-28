namespace CanonFlow.Evaluator

open System
open System.IO
open System.Text.Json
open CanonFlow.Assurance
open FsToolkit.ErrorHandling

[<RequireQualifiedAccess>]
module ConstructiveRunner =
    let ProfileId = "required-contact-constructive-v1"

    let private exactObject label names (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            Error $"{label} must be an object."
        else
            let actualProperties = element.EnumerateObject() |> Seq.toList
            let actualNames = actualProperties |> List.map _.Name
            if actualNames.Length <> (actualNames |> Set.ofList |> Set.count) then
                Error $"{label} contains duplicate fields."
            elif Set.ofList actualNames <> Set.ofList names then
                Error $"{label} fields do not match the constructive evidence schema."
            else Ok ()

    let private stringValue label (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.String then
            Error $"{label} must be a string."
        else
            let value = element.GetString()
            if String.IsNullOrWhiteSpace(value) then Error $"{label} is required."
            else Ok value

    let private configuredArtifact
        (manifest: EvaluationManifest)
        (configuredPath: string)
        =
        let root = Path.GetFullPath(manifest.Subject.Root)
        let rootPrefix =
            root.TrimEnd(Path.DirectorySeparatorChar)
            + string Path.DirectorySeparatorChar
        let fullPath =
            if Path.IsPathRooted(configuredPath) then Path.GetFullPath(configuredPath)
            else Path.GetFullPath(Path.Combine(root, configuredPath))
        let declared =
            manifest.Subject.Artifacts
            |> List.map Path.GetFullPath
            |> Set.ofList
        if not (fullPath.StartsWith(rootPrefix, StringComparison.Ordinal)) then
            Error $"Constructive artifact escapes the subject root: {configuredPath}"
        elif not (declared.Contains(fullPath)) then
            Error $"Constructive artifact is not declared by the subject: {configuredPath}"
        elif not (File.Exists(fullPath)) then
            Error $"Constructive artifact does not exist: {configuredPath}"
        else Ok fullPath

    let private parseVerdict label (element: JsonElement) =
        result {
            let! value = stringValue label element
            match value with
            | "Pass" -> return Verdict.Pass
            | "Inconclusive" -> return Verdict.Inconclusive
            | "Fail" -> return Verdict.Fail
            | "ToolFailure" -> return Verdict.ToolFailure
            | _ -> return! Error $"{label} has unsupported verdict '{value}'."
        }

    let private parseEvidence
        (manifest: EvaluationManifest)
        label
        (element: JsonElement)
        =
        result {
            do! exactObject label ["digest"; "kind"; "path"; "provenance"] element
            let! kind = stringValue $"{label}.kind" (element.GetProperty("kind"))
            let! path = stringValue $"{label}.path" (element.GetProperty("path"))
            let! digest = stringValue $"{label}.digest" (element.GetProperty("digest"))
            let provenanceElement = element.GetProperty("provenance")
            let! provenance =
                if provenanceElement.ValueKind = JsonValueKind.Null then Ok None
                else stringValue $"{label}.provenance" provenanceElement |> Result.map Some
            let! evidence = ConstructiveEvidence.create kind path digest provenance
            let! evidencePath = configuredArtifact manifest path
            let actualDigest =
                File.ReadAllBytes(evidencePath)
                |> Digest.sha256Bytes
                |> Digest.toString
            if actualDigest <> digest then
                return!
                    Error $"{label}.digest does not match declared artifact '{path}'."
            return evidence
        }

    let private parseObservation
        (manifest: EvaluationManifest)
        index
        (element: JsonElement)
        =
        let label = $"observations[{index}]"
        result {
            do!
                exactObject
                    label
                    [
                        "evidence"
                        "gateId"
                        "gateVersion"
                        "implementationDigest"
                        "obligationId"
                        "verdict"
                    ]
                    element
            let! obligationId =
                stringValue $"{label}.obligationId" (element.GetProperty("obligationId"))
            let! gateIdText =
                stringValue $"{label}.gateId" (element.GetProperty("gateId"))
            let! gateId = ProofGateId.create gateIdText
            let! gateVersion =
                stringValue $"{label}.gateVersion" (element.GetProperty("gateVersion"))
            let! implementationDigestText =
                stringValue
                    $"{label}.implementationDigest"
                    (element.GetProperty("implementationDigest"))
            let! implementationDigest = Digest.parse implementationDigestText
            let! verdict = parseVerdict $"{label}.verdict" (element.GetProperty("verdict"))
            let evidenceElement = element.GetProperty("evidence")
            if evidenceElement.ValueKind <> JsonValueKind.Array then
                return! Error $"{label}.evidence must be an array."
            let! evidence =
                evidenceElement.EnumerateArray()
                |> Seq.mapi (fun evidenceIndex evidence ->
                    parseEvidence
                        manifest
                        $"{label}.evidence[{evidenceIndex}]"
                        evidence)
                |> Seq.fold (fun state next ->
                    match state, next with
                    | Ok values, Ok value -> Ok (value :: values)
                    | Error error, _ | _, Error error -> Error error) (Ok [])
                |> Result.map List.rev
            return
                obligationId,
                ({
                    GateId = gateId
                    GateVersion = gateVersion
                    ImplementationDigest = implementationDigest
                    Verdict = verdict
                    Evidence = evidence
                }: ConstructiveGateObservation)
        }

    let private parseEvidenceBundle
        (manifest: EvaluationManifest)
        expectedManifestDigest
        path
        maxDepth
        =
        try
            use document =
                JsonDocument.Parse(
                    File.ReadAllBytes(path),
                    JsonDocumentOptions(
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow,
                        MaxDepth = maxDepth))
            let root = document.RootElement
            result {
                do!
                    exactObject
                        "constructiveEvidence"
                        ["manifestDigest"; "observations"; "schemaVersion"]
                        root
                let! schemaVersion =
                    stringValue "schemaVersion" (root.GetProperty("schemaVersion"))
                if schemaVersion <> "1.0" then
                    return! Error $"Unsupported constructive evidence schema '{schemaVersion}'."
                let! manifestDigest =
                    stringValue "manifestDigest" (root.GetProperty("manifestDigest"))
                if manifestDigest <> Digest.toString expectedManifestDigest then
                    return! Error "Constructive evidence manifestDigest does not match the obligation manifest."
                let observations = root.GetProperty("observations")
                if observations.ValueKind <> JsonValueKind.Array then
                    return! Error "constructiveEvidence.observations must be an array."
                return!
                    observations.EnumerateArray()
                    |> Seq.mapi (parseObservation manifest)
                    |> Seq.fold (fun state next ->
                        match state, next with
                        | Ok values, Ok value -> Ok (value :: values)
                        | Error error, _ | _, Error error -> Error error) (Ok [])
                    |> Result.map List.rev
            }
        with ex ->
            Error $"Constructive evidence parsing failed: {ex.Message}"

    let run (manifest: EvaluationManifest) (budget: EvaluationBudget) =
        result {
            let! configuration =
                match manifest.Configuration with
                | Some configuration -> Ok configuration
                | None -> Error "Constructive profile requires configuration."
            let! manifestPathValue =
                match configuration.ObligationManifestPath with
                | Some path -> Ok path
                | None ->
                    Error "Constructive profile requires obligationManifestPath."
            let! manifestPath = configuredArtifact manifest manifestPathValue
            let manifestBytes = File.ReadAllBytes(manifestPath)
            let! obligationManifest = ObligationManifest.parseBytes manifestBytes
            let manifestDigest = manifestBytes |> Digest.sha256Bytes
            let! observations =
                match configuration.ConstructiveEvidencePath with
                | None -> Ok []
                | Some evidencePathValue ->
                    result {
                        let! evidencePath =
                            configuredArtifact manifest evidencePathValue
                        return!
                            parseEvidenceBundle
                                manifest
                                manifestDigest
                                evidencePath
                                budget.MaxJsonDepth
                    }
            let obligations =
                obligationManifest
                |> ObligationManifest.obligations
                |> NonEmpty.toList
            let knownObligations =
                obligations
                |> List.map (Obligation.id >> ObligationId.value)
                |> Set.ofList
            let unknownObservation =
                observations
                |> List.tryFind (fun (obligationId, _) ->
                    not (knownObligations.Contains(obligationId)))
            match unknownObservation with
            | Some (obligationId, _) ->
                return!
                    Error $"Constructive evidence names unknown obligation '{obligationId}'."
            | None ->
                return!
                    obligations
                    |> List.map (fun obligation ->
                        let obligationId =
                            obligation
                            |> Obligation.id
                            |> ObligationId.value
                        observations
                        |> List.choose (fun (observedObligationId, observation) ->
                            if observedObligationId = obligationId then Some observation
                            else None)
                        |> ConstructiveAssessment.create manifestDigest obligation)
                    |> List.fold (fun state next ->
                        match state, next with
                        | Ok values, Ok value -> Ok (value :: values)
                        | Error error, _ | _, Error error -> Error error) (Ok [])
                    |> Result.map List.rev
        }
