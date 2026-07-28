namespace Canon.Wasm

open Fable.Core
open Canon.Core
open Canon.Fable
open Canon.Emit

module CanonCompiler =
    
    let parseConstraintStr (s: string) =
        let s = s.ToUpperInvariant().Trim()
        let s = if s.StartsWith("CHECK") then s.Substring(5).Trim([|' '; '('; ')'|]) else s
        if s.Contains(">=") then
            let p = s.Split(">=")
            Lattice.Leaf (Constraint.Range(Some (Bound.Inclusive(System.Decimal.TryParse(p.[1].Trim()) |> snd)), None))
        elif s.Contains(">") then
            let p = s.Split(">")
            Lattice.Leaf (Constraint.Range(Some (Bound.Exclusive(System.Decimal.TryParse(p.[1].Trim()) |> snd)), None))
        elif s.Contains("<=") then
            let p = s.Split("<=")
            Lattice.Leaf (Constraint.Range(None, Some (Bound.Inclusive(System.Decimal.TryParse(p.[1].Trim()) |> snd))))
        elif s.Contains("<") then
            let p = s.Split("<")
            Lattice.Leaf (Constraint.Range(None, Some (Bound.Exclusive(System.Decimal.TryParse(p.[1].Trim()) |> snd))))
        elif s.Contains("LIKE") || s.Contains("~") || s.Contains("SIMILAR TO") then
            Lattice.Leaf (Constraint.Opaque("Regex dummy")) // Dummy for playground
        else
            Lattice.True

    let transpile(sqlText: string) =
        try
            let tables = DdlParser.parseSql sqlText
            
            // Run Paradox Engine diagnostics
            let diagnostics = ResizeArray<string>()
            for t in tables do
                for c in t.Columns do
                    if c.CheckConstraints.Length > 0 then
                        let lattice = 
                            c.CheckConstraints 
                            |> List.map parseConstraintStr
                            |> List.reduce (fun a b -> Lattice.And(a, b))
                        let simplified = SemanticOptimizer.simplify lattice
                        if simplified = Lattice.False then
                            diagnostics.Add($"[DIAGNOSTIC CONTRADICTION] Table: {t.Name}, Column: {c.Name} has contradictory constraints that collapse to False.")

            // Generate Outputs
            let tsSb = System.Text.StringBuilder()
            let ktSb = System.Text.StringBuilder()
            let swSb = System.Text.StringBuilder()
            
            for t in tables do
                for c in t.Columns do
                    if not c.CheckConstraints.IsEmpty then
                        let lattice = 
                            c.CheckConstraints 
                            |> List.map parseConstraintStr 
                            |> List.reduce (fun a b -> Lattice.And(a, b))
                            
                        // Populate ParsedConstraints for FsCheckEmitter
                        let cNew = { c with ParsedConstraints = [lattice] }
                        
                        let tsCode, _ = Transpiler.emitValidator $"{t.Name}_{c.Name}" lattice c.IsNullable None
                        let ktCode, _ = KotlinTranspiler.emitValidator $"{t.Name}_{c.Name}" lattice c.IsNullable None
                        let swCode, _ = SwiftTranspiler.emitValidator $"{t.Name}_{c.Name}" lattice c.IsNullable None
                        tsSb.AppendLine(tsCode) |> ignore
                        ktSb.AppendLine(ktCode) |> ignore
                        swSb.AppendLine(swCode) |> ignore

            // Update tables to include the parsed constraints for FsCheck
            let updatedTables = 
                tables |> List.map (fun t -> 
                    let updatedCols =
                        t.Columns |> List.map (fun c -> 
                            if not c.CheckConstraints.IsEmpty then
                                { c with ParsedConstraints = c.CheckConstraints |> List.map parseConstraintStr }
                            else c)
                    { t with Columns = updatedCols }
                )

            let fscheckCode = FsCheckEmitter.emitGenerators updatedTables

            {| 
                typescript = string tsSb
                kotlin = string ktSb
                swift = string swSb
                fscheck = fscheckCode
                diagnostics = diagnostics.ToArray()
                error = null
            |}
        with ex ->
            {| 
                typescript = ""
                kotlin = ""
                swift = ""
                fscheck = ""
                diagnostics = [||]
                error = ex.Message
            |}
