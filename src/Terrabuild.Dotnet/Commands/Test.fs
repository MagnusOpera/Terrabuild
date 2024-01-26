namespace Terrabuild.Dotnet.Test
open Extensions
open Helpers

type Arguments = {
    Configuration: string option
    Filter: string option
}

type Command(projectFile: string) =
    interface ICommandFactory with
        member _.TypeOfArguments: System.Type option = Some typeof<Arguments>

        member _.GetSteps (parameters: obj): CommandLine list =
            let parameters = parameters :?> Arguments

            let config = parameters.Configuration |> Option.defaultValue "Debug"
            let filter = parameters.Filter |> Option.defaultValue "true"
            [ buildCmdLine "dotnet" $"test --no-build --configuration {config} {projectFile} --filter \"{filter}\"" Cacheability.Always ]
