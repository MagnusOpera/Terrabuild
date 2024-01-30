namespace Terrabuild.Dotnet.Restore
open Terrabuild.Extensibility
open Helpers

type Command(projectFile: string) =
    interface ICommandBuilder with
        member _.GetSteps () = 
            [ buildCmdLine "dotnet" $"restore {projectFile} --no-dependencies" Cacheability.Local ]
