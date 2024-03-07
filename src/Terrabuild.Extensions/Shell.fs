namespace Terrabuild.Extensions
open Terrabuild.Extensibility


type Shell() =

    static member __dispatch__ (context: ActionContext) (arguments: string option) =
        let arguments = arguments |> Option.defaultValue ""
        scope Cacheability.Always
        |> andThen context.Command arguments 
