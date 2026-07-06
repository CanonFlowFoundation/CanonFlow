# ADR-014: Phantom Tag on Functional Refined DU

**Status:** Accepted
**Date:** 2026-07-06

## Context
In purging the OO-flavored `IPredicate<'T>` interface to unify predicates under `Lattice<Constraint>`, we initially stripped the phantom type entirely, resulting in `Refined<'T>`. However, this degraded the "types as proofs" guarantee: `Refined<decimal>` proving positivity and `Refined<decimal>` proving a range collapsed into the exact same type at compile-time.

## Decision
We restore the phantom tag: `Refined<'T, 'Tag>`. 

The core type remains a pure DU storing a `Lattice<Constraint>` internally, but developers can now use empty marker types (e.g. `type Positive = class end`) to maintain compile-time boundary enforcement, recovering type-level proof identity while retaining our unified semantic algebra.
