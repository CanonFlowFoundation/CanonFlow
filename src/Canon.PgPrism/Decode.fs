namespace Canon.PgPrism

open System.Text.Json
open Canon.Core

type SqlType =
    | Uuid
    | Text
    | Varchar of int
    | Int
    | BigInt
    | Numeric of precision: int * scale: int
    | Bool
    | TimestampTz
    | Timestamp
    | Date
    | Jsonb
    | Array of SqlType
    | Enum of name: string * vals: string list

type ColumnDef = {
    Name: string
    Type: SqlType
    NotNull: bool
    PrimaryKey: bool
    Default: string option
    Unique: bool
}

type ConstraintDef = {
    Name: string
    Expr: Lattice<Constraint>
}

type ForeignKeyDef = {
    Name: string
    TargetTable: string
    LocalColumns: string list
    TargetColumns: string list
    OnUpdate: string option
    OnDelete: string option
}

type TableDef = {
    Name: string
    Columns: ColumnDef list
    Constraints: ConstraintDef list
    ForeignKeys: ForeignKeyDef list
}

module Decode =

    let rec decodeType (enums: Map<string, string list>) (cd: JsonElement) : SqlType =
        let tn = cd.GetProperty("typeName")
        let names = tn.GetProperty("names")
        let lastName = names.[names.GetArrayLength() - 1].GetProperty("String").GetProperty("sval").GetString()
        
        let getTypMod (index: int) =
            if tn.TryGetProperty("typmods") |> fst then
                let typmods = tn.GetProperty("typmods")
                if index < typmods.GetArrayLength() then
                    let modNode = typmods.[index]
                    if modNode.TryGetProperty("A_Const") |> fst then
                        modNode.GetProperty("A_Const").GetProperty("ival").GetProperty("ival").GetInt32()
                    else -1
                else -1
            else -1

        let baseType =
            match lastName with
            | "uuid" -> Uuid
            | "text" -> Text
            | "varchar" ->
                let len = getTypMod 0
                if len > 0 then Varchar len else Text
            | "int4" | "integer" -> Int
            | "int8" | "bigint" -> BigInt
            | "numeric" ->
                let p = getTypMod 0
                let s = getTypMod 1
                if p > 0 then Numeric (p, max 0 s) else Numeric (0, 0)
            | "bool" -> Bool
            | "timestamptz" -> TimestampTz
            | "timestamp" -> Timestamp
            | "date" -> Date
            | "jsonb" -> Jsonb
            | name when enums.ContainsKey(name) -> Enum (name, enums.[name])
            | other -> Text
            
        if tn.TryGetProperty("arrayBounds") |> fst then Array baseType else baseType

    let getColumnRef (expr: JsonElement) : string =
        let cr = expr.GetProperty("ColumnRef")
        cr.GetProperty("fields").[0].GetProperty("String").GetProperty("sval").GetString()

    let getStringConst (expr: JsonElement) : string =
        expr.GetProperty("A_Const").GetProperty("sval").GetProperty("sval").GetString()

    let getNumericConst (expr: JsonElement) : decimal =
        let ac = expr.GetProperty("A_Const")
        if ac.TryGetProperty("ival") |> fst then
            let ivalObj = ac.GetProperty("ival")
            if ivalObj.TryGetProperty("ival") |> fst then
                decimal (ivalObj.GetProperty("ival").GetInt32())
            else 0m
        elif ac.TryGetProperty("fval") |> fst then
            let fvalObj = ac.GetProperty("fval")
            if fvalObj.TryGetProperty("fval") |> fst then
                decimal (fvalObj.GetProperty("fval").GetString())
            else 0m
        else 0m

    type C = Canon.Core.Constraint

    let rec decodeExpr (expr: JsonElement) : Lattice<Constraint> =
        if expr.TryGetProperty("A_Expr") |> fst then
            let ae = expr.GetProperty("A_Expr")
            let opName = ae.GetProperty("name").[0].GetProperty("String").GetProperty("sval").GetString()
            match opName with
            | "~" ->
                let col = getColumnRef (ae.GetProperty("lexpr"))
                let pattern = getStringConst (ae.GetProperty("rexpr"))
                Lattice.Leaf (C.FieldBound(col, C.Opaque($"RegexMatch: {pattern}")))
            | "AND" ->
                Lattice.And (decodeExpr (ae.GetProperty("lexpr")), decodeExpr (ae.GetProperty("rexpr")))
            | "OR" ->
                Lattice.Or (decodeExpr (ae.GetProperty("lexpr")), decodeExpr (ae.GetProperty("rexpr")))
            | ">" | ">=" | "<" | "<=" | "=" | "!=" ->
                let col = getColumnRef (ae.GetProperty("lexpr"))
                let value = getNumericConst (ae.GetProperty("rexpr"))
                match opName with
                | ">" when value = 0m -> Lattice.Leaf (C.FieldBound(col, C.Range(Some(Exclusive 0m), None)))
                | ">=" when value = 0m -> Lattice.Leaf (C.FieldBound(col, C.Range(Some(Inclusive 0m), None)))
                | _ -> Lattice.Leaf (C.FieldBound(col, C.Opaque($"NumericRange {opName} {value}")))
            | _ -> Lattice.Leaf (C.Opaque $"Unsupported Operator: {opName}")

        elif expr.TryGetProperty("BoolExpr") |> fst then
            let be = expr.GetProperty("BoolExpr")
            let boolop = be.GetProperty("boolop").GetString()
            let args = be.GetProperty("args").EnumerateArray() |> Seq.toList
            if boolop = "OR_EXPR" then
                Lattice.Or (decodeExpr args.[0], decodeExpr args.[1])
            elif boolop = "AND_EXPR" then
                Lattice.And (decodeExpr args.[0], decodeExpr args.[1])
            else Lattice.Leaf (C.Opaque "unknown bool expr")

        elif expr.TryGetProperty("NullTest") |> fst then
            let nt = expr.GetProperty("NullTest")
            let nulltestType = nt.GetProperty("nulltesttype").GetString()
            let col = getColumnRef (nt.GetProperty("arg"))
            if nulltestType = "IS_NULL" then Lattice.Leaf (C.FieldBound(col, C.IsNull))
            else Lattice.Leaf (C.FieldBound(col, C.IsNotNull))

        elif expr.TryGetProperty("FuncCall") |> fst then
            let fc = expr.GetProperty("FuncCall")
            let funcName = fc.GetProperty("funcname").[0].GetProperty("String").GetProperty("sval").GetString()
            match funcName with
            | "length" -> Lattice.Leaf (C.Opaque "LengthCheck")
            | _ -> Lattice.Leaf (C.Opaque $"Unsupported Function: {funcName}")
        elif expr.TryGetProperty("ColumnRef") |> fst then
            Lattice.Leaf (C.Opaque "bare column ref")
        else
            Lattice.Leaf (C.Opaque "unknown expression")

    let decodeColumn (enums: Map<string, string list>) (cd: JsonElement) : ColumnDef =
        let name = cd.GetProperty("colname").GetString()
        let sqlType = decodeType enums cd
        let constraints =
            if cd.TryGetProperty("constraints") |> fst then
                cd.GetProperty("constraints").EnumerateArray() |> Seq.toList
            else []
        
        let notNull = constraints |> List.exists (fun c ->
            c.GetProperty("Constraint").GetProperty("contype").GetString() = "CONSTR_NOTNULL")
        let pk = constraints |> List.exists (fun c ->
            c.GetProperty("Constraint").GetProperty("contype").GetString() = "CONSTR_PRIMARY")
        let unique = constraints |> List.exists (fun c ->
            c.GetProperty("Constraint").GetProperty("contype").GetString() = "CONSTR_UNIQUE")
        let defaultVal = constraints |> List.tryPick (fun c ->
            let cstr = c.GetProperty("Constraint")
            if cstr.GetProperty("contype").GetString() = "CONSTR_DEFAULT" then
                let raw = cstr.GetProperty("raw_expr")
                if raw.TryGetProperty("A_Const") |> fst then
                    let ac = raw.GetProperty("A_Const")
                    if ac.TryGetProperty("sval") |> fst then
                        let strVal = ac.GetProperty("sval").GetProperty("sval").GetString()
                        Some (sprintf "'%s'" strVal)
                    elif ac.TryGetProperty("ival") |> fst then
                        let ivalObj = ac.GetProperty("ival")
                        if ivalObj.TryGetProperty("ival") |> fst then
                            Some (ivalObj.GetProperty("ival").GetInt32().ToString())
                        else Some "0"
                    elif ac.TryGetProperty("fval") |> fst then
                        let fvalObj = ac.GetProperty("fval")
                        if fvalObj.TryGetProperty("fval") |> fst then
                            Some (fvalObj.GetProperty("fval").GetString())
                        else Some "0.0"
                    else None
                else None
            else None)
            
        { Name = name; Type = sqlType; NotNull = notNull; PrimaryKey = pk; Default = defaultVal; Unique = unique }

    let decodeCreateTable (enums: Map<string, string list>) (ct: JsonElement) : TableDef =
        let name = ct.GetProperty("relation").GetProperty("relname").GetString()
        let elts = ct.GetProperty("tableElts")

        let columns =
            elts.EnumerateArray() |> Seq.choose (fun elt ->
                if elt.TryGetProperty("ColumnDef") |> fst then
                    Some (decodeColumn enums (elt.GetProperty("ColumnDef")))
                else None)
            |> Seq.toList

        // Table-level UNIQUE and other logic can be integrated into column properties or table-level objects
        let tableUniques =
            elts.EnumerateArray() |> Seq.choose (fun elt ->
                if elt.TryGetProperty("Constraint") |> fst then
                    let c = elt.GetProperty("Constraint")
                    let contype = c.GetProperty("contype").GetString()
                    if contype = "CONSTR_UNIQUE" then
                        let keys = c.GetProperty("keys").EnumerateArray() |> Seq.map (fun k -> k.GetProperty("String").GetProperty("sval").GetString()) |> Seq.toList
                        Some keys
                    else None
                else None)
            |> Seq.toList

        // Update columns that are part of a table-level single-column unique constraint
        let updatedColumns =
            columns |> List.map (fun col ->
                let isUnique = col.Unique || (tableUniques |> List.exists (fun keys -> keys = [col.Name]))
                { col with Unique = isUnique })

        let constraints =
            elts.EnumerateArray() |> Seq.choose (fun elt ->
                if elt.TryGetProperty("Constraint") |> fst then
                    let c = elt.GetProperty("Constraint")
                    let contype = c.GetProperty("contype").GetString()
                    if contype = "CONSTR_CHECK" then
                        Some { Name = c.GetProperty("conname").GetString()
                               Expr = decodeExpr (c.GetProperty("raw_expr")) }
                    else None
                else None)
            |> Seq.toList

        let fks =
            elts.EnumerateArray() |> Seq.choose (fun elt ->
                if elt.TryGetProperty("Constraint") |> fst then
                    let c = elt.GetProperty("Constraint")
                    let contype = c.GetProperty("contype").GetString()
                    if contype = "CONSTR_FOREIGN" then
                        let conname = if c.TryGetProperty("conname") |> fst then c.GetProperty("conname").GetString() else "fk"
                        let pktable = c.GetProperty("pktable").GetProperty("relname").GetString()
                        let fk_attrs = c.GetProperty("fk_attrs").EnumerateArray() |> Seq.map (fun a -> a.GetProperty("String").GetProperty("sval").GetString()) |> Seq.toList
                        let pk_attrs = c.GetProperty("pk_attrs").EnumerateArray() |> Seq.map (fun a -> a.GetProperty("String").GetProperty("sval").GetString()) |> Seq.toList
                        let onDel = if c.TryGetProperty("fk_del_action") |> fst then Some (c.GetProperty("fk_del_action").GetString()) else None
                        let onUpd = if c.TryGetProperty("fk_upd_action") |> fst then Some (c.GetProperty("fk_upd_action").GetString()) else None
                        Some { Name = conname; TargetTable = pktable; LocalColumns = fk_attrs; TargetColumns = pk_attrs; OnDelete = onDel; OnUpdate = onUpd }
                    else None
                else None)
            |> Seq.toList

        { Name = name; Columns = updatedColumns; Constraints = constraints; ForeignKeys = fks }

    let decodeSchema (root: JsonElement) : TableDef list =
        let stmts = root.GetProperty("stmts").EnumerateArray() |> Seq.toList
        
        let enums =
            stmts |> List.choose (fun stmtNode ->
                let stmt = stmtNode.GetProperty("stmt")
                if stmt.TryGetProperty("CreateEnumStmt") |> fst then
                    let ce = stmt.GetProperty("CreateEnumStmt")
                    let typeName = ce.GetProperty("typeName").[0].GetProperty("String").GetProperty("sval").GetString()
                    let vals = ce.GetProperty("vals").EnumerateArray() |> Seq.map (fun v -> v.GetProperty("String").GetProperty("sval").GetString()) |> Seq.toList
                    Some (typeName, vals)
                else None)
            |> Map.ofList

        stmts |> List.choose (fun stmtNode ->
            let stmt = stmtNode.GetProperty("stmt")
            if stmt.TryGetProperty("CreateStmt") |> fst then
                Some (decodeCreateTable enums (stmt.GetProperty("CreateStmt")))
            else None)
