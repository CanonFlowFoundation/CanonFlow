namespace CanonFlow.Assurance

open System

type SubjectRecord = {
    Root: string
    Schema: string
    SourceDirectories: string list
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

type ComponentAssessmentRecord = {
    ComponentId: string
    ComponentVersion: string
    Health: string
    Compliance: string
    ApplicableRules: int
    EvaluatedRules: int
    Evidence: EvidenceRef list
}

type CanonFlowEvidenceReceipt = {
    SchemaVersion: string
    ReceiptType: string
    Subject: SubjectRecord
    Evaluator: EvaluatorRecord
    Context: ReceiptContext
    Assessments: ComponentAssessmentRecord list
    Verdict: string
    Seal: ReceiptSeal option
}

