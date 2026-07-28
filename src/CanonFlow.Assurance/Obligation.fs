namespace CanonFlow.Assurance

open System

module private CanonicalIdentifier =
    let create label (value: string) =
        if String.IsNullOrWhiteSpace(value) then
            Error $"{label} must not be empty."
        elif value.Length > 160 then
            Error $"{label} must not exceed 160 characters."
        elif value.[0] < 'a' || value.[0] > 'z' then
            Error $"{label} must begin with a lowercase ASCII letter."
        elif
            value
            |> Seq.exists (fun character ->
                not (
                    (character >= 'a' && character <= 'z')
                    || (character >= '0' && character <= '9')
                    || character = '.'
                    || character = ':'
                    || character = '_'
                    || character = '-'
                ))
        then
            Error $"{label} contains a non-canonical character."
        else
            Ok value

type ObligationId = private ObligationId of string

[<RequireQualifiedAccess>]
module ObligationId =
    let create value =
        CanonicalIdentifier.create "ObligationId" value
        |> Result.map ObligationId

    let value (ObligationId value) = value

type ProofGateId = private ProofGateId of string

[<RequireQualifiedAccess>]
module ProofGateId =
    let create value =
        CanonicalIdentifier.create "ProofGateId" value
        |> Result.map ProofGateId

    let value (ProofGateId value) = value

type AssumptionId = private AssumptionId of string

[<RequireQualifiedAccess>]
module AssumptionId =
    let create value =
        CanonicalIdentifier.create "AssumptionId" value
        |> Result.map AssumptionId

    let value (AssumptionId value) = value

type AdmissionId = private AdmissionId of string

[<RequireQualifiedAccess>]
module AdmissionId =
    let create value =
        CanonicalIdentifier.create "AdmissionId" value
        |> Result.map AdmissionId

    let value (AdmissionId value) = value

type UnsupportedReasonId = private UnsupportedReasonId of string

[<RequireQualifiedAccess>]
module UnsupportedReasonId =
    let create value =
        CanonicalIdentifier.create "UnsupportedReasonId" value
        |> Result.map UnsupportedReasonId

    let value (UnsupportedReasonId value) = value

type ProofGateReference = private ProofGateReference of ProofGateId * string * Digest

[<RequireQualifiedAccess>]
module ProofGateReference =
    let create gateId version implementationDigest =
        match CanonicalIdentifier.create "ProofGateVersion" version with
        | Error error -> Error error
        | Ok canonicalVersion ->
            Ok (ProofGateReference (gateId, canonicalVersion, implementationDigest))

    let gateId (ProofGateReference (gateId, _, _)) = gateId
    let version (ProofGateReference (_, version, _)) = version
    let implementationDigest (ProofGateReference (_, _, digest)) = digest

[<RequireQualifiedAccess>]
type ProjectionState =
    | Dormant
    | CandidateRequiringApproval
    | Admitted
    | Unsupported

[<RequireQualifiedAccess>]
type ProjectionDerivation =
    | None
    | Candidate of NonEmpty<AssumptionId>
    | Admitted of AdmissionId
    | Unsupported of UnsupportedReasonId

[<RequireQualifiedAccess>]
module ProjectionDerivation =
    let state = function
        | ProjectionDerivation.None -> ProjectionState.Dormant
        | ProjectionDerivation.Candidate _ -> ProjectionState.CandidateRequiringApproval
        | ProjectionDerivation.Admitted _ -> ProjectionState.Admitted
        | ProjectionDerivation.Unsupported _ -> ProjectionState.Unsupported

type Obligation =
    private
        Obligation of
            ObligationId *
            Digest *
            Digest *
            NonEmpty<ProofGateReference> *
            ProjectionDerivation

[<RequireQualifiedAccess>]
module Obligation =
    let create obligationId sourceDigest normalizedPredicateDigest requiredGates derivation =
        match NonEmpty.ofList requiredGates with
        | Error _ -> Error "An obligation must name at least one required proof gate."
        | Ok gates ->
            let duplicateGate =
                gates
                |> NonEmpty.toList
                |> List.countBy (ProofGateReference.gateId >> ProofGateId.value)
                |> List.tryFind (fun (_, count) -> count > 1)
            match duplicateGate with
            | Some (gateId, _) -> Error $"Required proof gate '{gateId}' is duplicated."
            | None ->
                Ok (
                    Obligation (
                        obligationId,
                        sourceDigest,
                        normalizedPredicateDigest,
                        gates,
                        derivation
                    )
                )

    let id (Obligation (value, _, _, _, _)) = value
    let sourceDigest (Obligation (_, value, _, _, _)) = value
    let normalizedPredicateDigest (Obligation (_, _, value, _, _)) = value
    let requiredGates (Obligation (_, _, _, value, _)) = value
    let derivation (Obligation (_, _, _, _, value)) = value
    let projectionState obligation = obligation |> derivation |> ProjectionDerivation.state

[<RequireQualifiedAccess>]
module Projection =
    let evaluate gateResults obligation =
        match Obligation.derivation obligation with
        | ProjectionDerivation.Admitted _ ->
            let observed =
                gateResults
                |> List.groupBy (fst >> ProofGateId.value)
                |> List.map (fun (gateId, results) ->
                    gateId,
                    results
                    |> List.map snd
                    |> List.reduce Verdict.join)
                |> Map.ofList

            obligation
            |> Obligation.requiredGates
            |> NonEmpty.toList
            |> List.map (fun gate ->
                gate
                |> ProofGateReference.gateId
                |> ProofGateId.value
                |> fun gateId -> observed |> Map.tryFind gateId
                |> Option.defaultValue Verdict.Inconclusive)
            |> List.reduce Verdict.join
        | ProjectionDerivation.None
        | ProjectionDerivation.Candidate _
        | ProjectionDerivation.Unsupported _ ->
            Verdict.Inconclusive
