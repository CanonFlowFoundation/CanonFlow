namespace CanonFlow.Reports

open System
open System.Net
open CanonFlow.Assurance

module HtmlReport =
    let private listItems emptyText values =
        match values with
        | [] -> $"<li>{WebUtility.HtmlEncode(emptyText)}</li>"
        | entries ->
            entries
            |> List.map (fun (owner, description) ->
                $"<li><strong>{WebUtility.HtmlEncode(owner)}</strong>: {WebUtility.HtmlEncode(description)}</li>")
            |> String.concat ""

    let generate (receipt: CanonFlowEvidenceReceiptV11) =
        let verdict = receipt.Verdict |> ReceiptText.verdict |> WebUtility.HtmlEncode
        let assessments =
            receipt.Assessments
            |> List.map (fun assessment ->
                let componentName = WebUtility.HtmlEncode(assessment.ComponentId)
                let health = WebUtility.HtmlEncode(ReceiptText.health assessment.Health)
                let compliance = WebUtility.HtmlEncode(ReceiptText.compliance assessment.Compliance)
                $"<li><strong>{componentName}</strong>: health={health}; compliance={compliance}; rules={assessment.EvaluatedRules}/{assessment.ApplicableRules}</li>")
            |> String.concat ""
        let constructiveAssessments =
            receipt.ConstructiveAssessments
            |> List.map (fun assessment ->
                let obligation = WebUtility.HtmlEncode(assessment.ObligationId)
                let state = WebUtility.HtmlEncode(assessment.ProjectionState)
                let verdict = WebUtility.HtmlEncode(ReceiptText.verdict assessment.Verdict)
                $"<li><strong>{obligation}</strong>: projection={state}; verdict={verdict}; gates={assessment.EvaluatedGates}/{assessment.RequiredGates}</li>")
            |> String.concat ""
        let evidence =
            receipt.Assessments
            |> List.collect (fun assessment ->
                assessment.Evidence
                |> List.map (fun item ->
                    let value = item.Value |> Option.defaultValue "not supplied"
                    assessment.ComponentId,
                    $"{item.Kind} — {item.Path} — {value}"))
        let digest = ReportCommon.receiptDigest receipt |> WebUtility.HtmlEncode
        $"""<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>CanonFlow Evaluation Report</title>
    <style>body{{font-family:system-ui,sans-serif;max-width:72rem;margin:2rem auto;padding:0 1rem}}code{{overflow-wrap:anywhere}}.warning{{border-left:.3rem solid #b45309;padding-left:1rem}}</style>
</head>
<body>
    <h1>CanonFlow Evaluation Report</h1>
    <p>Verdict: <strong>{verdict}</strong></p>
    <p>Receipt digest: <code>{digest}</code></p>
    <p class="warning">Derived view only. This report is not a certificate; verify assessment.cff independently.</p>
    <h2>Components</h2>
    <ul>{assessments}</ul>
    <h2>Constructive components</h2>
    <ul>{constructiveAssessments}</ul>
    <h2>Findings</h2>
    <ul>{listItems "No proven violations." (ReportCommon.findings receipt)}</ul>
    <h2>Missing evidence and unsupported checks</h2>
    <ul>{listItems "No named evidence gaps." (ReportCommon.gaps receipt)}</ul>
    <h2>Tool failures</h2>
    <ul>{listItems "No tool failures." (ReportCommon.toolFailures receipt)}</ul>
    <h2>Evidence references</h2>
    <ul>{listItems "No evidence references." evidence}</ul>
</body>
</html>"""

