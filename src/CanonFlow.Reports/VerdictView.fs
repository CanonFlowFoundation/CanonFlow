namespace CanonFlow.Reports

open System
open CanonFlow.Assurance

module VerdictView =
    open Thoth.Json.Net

    let generate (receipt: CanonFlowEvidenceReceiptV11) =
        let exitCode = ExitCode.ofVerdict receipt.Verdict
        let components =
            receipt.Assessments
            |> List.map (fun assessment ->
                Encode.object [
                    "id", Encode.string assessment.ComponentId
                    "version", Encode.string assessment.ComponentVersion
                    "health", Encode.string (ReceiptText.health assessment.Health)
                    "compliance", Encode.string (ReceiptText.compliance assessment.Compliance)
                    "applicableRules", Encode.int assessment.ApplicableRules
                    "evaluatedRules", Encode.int assessment.EvaluatedRules
                    "evidence", assessment.Evidence |> List.map (fun evidence ->
                        Encode.object [
                            "path", Encode.string evidence.Path
                            "kind", Encode.string evidence.Kind
                            "value", evidence.Value |> Option.map Encode.string |> Option.defaultValue Encode.nil
                            "provenance", evidence.Provenance |> Option.map Encode.string |> Option.defaultValue Encode.nil
                        ]) |> Encode.list
                ])
        let pairs values =
            values
            |> List.map (fun (owner, description) ->
                Encode.object [
                    "component", Encode.string owner
                    "description", Encode.string description
                ])
            |> Encode.list
        let constructiveComponents =
            receipt.ConstructiveAssessments
            |> List.map (fun assessment ->
                Encode.object [
                    "obligationId", Encode.string assessment.ObligationId
                    "projectionState", Encode.string assessment.ProjectionState
                    "derivationKind", Encode.string assessment.DerivationKind
                    "derivationReference",
                        assessment.DerivationReference
                        |> Option.map Encode.string
                        |> Option.defaultValue Encode.nil
                    "sourceDigest", Encode.string assessment.SourceDigest
                    "manifestDigest", Encode.string assessment.ManifestDigest
                    "verdict", Encode.string (ReceiptText.verdict assessment.Verdict)
                    "requiredGates", Encode.int assessment.RequiredGates
                    "evaluatedGates", Encode.int assessment.EvaluatedGates
                    "missingGateIds",
                        assessment.MissingGateIds
                        |> List.map Encode.string
                        |> Encode.list
                ])
        let subjectDigests =
            receipt.Assessments
            |> List.collect (fun assessment -> assessment.Evidence)
            |> List.filter (fun evidence -> evidence.Kind.EndsWith("Digest", StringComparison.Ordinal))
            |> List.map (fun evidence ->
                Encode.object [
                    "path", Encode.string evidence.Path
                    "kind", Encode.string evidence.Kind
                    "value", evidence.Value |> Option.map Encode.string |> Option.defaultValue Encode.nil
                ])
        let json = Encode.object [
            "verdict", Encode.string (ReceiptText.verdict receipt.Verdict)
            "exitCode", Encode.int exitCode
            "receiptDigest", Encode.string (ReportCommon.receiptDigest receipt)
            "components", Encode.list components
            "constructiveComponents", Encode.list constructiveComponents
            "findings", ReportCommon.findings receipt |> pairs
            "missingEvidence", ReportCommon.gaps receipt |> pairs
            "toolFailures", ReportCommon.toolFailures receipt |> pairs
            "subjectDigests", Encode.list subjectDigests
        ]
        Encode.toString 4 json

