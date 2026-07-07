namespace Canon.Core

/// Classifies the fidelity of mapping a domain field to/from a storage field.
type FieldClass =
    /// The storage representation perfectly matches the domain representation.
    | Lossless
    /// The storage representation is wider than the domain representation (e.g. string vs varchar).
    | Widened of reason: string
    /// The storage representation is narrower than the domain representation, risking data loss.
    | Narrowed of reason: string
    /// The storage representation cannot hold the domain concept.
    | Unrepresentable of reason: string
