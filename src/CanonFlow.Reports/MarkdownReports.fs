namespace CanonFlow.Reports

open System
open CanonFlow.Assurance

module MarkdownReports =
    let private lines emptyText values =
        match values with
        | [] -> $"- {emptyText}"
        | entries ->
            entries
            |> List.map (fun (owner, description) -> $"- {owner}: {description}")
            |> String.concat "\n"

    let generateEvidence (receipt: CanonFlowEvidenceReceipt) =
        let evidence =
            receipt.Assessments
            |> List.collect (fun assessment ->
                assessment.Evidence
                |> List.map (fun item ->
                    let value = item.Value |> Option.defaultValue "not supplied"
                    let provenance = item.Provenance |> Option.defaultValue "not supplied"
                    assessment.ComponentId,
                    $"{item.Kind}; path={item.Path}; value={value}; provenance={provenance}"))
        $"""# EVIDENCE

This document summarizes the evidence presented in the receipt.
Verdict: {ReceiptText.verdict receipt.Verdict}
Receipt digest: {ReportCommon.receiptDigest receipt}

{lines "No evidence references." evidence}
"""

    let generateLoss (receipt: CanonFlowEvidenceReceipt) =
        let incompleteCoverage =
            receipt.Assessments
            |> List.choose (fun assessment ->
                if assessment.EvaluatedRules < assessment.ApplicableRules then
                    Some (
                        assessment.ComponentId,
                        $"{assessment.ApplicableRules - assessment.EvaluatedRules} applicable rules were not evaluated.")
                else None)
        $"""# LOSS

This document names evidence gaps, unsupported checks, tool failures, and proven violations. It is not a coverage score.

## Missing evidence

{lines "No named evidence gaps." (ReportCommon.gaps receipt)}

## Unevaluated rules

{lines "No unevaluated applicable rules." incompleteCoverage}

## Tool failures

{lines "No tool failures." (ReportCommon.toolFailures receipt)}

## Proven violations

{lines "No proven violations." (ReportCommon.findings receipt)}
"""

