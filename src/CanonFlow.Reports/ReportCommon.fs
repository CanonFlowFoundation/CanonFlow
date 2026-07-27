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
        receipt.Assessments
        |> List.collect (fun assessment ->
            match assessment.Compliance with
            | Compliance.NonConformant values ->
                values
                |> NonEmpty.toList
                |> List.map (fun finding -> assessment.ComponentId, finding.Description)
            | _ -> [])

    let gaps receipt =
        receipt.Assessments
        |> List.collect (fun assessment ->
            match assessment.Health with
            | EvidenceHealth.Partial missing ->
                missing
                |> NonEmpty.toList
                |> List.map (fun gap -> assessment.ComponentId, gap.Description)
            | _ -> [])

    let toolFailures receipt =
        receipt.Assessments
        |> List.choose (fun assessment ->
            match assessment.Health with
            | EvidenceHealth.Broken failure -> Some (assessment.ComponentId, failure.Description)
            | _ -> None)
