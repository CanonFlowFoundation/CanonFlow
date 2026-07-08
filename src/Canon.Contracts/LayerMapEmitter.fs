namespace Canon.Contracts

open Canon.Core
open Canon.Introspect
open System.Text

module LayerMapEmitter =
    let generateMap (tables: TableDef list) =
        let sb = StringBuilder()
        sb.AppendLine("# CanonFlow Layer Map") |> ignore
        sb.AppendLine("") |> ignore
        sb.AppendLine("This map defines the architectural boundary for each entity in the system.") |> ignore
        sb.AppendLine("") |> ignore
        
        let dbEnforced = 
            tables |> List.filter (fun t -> t.Columns |> List.exists (fun c -> not c.CheckConstraints.IsEmpty))
        let appEnforced = 
            tables |> List.filter (fun t -> t.Columns |> List.forall (fun c -> c.CheckConstraints.IsEmpty))
            
        sb.AppendLine("## Layer 1: DB-Enforced Structural Truth") |> ignore
        sb.AppendLine("These entities model pure structural nouns. Their constraints are enforced directly by Postgres CHECK constraints.") |> ignore
        for t in dbEnforced do
            sb.AppendLine($"- **{t.Name}**") |> ignore
        sb.AppendLine("") |> ignore
        
        sb.AppendLine("## Layer 2: App-Enforced Business Behavior") |> ignore
        sb.AppendLine("These entities model workflow and state-transition logic (Verbs). Their rules live in the application layer.") |> ignore
        for t in appEnforced do
            sb.AppendLine($"- **{t.Name}**") |> ignore
        sb.AppendLine("") |> ignore
        
        sb.AppendLine("## Layer 3: Unenforced Master Data") |> ignore
        sb.AppendLine("These entities map to external truths (e.g. EDI SKUs) where the DB cannot enforce correctness.") |> ignore
        sb.AppendLine("*(Identified via configuration or lineage declarations)*") |> ignore
        
        sb.ToString()
