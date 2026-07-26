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
    
    // ... previous fidelity functions ...
    let analyzeFidelity (field: string) (targetSystem: string) (fidelity: Fidelity) (dbConstraintStr: string) : DriftViolation option =
        match fidelity with
        | Fidelity.Exact -> None // No drift
        | Fidelity.Approximate (Stronger reason) ->
            Some {
                Field = field
                TargetSystem = targetSystem
                DatabaseTruth = dbConstraintStr
                TargetReality = "Approximate (Stronger)"
                Severity = Medium
                FixAction = $"Review frontend validator bounds. Reason: {reason}"
            }
        | Fidelity.Approximate (Weaker reason) ->
            Some {
                Field = field
                TargetSystem = targetSystem
                DatabaseTruth = dbConstraintStr
                TargetReality = "Approximate (Weaker)"
                Severity = High
                FixAction = $"Target admits invalid writes. Reason: {reason}"
            }
        | Fidelity.Conditional a ->
            let str = String.concat ", " a
            Some {
                Field = field
                TargetSystem = targetSystem
                DatabaseTruth = dbConstraintStr
                TargetReality = "Conditional"
                Severity = Low
                FixAction = sprintf "Ensure runtime assumptions hold: %s" str
            }
        | Fidelity.DatabaseOwned _ | Fidelity.Manual _ ->
            None
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

    // --- Phase 4 Semantic Drift Detection ---
    
    type SemanticDriftStatus =
        | Aligned
        | StrictTarget
        | LooseTarget
        | Disjoint
        | Unknown

    /// Compares two normalized ASTs for structural equivalence.
    /// This is safe because SemanticOptimizer produces canonical bounds.
    let rec private structuralEquals a b =
        match a, b with
        | True, True -> true
        | False, False -> true
        | Leaf c1, Leaf c2 -> c1 = c2
        | Not x, Not y -> structuralEquals x y
        | And(a1, b1), And(a2, b2) -> (structuralEquals a1 a2 && structuralEquals b1 b2) || (structuralEquals a1 b2 && structuralEquals b1 a2)
        | Or(a1, b1), Or(a2, b2) -> (structuralEquals a1 a2 && structuralEquals b1 b2) || (structuralEquals a1 b2 && structuralEquals b1 a2)
        | _ -> false

    /// Calculates the semantic drift status of a target constraint relative to a source constraint.
    let calculateSemanticDrift (source: Lattice<Constraint>) (target: Lattice<Constraint>) : SemanticDriftStatus =
        let optSource = SemanticOptimizer.simplify source
        let optTarget = SemanticOptimizer.simplify target
        
        let intersection = SemanticOptimizer.simplify (And(optSource, optTarget))
        
        match intersection with
        | False -> Disjoint
        | _ when structuralEquals optSource optTarget -> Aligned
        | _ when structuralEquals intersection optTarget -> StrictTarget
        | _ when structuralEquals intersection optSource -> LooseTarget
        | _ -> Unknown // Complex partial overlaps or unresolved nodes
