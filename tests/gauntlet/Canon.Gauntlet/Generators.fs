namespace Canon.Gauntlet

open FsCheck
open Canon.Core

module Generators =
    let genFieldBound =
        gen {
            let! op = Gen.elements [ ">"; "<"; ">="; "<="; "=" ]
            let! v = Arb.generate<int>
            return FieldBound("col", Opaque(sprintf "%s %d" op v))
        }
    
    let genConstraint = 
        Gen.oneof [
            Gen.map Leaf genFieldBound
        ]
        
    let genSchema =
        gen {
            let! tableName = Gen.elements ["test_table"; "random_tbl"]
            let! c = genConstraint
            return tableName, c
        }
