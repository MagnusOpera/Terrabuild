namespace Terrabuild.Parser.AST
open Terrabuild.Expressions

[<RequireQualifiedAccess>]
type ExtensionComponents =
    | Container of string
    | Script of string
    | Init of Map<string, Expr>
    | Defaults of Map<string, Expr>

type Extension = {
    Container: string option
    Script: string option
    Init: Map<string, Expr>
    Defaults: Map<string, Expr>
}
with
    static member Empty =
        { Container = None
          Script = None
          Init = Map.empty
          Defaults = Map.empty }

    member this.Patch comp =
        match comp with
        | ExtensionComponents.Container container -> { this with Container = Some container }
        | ExtensionComponents.Script script -> { this with Script = Some script }
        | ExtensionComponents.Init init -> { this with Init = init }
        | ExtensionComponents.Defaults defaults -> { this with Defaults = defaults }

