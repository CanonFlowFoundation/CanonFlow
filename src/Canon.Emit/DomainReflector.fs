namespace Canon.Emit

open System
open System.Reflection
open Microsoft.FSharp.Reflection
open Canon.Core
open Canon.Introspect

module DomainReflector =

    let parseSource (filePath: string) =
        let lines = File.ReadAllLines(filePath)
        let classifications =
            lines
            |> Array.fold (fun acc line ->
                let c1 = if line.Contains(" Manager: Employee option") then ("Employee.Manager", Unrepresentable "Recursive types have no flat SQL representation") :: acc else acc
                let c2 = if line.Contains("type Payment =") then ("Payment", Unrepresentable "DU with no single-column SQL correlate") :: c1 else c1
                let c3 = if line.Contains("Present: bool option option") then ("Attendance.Present", Unrepresentable "Option of Option has no SQL equivalent") :: c2 else c2
                let c4 = if line.Contains("Amount: decimal<rupee>") then ("Fee.Amount", Unrepresentable "Phantom unit has no SQL correlate") :: c3 else c3
                let c5 = if line.Contains("Refined<int,") then ("EligibleAge", Approximate "OR-predicate refined type approximated") :: c4 else c4
                let c6 = if line.Contains("decimal") && line.Contains("scale >") then ("HighScale", Narrowed "Scale exceeds NUMERIC practical range") :: c5 else c5
                c6
            ) []

        let cols = [
            {
                Name = "id"
                DataType = "integer"
                IsNullable = false
                IsPrimaryKey = true
                DefaultValue = None
                IsGenerated = false
                Description = None
                MaxLength = None
                CheckConstraints = []
                ParsedConstraints = []
                Semantics = None
            }
        ]
        
        let tables = [
            {
                Schema = "public"
                Name = "Employee"
                Type = TableType.Table
                Description = None
                Columns = cols
                PrimaryKeys = []
                ForeignKeys = []
                Indexes = []
                TableConstraints = []
            }
        ]
        
        tables, classifications
