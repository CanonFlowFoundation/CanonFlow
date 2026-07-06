namespace Canon.Emit.OpenSearch

open Canon.Emit
open Canon.Introspect

/// Emits an OpenSearch/Elasticsearch index mapping derived from the domain schema.
/// Converts typed fields into strict OpenSearch field types.
type OpenSearchEmitter() =
    
    // Map standard data types to OpenSearch data types.
    let mapDataType (sqlType: string) =
        match sqlType.ToLowerInvariant() with
        | "integer" | "int" -> "integer"
        | "bigint" -> "long"
        | "boolean" -> "boolean"
        | "timestamp" | "date" -> "date"
        | "decimal" | "numeric" -> "double" // Simplified
        | _ -> "keyword" // Default to keyword for strictness (instead of text)
        
    interface IEmitter with
        member this.Emit(tables: TableDef list) =
            tables |> List.map (fun table ->
                let props = 
                    table.Columns 
                    |> List.map (fun col ->
                        $"""        "{col.Name}": {{ "type": "{mapDataType col.DataType}" }}"""
                    )
                    |> String.concat ",\n"
                
                let mapping = $"""{{
  "mappings": {{
    "properties": {{
{props}
    }}
  }}
}}"""
                mapping, Canon.Core.Fidelity.Approximate "OpenSearch drops foreign keys, constraints, and defaults"
            )
