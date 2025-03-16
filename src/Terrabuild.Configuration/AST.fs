namespace Terrabuild.Configuration.AST
open Terrabuild.Expressions
open Errors

[<RequireQualifiedAccess>]
type ExtensionComponents =
    | Container of Expr
    | Platform of Expr
    | Variables of Expr list
    | Script of Expr
    | Defaults of Map<string, Expr>

type Extension = {
    Container: Expr option
    Platform: Expr option
    Variables: Expr Set
    Script: Expr option
    Defaults: Map<string, Expr>
}
with
    static member Build name components =
        let container =
            match components |> List.choose (function | ExtensionComponents.Container value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> Some value
            | _ -> raiseParseError "multiple container declared"

        let platform =
            match components |> List.choose (function | ExtensionComponents.Platform value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> Some value
            | _ -> raiseParseError "multiple platform declared"

        let variables =
            match components |> List.choose (function | ExtensionComponents.Variables value -> Some value | _ -> None) with
            | [] -> Set.empty
            | [value] -> value |> Set.ofList
            | _ -> raiseParseError "multiple variables declared"

        let script =
            match components |> List.choose (function | ExtensionComponents.Script value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> Some value
            | _ -> raiseParseError "multiple script declared"

        let defaults =
            match components |> List.choose (function | ExtensionComponents.Defaults value -> Some value | _ -> None) with
            | [] -> Map.empty
            | [value] -> value
            | _ -> raiseParseError "multiple defaults declared"

        name, { Container = container
                Platform = platform
                Variables = variables
                Script = script
                Defaults = defaults }
  