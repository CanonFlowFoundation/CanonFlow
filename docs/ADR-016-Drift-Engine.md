# ADR 016: Drift Engine

## Context
Real-world systems suffer from "drift" where the database rules (Source) diverge from the API or Frontend rules (Target). A common enterprise problem is missing constraints in the frontend, leading to late validation failures or data inconsistency. CanonFlow must explicitly detect and classify this drift to be useful for brownfield projects (Audit §5).

## Decision
We will implement a `DriftEngine` in `Canon.Core` that compares a `Source` Lattice and a `Target` Lattice for a given field and returns a `DriftStatus`.

```fsharp
type DriftStatus =
    | Aligned          // Source and Target enforce the exact same boundaries.
    | StrictTarget     // Target enforces everything Source does, plus more. Safe, but possibly restrictive.
    | LooseTarget      // Target is missing constraints enforced by Source. Unsafe.
    | Disjoint         // Target and Source have mutually exclusive constraints. Unsafe.
```

## Mechanism
Since our `SemanticOptimizer` already computes bounded intersections, we can determine implication by observing the result of intersections:
- If `optimize(Source AND Target) == optimize(Source)`, then `Source` is stronger than or equal to `Target` (i.e. Target is Loose or Aligned).
- If `optimize(Source AND Target) == optimize(Target)`, then `Target` is stronger than or equal to `Source` (i.e. Target is Strict or Aligned).

By combining these:
1. `Source == Target` -> Aligned
2. `(Source AND Target) == Target` -> StrictTarget
3. `(Source AND Target) == Source` -> LooseTarget
4. `(Source AND Target) == False` -> Disjoint
5. Otherwise -> Disjoint/Complex Drift

## Consequences
- We rely on `SemanticOptimizer` to accurately simplify expressions.
- We can produce a "Drift Severity Report" per column by mapping over the entire schema.
