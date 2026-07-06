namespace Canon.Core

/// Represents a phantom-typed predicate.
type IPredicate<'T> =
    abstract member Validate: 'T -> bool
    abstract member Name: string

/// A refined type containing a value that has been proven to satisfy the predicate 'P.
type Refined<'T, 'P when 'P :> IPredicate<'T>> = 
    private { Value: 'T }
    member this.Get() = this.Value

[<RequireQualifiedAccess>]
module Refined =
    let create<'T, 'P when 'P :> IPredicate<'T> and 'P : (new : unit -> 'P)> (value: 'T) : Result<Refined<'T, 'P>, string> =
        let pred = new 'P()
        if pred.Validate(value) then
            Ok { Value = value }
        else
            Error $"Validation failed for {pred.Name}"
