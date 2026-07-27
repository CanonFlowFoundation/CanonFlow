namespace CanonFlow.Reports

open CanonFlow.Assurance
open Thoth.Json.Net

module SarifReport =
    let private sourceLocation (assessment: ComponentAssessmentRecord) (description: string) =
        let code =
            match description.IndexOf(':') with
            | index when index > 0 -> description.Substring(0, index)
            | _ -> assessment.ComponentId
        let evidence =
            assessment.Evidence
            |> List.tryFind (fun item ->
                item.Kind = "FsAssayFinding"
                && item.Path.StartsWith($"fsassay/findings/{code}/", System.StringComparison.Ordinal))
        let location =
            evidence
            |> Option.bind (fun item ->
                let marker = $"fsassay/findings/{code}/"
                let address = item.Path.Substring(marker.Length)
                let segments = address.Split(':')
                if segments.Length = 3 then
                    match System.Int32.TryParse(segments[1]), System.Int32.TryParse(segments[2]) with
                    | (true, line), (true, column) ->
                        Some (segments[0], line, column)
                    | _ -> None
                else None)
        code, location

    let generate (receipt: CanonFlowEvidenceReceipt) =
        let results =
            receipt.Assessments
            |> List.collect (fun assessment ->
                match assessment.Compliance with
                | Compliance.NonConformant findings ->
                    findings
                    |> NonEmpty.toList
                    |> List.map (fun finding ->
                        let ruleId, location = sourceLocation assessment finding.Description
                        let baseProperties = [
                            "ruleId", Encode.string ruleId
                            "level", Encode.string "error"
                            "message", Encode.object [ "text", Encode.string finding.Description ]
                        ]
                        let properties =
                            match location with
                            | Some (path, line, column) ->
                                baseProperties @ [
                                    "locations", Encode.list [
                                        Encode.object [
                                            "physicalLocation", Encode.object [
                                                "artifactLocation", Encode.object [ "uri", Encode.string path ]
                                                "region", Encode.object [
                                                    "startLine", Encode.int line
                                                    "startColumn", Encode.int column
                                                ]
                                            ]
                                        ]
                                    ]
                                ]
                            | None -> baseProperties
                        Encode.object properties)
                | _ -> [])
        Encode.object [
            "version", Encode.string "2.1.0"
            "$schema", Encode.string "https://json.schemastore.org/sarif-2.1.0.json"
            "runs", Encode.list [
                Encode.object [
                    "tool", Encode.object [
                        "driver", Encode.object [ "name", Encode.string "CanonFlow Evaluator" ]
                    ]
                    "results", Encode.list results
                ]
            ]
        ]
        |> Encode.toString 2
