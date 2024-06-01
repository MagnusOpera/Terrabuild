namespace Terrabuild.Configuration.AST
open Terrabuild.Expressions

[<RequireQualifiedAccess>]
type ExtensionComponents =
    | Container of string
    | Variables of string list
    | Script of string
    | Defaults of Map<string, Expr>

type Extension = {
    Container: string option
    Variables: string Set
    Script: string option
    Defaults: Map<string, Expr>
}
with
    static member Empty =
        { Container = None
          Variables = Set.empty
          Script = None
          Defaults = Map.empty }

    member this.Patch comp =
        match comp with
        | ExtensionComponents.Container container -> { this with Container = Some container }
        | ExtensionComponents.Variables variables -> { this with Variables = Set variables }
        | ExtensionComponents.Script script -> { this with Script = Some script }
        | ExtensionComponents.Defaults defaults -> { this with Defaults = defaults }
