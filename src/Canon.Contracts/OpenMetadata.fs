namespace Canon.Contracts.OpenMetadata

open Canon.Introspect

/// Extracts the table structures and formats them into OpenMetadata compliant JSON entities.
/// Allows CanonFlow to act as a semantic catalog bridge.
module OpenMetadataEmitter =
    let emitTableEntity (table: TableDef) : string =
        // Simplified JSON generation. For production, Thoth.Json would be used.
        let columns = 
            table.Columns 
            |> List.map (fun col -> 
                $"""        {{ "name": "{col.Name}", "dataType": "{col.DataType.ToUpperInvariant()}" }}"""
            )
            |> String.concat ",\n"
            
        $"""{{
    "name": "{table.Name}",
    "databaseSchema": "{table.Schema}",
    "columns": [
{columns}
    ]
}}"""
