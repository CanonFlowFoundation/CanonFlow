module Canon.Core.Tests.LatticeLaws

open Xunit
open FsCheck
open FsCheck.Xunit
open Canon.Core

// Basic property to test that Not(Not(x)) == x (Involution)
[<Property>]
let ``Double negation is elimination`` (leafValue: string) =
    let l = Leaf leafValue
    Lattice.not(Lattice.not(l)) = l

// De Morgan's Laws: Not(And(a,b)) = Or(Not(a), Not(b))
// Can be added later once we fully implement De Morgan in normalize or smart constructors

