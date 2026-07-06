namespace Canon.Core

/// A refined type containing a value that has been proven to satisfy the Lattice<Constraint>.
/// Replaces OO IPredicate shell with pure functional data.
type Refined<'T, 'Tag> = 
    private { Value: 'T; Predicate: Lattice<Constraint> }
    member this.Get() = this.Value
    member this.Schema = this.Predicate

[<RequireQualifiedAccess>]
module Refined =
    /// Attempts to lift a value into a Refined type given a predicate and an evaluation function.
    let create<'T, 'Tag> (eval: 'T -> Lattice<Constraint> -> bool) (predicate: Lattice<Constraint>) (value: 'T) : Result<Refined<'T, 'Tag>, string> =
        if eval value predicate then
            Ok { Value = value; Predicate = predicate }
        else
            Error "Validation failed for structural constraints"
