namespace Canon.Cli

open System
open Argu
open Canon.Core
open Canon.Introspect.Postgres
open Canon.Fable

type CliArguments =
    | [<CliPrefix(CliPrefix.DoubleDash)>] Pg of string
    | [<CliPrefix(CliPrefix.DoubleDash)>] Contracts
    | [<CliPrefix(CliPrefix.DoubleDash)>] Demo
    
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Pg _ -> "Introspect a Postgres database using the provided connection string."
            | Contracts -> "Emit JSON Schema and TypeScript clients."
            | Demo -> "Run the 30-minute stranger demo."

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
                if results.Contains(Contracts) then
                    printfn "\n[Emitting OKF Catalog and OpenMetadata JSON]"
                    System.IO.Directory.CreateDirectory("output/openmetadata") |> ignore
                    
                    for t in tables do
                        // OKF Lineage
                        let md = Canon.Contracts.Okf.OkfCatalog.emitLineage t LineageGrade.Declared
                        System.IO.File.WriteAllText($"output/catalog_{t.Name}.md", md)
                        
                        // OpenMetadata
                        let omJson = Canon.Contracts.OpenMetadata.OpenMetadataEmitter.emitTableEntity t
                        System.IO.File.WriteAllText($"output/openmetadata/{t.Name}.json", omJson)
                        
                    printfn "Artifacts successfully saved to 'output/'."
                
                // Demo transpilation of a constraint
                if results.Contains(Demo) then
                    printfn "\n[Transpilation Demo]"
                    let exampleLattice = Lattice.Leaf (Range(Some(Exclusive 0m), None))
                    let tsCode = Transpiler.emitValidator "price" exampleLattice
                    printfn "%s" tsCode
                
                0
            with
            | ex -> 
                printfn $"Error connecting or introspecting: {ex.Message}"
                1
        else
            printfn "%s" (parser.PrintUsage())
            0
