namespace Terrabuild.Dotnet.Test
open Terrabuild.Extensibility
open Helpers

type Arguments = {
    Configuration: string option
    Filter: string option
}

type Command(projectFile: string) =
    interface ICommandBuilder<Arguments> with
        member _.GetSteps parameters =
            let config = parameters.Configuration |> Option.defaultValue "Debug"
            let filter = parameters.Filter |> Option.defaultValue "true"
            [ buildCmdLine "dotnet" $"test --no-build --configuration {config} {projectFile} --filter \"{filter}\"" Cacheability.Always ]
