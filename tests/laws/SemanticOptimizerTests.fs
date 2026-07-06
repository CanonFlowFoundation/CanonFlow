module Canon.Core.Tests.SemanticOptimizerTests

open Xunit
open FsCheck
open FsCheck.Xunit
open Canon.Core
open Canon.Core.Tests.AgreementTests

[<Property>]
let ``Semantic Optimizer preserves semantics`` (l: Lattice<Constraint>) (value: int) =
    let simplified = SemanticOptimizer.simplify l
    evalLattice simplified value = evalLattice l value

[<Property>]
let ``Semantic Optimizer is idempotent`` (l: Lattice<Constraint>) =
    let once = SemanticOptimizer.simplify l
    let twice = SemanticOptimizer.simplify once
    once = twice

[<Fact>]
let ``age > 18 AND age > 21 simplifies to > 21`` () =
    let age18 = Lattice.Leaf(FieldBound("age", Range(Some(Exclusive 18m), None)))
    let age21 = Lattice.Leaf(FieldBound("age", Range(Some(Exclusive 21m), None)))
    let query = Lattice.And(age18, age21)
    
    let simplified = SemanticOptimizer.simplify query
    
    Assert.Equal(age21, simplified)

[<Fact>]
let ``age > 50 AND age < 30 simplifies to False (contradiction)`` () =
    let age50 = Lattice.Leaf(FieldBound("age", Range(Some(Exclusive 50m), None)))
    let age30 = Lattice.Leaf(FieldBound("age", Range(None, Some(Exclusive 30m))))
    let query = Lattice.And(age50, age30)
    
    let simplified = SemanticOptimizer.simplify query
    
    Assert.Equal(Lattice.False, simplified)
