namespace Terrabuild.Dotnet.Pack
open Terrabuild.Extensibility
open Helpers

type Arguments = {
    Configuration: string option
    Version: string option
}

type Command(projectFile: string) =
    interface ICommandFactory with
        member _.TypeOfArguments: System.Type option = Some typeof<Arguments>

        member _.GetSteps (parameters: obj): CommandLine list =
            let parameters = parameters :?> Arguments

            let config = parameters.Configuration |> Option.defaultValue "Debug"
            let version = parameters.Version |> Option.defaultValue "0.0.0"
            // TargetsForTfmSpecificContentInPackage ==> https://github.com/dotnet/fsharp/issues/12320
            [ buildCmdLine "dotnet" $"pack {projectFile} --no-restore --no-build --configuration {config} /p:Version={version} /p:TargetsForTfmSpecificContentInPackage=" Cacheability.Always ]
