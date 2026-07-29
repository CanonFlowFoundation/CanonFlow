namespace Cff.Harness

open System
open CanonFlow.Assurance.Contracts

type EvaluatedRulePack =
    { Pack: RulePackDefinition
      PackDigest: ContentDigest
      Admission: RulePackAdmissionReceipt
      Results: RuleResult list
      Verdict: ProfileVerdict
      Evidence: EvidenceBundle
      Facts: ApplicabilityContext }

type VerificationStatus =
    | Valid
    | Invalid of string

type ReceiptVerification =
    { SealStatus: VerificationStatus
      VerdictStatus: VerificationStatus
      RecomputedVerdict: ProfileVerdict }

[<Struct>]
type WorkId = private WorkId of string

[<RequireQualifiedAccess>]
module WorkId =
    let create value =
        if String.IsNullOrWhiteSpace value then Error "WorkId cannot be empty"
        else Ok (WorkId value)
    let value (WorkId value) = value

type WorkPolicy =
    { PolicyDigest: ContentDigest
      RequiredGates: NonEmptyList<string>
      AuthorizedAdmitters: Set<string>
      AuthorizedReviewers: Set<string>
      AuthorizedSigners: Set<string> }

type Proposal =
    { WorkId: WorkId
      SpecDigest: ContentDigest
      ProposedBy: string }

type DraftBundle =
    { Proposal: Proposal
      GatePolicyDigest: ContentDigest }

type AdmittedBundle =
    { Draft: DraftBundle
      AdmittedBy: string
      AdmittedAt: DateTimeOffset }

type AttestedEvidence =
    { Commit: ContentDigest
      SpecDigest: ContentDigest
      PolicyDigest: ContentDigest
      GateId: string
      Passed: bool
      ObservedBy: string
      ObservationDigest: ContentDigest }

type ChangeEvidence =
    { Commit: ContentDigest
      ImplementedBy: string }

type AssessmentEvidence =
    { Commit: ContentDigest
      Gates: NonEmptyList<AttestedEvidence> }

type IndependentReview =
    { Commit: ContentDigest
      Implementer: string
      ReviewedBy: string
      Accepted: bool }

type WorkReceipt =
    { WorkId: WorkId
      Commit: ContentDigest
      SpecDigest: ContentDigest
      PolicyDigest: ContentDigest
      AssessmentDigest: ContentDigest
      ReviewDigest: ContentDigest
      Issuer: string
      SealedAt: DateTimeOffset
      Signature: byte array }

type ArchitecturalQuestion =
    { Code: string
      Question: string }

type WorkState =
    | Proposed of Proposal
    | Drafted of DraftBundle
    | AdmittedWork of AdmittedBundle
    | RedWitnessed of AdmittedBundle * AttestedEvidence
    | Implemented of AdmittedBundle * AttestedEvidence * ChangeEvidence
    | GreenWitnessed of AdmittedBundle * AttestedEvidence * ChangeEvidence * AttestedEvidence
    | Assessed of AdmittedBundle * ChangeEvidence * AssessmentEvidence
    | Reviewed of AdmittedBundle * ChangeEvidence * AssessmentEvidence * IndependentReview
    | Sealed of WorkReceipt
    | DecisionRequired of ArchitecturalQuestion
    | Abandoned of reason: string

type WorkflowEvent =
    | Draft of gatePolicyDigest: ContentDigest
    | Admit of actor: string * at: DateTimeOffset
    | WitnessRed of AttestedEvidence
    | RegisterChange of ChangeEvidence
    | WitnessGreen of AttestedEvidence
    | RecordAssessment of AssessmentEvidence
    | AcceptReview of IndependentReview
    | SealWork of actor: string * at: DateTimeOffset * signature: byte array
    | RequireDecision of ArchitecturalQuestion
    | Abandon of reason: string
