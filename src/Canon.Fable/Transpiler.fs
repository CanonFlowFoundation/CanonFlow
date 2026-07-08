namespace Canon.Fable

open Canon.Core

/// Transpiles the Lattice of constraints into TypeScript validation logic.
module Transpiler =
    
    let rec toTypeScript (predicate: Lattice<Constraint>) : string * Fidelity =
        match predicate with
        | Lattice.True -> "z.any()", Fidelity.Exact
        | Lattice.False -> "z.never()", Fidelity.Exact
        | Lattice.Not inner -> 
            let expr, f = toTypeScript inner
            $"z.any().refine(val => !({expr}.safeParse(val).success))", Fidelity.Approximate "Zod does not support generic NOT"
        | Lattice.And(left, right) -> 
            let lExpr, lF = toTypeScript left
            let rExpr, rF = toTypeScript right
            $"{lExpr}.and({rExpr})", Fidelity.combine lF rF
        | Lattice.Or(left, right) -> 
            let lExpr, lF = toTypeScript left
            let rExpr, rF = toTypeScript right
            $"{lExpr}.or({rExpr})", Fidelity.combine lF rF
        | Lattice.Leaf c ->
            match c with
            | Range (Some (Exclusive v), None) -> $"z.number().gt({v})", Fidelity.Exact
            | Range (None, Some (Exclusive v)) -> $"z.number().lt({v})", Fidelity.Exact
            | Range (Some (Inclusive v), None) -> $"z.number().gte({v})", Fidelity.Exact
            | Range (None, Some (Inclusive v)) -> $"z.number().lte({v})", Fidelity.Exact
            | Range _ -> "z.number()", Fidelity.Approximate "Complex range bounds not fully implemented in TS Zod"
            | IntRange _ -> "z.number().int()", Fidelity.Approximate "Int range requires precision bounds"
            | StringRange _ -> "z.string()", Fidelity.Approximate "String range collation may differ"
            | MaxLength len -> $"z.string().max({len})", Fidelity.Exact
            | InList items -> 
                let arr = items |> List.map (sprintf "\"%s\"") |> String.concat ", "
                if items.Length = 0 then "z.never()", Fidelity.Exact
                else $"z.enum([{arr}])", Fidelity.Exact
            | InSet items -> 
                let arr = items |> List.map (sprintf "\"%s\"") |> String.concat ", "
                if items.Length = 0 then "z.never()", Fidelity.Exact
                else $"z.enum([{arr}])", Fidelity.Exact
            | RelativeBound(colA, op, colB) ->
                let isLiteral (s: string) = s.StartsWith("'") || System.Char.IsDigit(s.[0]) || s.StartsWith("-")
                let a = if isLiteral colA then colA else $"data.{colA}"
                let b = if isLiteral colB then colB else $"data.{colB}"
                $"z.any().refine(data => {a} {op} {b})", Fidelity.Exact
            | PrimaryKey -> "z.any()", Fidelity.Unsupported "PrimaryKey concept does not exist in TS validators"
            | NonEmpty -> $"z.string().min(1)", Fidelity.Exact
            | Constraint.Opaque _ -> "z.any()", Fidelity.Unsupported "Cannot transpile raw SQL"
            | FieldBound(field, inner) -> 
                let innerExpr, innerF = toTypeScript (Lattice.Leaf inner)
                $"z.object({{ {field}: {innerExpr} }})", innerF

    /// Emits a full TypeScript validation function and its Fidelity grade.
    let emitValidator (name: string) (predicate: Lattice<Constraint>) (isNullable: bool) : string * Fidelity =
        let expr, fidelity = toTypeScript predicate
        let baseCode = if isNullable then $"{expr}.nullable()" else expr
        let code = $"""import {{ z }} from "zod";

export const {name}Schema = {baseCode};

export function validate_{name}(value: any): boolean {{
    return {name}Schema.safeParse(value).success;
}}"""
        code, fidelity
