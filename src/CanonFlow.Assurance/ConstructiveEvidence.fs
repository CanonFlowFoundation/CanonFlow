namespace CanonFlow.Assurance

open System

type ConstructiveGateObservation = {
    GateId: ProofGateId
    GateVersion: string
    ImplementationDigest: Digest
    Verdict: Verdict
    Evidence: ConstructiveEvidenceReference list
}

[<RequireQualifiedAccess>]
module ConstructiveEvidence =
    let create kind path digest provenance =
        if String.IsNullOrWhiteSpace(kind) then
            Error "Constructive evidence kind is required."
        elif String.IsNullOrWhiteSpace(path) then
            Error "Constructive evidence path is required."
        else
            match Digest.parse digest with
            | Error error -> Error error
            | Ok _ ->
                Ok {
                    Kind = kind
                    Path = path.Replace('\\', '/')
                    Digest = digest
                    Provenance = provenance
                }

[<RequireQualifiedAccess>]
module ConstructiveAssessment =
    let private evidenceKey (evidence: ConstructiveEvidenceReference) =
        evidence.Kind,
        evidence.Path,
        evidence.Digest,
        evidence.Provenance

    let private validateObservation
        (requiredById: Map<string, ProofGateReference>)
        (observation: ConstructiveGateObservation)
        =
        let gateId = observation.GateId |> ProofGateId.value
        match requiredById |> Map.tryFind gateId with
        | None ->
            Error $"Constructive evidence contains unrequired gate '{gateId}'."
        | Some required
            when observation.GateVersion <> ProofGateReference.version required ->
            Error $"Constructive gate '{gateId}' version does not match its obligation."
        | Some required
            when Digest.toString observation.ImplementationDigest
                 <> (required |> ProofGateReference.implementationDigest |> Digest.toString) ->
            Error $"Constructive gate '{gateId}' implementation digest does not match its obligation."
        | Some _ ->
            observation.Evidence
            |> List.fold (fun state evidence ->
                match state, Digest.parse evidence.Digest with
                | Error error, _ | _, Error error -> Error error
                | Ok (), Ok _ when String.IsNullOrWhiteSpace(evidence.Kind) ->
                    Error $"Constructive gate '{gateId}' has evidence without a kind."
                | Ok (), Ok _ when String.IsNullOrWhiteSpace(evidence.Path) ->
                    Error $"Constructive gate '{gateId}' has evidence without a path."
                | Ok (), Ok _ -> Ok ()) (Ok ())

    let create
        (manifestDigest: Digest)
        (obligation: Obligation)
        (observations: ConstructiveGateObservation list)
        =
        let required =
            obligation
            |> Obligation.requiredGates
            |> NonEmpty.toList
            |> List.sortBy (ProofGateReference.gateId >> ProofGateId.value)
        let requiredById =
            required
            |> List.map (fun gate ->
                gate |> ProofGateReference.gateId |> ProofGateId.value,
                gate)
            |> Map.ofList
        observations
        |> List.fold (fun state observation ->
            match state, validateObservation requiredById observation with
            | Ok (), Ok () -> Ok ()
            | Error error, _ | _, Error error -> Error error) (Ok ())
        |> Result.map (fun () ->
            let observedById =
                observations
                |> List.groupBy (fun observation ->
                    observation.GateId |> ProofGateId.value)
                |> Map.ofList
            let gates, missing =
                required
                |> List.fold (fun (gateRecords, missingIds) requiredGate ->
                    let gateId =
                        requiredGate
                        |> ProofGateReference.gateId
                        |> ProofGateId.value
                    match observedById |> Map.tryFind gateId with
                    | None ->
                        gateRecords, gateId :: missingIds
                    | Some gateObservations ->
                        let evidence =
                            gateObservations
                            |> List.collect _.Evidence
                            |> List.distinctBy evidenceKey
                            |> List.sortBy evidenceKey
                        let observedVerdict =
                            gateObservations
                            |> List.map _.Verdict
                            |> List.reduce Verdict.join
                        let gateVerdict =
                            if List.isEmpty evidence then Verdict.Inconclusive
                            else observedVerdict
                        let gateRecord: ConstructiveGateAssessmentRecord = {
                            GateId = gateId
                            GateVersion = ProofGateReference.version requiredGate
                            ImplementationDigest =
                                requiredGate
                                |> ProofGateReference.implementationDigest
                                |> Digest.toString
                            Verdict = gateVerdict
                            Evidence = evidence
                        }
                        gateRecord :: gateRecords,
                        (if List.isEmpty evidence then gateId :: missingIds else missingIds)
                ) ([], [])
            let gates = gates |> List.rev
            let missing = missing |> List.distinct |> List.sort
            let baseVerdict =
                match Obligation.derivation obligation, missing with
                | ProjectionDerivation.Admitted _, [] -> Verdict.Pass
                | _ -> Verdict.Inconclusive
            let verdict =
                gates
                |> List.fold
                    (fun state gate -> Verdict.join state gate.Verdict)
                    baseVerdict
            let derivationKind, derivationReference =
                obligation
                |> Obligation.derivation
                |> ReceiptText.derivation
            ({
                ObligationId = obligation |> Obligation.id |> ObligationId.value
                ProjectionState =
                    obligation
                    |> Obligation.projectionState
                    |> ReceiptText.projectionState
                DerivationKind = derivationKind
                DerivationReference = derivationReference
                SourceDigest =
                    obligation
                    |> Obligation.sourceDigest
                    |> Digest.toString
                ManifestDigest = Digest.toString manifestDigest
                RequiredGates = required.Length
                EvaluatedGates =
                    gates
                    |> List.filter (fun gate -> not gate.Evidence.IsEmpty)
                    |> List.length
                MissingGateIds = missing
                Gates = gates
                Verdict = verdict
            }: ConstructiveAssessmentRecord))

    let aggregate (assessments: ConstructiveAssessmentRecord list) =
        assessments
        |> List.map _.Verdict
        |> function
            | [] -> Verdict.Pass
            | head :: tail -> tail |> List.fold Verdict.join head

    let isPromotable (assessment: ConstructiveAssessmentRecord) =
        assessment.Verdict = Verdict.Pass
        && assessment.ProjectionState = "Admitted"
        && assessment.RequiredGates > 0
        && assessment.EvaluatedGates = assessment.RequiredGates
        && assessment.MissingGateIds.IsEmpty
