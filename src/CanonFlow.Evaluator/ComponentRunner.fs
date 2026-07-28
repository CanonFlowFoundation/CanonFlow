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
        exitCode
        |> ExitCode.tryVerdict
        |> Option.defaultValue Verdict.ToolFailure

    let runComponentAsync (executablePath: string) (args: string list) (budget: EvaluationBudget) =
        async {
            let stopwatch = Stopwatch.StartNew()
            try
                use process_ = new Process()
                process_.StartInfo.FileName <- executablePath
                args |> List.iter process_.StartInfo.ArgumentList.Add
                process_.StartInfo.UseShellExecute <- false
                process_.StartInfo.RedirectStandardOutput <- true
                process_.StartInfo.RedirectStandardError <- true
                process_.StartInfo.CreateNoWindow <- true

                if not (process_.Start()) then
                    stopwatch.Stop()
                    return {
                        ExitCode = 3
                        Output = ""
                        Error = $"Component process did not start: {executablePath}"
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                    }
                else
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
                            ExitCode = 3
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
            with ex ->
                stopwatch.Stop()
                return {
                    ExitCode = 3
                    Output = ""
                    Error = $"Component execution failed: {ex.Message}"
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                }
        }

