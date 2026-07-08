namespace Canon.Emit

open Canon.Core

module OpenApiTranspiler =
    
    let rec toOpenApiSchema (predicate: Lattice<Constraint>) : string * Fidelity =
        match predicate with
        | Lattice.True -> "{}", Fidelity.Exact
        | Lattice.False -> """{"not": {}}""", Fidelity.Exact
        | Lattice.Not inner -> 
            let expr, f = toOpenApiSchema inner
            $"""{{"not": {expr}}}""", f
        | Lattice.And(left, right) -> 
            let lExpr, lF = toOpenApiSchema left
            let rExpr, rF = toOpenApiSchema right
            $"""{{"allOf": [{lExpr}, {rExpr}]}}""", Fidelity.combine lF rF
        | Lattice.Or(left, right) -> 
            let lExpr, lF = toOpenApiSchema left
            let rExpr, rF = toOpenApiSchema right
            $"""{{"anyOf": [{lExpr}, {rExpr}]}}""", Fidelity.combine lF rF
        | Lattice.Leaf c ->
            match c with
            | Range (Some (Exclusive v), None) -> $"""{{"exclusiveMinimum": true, "minimum": {v}}}""", Fidelity.Exact
            | Range (None, Some (Exclusive v)) -> $"""{{"exclusiveMaximum": true, "maximum": {v}}}""", Fidelity.Exact
            | Range (Some (Inclusive v), None) -> $"""{{"minimum": {v}}}""", Fidelity.Exact
            | Range (None, Some (Inclusive v)) -> $"""{{"maximum": {v}}}""", Fidelity.Exact
            | Range _ -> "{}", Fidelity.Approximate "Complex range bounds not fully implemented in OpenAPI schema"
            | IntRange _ -> """{"type": "integer"}""", Fidelity.Approximate "Int range requires precision bounds"
            | StringRange _ -> "{}", Fidelity.Approximate "String range collation may differ and cannot be represented in OpenAPI"
            | MaxLength len -> $"""{{"maxLength": {len}}}""", Fidelity.Exact
            | InList items -> 
                let arr = items |> List.map (sprintf "\"%s\"") |> String.concat ", "
                $"""{{"enum": [{arr}]}}""", Fidelity.Exact
            | InSet items -> 
                let arr = items |> List.map (sprintf "\"%s\"") |> String.concat ", "
                $"""{{"enum": [{arr}]}}""", Fidelity.Exact
            | RelativeBound(colA, op, colB) ->
                $"""{{"description": "Requires {colA} {op} {colB}"}}""", Fidelity.Approximate "OpenAPI maps cross-field validation to description strings"
            | PrimaryKey -> "{}", Fidelity.Unsupported "PrimaryKey concept does not exist in OpenAPI validators"
            | NonEmpty -> """{"minLength": 1}""", Fidelity.Exact
            | Constraint.Opaque raw -> "{}", Fidelity.Unsupported $"Cannot transpile raw SQL to OpenAPI: {raw}"
            | FieldBound(field, inner) -> 
                let innerExpr, innerF = toOpenApiSchema (Lattice.Leaf inner)
                $"""{{"properties": {{"{field}": {innerExpr}}}}}""", innerF

    let emitSchema (name: string) (predicate: Lattice<Constraint>) : string * Fidelity =
        let expr, f = toOpenApiSchema predicate
        let code = $"""
"{name}": {expr}
"""
        code.Trim(), f
