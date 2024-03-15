namespace Terrabuild.Extensions
open Terrabuild.Extensibility


type Null() =

    static member __init__ (context: InitContext) =
        if context.Debug then printfn "__init__ invoked"
        ProjectInfo.Default

    static member __dispatch__ (context: ActionContext) =
        if context.Debug then printfn $"__dispatch__ {context} invoked"
