namespace Canon.Core

/// Represents Helios capability-typed field kinds for semantic indexing.
type FieldKind =
    | Keyword
    | Text
    | TextWithKeyword
    | Date
    | Numeric
    | Bool
    | Nested

/// Represents boundary definitions for Symphony constraints.
type Bound<'T> =
    | Inclusive of 'T
    | Exclusive of 'T

/// Defines common validation constraints that can be used as Leaves in our Lattice (Symphony model).
type Constraint =
    | Range of lo: Bound<decimal> option * hi: Bound<decimal> option
    | IntRange of lo: Bound<int64> option * hi: Bound<int64> option
    | StringRange of lo: Bound<string> option * hi: Bound<string> option
    | MaxLength of int
    | InList of string list
    | InSet of string list // represents IN or ANY(ARRAY[...])
    | RelativeBound of colA: string * op: string * colB: string
    | NonEmpty
    | PrimaryKey
    | Opaque of string
    | FieldBound of string * Constraint

/// A closed six-constructor bounded complemented lattice for query formulation.
type Lattice<'Leaf> =
    | True
    | False
    | Leaf of 'Leaf
    | Not of Lattice<'Leaf>
    | And of Lattice<'Leaf> * Lattice<'Leaf>
    | Or of Lattice<'Leaf> * Lattice<'Leaf>

[<RequireQualifiedAccess>]
module Lattice =
    let not l =
        match l with
        | True -> False
        | False -> True
        | Not x -> x
        | _ -> Not l

    let and' left right =
        match left, right with
        | True, x | x, True -> x
        | False, _ | _, False -> False
        | _ -> And(left, right)

    let or' left right =
        match left, right with
        | True, _ | _, True -> True
        | False, x | x, False -> x
        | _ -> Or(left, right)

    let rec toNNF (l: Lattice<'Leaf>) : Lattice<'Leaf> =
        match l with
        | True -> True
        | False -> False
        | Leaf c -> Leaf c
        | And(a, b) -> And(toNNF a, toNNF b)
        | Or(a, b) -> Or(toNNF a, toNNF b)
        | Not inner ->
            match inner with
            | True -> False
            | False -> True
            | Leaf c -> Not (Leaf c)
            | Not x -> toNNF x
            | And(a, b) -> Or(toNNF (Not a), toNNF (Not b))
            | Or(a, b) -> And(toNNF (Not a), toNNF (Not b))

    let rec eval (evalLeaf: 'Leaf -> bool) (l: Lattice<'Leaf>) =
        match l with
        | True -> true
        | False -> false
        | Leaf x -> evalLeaf x
        | Not x -> Operators.not (eval evalLeaf x)
        | And(a, b) -> (eval evalLeaf a) && (eval evalLeaf b)
        | Or(a, b) -> (eval evalLeaf a) || (eval evalLeaf b)

    let equivalent (a: Lattice<'Leaf>) (b: Lattice<'Leaf>) (evalLeaf: 'Leaf -> bool) =
        eval evalLeaf a = eval evalLeaf b

module SemanticOptimizer =
    let intersectBounds (b1: Bound<decimal> option) (b2: Bound<decimal> option) (isMax: bool) : Bound<decimal> option =
        match b1, b2 with
        | None, b | b, None -> b
        | Some x, Some y ->
            let xv = match x with Inclusive v -> v | Exclusive v -> v
            let yv = match y with Inclusive v -> v | Exclusive v -> v
            if isMax then
                if xv < yv then Some x
                elif yv < xv then Some y
                else
                    match x, y with
                    | Exclusive _, _ | _, Exclusive _ -> Some (Exclusive xv)
                    | _ -> Some (Inclusive xv)
            else
                if xv > yv then Some x
                elif yv > xv then Some y
                else
                    match x, y with
                    | Exclusive _, _ | _, Exclusive _ -> Some (Exclusive xv)
                    | _ -> Some (Inclusive xv)

    let rec simplify (l: Lattice<Constraint>) : Lattice<Constraint> =
        match l with
        | Lattice.And(Lattice.Leaf x, Lattice.Leaf y) when x = y -> Lattice.Leaf x
        | Lattice.Or(Lattice.Leaf x, Lattice.Leaf y) when x = y -> Lattice.Leaf x
        | Lattice.And(Lattice.Leaf (FieldBound(f1, Range(min1, max1))), Lattice.Leaf (FieldBound(f2, Range(min2, max2)))) when f1 = f2 ->
            let newMin = intersectBounds min1 min2 false
            let newMax = intersectBounds max1 max2 true
            
            let isValid = 
                match newMin, newMax with
                | Some minB, Some maxB ->
                    let minV = match minB with Inclusive v -> v | Exclusive v -> v
                    let maxV = match maxB with Inclusive v -> v | Exclusive v -> v
                    if minV > maxV then false
                    elif minV = maxV then
                        match minB, maxB with
                        | Inclusive _, Inclusive _ -> true
                        | _ -> false
                    else true
                | _ -> true
                
            if isValid then Lattice.Leaf(FieldBound(f1, Range(newMin, newMax)))
            else Lattice.False
        | Lattice.And(a, b) ->
            let sa = simplify a
            let sb = simplify b
            if sa = sb then sa
            elif sa = Lattice.False || sb = Lattice.False then Lattice.False
            elif sa = Lattice.True then sb
            elif sb = Lattice.True then sa
            else
                match sa, sb with
                | Lattice.Leaf (FieldBound(f1, Range(min1, max1))), Lattice.Leaf (FieldBound(f2, Range(min2, max2))) when f1 = f2 ->
                    simplify (Lattice.And(sa, sb)) // Retry combining
                | _ -> Lattice.And(sa, sb)
        | Lattice.Or(a, b) ->
            let sa = simplify a
            let sb = simplify b
            if sa = sb then sa
            elif sa = Lattice.True || sb = Lattice.True then Lattice.True
            elif sa = Lattice.False then sb
            elif sb = Lattice.False then sa
            else Lattice.Or(sa, sb)
        | Lattice.Not inner ->
            let sInner = simplify inner
            match sInner with
            | Lattice.True -> Lattice.False
            | Lattice.False -> Lattice.True
            | _ -> Lattice.Not sInner
        | _ -> l

/// Phantom-typed query shell.
type Query<'doc, 'Leaf> = {
    Predicate: Lattice<'Leaf>
}
