namespace CanonFlow.Assurance

type Verdict =
    | Pass
    | Fail
    | Inconclusive
    | ToolFailure

module Verdict =
    
    // XR-3: The aggregate of no verdicts is Inconclusive, never Pass.
    // Folding the empty list yields the identity for the empty set.
    let aggregate (verdicts: Verdict list) =
        match verdicts with
        | [] -> Inconclusive
        | vs ->
            if vs |> List.contains ToolFailure then ToolFailure
            elif vs |> List.contains Fail then Fail
            elif vs |> List.contains Inconclusive then Inconclusive
            else Pass
