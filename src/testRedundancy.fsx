#r "nuget: FParsec, 1.1.1"
#load "Canon.Core/FieldClass.fs"
#load "Canon.Core/Lattice.fs"
#load "Canon.Core/Lineage.fs"
#load "Canon.Introspect/SqlParser.fs"

open Canon.Core
open Canon.Introspect

let c1 = SqlParser.parseConstraint "((interest_pct <= (24)::numeric))"
let c2 = SqlParser.parseConstraint "((interest_pct <= (18)::numeric))"

let combined = SemanticOptimizer.simplify (Lattice.And(c1, c2))

printfn "c1 = %A" c1
printfn "c2 = %A" c2
printfn "combined = %A" combined
