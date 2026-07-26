open PgSqlParser
open System.Text.Json
open Canon.PgPrism
open Canon.Core

[<EntryPoint>]
let main argv =
    let sql = """
    CREATE TYPE status_enum AS ENUM ('active', 'inactive');

    CREATE TABLE users (
        id UUID PRIMARY KEY,
        status status_enum DEFAULT 'active',
        metadata JSONB,
        tags TEXT[]
    );

    CREATE TABLE posts (
        id UUID PRIMARY KEY,
        user_id UUID,
        title VARCHAR(100),
        published_at TIMESTAMP,
        CONSTRAINT fk_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
        CONSTRAINT check_title_published CHECK (
            (published_at IS NULL) OR (title IS NOT NULL)
        ),
        UNIQUE (user_id, title)
    );
    """

    printfn "=== 1. SQL -> JSON (PgQuery) ==="
    let result = Parser.Parse(sql)
    if not result.IsSuccess then
        printfn "Parse Error: %A" result.Error
    else
        printfn "Parse Successful!"
        // Format the Protobuf ParseResult as JSON for our JSON decoder
        let json = Google.Protobuf.JsonFormatter.Default.Format(result.Value)
        let doc = JsonDocument.Parse(json)
        
        printfn "\n=== 2. JSON -> CanonFlow IR (Decode.fs) ==="
        let tables = Decode.decodeSchema doc.RootElement
        for table in tables do
            printfn "\nExtracted Table: %s" table.Name
            printfn "Columns:"
            for c in table.Columns do
                printfn "  - %s (Type: %A, NotNull: %b, PK: %b, Unique: %b, Default: %A)" c.Name c.Type c.NotNull c.PrimaryKey c.Unique c.Default
            
            printfn "Constraints (Lattice AST):"
            for c in table.Constraints do
                printfn "  - %s: %A" c.Name c.Expr
                
            printfn "Foreign Keys:"
            for fk in table.ForeignKeys do
                printfn "  - %s -> %s" fk.Name fk.TargetTable
                
            printfn "\n=== 3. CanonFlow IR -> F# Domain (Emit.fs) ==="
            let fsCode = Emit.generateFSharpDomain table
            printfn "%s" fsCode
            
            printfn "=== 4. CanonFlow IR -> SQL DDL (Sql.fs) ==="
            let sqlOut = Sql.emitTable table
            printfn "%s" sqlOut
    
    0
