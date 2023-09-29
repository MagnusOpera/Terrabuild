module CLI
open Argu

[<RequireQualifiedAccess>]
type BuildArgs =
    | [<Unique; AltCommandLine("--nc"); Inherit>] NoCache
    | [<CustomCommandLine("--tag"); AltCommandLine("--t"); Inherit>] Tag of tag:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | NoCache -> "Do not use cache when building target."
            | Tag _ -> "Select projets to build using one or more tags."

[<RequireQualifiedAccess>]
type RunArgs =
    | [<MainCommand; ExactlyOnce; First>] Target of target:string
    | [<Unique; AltCommandLine("--nc"); Inherit>] NoCache
    | [<CustomCommandLine("--tag"); AltCommandLine("--t"); Inherit>] Tag of tag:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "Specify build target."
            | NoCache -> "Do not use cache when building target."
            | Tag _ -> "Select projets to build using one or more tags."

[<RequireQualifiedAccess>]
type TerrabuildArgs =
    | [<CliPrefix(CliPrefix.None)>] Build of ParseResults<BuildArgs>
    | [<CliPrefix(CliPrefix.None)>] Run of ParseResults<RunArgs>
    | [<Unique; AltCommandLine("--ws"); Inherit>] Workspace of path:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Build _ -> "Run target 'build'."
            | Run _ -> "Run specified target."
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
