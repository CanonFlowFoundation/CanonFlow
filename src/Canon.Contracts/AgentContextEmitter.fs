namespace Canon.Contracts

open Canon.Core
open Canon.Introspect
open System.Text.Json

module AgentContextEmitter =
    type AgentColumn = {
        name: string
        dataType: string
        isNullable: bool
        constraints: string list
        provenance: string list
    }

    type AgentTable = {
        name: string
        schema: string
        columns: AgentColumn list
        isDBEnforced: bool
        isAppEnforced: bool
        isUnenforcedMasterData: bool
    }

    type AgentContext = {
        tables: AgentTable list
    }

    let emitContext (tables: TableDef list) =
        let agentTables = 
            tables |> List.map (fun t -> 
                let hasDbChecks = t.Columns |> List.exists (fun c -> not c.CheckConstraints.IsEmpty)
                let isDBEnforced = hasDbChecks
                let isAppEnforced = not hasDbChecks // simplified heuristic
                let isUnenforced = false // placeholder
                
                let agentCols = 
                    t.Columns |> List.map (fun c -> 
                        {
                            name = c.Name
                            dataType = c.DataType
                            isNullable = c.IsNullable
                            constraints = c.CheckConstraints |> List.map (fun s -> if s.StartsWith("CHECK ") then s.Substring(6) else s)
                            provenance = c.CheckConstraints
                        }
                    )
                {
                    name = t.Name
                    schema = t.Schema
                    columns = agentCols
                    isDBEnforced = isDBEnforced
                    isAppEnforced = isAppEnforced
                    isUnenforcedMasterData = isUnenforced
                }
            )
        let ctx = { tables = agentTables }
        let options = JsonSerializerOptions(WriteIndented = true)
        JsonSerializer.Serialize(ctx, options)
