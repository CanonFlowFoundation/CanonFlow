namespace CanonFlow.Assurance

[<RequireQualifiedAccess>]
type Verdict =
    | Pass
    | Inconclusive
    | Fail
    | ToolFailure

module Verdict =

    /// Normative severity table.
    let rank v =
        match v with
        | Verdict.Pass         -> 0
        | Verdict.Inconclusive -> 1
        | Verdict.Fail         -> 2
        | Verdict.ToolFailure  -> 3

    /// Bounded join-semilattice operator
    let join a b = 
        if rank a >= rank b then a else b
