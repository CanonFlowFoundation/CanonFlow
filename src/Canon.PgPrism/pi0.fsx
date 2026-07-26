#I @"bin/Debug/net10.0"
#r @"Canon.Core.dll"
#r @"Canon.PgPrism.dll"
#r "nuget: PgQuery"

open PgQuery
open System.Text.Json
open Canon.PgPrism
open Canon.Core

let sql = """
CREATE TABLE accounts (
    id UUID PRIMARY KEY,
    username VARCHAR(50) NOT NULL UNIQUE,
    balance NUMERIC(15,2) NOT NULL,
    CONSTRAINT balance_positive CHECK (balance >= 0)
);
"""

printfn "=== 1. SQL -> JSON (PgQuery) ==="
let result = PgQuery.Parse(sql)
if result.Error <> null then
    printfn "Parse Error: %s" result.Error.Message
else
    printfn "Parse Successful!"
    let json = result.ParseTree
    let doc = JsonDocument.Parse(json)
    
    printfn "\n=== 2. JSON -> CanonFlow IR (Decode.fs) ==="
    let tables = Decode.decodeSchema doc.RootElement
    let accountTable = tables |> List.head
    printfn "Extracted Table: %s" accountTable.Name
    printfn "Columns:"
    for c in accountTable.Columns do
        printfn "  - %s (Type: %A, NotNull: %b, PK: %b)" c.Name c.Type c.NotNull c.PrimaryKey
    
    printfn "Constraints (Lattice AST):"
    for c in accountTable.Constraints do
        printfn "  - %s: %A" c.Name c.Expr
        
    printfn "\n=== 3. CanonFlow IR -> F# Domain (Emit.fs) ==="
    let fsCode = Emit.generateFSharpDomain accountTable
    printfn "%s" fsCode
    
    printfn "=== 4. CanonFlow IR -> SQL DDL (Sql.fs) ==="
    let sqlOut = Sql.emitTable accountTable
    printfn "%s" sqlOut
