namespace Canon.Introspect.Postgres

open System
open System.Text.RegularExpressions
open Canon.Introspect
open Canon.Core
open Npgsql

/// Parses raw SQL constraint strings into our Lattice algebra.
module ConstraintParser =
    let parseCheck (checkClause: string) =
        // Extremely naive parser for demonstration (e.g. "price > 0")
        if checkClause.Contains("> 0") then
            Lattice.Leaf (Range(Some (Exclusive 0m), None))
        else if checkClause.Contains("length") then
            Lattice.Leaf (MaxLength 255)
        else
            Lattice.True

/// Postgres implementation of the ISchemaProvider.
type PostgresSchemaProvider(connectionString: string) =
    interface ISchemaProvider with
        member this.Harvest() =
            use conn = new NpgsqlConnection(connectionString)
            conn.Open()
            
            // Simplified harvest strategy querying both columns and constraints
            let query = @"
                SELECT 
                    c.table_schema, 
                    c.table_name, 
                    c.column_name, 
                    c.data_type, 
                    c.is_nullable, 
                    c.character_maximum_length,
                    (SELECT pg_get_constraintdef(con.oid)
                     FROM pg_constraint con
                     INNER JOIN pg_attribute a ON a.attnum = ANY(con.conkey)
                     WHERE con.conrelid = (c.table_schema || '.' || c.table_name)::regclass
                       AND a.attname = c.column_name
                       AND con.contype = 'c'
                     LIMIT 1) as check_constraint
                FROM information_schema.columns c
                WHERE c.table_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY c.table_schema, c.table_name, c.ordinal_position;
            "
            
            use cmd = new NpgsqlCommand(query, conn)
            use reader = cmd.ExecuteReader()
            
            let columnsData = 
                [ while reader.Read() do
                    let tSchema = reader.GetString(0)
                    let tName = reader.GetString(1)
                    let cName = reader.GetString(2)
                    let dType = reader.GetString(3)
                    let isNull = reader.GetString(4) = "YES"
                    let maxLen = if reader.IsDBNull(5) then None else Some(reader.GetInt32(5))
                    
                    let checkConstraintStr = if reader.IsDBNull(6) then "" else reader.GetString(6)
                    
                    yield (tSchema, tName, { 
                        Name = cName
                        DataType = dType
                        IsNullable = isNull
                        MaxLength = maxLen
                        CheckConstraints = if String.IsNullOrEmpty(checkConstraintStr) then [] else [checkConstraintStr]
                        Semantics = None // Seeded as None, to be enriched by human or OKF
                    })
                ]

            columnsData
            |> List.groupBy (fun (s, t, _) -> (s, t))
            |> List.map (fun ((schema, name), cols) ->
                {
                    Schema = schema
                    Name = name
                    Columns = cols |> List.map (fun (_, _, c) -> c)
                }
            )
