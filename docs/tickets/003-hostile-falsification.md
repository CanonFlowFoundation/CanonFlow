# Ticket 3: Property-Based Hostile Falsification

## Context
In GSTFlow, property-based tests (FsCheck) that generated structurally invalid inputs but mathematically valid checksums proved invaluable at breaking the state machine. We called this "Operation DIVE".

## Action for CanonFlow
CanonFlow relies on `Canon.Core/Lattice.fs` for boolean logic simplifications and AST processing. We need property-based tests using FsCheck to ensure that the lattice's `toNNF` (Negation Normal Form) logic and `SemanticOptimizer` do not drop constraints under hostile / deeply nested boolean operations. 
We need tests that generate random deep lattices and verify that evaluating them against any context yields the same result before and after `toNNF` and `simplify`.
