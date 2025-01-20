namespace Terrabuild.Configuration.AST
open Terrabuild.Expressions
open Errors

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
    static member Build name components =
        let container =
            match components |> List.choose (function | ExtensionComponents.Container value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> Some value
            | _ -> TerrabuildException.Raise("multiple container declared")

        let variables =
            match components |> List.choose (function | ExtensionComponents.Variables value -> Some value | _ -> None) with
            | [] -> Set.empty
            | [value] -> value |> Set.ofList
            | _ -> TerrabuildException.Raise("multiple variables declared")

        let script =
            match components |> List.choose (function | ExtensionComponents.Script value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> Some value
            | _ -> TerrabuildException.Raise("multiple script declared")

        let defaults =
            match components |> List.choose (function | ExtensionComponents.Defaults value -> Some value | _ -> None) with
            | [] -> Map.empty
            | [value] -> value
            | _ -> TerrabuildException.Raise("multiple defaults declared")

        name, { Container = container
                Variables = variables
                Script = script
                Defaults = defaults }
  