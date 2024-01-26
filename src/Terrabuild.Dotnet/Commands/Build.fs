namespace Terrabuild.Dotnet.Build
open Extensions
open Helpers

type Arguments = {
    Configuration: string option
}

type Command(projectFile: string) =
    interface ICommandFactory with
        member _.TypeOfArguments: System.Type option = Some typeof<Arguments>

        member _.GetSteps (parameters: obj): CommandLine list = 
            let parameters = parameters :?> Arguments

            let config = parameters.Configuration |> Option.defaultValue "Debug"
            [ buildCmdLine "dotnet" $"restore {projectFile} --no-dependencies" Cacheability.Local
              buildCmdLine "dotnet" $"build {projectFile} -m:1 --no-dependencies --no-restore --configuration {config}" Cacheability.Always ]
