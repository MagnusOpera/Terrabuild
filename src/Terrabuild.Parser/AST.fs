namespace Terrabuild.Parser.AST

[<RequireQualifiedAccessAttribute>]
type Function =
    | Plus
    | Trim
    | Upper
    | Lower

[<RequireQualifiedAccessAttribute>]
type Expr =
    | Nothing
    | Boolean of value:bool
    | String of value:string
    | Variable of name:string
    | Map of Map<string, Expr>
    | Function of Function * Expr list


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

