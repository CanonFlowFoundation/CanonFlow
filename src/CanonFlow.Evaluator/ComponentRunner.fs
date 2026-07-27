namespace CanonFlow.Evaluator

open System
open System.Diagnostics
open CanonFlow.Assurance

type ComponentExecutionResult = {
    ExitCode: int
    Output: string
    Error: string
    ExecutionTimeMs: int64
}

module ComponentRunner =

    let mapExitCodeToVerdict (exitCode: int) =
        match exitCode with
        | 0 -> Verdict.Pass
        | 1 -> Verdict.Fail
        | 2 -> Verdict.Inconclusive
        | _ -> Verdict.ToolFailure

    let runComponentAsync (executablePath: string) (args: string) (budget: EvaluationBudget) =
        async {
            let stopwatch = Stopwatch.StartNew()
            
            use process_ = new Process()
            process_.StartInfo.FileName <- executablePath
            process_.StartInfo.Arguments <- args
            process_.StartInfo.UseShellExecute <- false
            process_.StartInfo.RedirectStandardOutput <- true
            process_.StartInfo.RedirectStandardError <- true
            process_.StartInfo.CreateNoWindow <- true

            let! started = process_.Start() |> ignore; async.Return(true)
            
            let outputTask = process_.StandardOutput.ReadToEndAsync()
            let errorTask = process_.StandardError.ReadToEndAsync()

            let! isCompleted = 
                Async.AwaitTask(
                    System.Threading.Tasks.Task.Run(fun () -> 
                        process_.WaitForExit(budget.ComponentTimeoutSeconds * 1000)
                    )
                )

            stopwatch.Stop()

            if not isCompleted then
                process_.Kill()
                return {
                    ExitCode = 3 // ToolFailure due to timeout
                    Output = ""
                    Error = "Process exceeded timeout budget"
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                }
            else
                let! output = outputTask |> Async.AwaitTask
                let! error = errorTask |> Async.AwaitTask
                
                return {
                    ExitCode = process_.ExitCode
                    Output = output
                    Error = error
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                }
        }

