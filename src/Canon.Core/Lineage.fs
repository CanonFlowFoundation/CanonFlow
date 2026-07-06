namespace Canon.Core

/// Lineage grade indicates the degree of trust/verification for a field or constraint.
/// Inspired by Symphony's Lineage concepts.
type LineageGrade =
    /// Computed directly and verifiably from the expression structure / database constraints.
    | Exact
    /// Asserted by the author or driver, but lacking structural F# proof.
    | Declared
    /// Intentionally unknown or untracked. (Blocked for governed serving fields).
    | Opaque
