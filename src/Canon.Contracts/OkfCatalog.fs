namespace Canon.Contracts.Okf

open Canon.Introspect
open Canon.Core

/// Emits OKF-style Markdown catalogs for human-readable lineage tracking.
/// Borrowed directly from Symphony concepts.
module OkfCatalog =
    let emitLineage (table: TableDef) (grade: LineageGrade) : string =
        let sb = System.Text.StringBuilder()
        sb.AppendLine($"# OKF Catalog: {table.Schema}.{table.Name}") |> ignore
        sb.AppendLine($"**Overall Lineage Grade:** {grade}") |> ignore
        sb.AppendLine() |> ignore
        sb.AppendLine("| Column | Type | Nullable | Checks |") |> ignore
        sb.AppendLine("|---|---|---|---|") |> ignore
        
        for c in table.Columns do
            let constraints = if c.CheckConstraints.IsEmpty then "None" else String.concat ", " c.CheckConstraints
            sb.AppendLine($"| {c.Name} | {c.DataType} | {c.IsNullable} | {constraints} |") |> ignore
            
        sb.ToString()
