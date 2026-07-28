namespace CanonFlow.Assurance

open System

type ArtifactRecord = {
    Path: string
    Digest: string
}

type SubjectRecord = {
    Root: string
    Schema: string
    SourceDirectories: string list
    ManifestDigest: string option
    Artifacts: ArtifactRecord list
}

type EvaluatorRecord = {
    EngineId: string
    EngineVersion: string
}

type ReceiptContext = {
    Instant: string
    TimeProvenance: string
    Locale: string
    NetworkPolicy: string
}

type EvidenceRef = {
    Path: string
    Kind: string
    Value: string option
    Provenance: string option
}

[<RequireQualifiedAccess>]
type Compliance =
    | Conformant
    | NonConformant of NonEmpty<Finding>
    | NotEstablished

type ComponentAssessmentRecord = {
    ComponentId: string
    ComponentVersion: string
    Health: EvidenceHealth
    Compliance: Compliance
    ApplicableRules: int
    EvaluatedRules: int
    Evidence: EvidenceRef list
}

type ConstructiveEvidenceReference = {
    Kind: string
    Path: string
    Digest: string
    Provenance: string option
}

type ConstructiveGateAssessmentRecord = {
    GateId: string
    GateVersion: string
    ImplementationDigest: string
    Verdict: Verdict
    Evidence: ConstructiveEvidenceReference list
}

type ConstructiveAssessmentRecord = {
    ObligationId: string
    ProjectionState: string
    DerivationKind: string
    DerivationReference: string option
    SourceDigest: string
    ManifestDigest: string
    RequiredGates: int
    EvaluatedGates: int
    MissingGateIds: string list
    Gates: ConstructiveGateAssessmentRecord list
    Verdict: Verdict
}

// Keep this v1.0 CLR shape stable: released profile assemblies construct it directly.
type CanonFlowEvidenceReceipt = {
    SchemaVersion: string
    ReceiptType: string
    ReplayIdentity: string
    Subject: SubjectRecord
    Evaluator: EvaluatorRecord
    Context: ReceiptContext
    Assessments: ComponentAssessmentRecord list
    Verdict: Verdict
    Seal: ReceiptSeal option
}

type CanonFlowEvidenceReceiptV11 = {
    SchemaVersion: string
    ReceiptType: string
    ReplayIdentity: string
    Subject: SubjectRecord
    Evaluator: EvaluatorRecord
    Context: ReceiptContext
    Assessments: ComponentAssessmentRecord list
    ConstructiveAssessments: ConstructiveAssessmentRecord list
    Verdict: Verdict
    Seal: ReceiptSeal option
}

module ReceiptText =
    let verdict = function
        | Verdict.Pass -> "Pass"
        | Verdict.Fail -> "Fail"
        | Verdict.Inconclusive -> "Inconclusive"
        | Verdict.ToolFailure -> "ToolFailure"

    let health = function
        | EvidenceHealth.Complete -> "Complete"
        | EvidenceHealth.Partial _ -> "Partial"
        | EvidenceHealth.Broken _ -> "Broken"

    let compliance = function
        | Compliance.Conformant -> "Conformant"
        | Compliance.NonConformant _ -> "NonConformant"
        | Compliance.NotEstablished -> "NotEstablished"

    let projectionState = function
        | ProjectionState.Dormant -> "Dormant"
        | ProjectionState.CandidateRequiringApproval -> "CandidateRequiringApproval"
        | ProjectionState.Admitted -> "Admitted"
        | ProjectionState.Unsupported -> "Unsupported"

    let derivation = function
        | ProjectionDerivation.None -> "None", None
        | ProjectionDerivation.Candidate assumptions ->
            "Candidate",
            Some (
                assumptions
                |> NonEmpty.toList
                |> List.map AssumptionId.value
                |> List.sort
                |> String.concat ",")
        | ProjectionDerivation.Admitted admissionId ->
            "Admitted", Some (AdmissionId.value admissionId)
        | ProjectionDerivation.Unsupported reasonId ->
            "Unsupported", Some (UnsupportedReasonId.value reasonId)

