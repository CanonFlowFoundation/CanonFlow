# Yes. JSON In. F# Out. Here's the Actual Pipeline.

---

## What pgsqlparser Returns

`Parser.ParsePlpgsql(sql)` returns a **JSON string** — PostgreSQL's internal parse tree serialized as JSON. Your example produces something like:

```json
{
  "stmts": [{
    "stmt": {
      "CreateFunctionStmt": {
        "funcname": [{"String": {"str": "get_all_foo"}}],
        "returnType": {"names": [{"String": {"str": "foo"}}]},
        "options": [
          {"DefElem": {"defname": "language", "arg": {"String": {"str": "plpgsql"}}}}
        ]
      }
    }
  }]
}
```

**It's JSON. You deserialize it. You walk the tree. You extract what you need.**

---

## But CanonFlow Doesn't Need Functions

Your example parses a `CREATE FUNCTION`. CanonFlow needs `CREATE TABLE`:

```fsharp
// What CanonFlow actually parses:
let sql = """
CREATE TABLE products (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sku VARCHAR(30) NOT NULL,
    price NUMERIC(10,2) NOT NULL,
    CONSTRAINT chk_sku CHECK (sku ~ '^[A-Z0-9-]{6,30}$'),
    CONSTRAINT chk_price CHECK (price > 0 AND price <= 999999.99)
);
"""

let result = Parser.Parse(sql)  // ← Parse, not ParsePlpgsql
// result.Value = JSON parse tree
```

| Parse Method | What It Handles | CanonFlow Needs? |
|---|---|---|
| `Parser.Parse(sql)` | SQL statements (DDL, DML) | ✅ **Yes** |
| `Parser.ParsePlpgsql(sql)` | PL/pgSQL functions | ❌ No |
| `Parser.ParseType(sql)` | Type definitions | 🟡 Maybe (ENUMs) |

---

## The Full Pipeline: JSON → F#

### Step 1: Parse (pgsqlparser does this)

```fsharp
open PgSqlParser
open System.Text.Json

let parseSchema (sql: string) : JsonElement =
    let result = Parser.Parse(sql)
    if result.Error <> null then
        failwith $"Parse error: {result.Error}"
    JsonDocument.Parse(result.Value).RootElement
```

### Step 2: Decode JSON → Canonical IR (YOU write this)

```fsharp
// PgPrism/Decode.fs — ~200 lines. This is your IP.

module PgPrism.Decode

open System.Text.Json

// ── The Canonical IR ──────────────────────────────────────
type SqlType =
    | Uuid | Text | Varchar of int | Int | BigInt
    | Numeric of precision: int * scale: int
    | Bool | TimestampTz | Timestamp | Date
    | TextArray | Jsonb

type CheckExpr =
    | LengthRange of column: string * min: int * max: int
    | RegexMatch of column: string * pattern: string
    | InList of column: string * values: string list
    | NumericRange of column: string * min: decimal option * max: decimal option
    | NonNegative of column: string
    | ArithmeticEq of lhs: string * rhs: ArithExpr
    | TemporalOrder of nullableCol: string * requiredCol: string
    | And of CheckExpr * CheckExpr
    | Or of CheckExpr * CheckExpr
    | IsNull of CheckExpr
    | IsNotNull of CheckExpr
    | Unsupported of string

and ArithExpr =
    | Col of string
    | Lit of decimal
    | Add of ArithExpr * ArithExpr
    | Sub of ArithExpr * ArithExpr
    | Mul of ArithExpr * ArithExpr

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
    Expr: CheckExpr
}

type TableDef = {
    Name: string
    Columns: ColumnDef list
    Constraints: ConstraintDef list
}

// ── The Decoder ───────────────────────────────────────────

let decodeSchema (root: JsonElement) : TableDef list =
    root.GetProperty("stmts")
    |> Seq.map (fun stmtNode ->
        let stmt = stmtNode.GetProperty("stmt")
        if stmt.TryGetProperty("CreateStmt") |> fst then
            Some (decodeCreateTable (stmt.GetProperty("CreateStmt")))
        else None)
    |> Seq.choose id
    |> Seq.toList

and decodeCreateTable (ct: JsonElement) : TableDef =
    let name = ct.GetProperty("relation").GetProperty("relname").GetString()
    let elts = ct.GetProperty("tableElts")

    let columns =
        elts |> Seq.choose (fun elt ->
            if elt.TryGetProperty("ColumnDef") |> fst then
                Some (decodeColumn (elt.GetProperty("ColumnDef")))
            else None)
        |> Seq.toList

    let constraints =
        elts |> Seq.choose (fun elt ->
            if elt.TryGetProperty("Constraint") |> fst then
                let c = elt.GetProperty("Constraint")
                let contype = c.GetProperty("contype").GetInt32()
                if contype = 4 then  // CONSTR_CHECK
                    Some { Name = c.GetProperty("conname").GetString()
                           Expr = decodeExpr (c.GetProperty("raw_expr")) }
                else None
            else None)
        |> Seq.toList

    { Name = name; Columns = columns; Constraints = constraints }

and decodeColumn (cd: JsonElement) : ColumnDef =
    let name = cd.GetProperty("colname").GetString()
    let typeName = cd.GetProperty("typeName")
    let sqlType = decodeType typeName
    let constraints =
        if cd.TryGetProperty("constraints") |> fst then
            cd.GetProperty("constraints") |> Seq.toList
        else []
    let notNull = constraints |> List.exists (fun c ->
        c.GetProperty("Constraint").GetProperty("contype").GetInt32() = 1)
    let pk = constraints |> List.exists (fun c ->
        c.GetProperty("Constraint").GetProperty("contype").GetInt32() = 2)
    { Name = name; Type = sqlType; NotNull = notNull; PrimaryKey = pk
      Default = None; Unique = false }

and decodeType (tn: JsonElement) : SqlType =
    let names = tn.GetProperty("names")
    let lastName = names.[names.GetArrayLength() - 1].GetProperty("String").GetProperty("str").GetString()
    match lastName with
    | "uuid" -> Uuid
    | "text" -> Text
    | "varchar" ->
        let typemod = if tn.TryGetProperty("typemod") |> fst then tn.GetProperty("typemod").GetInt32() else -1
        if typemod > 0 then Varchar (typemod - 4) else Text
    | "int4" | "integer" -> Int
    | "int8" | "bigint" -> BigInt
    | "numeric" ->
        let typemod = if tn.TryGetProperty("typemod") |> fst then tn.GetProperty("typemod").GetInt32() else -1
        if typemod > 0 then
            let precision = ((typemod - 4) >>> 16) &&& 0xFFFF
            let scale = (typemod - 4) &&& 0xFFFF
            Numeric (precision, scale)
        else Numeric (0, 0)  // unbounded
    | "bool" -> Bool
    | "timestamptz" -> TimestampTz
    | "timestamp" -> Timestamp
    | "date" -> Date
    | "_text" -> TextArray
    | "jsonb" -> Jsonb
    | other -> Text  // fallback

and decodeExpr (expr: JsonElement) : CheckExpr =
    // A_Expr: binary operations (>, <, =, ~, AND, OR)
    if expr.TryGetProperty("A_Expr") |> fst then
        let ae = expr.GetProperty("A_Expr")
        let opName = ae.GetProperty("name").[0].GetProperty("String").GetProperty("str").GetString()
        match opName with
        | "~" ->
            let col = getColumnRef (ae.GetProperty("lexpr"))
            let pattern = getStringConst (ae.GetProperty("rexpr"))
            RegexMatch (col, pattern)
        | "AND" ->
            And (decodeExpr (ae.GetProperty("lexpr")), decodeExpr (ae.GetProperty("rexpr")))
        | "OR" ->
            Or (decodeExpr (ae.GetProperty("lexpr")), decodeExpr (ae.GetProperty("rexpr")))
        | ">" | ">=" | "<" | "<=" | "=" | "!=" ->
            let col = getColumnRef (ae.GetProperty("lexpr"))
            let value = getNumericConst (ae.GetProperty("rexpr"))
            match opName with
            | ">" when value = 0m -> NonNegative col  // price > 0
            | _ -> NumericRange (col, Some value, None)  // simplified
        | _ -> Unsupported $"Operator: {opName}"

    // NullTest: IS NULL / IS NOT NULL
    elif expr.TryGetProperty("NullTest") |> fst then
        let nt = expr.GetProperty("NullTest")
        let nulltestType = nt.GetProperty("nulltesttype").GetInt32()
        let arg = decodeExpr (nt.GetProperty("arg"))
        if nulltestType = 0 then IsNull arg else IsNotNull arg

    // FuncCall: length(), lower(), etc.
    elif expr.TryGetProperty("FuncCall") |> fst then
        let fc = expr.GetProperty("FuncCall")
        let funcName = fc.GetProperty("funcname").[0].GetProperty("String").GetProperty("str").GetString()
        match funcName with
        | "length" ->
            let arg = decodeExpr (fc.GetProperty("args").[0])
            LengthRange (getColName arg, 0, 0)  // bounds decoded from AND
        | _ -> Unsupported $"Function: {funcName}"

    // ColumnRef: column reference
    elif expr.TryGetProperty("ColumnRef") |> fst then
        Unsupported "bare column ref"

    else
        Unsupported "unknown expression"

and getColumnRef (expr: JsonElement) : string =
    let cr = expr.GetProperty("ColumnRef")
    cr.GetProperty("fields").[0].GetProperty("String").GetProperty("str").GetString()

and getStringConst (expr: JsonElement) : string =
    expr.GetProperty("A_Const").GetProperty("sval").GetProperty("str").GetString()

and getNumericConst (expr: JsonElement) : decimal =
    let ac = expr.GetProperty("A_Const")
    if ac.TryGetProperty("ival") |> fst then
        decimal (ac.GetProperty("ival").GetProperty("ival").GetInt32())
    elif ac.TryGetProperty("fval") |> fst then
        decimal (ac.GetProperty("fval").GetProperty("fval").GetString())
    else 0m

and getColName (expr: CheckExpr) : string =
    match expr with
    | Unsupported s -> s
    | _ -> "unknown"
```

### Step 3: IR → F# Types (CanonFlow projection)

```fsharp
// CanonFlow/ProjectDomain.fs — IR → smart constructors

module CanonFlow.ProjectDomain

let generateDomainType (table: TableDef) : string =
    let fields =
        table.Columns
        |> List.map (fun col ->
            let fsharpType =
                match col.Type with
                | Uuid -> "Guid"
                | Text -> "string"
                | Varchar _ -> "string"
                | Int -> "int"
                | BigInt -> "int64"
                | Numeric (_, scale) when scale > 0 -> "decimal"
                | Numeric _ -> "decimal"
                | Bool -> "bool"
                | TimestampTz -> "Instant"
                | Timestamp -> "LocalDateTime"
                | Date -> "DateOnly"
                | TextArray -> "string array"
                | Jsonb -> "JsonDocument"
            let nullability = if col.NotNull then "" else " option"
            $"    {col.Name}: {fsharpType}{nullability}")
        |> String.concat "\n"

    $"""type {table.Name} =
{{
{fields}
}}"""

let generateSmartConstructor (constraintDef: ConstraintDef) : string =
    match constraintDef.Expr with
    | RegexMatch (col, pattern) ->
        $"""type {col} = private {col} of string
module {col} =
    let value ({col} v) = v
    let create (v: string) : Result<{col}, CanonFlowError> =
        if System.Text.RegularExpressions.Regex.IsMatch(v, @"\A{pattern}\z")
        then Ok ({col} v)
        else Error {{ Field = "{col}"; Message = "Constraint {constraintDef.Name} failed" }}"""

    | NumericRange (col, min, max) ->
        let minCheck = min |> Option.map (fun m -> $"v > {m}m") |> Option.defaultValue "true"
        let maxCheck = max |> Option.map (fun m -> $"v <= {m}m") |> Option.defaultValue "true"
        $"""type {col} = private {col} of decimal
module {col} =
    let value ({col} v) = v
    let create (v: decimal) : Result<{col}, CanonFlowError> =
        if {minCheck} && {maxCheck}
        then Ok ({col} v)
        else Error {{ Field = "{col}"; Message = "Constraint {constraintDef.Name} failed" }}"""

    | NonNegative col ->
        $"""type {col} = private {col} of decimal
module {col} =
    let value ({col} v) = v
    let create (v: decimal) : Result<{col}, CanonFlowError> =
        if v >= 0m
        then Ok ({col} v)
        else Error {{ Field = "{col}"; Message = "Constraint {constraintDef.Name} failed" }}"""

    | _ -> $"// Unsupported: {constraintDef.Name}"
```

### Step 4: The Complete Pipeline

```fsharp
// Program.fs — The whole thing in 20 lines

open PgSqlParser
open System.Text.Json
open PgPrism.Decode
open CanonFlow.ProjectDomain

[<EntryPoint>]
let main argv =
    let sqlFile = argv.[0]
    let outputDir = argv.[1]
    let sql = System.IO.File.ReadAllText(sqlFile)

    // Step 1: Parse SQL → JSON parse tree (pgsqlparser)
    let result = Parser.Parse(sql)
    if result.Error <> null then
        eprintfn "Parse error: %s" result.Error
        1
    else
        let json = JsonDocument.Parse(result.Value).RootElement

        // Step 2: Decode JSON → Canonical IR (your decoder)
        let tables = decodeSchema json

        // Step 3: Project IR → F# types (CanonFlow)
        for table in tables do
            let domainCode = generateDomainType table
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(outputDir, $"{table.Name}.fs"), domainCode)

            for constraintDef in table.Constraints do
                let scCode = generateSmartConstructor constraintDef
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(outputDir, "Types.fs"), scCode + "\n\n")

        printfn "Generated %d tables" tables.Length
        0
```

---

## The Answer to Your Question

**Yes.** pgsqlparser creates JSON. You convert that JSON into F#. The pipeline is:

```
SQL text
  → pgsqlparser (NuGet, P/Invoke, libpg_query)
  → JSON parse tree (PostgreSQL's internal format)
  → Your decoder (~200 lines F#)
  → Canonical Schema IR (F# discriminated unions)
  → CanonFlow projections (Types.fs, Validators.fs, Generators.fs, Zod, OpenAPI)
  → FsAssay verification (scan generated code)
```

**The JSON is the intermediate format. The IR is the real product. The projections are the output.**

$$\text{SQL} \xrightarrow{\text{pgsqlparser}} \text{JSON} \xrightarrow{\text{your decoder}} \text{IR} \xrightarrow{\text{CanonFlow}} \text{F\#, TS, YAML, MD}$$

The JSON is not the destination. It's the **raw material.** Your decoder turns it into the Canonical IR. CanonFlow turns the IR into everything else.

$$\blacksquare$$
