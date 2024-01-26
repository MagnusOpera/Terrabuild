namespace Terrabuild.Dotnet.Publish
open Terrabuild.Extensibility
open Helpers

type Arguments = {
    Configuration: string option
    Runtime: string option
    Trim: bool option
    Single: bool option
}

type Command(projectFile: string) =
    interface ICommandFactory with
        member _.TypeOfArguments: System.Type option = Some typeof<Arguments>

        member _.GetSteps (parameters: obj): CommandLine list = 
            let parameters = parameters :?> Arguments

            let config = parameters.Configuration |> Option.defaultValue "Debug"
            let runtime =
                match parameters.Runtime with
                | Some identifier -> $" -r {identifier}"
                | _ -> " --no-restore --no-build"
            let trim =
                match parameters.Trim with
                | Some true -> " -p:PublishTrimmed=true"
                | _ -> ""
            let single =
                match parameters.Single with
                | Some true -> " --self-contained"
                | _ -> ""
            [ buildCmdLine "dotnet" $"publish {projectFile} --configuration {config}{runtime}{trim}{single}" Cacheability.Always ]
