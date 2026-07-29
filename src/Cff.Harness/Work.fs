namespace Cff.Harness

open System
open CanonFlow.Assurance.Contracts

[<RequireQualifiedAccess>]
module Work =
    let private error code message =
        { RuleId = RuleId.create "CFF-WORK-STATE" |> Result.defaultWith invalidOp
          Code = code
          Severity = Severity.Error
          Message = message
          Expected = None
          Observed = None
          Evidence = []
          Authority = ClauseId.create "CFF.work-policy" |> Result.defaultWith invalidOp }
        |> fun finding -> Error (NonEmptyList.create finding [])

    let private proposal = function
        | Proposed value -> Some value
        | Drafted value -> Some value.Proposal
        | AdmittedWork value
        | RedWitnessed (value, _)
        | Implemented (value, _, _)
        | GreenWitnessed (value, _, _, _)
        | Assessed (value, _, _)
        | Reviewed (value, _, _, _) -> Some value.Draft.Proposal
        | _ -> None

    let private attestationMatches
        (policy: WorkPolicy)
        (admitted: AdmittedBundle)
        (evidence: AttestedEvidence)
        =
        evidence.SpecDigest = admitted.Draft.Proposal.SpecDigest
        && evidence.PolicyDigest = policy.PolicyDigest

    let transition (policy: WorkPolicy) (state: WorkState) (event: WorkflowEvent) =
        match state, event with
        | _, RequireDecision question -> Ok (DecisionRequired question)
        | _, Abandon reason when not (String.IsNullOrWhiteSpace reason) -> Ok (Abandoned reason)
        | Proposed proposal, Draft gatePolicyDigest when gatePolicyDigest = policy.PolicyDigest ->
            Ok (Drafted { Proposal = proposal; GatePolicyDigest = gatePolicyDigest })
        | Drafted draft, Admit (actor, at)
            when Set.contains actor policy.AuthorizedAdmitters
                 && draft.GatePolicyDigest = policy.PolicyDigest ->
            Ok (AdmittedWork { Draft = draft; AdmittedBy = actor; AdmittedAt = at })
        | AdmittedWork admitted, WitnessRed evidence
            when not evidence.Passed
                 && attestationMatches policy admitted evidence
                 && evidence.ObservedBy <> admitted.Draft.Proposal.ProposedBy ->
            Ok (RedWitnessed (admitted, evidence))
        | RedWitnessed (admitted, red), RegisterChange change
            when change.Commit <> red.Commit ->
            Ok (Implemented (admitted, red, change))
        | Implemented (admitted, red, change), WitnessGreen green
            when green.Passed
                 && green.Commit = change.Commit
                 && attestationMatches policy admitted green
                 && green.ObservedBy <> change.ImplementedBy ->
            Ok (GreenWitnessed (admitted, red, change, green))
        | GreenWitnessed (admitted, _, change, green), RecordAssessment assessment
            when assessment.Commit = change.Commit
                 && assessment.Gates
                    |> NonEmptyList.toList
                    |> List.forall (fun gate ->
                        gate.Passed
                        && gate.Commit = change.Commit
                        && attestationMatches policy admitted gate)
                 && (policy.RequiredGates
                     |> NonEmptyList.toList
                     |> Set.ofList)
                    .IsSubsetOf(
                        assessment.Gates
                        |> NonEmptyList.toList
                        |> List.map _.GateId
                        |> Set.ofList
                    ) ->
            Ok (Assessed (admitted, change, assessment))
        | Assessed (admitted, change, assessment), AcceptReview review
            when review.Accepted
                 && review.Commit = change.Commit
                 && review.Implementer = change.ImplementedBy
                 && review.ReviewedBy <> change.ImplementedBy
                 && Set.contains review.ReviewedBy policy.AuthorizedReviewers ->
            Ok (Reviewed (admitted, change, assessment, review))
        | Reviewed (admitted, change, assessment, review), SealWork (actor, at, signature)
            when Set.contains actor policy.AuthorizedSigners
                 && not (isNull signature)
                 && signature.Length > 0 ->
            let assessmentDigest =
                assessment.Gates
                |> NonEmptyList.toList
                |> List.map (fun gate -> ContentDigest.value gate.ObservationDigest)
                |> String.concat "\n"
                |> Canonical.sha256Text
            let reviewDigest =
                String.concat "\n" [
                    ContentDigest.value review.Commit
                    review.Implementer
                    review.ReviewedBy
                    string review.Accepted
                ]
                |> Canonical.sha256Text
            Ok (
                Sealed {
                    WorkId = admitted.Draft.Proposal.WorkId
                    Commit = change.Commit
                    SpecDigest = admitted.Draft.Proposal.SpecDigest
                    PolicyDigest = policy.PolicyDigest
                    AssessmentDigest = assessmentDigest
                    ReviewDigest = reviewDigest
                    Issuer = actor
                    SealedAt = at
                    Signature = Array.copy signature
                }
            )
        | _ ->
            let current =
                match proposal state with
                | Some value -> WorkId.value value.WorkId
                | None -> string state
            error "INVALID_TRANSITION" ($"Event {event} is invalid for work {current}")

    let isPromotionEligible (policy: WorkPolicy) expectedCommit = function
        | Sealed receipt ->
            receipt.Commit = expectedCommit
            && receipt.PolicyDigest = policy.PolicyDigest
            && Set.contains receipt.Issuer policy.AuthorizedSigners
            && receipt.Signature.Length > 0
        | _ -> false
