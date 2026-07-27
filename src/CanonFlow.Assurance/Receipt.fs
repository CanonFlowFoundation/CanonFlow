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

