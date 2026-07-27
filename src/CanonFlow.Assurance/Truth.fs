namespace CanonFlow.Assurance

type Finding = { Description: string }
type EvidenceRequirement = { Description: string }
type ToolFailureInfo = { Description: string }

type ClearOutcome =
    | Conformant
    | NonConformant of NonEmpty<Finding>

type QualifiedOutcome =
    | ViolationFound of NonEmpty<Finding>
    | NoViolationFound

type Truth =
    /// Evidence complete: the only constructor that may assert conformance.
    | Clear of ClearOutcome
    /// Evidence has named gaps: violations may still be proven, absence may not.
    | Qualified of missing: NonEmpty<EvidenceRequirement> * QualifiedOutcome
    /// A required tool did not complete. Findings observed so far are retained.
    | Interrupted of failure: ToolFailureInfo * observed: Finding list

type EvidenceHealth =
    | Complete
    | Partial of NonEmpty<EvidenceRequirement>
    | Broken of ToolFailureInfo

module EvidenceHealth =
    let toVerdict (h: EvidenceHealth) =
        match h with
        | Complete -> Verdict.Pass
        | Partial _ -> Verdict.Inconclusive
        | Broken _ -> Verdict.ToolFailure

    let ofTruth (t: Truth) =
        match t with
        | Clear _ -> Complete
        | Qualified (missing, _) -> Partial missing
        | Interrupted (failure, _) -> Broken failure

module Truth =
    let toVerdict (t: Truth) =
        match t with
        | Clear Conformant -> Verdict.Pass
        | Clear (NonConformant _) -> Verdict.Fail
        | Qualified (_, ViolationFound _) -> Verdict.Fail
        | Qualified (_, NoViolationFound) -> Verdict.Inconclusive
        | Interrupted _ -> Verdict.ToolFailure
