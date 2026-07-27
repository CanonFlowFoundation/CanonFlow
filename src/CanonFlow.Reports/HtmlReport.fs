namespace CanonFlow.Reports

open System
open CanonFlow.Assurance

module HtmlReport =

    let generate (receipt: CanonFlowEvidenceReceipt) =
        $"""<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>CanonFlow Evaluation Report</title>
</head>
<body>
    <h1>CanonFlow Evaluation Report</h1>
    <p>Verdict: <strong>{receipt.Verdict}</strong></p>
    <p>This is a derived view from assessment.cff</p>
</body>
</html>"""

