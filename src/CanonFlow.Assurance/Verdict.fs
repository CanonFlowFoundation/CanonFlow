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

[<RequireQualifiedAccess>]
module ExitCode =
    [<Literal>]
    let InvalidInvocation = 64

    let ofVerdict = function
        | Verdict.Pass -> 0
        | Verdict.Fail -> 1
        | Verdict.Inconclusive -> 2
        | Verdict.ToolFailure -> 3

    let tryVerdict = function
        | 0 -> Some Verdict.Pass
        | 1 -> Some Verdict.Fail
        | 2 -> Some Verdict.Inconclusive
        | 3 -> Some Verdict.ToolFailure
        | _ -> None
