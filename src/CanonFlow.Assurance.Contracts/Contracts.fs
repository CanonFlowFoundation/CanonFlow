namespace CanonFlow.Assurance.Contracts

open System

[<Struct>]
type RuleId = private RuleId of string

[<RequireQualifiedAccess>]
module RuleId =
    let create value =
        if String.IsNullOrWhiteSpace value then Error "RuleId cannot be empty"
        else Ok (RuleId value)
    let value (RuleId value) = value

[<Struct>]
type ClauseId = private ClauseId of string

[<RequireQualifiedAccess>]
module ClauseId =
    let create value =
        if String.IsNullOrWhiteSpace value then Error "ClauseId cannot be empty"
        else Ok (ClauseId value)
    let value (ClauseId value) = value

[<Struct>]
type ProfileId = private ProfileId of string

[<RequireQualifiedAccess>]
module ProfileId =
    let create value =
        if String.IsNullOrWhiteSpace value then Error "ProfileId cannot be empty"
        else Ok (ProfileId value)
    let value (ProfileId value) = value

[<Struct>]
type EvaluatorId = private EvaluatorId of string

[<RequireQualifiedAccess>]
module EvaluatorId =
    let create value =
        if String.IsNullOrWhiteSpace value then Error "EvaluatorId cannot be empty"
        else Ok (EvaluatorId value)
    let value (EvaluatorId value) = value

[<Struct>]
type RulePackId = private RulePackId of string

[<RequireQualifiedAccess>]
module RulePackId =
    let create value =
        if String.IsNullOrWhiteSpace value then Error "RulePackId cannot be empty"
        else Ok (RulePackId value)
    let value (RulePackId value) = value

[<Struct>]
type FactPath = private FactPath of string

[<RequireQualifiedAccess>]
module FactPath =
    let create value =
        if String.IsNullOrWhiteSpace value then Error "FactPath cannot be empty"
        else Ok (FactPath value)
    let value (FactPath value) = value

[<Struct>]
type ContentDigest = private ContentDigest of string

[<RequireQualifiedAccess>]
module ContentDigest =
    let createSha256 (value: string) =
        let valid =
            not (isNull value)
            && value.Length = 71
            && value.StartsWith("sha256:", StringComparison.Ordinal)
            && value.Substring(7)
               |> Seq.forall (fun character ->
                   (character >= '0' && character <= '9')
                   || (character >= 'a' && character <= 'f'))
        if valid then Ok (ContentDigest value)
        else Error "ContentDigest must use canonical lowercase sha256:<64-hex> form"
    let value (ContentDigest value) = value
    let internal trusted value = ContentDigest value

type NonEmptyList<'item> = private NonEmptyList of 'item * 'item list

[<RequireQualifiedAccess>]
module NonEmptyList =
    let create head tail = NonEmptyList (head, tail)
    let ofList = function
        | head :: tail -> Ok (NonEmptyList (head, tail))
        | [] -> Error "A non-empty list is required"
    let toList (NonEmptyList (head, tail)) = head :: tail
    let map mapping value =
        value
        |> toList
        |> List.map mapping
        |> function
            | head :: tail -> NonEmptyList (head, tail)
            | [] -> invalidOp "NonEmptyList invariant broken"

type CanonicalizationProfile =
    { ProfileName: string
      ProfileVersion: int
      ProfileDigest: ContentDigest }

type SourceKind =
    | OfficialSpecification
    | NetworkExtension
    | Schema
    | Taxonomy
    | DeveloperGuide
    | OfficialScenario
    | LocalPolicy

type SourceLocator =
    { DocumentId: string
      Version: string
      Section: string
      Uri: string option }

type SourceClause =
    { ClauseId: ClauseId
      SourceKind: SourceKind
      Locator: SourceLocator
      SourceDigest: ContentDigest
      ExtractDigest: ContentDigest
      EffectiveFrom: DateOnly option
      Supersedes: ClauseId option
      InterpretationNote: string
      AdmittedBy: string
      AdmittedAt: DateTimeOffset }

[<RequireQualifiedAccess>]
type FactValue =
    | Text of string
    | Number of decimal
    | Boolean of bool

type ApplicabilityExpression =
    | Always
    | ProfileIs of ProfileId
    | DomainIs of string
    | VersionIs of string
    | RoleIs of string
    | FlowIs of string
    | ActionPresent of string
    | FactEquals of FactPath * FactValue
    | AllOf of ApplicabilityExpression list
    | AnyOf of ApplicabilityExpression list
    | Not of ApplicabilityExpression

type ApplicabilityResult =
    | Applicable
    | NotApplicable of reason: string
    | ApplicabilityUndetermined of missingFacts: FactPath list

type FactSource =
    | DerivedFromEvidence of ContentDigest
    | DerivedFromProfile of ContentDigest
    | AuthenticatedExternalFact of ContentDigest

type EstablishedFact =
    { Path: FactPath
      Value: FactValue
      Source: FactSource }

type ApplicabilityContext =
    { Profile: ProfileId
      Facts: EstablishedFact list
      FactsDigest: ContentDigest }

type EvidenceKind =
    | ProtocolMessage of action: string
    | PairedMessage of requestAction: string * callbackAction: string
    | RegistryObservation
    | SignatureObservation
    | TransportObservation
    | ReplayObservation
    | BuildIdentity

type TrustRequirement =
    | ProducerSupplied
    | IndependentlyCaptured
    | AuthenticatedExternalSource

type Cardinality =
    | ExactlyOne
    | AtLeastOne
    | ZeroOrMore

type EvidenceRequirement =
    { RequirementId: string
      Kind: EvidenceKind
      Cardinality: Cardinality
      Trust: TrustRequirement
      Description: string }

type CaptureMethod =
    | HttpCollector
    | LogCollector
    | FileImport
    | ExternalToolImport

type EvidenceProvenance =
    { CapturedBy: string
      CapturedAt: DateTimeOffset
      CaptureMethod: CaptureMethod
      Producer: string option
      AttestationDigest: ContentDigest option
      EstablishedTrust: TrustRequirement }

type MessageCorrelation =
    { TransactionId: string
      MessageId: string
      SubscriberId: string option
      CounterpartyId: string option }

type MessageEvidence =
    { Action: string
      Correlation: MessageCorrelation
      Timestamp: DateTimeOffset
      RawPayload: byte array
      RawPayloadDigest: ContentDigest
      CanonicalPayload: byte array option
      CanonicalPayloadDigest: ContentDigest option
      Provenance: EvidenceProvenance }

type RegistryEvidence =
    { SubscriberId: string
      KeyId: string
      ObservedAt: DateTimeOffset
      ObservationDigest: ContentDigest
      Provenance: EvidenceProvenance }

type ObservationEvidence =
    { Kind: EvidenceKind
      Payload: byte array
      ObservationDigest: ContentDigest
      Provenance: EvidenceProvenance }

type EvidenceItem =
    | Message of MessageEvidence
    | Registry of RegistryEvidence
    | Observation of ObservationEvidence

type EvidenceBundle =
    { BundleId: string
      Profile: ProfileId
      Items: EvidenceItem list
      BundleDigest: ContentDigest }

[<RequireQualifiedAccess>]
type Severity =
    | Information
    | Warning
    | Error
    | Critical

type EvidenceReference =
    { EvidenceDigest: ContentDigest
      JsonPath: string option }

type Finding =
    { RuleId: RuleId
      Code: string
      Severity: Severity
      Message: string
      Expected: string option
      Observed: string option
      Evidence: EvidenceReference list
      Authority: ClauseId }

type MissingEvidence =
    { RequirementId: string
      Reason: string }

type RuleVerdict =
    | Satisfied
    | Violated of NonEmptyList<Finding>
    | Inconclusive of NonEmptyList<MissingEvidence>
    | ToolFailure of error: string

type ObligationAssessment =
    | NotApplicableAssessment of reason: string
    | CompletedAssessment of RuleVerdict

type EvidenceReadError =
    | MalformedEvidence of EvidenceReference * reason: string
    | UnsupportedEvidenceVersion of string
    | ParserToolFailure of string

type ObligationDefinition =
    { RuleId: RuleId
      Title: string
      Authority: SourceClause
      SupportingAuthorities: SourceClause list
      Applicability: ApplicabilityExpression
      RequiredEvidence: EvidenceRequirement list
      EvaluatorId: EvaluatorId
      RuleVersion: int }

type EvaluatorIdentity =
    { EvaluatorId: EvaluatorId
      AssemblyDigest: ContentDigest
      DependencySetDigest: ContentDigest
      PackageVersion: string
      RuntimeVersion: string
      RuntimeIdentifier: string
      BuildProvenanceDigest: ContentDigest }

type RuleEvaluator = EvidenceBundle -> RuleVerdict

type RegisteredEvaluator =
    { Identity: EvaluatorIdentity
      Evaluate: RuleEvaluator }

type EvaluatorRegistry = Map<EvaluatorId, RegisteredEvaluator>

type RulePackDefinition =
    { RulePackId: RulePackId
      Profile: ProfileId
      Version: int
      Obligations: ObligationDefinition list
      SourceProfileDigest: ContentDigest
      CanonicalizationProfileDigest: ContentDigest
      AggregationPolicyDigest: ContentDigest
      Supersedes: ContentDigest option }

type AdmissionDecision =
    | Admitted
    | Rejected

type RulePackAdmissionReceipt =
    { RulePackDigest: ContentDigest
      SourceProfileDigest: ContentDigest
      PolicyDigest: ContentDigest
      Decision: AdmissionDecision
      FindingsDigest: ContentDigest option
      AdmittedBy: string
      AdmittedAt: DateTimeOffset
      PreviousAdmissionDigest: ContentDigest option
      SignatureAlgorithm: string
      Signature: byte array }

type RuleResult =
    { RuleId: RuleId
      DefinitionDigest: ContentDigest
      EvaluatorIdentity: EvaluatorIdentity
      Assessment: ObligationAssessment }

type ProfileVerdict =
    | Pass
    | Fail of NonEmptyList<Finding>
    | InconclusiveProfile of NonEmptyList<MissingEvidence>
    | ProfileToolFailure of string

type RulePackEvaluationReceipt =
    { ReceiptVersion: int
      RulePackDigest: ContentDigest
      AdmissionReceiptDigest: ContentDigest
      SourceProfileDigest: ContentDigest
      AggregationPolicyDigest: ContentDigest
      CanonicalizationProfileDigest: ContentDigest
      EvaluatorSetDigest: ContentDigest
      ToolchainDigest: ContentDigest
      SandboxPolicyDigest: ContentDigest
      EvidenceBundleDigest: ContentDigest
      ApplicabilityFactsDigest: ContentDigest
      RuleResultsDigest: ContentDigest
      ProfileVerdict: ProfileVerdict
      EvaluatedAt: DateTimeOffset
      PreviousReceiptDigest: ContentDigest option
      Issuer: string
      SignatureAlgorithm: string
      Signature: byte array }
