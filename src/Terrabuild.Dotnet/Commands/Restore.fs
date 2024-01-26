namespace Terrabuild.Dotnet.Restore
open Extensions
open Helpers

type Command(projectFile: string) =
    interface ICommandFactory with
        member _.TypeOfArguments: System.Type option = None

        member _.GetSteps (parameters: obj): CommandLine list = 
            [ buildCmdLine "dotnet" $"restore {projectFile} --no-dependencies" Cacheability.Local ]
