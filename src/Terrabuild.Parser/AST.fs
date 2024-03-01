namespace Terrabuild.Parser.AST
open Terrabuild.Expressions

[<RequireQualifiedAccess>]
type ExtensionComponents =
    | Container of string
    | Script of string
    | Parameters of Map<string, Expr>

type Extension = {
    Container: string option
    Script: string option
    Parameters: Map<string, Expr>
}
with
    static member Empty =
        { Container = None
          Script = None
          Parameters = Map.empty }

    member this.Patch comp =
        match comp with
        | ExtensionComponents.Container container -> { this with Container = Some container }
        | ExtensionComponents.Script script -> { this with Script = Some script }
        | ExtensionComponents.Parameters parameters -> { this with Parameters = parameters }
