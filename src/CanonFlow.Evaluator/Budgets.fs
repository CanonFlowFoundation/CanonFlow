namespace CanonFlow.Evaluator

open System
open System.IO
open CanonFlow.Assurance

module Budgets =
    
    let Default = {
        MaxFiles = 10000
        MaxInputBytes = 50L * 1024L * 1024L // 50MB
        MaxJsonDepth = 100
        ComponentTimeoutSeconds = 300 // 5 mins
        TotalTimeoutSeconds = 1800 // 30 mins
    }

    let checkFileSize (fileInfo: FileInfo) (budget: EvaluationBudget) =
        if fileInfo.Length > budget.MaxInputBytes then
            Error (sprintf "File %s exceeds budget size %d bytes" fileInfo.Name budget.MaxInputBytes)
        else
            Ok ()

