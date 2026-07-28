namespace CanonFlow.Evaluator

open System
open System.IO
open System.Text.Json
open CanonFlow.Assurance

module FsAssayRunner =
    let private profileId = "fsassay-production-v1"
    let private profileDigest = "sha256:ad0f2c8bca7a08d5022ca54eec83867a9839186a4422ec781adde57f6442da8a"
    let private toolVersion = "1.0.4"
    let private admittedRuleCount = 21

    type private ParsedFinding = {
        File: string
        Code: string
        Message: string
        StartLine: int
        StartColumn: int
        EndLine: int
        EndColumn: int
    }

    type private ParsedOutput = {
        ScannedFiles: string list
        Findings: ParsedFinding list
    }

    let private normalizePath (subjectRoot: string) (sourcePath: string) =
        let normalized =
            if Path.IsPathRooted(sourcePath) then
                Path.GetRelativePath(subjectRoot, Path.GetFullPath(sourcePath))
            else sourcePath
        normalized.Replace(Path.DirectorySeparatorChar, '/')

    let private parseFindings subjectRoot jsonPath =
        try
            use document = JsonDocument.Parse(File.ReadAllText(jsonPath))
            if document.RootElement.ValueKind <> JsonValueKind.Array then
                Error "FsAssay JSON output is not an array."
            else
                let fileResults =
                    document.RootElement.EnumerateArray()
                    |> Seq.map (fun fileResult ->
                        let sourceFile =
                            fileResult.GetProperty("file").GetString()
                            |> normalizePath subjectRoot
                        let findings =
                            fileResult.GetProperty("violations").EnumerateArray()
                            |> Seq.map (fun violation ->
                                {
                                    File = sourceFile
                                    Code = violation.GetProperty("code").GetString()
                                    Message = violation.GetProperty("message").GetString()
                                    StartLine = violation.GetProperty("startLine").GetInt32()
                                    StartColumn = violation.GetProperty("startColumn").GetInt32()
                                    EndLine = violation.GetProperty("endLine").GetInt32()
                                    EndColumn = violation.GetProperty("endColumn").GetInt32()
                                })
                            |> Seq.toList
                        sourceFile, findings)
                    |> Seq.toList
                let findings =
                    fileResults
                    |> List.collect snd
                    |> List.sortBy (fun finding ->
                        finding.File,
                        finding.StartLine,
                        finding.StartColumn,
                        finding.Code)
                Ok {
                    ScannedFiles =
                        fileResults
                        |> List.map fst
                        |> List.filter (fun path -> path.EndsWith(".fs", StringComparison.OrdinalIgnoreCase))
                        |> List.sort
                    Findings = findings
                }
        with ex ->
            Error $"FsAssay JSON ingestion failed: {ex.Message}"

    let private validateSarif sarifPath =
        try
            use document = JsonDocument.Parse(File.ReadAllText(sarifPath))
            let hasVersion, version = document.RootElement.TryGetProperty("version")
            let hasRuns, runs = document.RootElement.TryGetProperty("runs")
            if hasVersion
               && version.ValueKind = JsonValueKind.String
               && hasRuns
               && runs.ValueKind = JsonValueKind.Array then Ok ()
            else Error "FsAssay SARIF output is missing version or runs."
        with ex ->
            Error $"FsAssay SARIF ingestion failed: {ex.Message}"

    let private broken description =
        {
            ComponentId = "fsassay"
            ComponentVersion = toolVersion
            Health = EvidenceHealth.Broken { Description = description }
            Compliance = Compliance.NotEstablished
            ApplicableRules = admittedRuleCount
            EvaluatedRules = 0
            Evidence = [
                {
                    Path = "profiles/fsassay-production-v1/profile.json"
                    Kind = "RulePackDigest"
                    Value = Some profileDigest
                    Provenance = Some profileId
                }
            ]
        }

    let run (manifest: EvaluationManifest) (budget: EvaluationBudget) =
        let targets =
            manifest.Subject.Artifacts
            |> List.filter (fun artifact -> artifact.EndsWith(".fs", StringComparison.OrdinalIgnoreCase))
            |> List.map Path.GetFullPath
            |> List.distinct
            |> List.sort
        let executable =
            match Environment.GetEnvironmentVariable("CANONFLOW_FSASSAY_PATH") with
            | value when not (String.IsNullOrWhiteSpace(value)) -> value
            | _ -> "fsassay"
        let evidenceDirectory = Path.Combine(Path.GetTempPath(), "canonflow-fsassay-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(evidenceDirectory) |> ignore
        let jsonPath = Path.Combine(evidenceDirectory, "findings.json")
        let sarifPath = Path.Combine(evidenceDirectory, "findings.sarif")
        let args = [
            "--out-json"; jsonPath
            "--out-sarif"; sarifPath
            "--profile"; "core"
            "--files"; String.concat "," targets
            manifest.Subject.Root
        ]

        async {
            let requestedProfile =
                manifest.Configuration
                |> Option.bind (fun configuration -> configuration.FsassayRulePack)
            if requestedProfile.IsSome
               && requestedProfile <> Some profileId
               && requestedProfile <> Some $"{profileId}@{profileDigest}" then
                return broken "The manifest attempted to replace the protected FsAssay production profile."
            elif List.isEmpty targets then
                return {
                    ComponentId = "fsassay"
                    ComponentVersion = toolVersion
                    Health =
                        EvidenceHealth.Partial (
                            NonEmpty.create
                                { Description = "No declared F# source artifacts were available to scan." }
                                [])
                    Compliance = Compliance.NotEstablished
                    ApplicableRules = admittedRuleCount
                    EvaluatedRules = 0
                    Evidence = [
                        {
                            Path = "profiles/fsassay-production-v1/profile.json"
                            Kind = "RulePackDigest"
                            Value = Some profileDigest
                            Provenance = Some profileId
                        }
                    ]
                }
            elif targets |> List.exists (fun target -> target.Contains(',')) then
                return broken "A declared F# artifact contains a comma and cannot be represented by the FsAssay explicit-file protocol."
            else
                let! result = ComponentRunner.runComponentAsync executable args budget
                let findings =
                    if File.Exists(jsonPath) then parseFindings manifest.Subject.Root jsonPath
                    else Error "FsAssay did not produce its required JSON output."
                let sarif =
                    if File.Exists(sarifPath) then validateSarif sarifPath
                    else Error "FsAssay did not produce its required SARIF output."
                match result.ExitCode, findings, sarif with
                | (0 | 1 | 2), Ok parsedOutput, Ok () ->
                    let expectedFiles =
                        targets
                        |> List.map (normalizePath manifest.Subject.Root)
                        |> List.sort
                    if parsedOutput.ScannedFiles <> expectedFiles then
                        let declaredFiles = String.concat "; " expectedFiles
                        let scannedFiles = String.concat "; " parsedOutput.ScannedFiles
                        return
                            broken
                                $"FsAssay scanned-set mismatch. Declared [{declaredFiles}], scanned [{scannedFiles}]."
                    else
                        let parsedFindings = parsedOutput.Findings
                        let normalizedFindings =
                            parsedFindings
                            |> List.map (fun finding ->
                                $"{finding.Code}|{finding.File}|{finding.StartLine}|{finding.StartColumn}|{finding.EndLine}|{finding.EndColumn}|{finding.Message}")
                            |> String.concat "\n"
                        let evidence = [
                            {
                                Path = "profiles/fsassay-production-v1/profile.json"
                                Kind = "RulePackDigest"
                                Value = Some profileDigest
                                Provenance = Some profileId
                            }
                            {
                                Path = "fsassay/findings"
                                Kind = "NormalizedFindingSetDigest"
                                Value = Some ("sha256:" + Hash.computeSha256 normalizedFindings)
                                Provenance = Some $"FsAssay.Cli@{toolVersion} JSON+SARIF"
                            }
                            {
                                Path = "fsassay/finding-count"
                                Kind = "FindingCount"
                                Value = Some (string parsedFindings.Length)
                                Provenance = Some $"FsAssay.Cli@{toolVersion}"
                            }
                            {
                                Path = "fsassay/declared-file-count"
                                Kind = "DeclaredApplicableFileCount"
                                Value = Some (string expectedFiles.Length)
                                Provenance = Some profileDigest
                            }
                            {
                                Path = "fsassay/scanned-file-count"
                                Kind = "ScannedFileCount"
                                Value = Some (string parsedOutput.ScannedFiles.Length)
                                Provenance = Some $"FsAssay.Cli@{toolVersion}"
                            }
                            for scannedFile in parsedOutput.ScannedFiles do
                                {
                                    Path = $"fsassay/scanned/{scannedFile}"
                                    Kind = "ScannedFile"
                                    Value = None
                                    Provenance = Some $"FsAssay.Cli@{toolVersion}"
                                }
                            for finding in parsedFindings do
                                {
                                    Path = $"fsassay/findings/{finding.Code}/{finding.File}:{finding.StartLine}:{finding.StartColumn}"
                                    Kind = "FsAssayFinding"
                                    Value = Some finding.Message
                                    Provenance = Some profileDigest
                                }
                            {
                                Path = "fsassay/exit-code"
                                Kind = "ProcessExitCode"
                                Value = Some (string result.ExitCode)
                                Provenance = Some $"FsAssay.Cli@{toolVersion}"
                            }
                        ]
                        return {
                            ComponentId = "fsassay"
                            ComponentVersion = toolVersion
                            Health =
                                if result.ExitCode = 2 then
                                    EvidenceHealth.Partial (
                                        NonEmpty.create
                                            { Description = "FsAssay reported an inconclusive finding from outside the admitted blocking subset." }
                                            [])
                                else EvidenceHealth.Complete
                            Compliance =
                                match result.ExitCode with
                                | 0 -> Compliance.Conformant
                                | 1 ->
                                    match
                                        parsedFindings
                                        |> List.map (fun finding ->
                                            ({ Description = $"{finding.Code}: {finding.Message}" } : Finding))
                                        |> NonEmpty.ofList
                                    with
                                    | Ok findings -> Compliance.NonConformant findings
                                    | Error _ ->
                                        Compliance.NonConformant (NonEmpty.create { Description = "FsAssay reported policy violations." } [])
                                | _ -> Compliance.NotEstablished
                            ApplicableRules = admittedRuleCount
                            EvaluatedRules = admittedRuleCount
                            Evidence = evidence
                        }
                | _, Error error, _ -> return broken error
                | _, _, Error error -> return broken error
                | _ -> return broken result.Error
        }

