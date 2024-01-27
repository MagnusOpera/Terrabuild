namespace Terrabuild.Dotnet.Build
open Terrabuild.Extensibility
open Helpers

type Arguments = {
    Configuration: string option
}

type Command(projectFile: string) =
    interface ICommandFactory<Arguments> with
        member _.GetSteps parameters = 
            let config = parameters.Configuration |> Option.defaultValue "Debug"
            [ buildCmdLine "dotnet" $"restore {projectFile} --no-dependencies" Cacheability.Local
              buildCmdLine "dotnet" $"build {projectFile} -m:1 --no-dependencies --no-restore --configuration {config}" Cacheability.Always ]
