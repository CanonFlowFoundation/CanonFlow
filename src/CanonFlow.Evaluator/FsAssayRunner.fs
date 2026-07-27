namespace CanonFlow.Evaluator

open System
open System.IO
open System.Text.Json
open CanonFlow.Assurance

module FsAssayRunner =
    let private profileId = "fsassay-production-v1"
    let private profileDigest = "sha256:342e02fe2071ecda7ac5f764de9212f243fe825f0cb6e2743dac749b5c27d8ab"

    type private ParsedFinding = {
        File: string
        Code: string
        Message: string
        StartLine: int
        StartColumn: int
        EndLine: int
        EndColumn: int
    }

    let private parseFindings jsonPath =
        try
            use document = JsonDocument.Parse(File.ReadAllText(jsonPath))
            if document.RootElement.ValueKind <> JsonValueKind.Array then
                Error "FsAssay JSON output is not an array."
            else
                document.RootElement.EnumerateArray()
                |> Seq.collect (fun fileResult ->
                    let sourceFile = fileResult.GetProperty("file").GetString() |> Path.GetFileName
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
                        }))
                |> Seq.sortBy (fun finding ->
                    finding.File,
                    finding.StartLine,
                    finding.StartColumn,
                    finding.Code)
                |> Seq.toList
                |> Ok
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
            ComponentVersion = "1.0.1"
            Health = EvidenceHealth.Broken { Description = description }
            Compliance = Compliance.NotEstablished
            ApplicableRules = 1
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
        let target =
            manifest.Subject.Artifacts
            |> List.tryFind (fun artifact -> artifact.EndsWith(".fs", StringComparison.OrdinalIgnoreCase))
            |> Option.defaultValue manifest.Subject.Root
        let executable =
            match Environment.GetEnvironmentVariable("CANONFLOW_FSASSAY_PATH") with
            | value when not (String.IsNullOrWhiteSpace(value)) -> value
            | _ -> "fsassay"
        let evidenceDirectory = Path.Combine(Path.GetTempPath(), "canonflow-fsassay-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(evidenceDirectory) |> ignore
        let jsonPath = Path.Combine(evidenceDirectory, "findings.json")
        let sarifPath = Path.Combine(evidenceDirectory, "findings.sarif")
        let args = [ "--out-json"; jsonPath; "--out-sarif"; sarifPath; "--profile"; "core"; target ]

        async {
            let requestedProfile =
                manifest.Configuration
                |> Option.bind (fun configuration -> configuration.FsassayRulePack)
            if requestedProfile.IsSome
               && requestedProfile <> Some profileId
               && requestedProfile <> Some $"{profileId}@{profileDigest}" then
                return broken "The manifest attempted to replace the protected FsAssay production profile."
            else
                let! result = ComponentRunner.runComponentAsync executable args budget
                let findings =
                    if File.Exists(jsonPath) then parseFindings jsonPath
                    else Error "FsAssay did not produce its required JSON output."
                let sarif =
                    if File.Exists(sarifPath) then validateSarif sarifPath
                    else Error "FsAssay did not produce its required SARIF output."
                match result.ExitCode, findings, sarif with
                | (0 | 1), Ok parsedFindings, Ok () ->
                    let normalizedFindings =
                        parsedFindings
                        |> List.map (fun finding ->
                            $"{finding.Code}|{finding.File}|{finding.StartLine}|{finding.StartColumn}|{finding.EndLine}|{finding.EndColumn}|{finding.Message}")
                        |> String.concat "\n"
                    let verdict = ComponentRunner.mapExitCodeToVerdict result.ExitCode
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
                            Provenance = Some "FsAssay.Cli@1.0.1 JSON+SARIF"
                        }
                        {
                            Path = "fsassay/finding-count"
                            Kind = "FindingCount"
                            Value = Some (string parsedFindings.Length)
                            Provenance = Some "FsAssay.Cli@1.0.1"
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
                            Provenance = Some "FsAssay.Cli@1.0.1"
                        }
                    ]
                    return {
                        ComponentId = "fsassay"
                        ComponentVersion = "1.0.1"
                        Health = EvidenceHealth.Complete
                        Compliance =
                            match verdict with
                            | Verdict.Pass -> Compliance.Conformant
                            | Verdict.Fail ->
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
                        ApplicableRules = 1
                        EvaluatedRules = 1
                        Evidence = evidence
                    }
                | _, Error error, _ -> return broken error
                | _, _, Error error -> return broken error
                | _ -> return broken result.Error
        }

