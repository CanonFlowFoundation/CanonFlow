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
    | TextArray
    | Jsonb

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

type TableDef = {
    Name: string
    Columns: ColumnDef list
    Constraints: ConstraintDef list
}

module Decode =

    let rec decodeType (tn: JsonElement) : SqlType =
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
        | "_text" -> TextArray
        | "jsonb" -> Jsonb
        | other -> Text

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

    let rec decodeExpr (expr: JsonElement) : Lattice<Constraint> =
        if expr.TryGetProperty("A_Expr") |> fst then
            let ae = expr.GetProperty("A_Expr")
            let opName = ae.GetProperty("name").[0].GetProperty("String").GetProperty("sval").GetString()
            match opName with
            | "~" ->
                let col = getColumnRef (ae.GetProperty("lexpr"))
                let pattern = getStringConst (ae.GetProperty("rexpr"))
                Lattice.Leaf (FieldBound(col, Opaque($"RegexMatch: {pattern}")))
            | "AND" ->
                Lattice.And (decodeExpr (ae.GetProperty("lexpr")), decodeExpr (ae.GetProperty("rexpr")))
            | "OR" ->
                Lattice.Or (decodeExpr (ae.GetProperty("lexpr")), decodeExpr (ae.GetProperty("rexpr")))
            | ">" | ">=" | "<" | "<=" | "=" | "!=" ->
                let col = getColumnRef (ae.GetProperty("lexpr"))
                let value = getNumericConst (ae.GetProperty("rexpr"))
                match opName with
                | ">" when value = 0m -> Lattice.Leaf (FieldBound(col, Range(Some(Exclusive 0m), None)))
                | ">=" when value = 0m -> Lattice.Leaf (FieldBound(col, Range(Some(Inclusive 0m), None)))
                | _ -> Lattice.Leaf (FieldBound(col, Opaque($"NumericRange {opName} {value}"))) // simplified for now
            | _ -> Lattice.Leaf (Opaque $"Unsupported Operator: {opName}")

        elif expr.TryGetProperty("NullTest") |> fst then
            let nt = expr.GetProperty("NullTest")
            let nulltestType = nt.GetProperty("nulltesttype").GetInt32()
            let arg = decodeExpr (nt.GetProperty("arg"))
            if nulltestType = 0 then Lattice.Leaf (Opaque "IsNull") else Lattice.Leaf (Opaque "IsNotNull")

        elif expr.TryGetProperty("FuncCall") |> fst then
            let fc = expr.GetProperty("FuncCall")
            let funcName = fc.GetProperty("funcname").[0].GetProperty("String").GetProperty("sval").GetString()
            match funcName with
            | "length" ->
                // Simplified mapping for length check
                Lattice.Leaf (Opaque "LengthCheck")
            | _ -> Lattice.Leaf (Opaque $"Unsupported Function: {funcName}")
        elif expr.TryGetProperty("ColumnRef") |> fst then
            Lattice.Leaf (Opaque "bare column ref")
        else
            Lattice.Leaf (Opaque "unknown expression")

    let decodeColumn (cd: JsonElement) : ColumnDef =
        let name = cd.GetProperty("colname").GetString()
        let typeName = cd.GetProperty("typeName")
        let sqlType = decodeType typeName
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
            
        { Name = name; Type = sqlType; NotNull = notNull; PrimaryKey = pk; Default = None; Unique = unique }

    let decodeCreateTable (ct: JsonElement) : TableDef =
        let name = ct.GetProperty("relation").GetProperty("relname").GetString()
        let elts = ct.GetProperty("tableElts")

        let columns =
            elts.EnumerateArray() |> Seq.choose (fun elt ->
                if elt.TryGetProperty("ColumnDef") |> fst then
                    Some (decodeColumn (elt.GetProperty("ColumnDef")))
                else None)
            |> Seq.toList

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

        { Name = name; Columns = columns; Constraints = constraints }

    let decodeSchema (root: JsonElement) : TableDef list =
        root.GetProperty("stmts").EnumerateArray()
        |> Seq.map (fun stmtNode ->
            let stmt = stmtNode.GetProperty("stmt")
            if stmt.TryGetProperty("CreateStmt") |> fst then
                Some (decodeCreateTable (stmt.GetProperty("CreateStmt")))
            else None)
        |> Seq.choose id
        |> Seq.toList
