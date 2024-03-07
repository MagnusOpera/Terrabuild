namespace Terrabuild.Extensions

open Terrabuild.Extensibility


type Make() =

    static member __dispatch__ (context: ActionContext) (variables: Map<string, string>) =
        let args = variables |> Seq.fold (fun acc kvp -> $"{acc} {kvp.Key}=\"{kvp.Value}\"") $"{context.Command}"
        scope Cacheability.Always
        |> andThen "make" args
