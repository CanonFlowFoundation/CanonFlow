namespace Canon.Emit

open System
open System.Text
open Canon.Core
open Canon.Introspect

/// Emits an Agent-Readable Semantic Catalog in Open Knowledge Framework (OKF) format.
/// This YAML is designed to be ingested by other LLMs/AI agents so they understand
/// the exact bounded truth of the database before writing frontend or API code.
module OkfEmitter =

    let rec private formatLattice (l: Lattice<Constraint>) : string =
        match l with
        | True -> "Any"
        | False -> "None"
        | Leaf(FieldBound(f, Range(Some(Inclusive min), None))) -> sprintf "%s >= %M" f min
        | Leaf(FieldBound(f, Range(None, Some(Inclusive max)))) -> sprintf "%s <= %M" f max
        | Leaf(FieldBound(f, Range(Some(Exclusive min), None))) -> sprintf "%s > %M" f min
        | Leaf(FieldBound(f, Range(None, Some(Exclusive max)))) -> sprintf "%s < %M" f max
        | Leaf(FieldBound(f, Range(Some(Inclusive min), Some(Inclusive max)))) -> sprintf "%M <= %s <= %M" min f max
        | Leaf(Opaque sql) -> sql
        | And(a, b) -> sprintf "(%s AND %s)" (formatLattice a) (formatLattice b)
        | Or(a, b) -> sprintf "(%s OR %s)" (formatLattice a) (formatLattice b)
        | Not a -> sprintf "NOT (%s)" (formatLattice a)
        | _ -> "ComplexBound"

    let emitAgentCatalog (table: TableDef) : string =
        let sb = StringBuilder()
        let add (s: string) = sb.AppendLine(s) |> ignore
        
        add "---"
        add "kind: SemanticModel"
        add "version: 1.0"
        add $"metadata:"
        add $"  name: {table.Name}"
        add $"  schema: {table.Schema}"
        add $"  type: {table.Type}"
        
        add "columns:"
        for col in table.Columns do
            add $"  - name: {col.Name}"
            add $"    type: {col.DataType}"
            let nullableStr = if col.IsNullable then "true" else "false"
            let pkStr = if col.IsPrimaryKey then "true" else "false"
            add $"    nullable: {nullableStr}"
            add $"    isPrimaryKey: {pkStr}"
            
            // Add constraints
            if not col.ParsedConstraints.IsEmpty then
                add "    semanticBounds:"
                for ast in col.ParsedConstraints do
                    let simplified = SemanticOptimizer.simplify ast
                    let formatted = formatLattice simplified
                    add $"      - \"{formatted}\""
                
                // Provide safe query hints for AI agents
                add "    agentDirectives:"
                add $"      - \"Always ensure frontend validators restrict {col.Name} to these semantic bounds to avoid 500s.\""
        
        add "relationships:"
        for fk in table.ForeignKeys do
            add $"  - column: {fk.ColumnName}"
            add $"    referencesTable: {fk.RefTable}"
            add $"    referencesColumn: {fk.RefColumn}"
            
        sb.ToString()
