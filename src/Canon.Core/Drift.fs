namespace Canon.Core

/// Represents the severity of constraint drift between the database and a target system.
type DriftSeverity =
    | Low
    | Medium
    | High

/// A report detailing a specific drift violation.
type DriftViolation = {
    Field: string
    TargetSystem: string
    DatabaseTruth: string
    TargetReality: string
    Severity: DriftSeverity
    FixAction: string
}

module DriftEngine =
    
    /// Analyzes a constraint's translation fidelity to determine if actionable drift exists.
    let analyzeFidelity (field: string) (targetSystem: string) (fidelity: Fidelity) (dbConstraintStr: string) : DriftViolation option =
        match fidelity with
        | Fidelity.Exact -> None // No drift
        | Fidelity.Approximate reason ->
            Some {
                Field = field
                TargetSystem = targetSystem
                DatabaseTruth = dbConstraintStr
                TargetReality = "Approximate"
                Severity = Medium
                FixAction = $"Review frontend validator bounds. Reason: {reason}"
            }
        | Fidelity.Unsupported reason ->
            Some {
                Field = field
                TargetSystem = targetSystem
                DatabaseTruth = dbConstraintStr
                TargetReality = "Missing / Unsupported"
                Severity = High
                FixAction = $"Implement custom backend middleware guard. Reason: {reason}"
            }

    /// Generates a full drift report for a table based on generated fidelities.
    let detectDrift (table: string) (fidelities: (string * string * Fidelity * string) list) =
        // fidelities = list of (Field, TargetSystem, Fidelity, DbConstraintStr)
        fidelities
        |> List.choose (fun (fld, sys, fid, dbStr) -> analyzeFidelity fld sys fid dbStr)
