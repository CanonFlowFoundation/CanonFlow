#r "nuget: FParsec, 1.1.1"
#load "Canon.Core/FieldClass.fs"
#load "Canon.Core/Lattice.fs"
#load "Canon.Core/Lineage.fs"
#load "Canon.Introspect/SqlParser.fs"

open Canon.Core
open Canon.Introspect.SqlParser

let tests = [
    // S1
    "((guarantor_share_pct >= (10)::numeric))"
    // S2
    "(((age >= 21) OR (guardian_member_id IS NOT NULL)))"
    // S3
    "(((ledger_adjustment >= ('-5000'::integer)::numeric) AND (ledger_adjustment <= (5000)::numeric)))"
    // S4
    "((interest_pct > (0)::numeric))"
    "((interest_pct <= (24)::numeric))"
    "((interest_pct <= (18)::numeric))"
    // S6
    "((((\"riskGrade\")::text >= 'A'::text) AND ((\"riskGrade\")::text <= 'E'::text)))"
]

for t in tests do
    printfn "\nParsing: %s" t
    printfn "Result: %A" (parseConstraint t)
