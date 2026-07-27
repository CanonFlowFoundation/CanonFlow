namespace CanonFlow.Evaluator

open System
open System.IO
open CanonFlow.Assurance

module FsAssayRunner =

    let run (manifest: EvaluationManifest) (budget: EvaluationBudget) =
        // Real FsAssay runner logic would invoke the FsAssay executable
        // Here we simulate the execution and exit code mapping.
        
        let target = manifest.Subject.Root
        let executable = "dotnet" // In reality, we run FsAssay.Runner from .nuget/offline
        let args = sprintf "fsassay --target %s" target
        
        async {
            let! result = ComponentRunner.runComponentAsync executable args budget
            let verdict = ComponentRunner.mapExitCodeToVerdict result.ExitCode
            
            return {
                ComponentId = "fsassay"
                ComponentVersion = "1.0.0"
                Health = "Complete"
                Compliance = if verdict = Verdict.Pass then "Conformant" else "NonConformant"
                ApplicableRules = 0
                EvaluatedRules = 0
                Evidence = []
            }
        }

