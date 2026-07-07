namespace Canon.Cli

open System
open Argu
open Canon.Core
open Canon.Introspect.Postgres
open Canon.Fable

type CliArguments =
    | [<CliPrefix(CliPrefix.DoubleDash)>] Pg of string
    | [<CliPrefix(CliPrefix.DoubleDash)>] Contracts
    | [<CliPrefix(CliPrefix.DoubleDash)>] ContractsKotlin
    | [<CliPrefix(CliPrefix.DoubleDash)>] ContractsSwift
    | [<CliPrefix(CliPrefix.DoubleDash)>] Demo
    | [<CliPrefix(CliPrefix.DoubleDash)>] Verify
    | [<CliPrefix(CliPrefix.DoubleDash)>] ScaffoldForms
    | [<CliPrefix(CliPrefix.DoubleDash)>] ScaffoldCompose
    | [<CliPrefix(CliPrefix.DoubleDash)>] Diagnose
    | [<CliPrefix(CliPrefix.DoubleDash)>] MigrateTo of string
    | [<CliPrefix(CliPrefix.DoubleDash)>] RestApi
    
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Pg _ -> "Introspect a Postgres database using the provided connection string."
            | Contracts -> "Emit JSON Schema and TypeScript clients."
            | ContractsKotlin -> "Emit Kotlin validators."
            | ContractsSwift -> "Emit Swift validators."
            | Demo -> "Run the 30-minute stranger demo."
            | Verify -> "Run in strict mode for CI/CD. Fails if any constraints are unsupported or approximate."
            | ScaffoldForms -> "Generate AI-assisted React Hook Form components from DB constraints."
            | ScaffoldCompose -> "Generate Jetpack Compose components from DB constraints."
            | Diagnose -> "Run semantic optimizer to find contradictory constraints (e.g. constraints that collapse to False)."
            | MigrateTo _ -> "Compare with a target database and generate migration SQL scripts."
            | RestApi -> "Generate a PostgREST-style F# Giraffe API."

module Program =
    [<EntryPoint>]
    let main argv =
        let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
        let parser = ArgumentParser.Create<CliArguments>(programName = "canonflow", errorHandler = errorHandler)
        
        let results = parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)
        
        if results.Contains(Pg) then
            let connStr = results.GetResult(Pg)
            printfn "Starting CanonFlow Introspection on Postgres..."
            
            try
                let provider = PostgresSchemaProvider(connStr) :> Canon.Introspect.ISchemaProvider
                let tables = provider.Harvest()
                
                printfn "\n[Harvest Results]"
                for t in tables do
                    printfn $"Table: {t.Schema}.{t.Name}"
                    for c in t.Columns do
                        let constraints = String.Join(", ", c.CheckConstraints)
                        printfn $"  - {c.Name} ({c.DataType}) -> {constraints}"
                
                // Emitting the Semantic Catalog and Contracts
                if results.Contains(Diagnose) then
                    printfn "\n[Diagnostic Engine] Scanning for logical contradictions..."
                    let mutable foundContradiction = false
                    for t in tables do
                        for c in t.Columns do
                            if c.CheckConstraints.Length > 1 then
                                let lattice = 
                                    c.CheckConstraints 
                                    |> List.map (fun s -> if s.StartsWith("CHECK ") then s.Substring(6) else s)
                                    |> List.map Canon.Introspect.SqlParser.parseConstraint
                                    |> List.reduce (fun a b -> Lattice.And(a, b))
                                
                                let simplified = Canon.Core.SemanticOptimizer.simplify lattice
                                if simplified = Lattice.False then
                                    let constraintsStr = String.Join(" AND ", c.CheckConstraints)
                                    printfn $"[DIAGNOSTIC CONTRADICTION] Table: {t.Name}, Column: {c.Name} has contradictory constraints that collapse to False: {constraintsStr}"
                                    foundContradiction <- true
                    
                    if not foundContradiction then
                        printfn "No logical contradictions found in the schema."

                    let mutable foundRedundancy = false
                    for t in tables do
                        for c in t.Columns do
                            if c.CheckConstraints.Length > 1 then
                                let parsed = 
                                    c.CheckConstraints 
                                    |> List.map (fun s -> s, if s.StartsWith("CHECK ") then s.Substring(6) else s)
                                    |> List.map (fun (orig, s) -> orig, Canon.Introspect.SqlParser.parseConstraint s)
                                    
                                for i in 0 .. parsed.Length - 1 do
                                    for j in i + 1 .. parsed.Length - 1 do
                                        let s1_str, c1 = parsed.[i]
                                        let s2_str, c2 = parsed.[j]
                                        let combined = Canon.Core.SemanticOptimizer.simplify (Lattice.And(c1, c2))
                                        if combined <> Lattice.False then
                                            let sim1 = Canon.Core.SemanticOptimizer.simplify c1
                                            let sim2 = Canon.Core.SemanticOptimizer.simplify c2
                                            if combined = sim1 && sim1 <> sim2 then
                                                printfn $"[DIAGNOSTIC REDUNDANCY] Table: {t.Name}, Column: {c.Name} has redundant constraint '{s2_str}' which is subsumed by '{s1_str}'"
                                                foundRedundancy <- true
                                            elif combined = sim2 && sim1 <> sim2 then
                                                printfn $"[DIAGNOSTIC REDUNDANCY] Table: {t.Name}, Column: {c.Name} has redundant constraint '{s1_str}' which is subsumed by '{s2_str}'"
                                                foundRedundancy <- true
                                            elif combined = sim1 && combined = sim2 then
                                                printfn $"[DIAGNOSTIC REDUNDANCY] Table: {t.Name}, Column: {c.Name} has duplicate constraints '{s1_str}' and '{s2_str}'"
                                                foundRedundancy <- true
                    
                    if not foundRedundancy then
                        printfn "No redundancies found in the schema."
                    
                    printfn "\n[Golden Rule Heuristics] Analyzing DDD vs DB-First boundaries..."
                    for t in tables do
                        if t.Columns |> List.forall (fun c -> c.CheckConstraints.IsEmpty) then
                            printfn $"  [HEURISTIC WARNING] Table '{t.Name}' has ZERO check constraints."
                            printfn $"    -> If this table models structural Nouns (e.g. Accounts, Limits), you MUST enforce boundaries in DB-First."
                            printfn $"    -> If this table models transient Verbs/Events, proceed with DDD Code-First."
                        
                        for c in t.Columns do
                            for constraintStr in c.CheckConstraints do
                                if constraintStr.ToUpper().Contains("CASE") && constraintStr.ToUpper().Contains("WHEN") then
                                    printfn $"  [HEURISTIC WARNING] Table '{t.Name}', Column '{c.Name}': Complex CASE/WHEN constraint detected."
                                    printfn $"    -> '{constraintStr}'"
                                    printfn $"    -> If this represents workflow or state-transition logic, it is a Verb. It is best done in DDD Code-First."
                if results.Contains(Contracts) then
                    printfn "\n[Emitting OKF Catalog, OpenMetadata, and TypeScript]"
                    System.IO.Directory.CreateDirectory("output/openmetadata") |> ignore
                    System.IO.Directory.CreateDirectory("client/src") |> ignore
                    
                    let tsSb = System.Text.StringBuilder()
                    tsSb.AppendLine("// AUTO-GENERATED BY CANONFLOW") |> ignore
                    
                    for t in tables do
                        let md = Canon.Contracts.Okf.OkfCatalog.emitLineage t LineageGrade.Declared
                        System.IO.File.WriteAllText($"output/catalog_{t.Name}.md", md)
                        
                        let omJson = Canon.Contracts.OpenMetadata.OpenMetadataEmitter.emitTableEntity t
                        System.IO.File.WriteAllText($"output/openmetadata/{t.Name}.json", omJson)
                        
                        for c in t.Columns do
                            if not c.CheckConstraints.IsEmpty then
                                let lattice = 
                                    c.CheckConstraints 
                                    |> List.map (fun s -> if s.StartsWith("CHECK ") then s.Substring(6) else s)
                                    |> List.map Canon.Introspect.SqlParser.parseConstraint 
                                    |> List.reduce (fun a b -> Lattice.And(a, b))
                                let tsCode, fidelity = Transpiler.emitValidator $"{t.Name}_{c.Name}" lattice c.IsNullable
                                tsSb.AppendLine($"// Fidelity: {fidelity}") |> ignore
                                tsSb.AppendLine(tsCode) |> ignore
                    
                    System.IO.File.WriteAllText("client/src/validators.ts", tsSb.ToString())
                    printfn "Artifacts saved to 'output/' and TypeScript generated in 'client/src/validators.ts'."

                if results.Contains(ContractsKotlin) then
                    printfn "\n[Emitting Kotlin Validators]"
                    System.IO.Directory.CreateDirectory("client/android/validators") |> ignore
                    let ktSb = System.Text.StringBuilder()
                    ktSb.AppendLine("package com.layam.validators") |> ignore
                    ktSb.AppendLine("// AUTO-GENERATED BY CANONFLOW") |> ignore
                    for t in tables do
                        for c in t.Columns do
                            if not c.CheckConstraints.IsEmpty then
                                let lattice = 
                                    c.CheckConstraints 
                                    |> List.map (fun s -> if s.StartsWith("CHECK ") then s.Substring(6) else s)
                                    |> List.map Canon.Introspect.SqlParser.parseConstraint 
                                    |> List.reduce (fun a b -> Lattice.And(a, b))
                                let ktCode, fidelity = KotlinTranspiler.emitValidator $"{t.Name}_{c.Name}" lattice c.IsNullable
                                ktSb.AppendLine($"// Fidelity: {fidelity}") |> ignore
                                ktSb.AppendLine(ktCode) |> ignore
                    System.IO.File.WriteAllText("client/android/validators/Validators.kt", ktSb.ToString())
                    printfn "Kotlin validators generated in 'client/android/validators/Validators.kt'."

                if results.Contains(ContractsSwift) then
                    printfn "\n[Emitting Swift Validators]"
                    System.IO.Directory.CreateDirectory("client/ios/validators") |> ignore
                    let swSb = System.Text.StringBuilder()
                    swSb.AppendLine("import Foundation") |> ignore
                    swSb.AppendLine("// AUTO-GENERATED BY CANONFLOW") |> ignore
                    for t in tables do
                        for c in t.Columns do
                            if not c.CheckConstraints.IsEmpty then
                                let lattice = 
                                    c.CheckConstraints 
                                    |> List.map (fun s -> if s.StartsWith("CHECK ") then s.Substring(6) else s)
                                    |> List.map Canon.Introspect.SqlParser.parseConstraint 
                                    |> List.reduce (fun a b -> Lattice.And(a, b))
                                let swCode, fidelity = SwiftTranspiler.emitValidator $"{t.Name}_{c.Name}" lattice c.IsNullable
                                swSb.AppendLine($"// Fidelity: {fidelity}") |> ignore
                                swSb.AppendLine(swCode) |> ignore
                    System.IO.File.WriteAllText("client/ios/validators/Validators.swift", swSb.ToString())
                    printfn "Swift validators generated in 'client/ios/validators/Validators.swift'."

                if results.Contains(Contracts) || results.Contains(ContractsKotlin) || results.Contains(ContractsSwift) then
                    // Generate the Mathematical Proof Report
                    let proofReport, totalApprox, totalUnsupported = Canon.Cli.ProofEmitter.emitProofReport tables
                    System.IO.File.WriteAllText("output/PROOF.md", proofReport)
                    printfn "\n[Proof Engine] Signed certification generated at 'output/PROOF.md'."
                    
                    if results.Contains(Verify) then
                        if totalUnsupported > 0 || totalApprox > 0 then
                            printfn $"\n[CI/CD CHECK FAILED] Found {totalUnsupported} unsupported and {totalApprox} approximate bounds!"
                            Environment.Exit(1)
                        else
                            printfn "\n[CI/CD CHECK PASSED] 100%% exact mathematical fidelity achieved!"
                
                // Scaffold React Forms
                if results.Contains(ScaffoldForms) then
                    printfn "\n[Scaffolding AI-Assisted React Forms]"
                    System.IO.Directory.CreateDirectory("client/src/forms") |> ignore
                    for t in tables do
                        let formCode = 
                            let aiForm = Canon.Cli.ReactScaffold.tryGenerateSmartFormAsync t |> Async.RunSynchronously
                            match aiForm with
                            | Some code -> 
                                printfn "  -> ✨ AI Smart Generation successful for %s!" t.Name
                                code
                            | None -> 
                                Canon.Cli.ReactScaffold.generateForm t
                        let fileName = $"client/src/forms/{t.Name.Substring(0, 1).ToUpper()}{t.Name.Substring(1)}Form.tsx"
                        System.IO.File.WriteAllText(fileName, formCode)
                        printfn $"Generated React Form: {fileName}"

                // Scaffold Jetpack Compose
                if results.Contains(ScaffoldCompose) then
                    printfn "\n[Scaffolding Jetpack Compose UI]"
                    System.IO.Directory.CreateDirectory("client/android/compose") |> ignore
                    for t in tables do
                        let formCode = Canon.Cli.ComposeScaffold.generateForm t
                        let fileName = $"client/android/compose/{t.Name.Substring(0, 1).ToUpper()}{t.Name.Substring(1)}Form.kt"
                        System.IO.File.WriteAllText(fileName, formCode)
                        printfn $"Generated Compose Form: {fileName}"

                // Demo transpilation of a constraint
                if results.Contains(Demo) then
                    printfn "\n[Transpilation Demo]"
                    let exampleLattice = Lattice.Leaf (Range(Some(Exclusive 0m), None))
                    let tsCode, fidelity = Transpiler.emitValidator "price" exampleLattice false
                    printfn "Fidelity: %A" fidelity
                    printfn "%s" tsCode
                
                // Diff and Migration Output
                if results.Contains(MigrateTo) then
                    printfn "\n[Schema Migration Engine]"
                    let newConnStr = results.GetResult(MigrateTo)
                    let newProvider = PostgresSchemaProvider(newConnStr) :> Canon.Introspect.ISchemaProvider
                    let newTables = newProvider.Harvest()
                    
                    let migrationSql = Canon.Emit.MigrationEmitter.generateMigration tables newTables
                    System.IO.Directory.CreateDirectory("output/migrations") |> ignore
                    let path = $"output/migrations/{DateTime.UtcNow:yyyyMMddHHmmss}_migration.sql"
                    System.IO.File.WriteAllText(path, migrationSql)
                    printfn $"Migration script generated: {path}"
                    printfn "\n=== MIGRATION PREVIEW ==="
                    printfn "%s" migrationSql
                
                // CanonFlowRest Engine
                if results.Contains(RestApi) then
                    printfn "\n[CanonFlowRest Engine] Scaffolding PostgREST-style F# Giraffe API..."
                    System.IO.Directory.CreateDirectory("server/src") |> ignore
                    let apiSb = System.Text.StringBuilder()
                    apiSb.AppendLine("module CanonFlowRest.Api") |> ignore
                    apiSb.AppendLine("open Giraffe") |> ignore
                    apiSb.AppendLine("open Microsoft.AspNetCore.Http") |> ignore
                    apiSb.AppendLine("open Npgsql") |> ignore
                    apiSb.AppendLine("open Dapper") |> ignore
                    apiSb.AppendLine("open System.Threading.Tasks") |> ignore
                    apiSb.AppendLine("") |> ignore
                    apiSb.AppendLine("// AUTO-GENERATED BY CANONFLOWREST") |> ignore
                    apiSb.AppendLine("// These endpoints provide Noun-level CRUD backed by the DB boundaries.") |> ignore
                    apiSb.AppendLine("// For complex workflows (Verbs), implement custom Handlers below.") |> ignore
                    apiSb.AppendLine("") |> ignore
                    
                    let routes = System.Text.StringBuilder()
                    routes.AppendLine("let webApp (connectionString: string) =") |> ignore
                    routes.AppendLine("    choose [") |> ignore
                    
                    for t in tables do
                        apiSb.AppendLine($"let get{t.Name}Handler (connString: string) : HttpHandler =") |> ignore
                        apiSb.AppendLine($"    fun (next : HttpFunc) (ctx : HttpContext) ->") |> ignore
                        apiSb.AppendLine($"        task {{") |> ignore
                        apiSb.AppendLine($"            use conn = new NpgsqlConnection(connString)") |> ignore
                        apiSb.AppendLine($"            let! jsonResult = conn.QuerySingleOrDefaultAsync<string>(\"SELECT COALESCE(json_agg(row_to_json(t)), '[]') FROM {t.Name} t\")") |> ignore
                        apiSb.AppendLine($"            let result = if System.String.IsNullOrEmpty(jsonResult) then \"[]\" else jsonResult") |> ignore
                        apiSb.AppendLine($"            ctx.SetHttpHeader(\"Content-Type\", \"application/json\")") |> ignore
                        apiSb.AppendLine($"            return! ctx.WriteStringAsync(result)") |> ignore
                        apiSb.AppendLine($"        }}") |> ignore
                        apiSb.AppendLine("") |> ignore
                        apiSb.AppendLine($"let post{t.Name}Handler (connString: string) : HttpHandler =") |> ignore
                        apiSb.AppendLine($"    fun (next : HttpFunc) (ctx : HttpContext) ->") |> ignore
                        apiSb.AppendLine($"        task {{") |> ignore
                        apiSb.AppendLine($"            // OVERRIDE FOR DDD VERBS") |> ignore
                        apiSb.AppendLine($"            // Parse JSON payload into DTO, run DDD logic, and INSERT.") |> ignore
                        apiSb.AppendLine($"            ctx.SetStatusCode 501") |> ignore
                        apiSb.AppendLine($"            return! text \"POST /{t.Name} - Auto-Insert Not Fully Implemented (Override Here)\" next ctx") |> ignore
                        apiSb.AppendLine($"        }}") |> ignore
                        apiSb.AppendLine("") |> ignore
                        
                        routes.AppendLine($"        route \"/api/{t.Name}\" >=> choose [") |> ignore
                        routes.AppendLine($"            GET >=> get{t.Name}Handler connectionString") |> ignore
                        routes.AppendLine($"            POST >=> post{t.Name}Handler connectionString") |> ignore
                        routes.AppendLine($"        ]") |> ignore
                        
                    routes.AppendLine("        setStatusCode 404 >=> text \"Not Found\"") |> ignore
                    routes.AppendLine("    ]") |> ignore
                    
                    apiSb.AppendLine("") |> ignore
                    apiSb.Append(routes.ToString()) |> ignore
                    
                    let path = "server/src/Api.fs"
                    System.IO.File.WriteAllText(path, apiSb.ToString())
                    printfn $"CanonFlowRest Giraffe API generated at: {path}"

                0
            with
            | ex -> 
                printfn $"Error connecting or introspecting: {ex.Message}"
                1
        else
            printfn "%s" (parser.PrintUsage())
            0
