namespace Canon.Core

/// Represents the translation fidelity of a constraint into a target language.
[<RequireQualifiedAccess>]
type Fidelity =
    | Exact
    | Approximate of reason: string
    | Unsupported of reason: string
    override this.ToString() =
        match this with
        | Exact -> "Exact"
        | Approximate r -> $"Approximate: {Sanitizer.sanitizeComment r}"
        | Unsupported r -> $"Unsupported: {Sanitizer.sanitizeComment r}"

type ConstraintFidelity = {
    Constraint: Lattice<Constraint>
    Fidelity: Fidelity
    Target: string
}

module Fidelity =
    let combine f1 f2 =
        match f1, f2 with
        | Fidelity.Unsupported r1, Fidelity.Unsupported r2 -> Fidelity.Unsupported $"{r1}; {r2}"
        | Fidelity.Unsupported r, _ | _, Fidelity.Unsupported r -> Fidelity.Unsupported r
        | Fidelity.Approximate r1, Fidelity.Approximate r2 -> Fidelity.Approximate $"{r1}; {r2}"
        | Fidelity.Approximate r, _ | _, Fidelity.Approximate r -> Fidelity.Approximate r
        | Fidelity.Exact, Fidelity.Exact -> Fidelity.Exact

type FidelityReport = {
    Schema: string
    Passed: bool
    Score: float
    LostMeaning: string list
}

/// Lineage grade indicates the degree of trust/verification for a field or constraint.
/// Inspired by Symphony's Lineage concepts.
type LineageGrade =
    /// Computed directly and verifiably from the expression structure / database constraints.
    | Exact
    /// Asserted by the author or driver, but lacking structural F# proof.
    | Declared
    /// Intentionally unknown or untracked. (Blocked for governed serving fields).
    | Opaque
