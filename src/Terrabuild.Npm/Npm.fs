namespace Extensions
open System
open System.ComponentModel.Composition


type Npm(context: IContext) =
    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = Cacheability.Always }

    interface IExtension with
        member _.Container = Some "node:20.9"

        member _.Dependencies = [] 

        member _.Outputs = [ "dist" ]

        member _.Ignores = [ "node_modules" ]

        member _.GetStepParameters action =
            match action with
            | "install" -> None
            | "build" -> None
            | "test" -> None
            | _ -> ArgumentException($"Unknown action {action}") |> raise

        member _.BuildStepCommands (action, parameters) =
            match parameters, action with
            | _, "install" ->
                [ buildCmdLine "npm" "ci" ]
            | _, "build" ->
                [ buildCmdLine "npm" "ci"
                  buildCmdLine "npm" "run build" ]
            | _, "test" ->
                [ buildCmdLine "npm" "ci"
                  buildCmdLine "npm" "run test" ]
            | _ -> ArgumentException($"Unknown action") |> raise


[<Export("npm", typeof<IExtensionFactory>)>]
type NpmFactory() =
    interface IExtensionFactory with
        member _.Create ctx =
            Npm(ctx)
