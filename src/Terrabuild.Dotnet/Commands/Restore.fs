namespace Terrabuild.Dotnet.Restore
open Terrabuild.Extensibility
open Helpers

type Command(projectFile: string) =
    interface ICommandFactory with
        member _.TypeOfArguments: System.Type option = None

        member _.GetSteps (parameters: obj): CommandLine list = 
            [ buildCmdLine "dotnet" $"restore {projectFile} --no-dependencies" Cacheability.Local ]
