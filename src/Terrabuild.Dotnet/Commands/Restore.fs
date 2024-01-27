namespace Terrabuild.Dotnet.Restore
open Terrabuild.Extensibility
open Helpers

type Command(projectFile: string) =
    interface ICommandFactoryParameterless with
        member _.GetSteps () = 
            [ buildCmdLine "dotnet" $"restore {projectFile} --no-dependencies" Cacheability.Local ]
