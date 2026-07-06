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

type LatticeGenerators =
    static member Lattice() =
        let rec latticeGen size =
            if size <= 0 then
                Gen.oneof [
                    Gen.constant True
                    Gen.constant False
                    Arb.generate<int> |> Gen.map Leaf
                ]
            else
                Gen.oneof [
                    Gen.constant True
                    Gen.constant False
                    Arb.generate<int> |> Gen.map Leaf
                    latticeGen (size / 2) |> Gen.map Not
                    Gen.map2 (fun a b -> And(a, b)) (latticeGen (size / 2)) (latticeGen (size / 2))
                    Gen.map2 (fun a b -> Or(a, b)) (latticeGen (size / 2)) (latticeGen (size / 2))
                ]
        let rec shrinkLattice l =
            match l with
            | True | False | Leaf _ -> seq []
            | Not x -> seq { yield x; yield! shrinkLattice x |> Seq.map Not }
            | And(a, b) -> 
                seq {
                    yield a
                    yield b
                    yield! shrinkLattice a |> Seq.map (fun a' -> And(a', b))
                    yield! shrinkLattice b |> Seq.map (fun b' -> And(a, b'))
                }
            | Or(a, b) -> 
                seq {
                    yield a
                    yield b
                    yield! shrinkLattice a |> Seq.map (fun a' -> Or(a', b))
                    yield! shrinkLattice b |> Seq.map (fun b' -> Or(a, b'))
                }

        Arb.fromGenShrink (Gen.sized latticeGen, shrinkLattice)

[<assembly: Properties(Arbitrary = [| typeof<LatticeGenerators> |])>]
do()

[<Property>]
let ``De Morgan: not (a AND b) ≡ (not a) OR (not b)`` (a: Lattice<int>) (b: Lattice<int>) (evalFn: int -> bool) =
    let left = Lattice.not (Lattice.and' a b)
    let right = Lattice.or' (Lattice.not a) (Lattice.not b)
    Lattice.equivalent left right evalFn

[<Property>]
let ``De Morgan: not (a OR b) ≡ (not a) AND (not b)`` (a: Lattice<int>) (b: Lattice<int>) (evalFn: int -> bool) =
    let left = Lattice.not (Lattice.or' a b)
    let right = Lattice.and' (Lattice.not a) (Lattice.not b)
    Lattice.equivalent left right evalFn

[<Property>]
let ``Double negation is elimination semantically`` (l: Lattice<int>) (evalFn: int -> bool) =
    let nn = Lattice.not(Lattice.not(l))
    Lattice.equivalent nn l evalFn
