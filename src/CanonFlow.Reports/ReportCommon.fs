namespace CanonFlow.Reports

open CanonFlow.Assurance

module ReportCommon =
    let receiptDigest receipt =
        let payload =
            match receipt.Seal with
            | Some seal when seal.Status = SealStatus.Signed ->
                CanonicalReceiptJson.serializeSigningPayload receipt
            | _ ->
                CanonicalReceiptJson.serializeEnvelope receipt
        "sha256:" + Hash.computeSha256 payload

    let findings receipt =
        let verification =
            receipt.Assessments
            |> List.collect (fun assessment ->
                match assessment.Compliance with
                | Compliance.NonConformant values ->
                    values
                    |> NonEmpty.toList
                    |> List.map (fun finding -> assessment.ComponentId, finding.Description)
                | _ -> [])
        let constructive =
            receipt.ConstructiveAssessments
            |> List.collect (fun assessment ->
                assessment.Gates
                |> List.choose (fun gate ->
                    if gate.Verdict = Verdict.Fail then
                        Some (assessment.ObligationId, $"Constructive gate failed: {gate.GateId}")
                    else None))
        verification @ constructive

    let gaps receipt =
        let verification =
            receipt.Assessments
            |> List.collect (fun assessment ->
                match assessment.Health with
                | EvidenceHealth.Partial missing ->
                    missing
                    |> NonEmpty.toList
                    |> List.map (fun gap -> assessment.ComponentId, gap.Description)
                | _ -> [])
        let constructive =
            receipt.ConstructiveAssessments
            |> List.collect (fun assessment ->
                assessment.MissingGateIds
                |> List.map (fun gateId ->
                    assessment.ObligationId,
                    $"Missing constructive gate evidence: {gateId}"))
        verification @ constructive

    let toolFailures receipt =
        let verification =
            receipt.Assessments
            |> List.choose (fun assessment ->
                match assessment.Health with
                | EvidenceHealth.Broken failure -> Some (assessment.ComponentId, failure.Description)
                | _ -> None)
        let constructive =
            receipt.ConstructiveAssessments
            |> List.collect (fun assessment ->
                assessment.Gates
                |> List.choose (fun gate ->
                    if gate.Verdict = Verdict.ToolFailure then
                        Some (assessment.ObligationId, $"Constructive gate tool failure: {gate.GateId}")
                    else None))
        verification @ constructive
