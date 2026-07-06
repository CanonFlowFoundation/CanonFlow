open System
open System.IO
open System.Threading.Tasks
open Testcontainers.PostgreSql
open Npgsql
open Canon.Introspect
open Canon.Introspect.Postgres
open Canon.Emit
open Canon.Core
open Canon.Fable

let runDemo () = task {
    Console.WriteLine("CanonFlow E-Commerce Demo")
    Console.WriteLine("-------------------------")
    
    // 1. Spin up ephemeral DB
    Console.WriteLine("[1/5] Starting Postgres Testcontainer...")
    use container = PostgreSqlBuilder().WithImage("postgres:15-alpine").Build()
    do! container.StartAsync()
    let connStr = container.GetConnectionString()
    
    // 2. Load and execute schema
    Console.WriteLine("[2/5] Creating schema in database...")
    let schemaSql = File.ReadAllText("schema.sql")
    use conn = new NpgsqlConnection(connStr)
    do! conn.OpenAsync()
    use cmd = new NpgsqlCommand(schemaSql, conn)
    let! _ = cmd.ExecuteNonQueryAsync()
    
    // 3. Introspect and extract domain model
    Console.WriteLine("[3/5] Introspecting database schema and constraints...")
    let provider = PostgresSchemaProvider(connStr) :> ISchemaProvider
    let tables = provider.Harvest()
    
    // Optimize the constraints using SemanticOptimizer
    let optimizedTables = 
        tables |> List.map (fun t -> 
            { t with 
                Columns = t.Columns |> List.map (fun c ->
                    { c with 
                        ParsedConstraints = c.ParsedConstraints |> List.map SemanticOptimizer.simplify
                    }
                )
            })
            
    // 4. Generate OpenAPI Specification
    Console.WriteLine("[4/5] Emitting OpenAPI specifications...")
    let openApiDocs = 
        optimizedTables 
        |> List.collect (fun t -> 
            t.Columns |> List.collect (fun c ->
                c.ParsedConstraints |> List.map (fun ast ->
                    let json, fidelity = OpenApiTranspiler.emitSchema (sprintf "%s_%s" t.Name c.Name) ast
                    printfn "  -> OpenAPI Fidelity for %s.%s: %A" t.Name c.Name fidelity
                    json
                )
            )
        )
    let finalOpenApiJson = sprintf "[\n%s\n]" (String.Join(",\n", openApiDocs))
    File.WriteAllText("OpenApi.json", finalOpenApiJson)
    
    // 5. Generate TypeScript Validators
    Console.WriteLine("[5/5] Emitting TypeScript validators...")
    let tsDocs =
        optimizedTables
        |> List.collect (fun t ->
            t.Columns |> List.collect (fun c ->
                c.ParsedConstraints |> List.map (fun ast ->
                    let tsCode, fidelity = Transpiler.emitValidator (sprintf "%s_%s" t.Name c.Name) ast
                    printfn "  -> TypeScript Fidelity for %s.%s: %A" t.Name c.Name fidelity
                    tsCode
                )
            )
        )
    let finalTsCode = String.Join("\n\n", tsDocs)
    File.WriteAllText("validators.ts", finalTsCode)

    // 6. Generate Agent-Readable Semantic Catalog
    Console.WriteLine("[6/6] Emitting Agent-Readable Semantic Catalog (OKF)...")
    let catalogDocs =
        optimizedTables
        |> List.map (fun t -> OkfEmitter.emitAgentCatalog t)
    let finalCatalog = String.Join("\n", catalogDocs)
    File.WriteAllText("AgentCatalog.yaml", finalCatalog)
    
    // Clean up
    do! container.StopAsync()
    
    Console.WriteLine("\nDemo complete! Check OpenApi.json, validators.ts, and AgentCatalog.yaml")
}

[<EntryPoint>]
let main argv =
    runDemo().Wait()
    0
