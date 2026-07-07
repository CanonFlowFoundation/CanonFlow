namespace Canon.Introspect.Postgres

open System
open System.Text.RegularExpressions
open Canon.Introspect
open Canon.Core
open Npgsql



/// Postgres implementation of the ISchemaProvider.
type PostgresSchemaProvider(connectionString: string) =
    interface ISchemaProvider with
        member this.Harvest() =
            use conn = new NpgsqlConnection(connectionString)
            conn.Open()
            
            // 1. Fetch Primary Keys
            let pkQuery = @"
                SELECT tc.table_schema, tc.table_name, kcu.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
                WHERE tc.constraint_type = 'PRIMARY KEY' AND tc.table_schema NOT IN ('pg_catalog', 'information_schema');
            "
            use pkCmd = new NpgsqlCommand(pkQuery, conn)
            use pkReader = pkCmd.ExecuteReader()
            let pks = 
                [ while pkReader.Read() do
                    yield (pkReader.GetString(0), pkReader.GetString(1), pkReader.GetString(2)) ]
                |> List.sort
            pkReader.Close()
            let isPk schema table col = pks |> List.contains (schema, table, col)

            // 2. Fetch Foreign Keys
            let fkQuery = @"
                SELECT tc.table_schema, tc.table_name, kcu.column_name, ccu.table_name, ccu.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
                JOIN information_schema.constraint_column_usage ccu ON ccu.constraint_name = tc.constraint_name AND ccu.table_schema = tc.table_schema
                WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_schema NOT IN ('pg_catalog', 'information_schema');
            "
            use fkCmd = new NpgsqlCommand(fkQuery, conn)
            use fkReader = fkCmd.ExecuteReader()
            let fks = 
                [ while fkReader.Read() do
                    yield (fkReader.GetString(0), fkReader.GetString(1), fkReader.GetString(2), fkReader.GetString(3), fkReader.GetString(4)) ]
                |> List.sort
            fkReader.Close()

            // 3. Fetch Columns and Check Constraints
            let colQuery = @"
                SELECT 
                    c.table_schema, c.table_name, c.column_name, c.data_type, c.is_nullable, c.character_maximum_length, c.column_default, c.is_generated,
                    (SELECT string_agg(def, '|||') FROM (
                        SELECT pg_get_constraintdef(con.oid) as def
                        FROM pg_constraint con
                        INNER JOIN pg_attribute a ON a.attnum = ANY(con.conkey) AND a.attrelid = con.conrelid
                        WHERE con.conrelid = (c.table_schema || '.' || c.table_name)::regclass
                          AND a.attname = c.column_name AND con.contype = 'c'
                        UNION ALL
                        SELECT REPLACE(pg_get_constraintdef(con.oid), 'VALUE', c.column_name) as def
                        FROM pg_constraint con
                        JOIN pg_type t ON t.oid = con.contypid
                        JOIN pg_namespace n ON n.oid = t.typnamespace
                        WHERE t.typname = c.domain_name AND n.nspname = c.domain_schema AND c.domain_name IS NOT NULL
                    ) sub) as check_constraints
                FROM information_schema.columns c
                WHERE c.table_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY c.table_schema, c.table_name, c.ordinal_position;
            "
            use colCmd = new NpgsqlCommand(colQuery, conn)
            use colReader = colCmd.ExecuteReader()
            
            let columnsData = 
                [ while colReader.Read() do
                    let rawSchema = colReader.GetString(0)
                    let rawTable = colReader.GetString(1)
                    let rawColumn = colReader.GetString(2)
                    let tSchema = Sanitizer.sanitizeIdentifier rawSchema
                    let tName = Sanitizer.sanitizeIdentifier rawTable
                    let cName = Sanitizer.sanitizeIdentifier rawColumn
                    if rawSchema <> tSchema || rawTable <> tName || rawColumn <> cName then
                        printfn "[WARNING] Sanitized hostile identifier: %s.%s.%s -> %s.%s.%s" rawSchema rawTable rawColumn tSchema tName cName
                    let dType = colReader.GetString(3)
                    let isNull = colReader.GetString(4) = "YES"
                    let maxLen = if colReader.IsDBNull(5) then None else Some(colReader.GetInt32(5))
                    let defVal = if colReader.IsDBNull(6) then None else Some(colReader.GetString(6))
                    let isGen = if colReader.IsDBNull(7) then false else colReader.GetString(7) = "ALWAYS"
                    let checkConstraintsStr = if colReader.IsDBNull(8) then "" else colReader.GetString(8)
                    
                    let checkConstraintStrs = 
                        if String.IsNullOrEmpty(checkConstraintsStr) then []
                        else checkConstraintsStr.Split("|||", StringSplitOptions.RemoveEmptyEntries) |> Array.toList |> List.sort

                    let cleanCheckStrs = 
                        checkConstraintStrs
                        |> List.map (fun s -> s.Replace("CHECK ", "").Replace("::numeric", ""))
                    
                    let parsedConstraints =
                        cleanCheckStrs
                        |> List.map SqlParser.parseConstraint
                    
                    yield (tSchema, tName, { 
                        Name = cName
                        DataType = dType
                        IsNullable = isNull
                        IsPrimaryKey = isPk tSchema tName cName
                        DefaultValue = defVal
                        IsGenerated = isGen
                        Description = None
                        MaxLength = maxLen
                        CheckConstraints = checkConstraintStrs
                        ParsedConstraints = parsedConstraints
                        Semantics = None
                    })
                ]
            colReader.Close()

            // Group into TableDefs
            columnsData
            |> List.groupBy (fun (s, t, _) -> (s, t))
            |> List.sortBy fst
            |> List.map (fun ((schema, name), cols) ->
                let tableFks = fks |> List.choose (fun (s, t, c, rt, rc) -> 
                    if s = schema && t = name then Some { ColumnName = c; RefTable = rt; RefColumn = rc } else None)
                
                let tablePks = pks |> List.choose (fun (s, t, c) -> if s = schema && t = name then Some c else None)

                {
                    Schema = schema
                    Name = name
                    Type = TableType.Table
                    Description = None
                    Columns = cols |> List.map (fun (_, _, c) -> c)
                    PrimaryKeys = tablePks
                    ForeignKeys = tableFks
                    Indexes = [] // Full index parsing omitted for brevity
                    TableConstraints = []
                }
            )
