namespace CanonFlow.Reports

open System
open CanonFlow.Assurance

module MarkdownReports =

    let generateEvidence (receipt: CanonFlowEvidenceReceipt) =
        $"""# EVIDENCE

This document summarizes the evidence presented in the receipt.
Verdict: {receipt.Verdict}
"""

    let generateLoss (receipt: CanonFlowEvidenceReceipt) =
        $"""# LOSS

This document details gaps and unsupported constraints from the evaluation.
"""

