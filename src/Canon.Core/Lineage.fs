namespace Canon.Core

/// Direction matters. These have different owners and different blast radii.
type Divergence =
    /// F# rejects rows PostgreSQL admits — breaks READS of resident data.
    | Stronger of reason: string
    /// F# admits rows PostgreSQL rejects — breaks WRITES, surfaces at INSERT.
    | Weaker of reason: string
    override this.ToString() =
        match this with
        | Stronger r -> sprintf "Stronger: %s" (Sanitizer.sanitizeComment r)
        | Weaker r -> sprintf "Weaker: %s" (Sanitizer.sanitizeComment r)

/// Represents the translation fidelity of a constraint into a target language.
[<RequireQualifiedAccess>]
type Fidelity =
    | Exact
    | Conditional   of assumptions : string list
    | Approximate   of Divergence
    | DatabaseOwned of enforcer : string
    | Manual        of owner : string * evidenceRef : string
    | Unsupported   of reason : string
    override this.ToString() =
        match this with
        | Exact -> "Exact"
        | Conditional a -> let str = String.concat ", " a in sprintf "Conditional: [%s]" str
        | Approximate d -> sprintf "Approximate (%O)" d
        | DatabaseOwned e -> sprintf "DatabaseOwned by %s" e
        | Manual (o, e) -> sprintf "Manual (%s, %s)" o e
        | Unsupported r -> sprintf "Unsupported: %s" (Sanitizer.sanitizeComment r)

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
        | Fidelity.Approximate d1, Fidelity.Approximate d2 -> Fidelity.Approximate (Weaker $"{d1} and {d2}")
        | Fidelity.Approximate d, _ | _, Fidelity.Approximate d -> Fidelity.Approximate d
        | Fidelity.Manual (o, e), _ | _, Fidelity.Manual (o, e) -> Fidelity.Manual (o, e)
        | Fidelity.DatabaseOwned e1, _ | _, Fidelity.DatabaseOwned e1 -> Fidelity.DatabaseOwned e1
        | Fidelity.Conditional a1, Fidelity.Conditional a2 -> Fidelity.Conditional (a1 @ a2)
        | Fidelity.Conditional a, _ | _, Fidelity.Conditional a -> Fidelity.Conditional a
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
