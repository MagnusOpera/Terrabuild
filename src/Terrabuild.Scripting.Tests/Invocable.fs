module Terrabuild.Scripting.Tests.Project

open NUnit.Framework
open FsUnit

open Terrabuild.Scripting
open Terrabuild.Expressions


type Arguments = {
    Name: string
    Address: string option
}

type Scripting() =
    static member Hello (name: string) =
        $"Hello {name}"

    static member HelloArgs (args: Arguments) =
        $"Hello {args.Name} !"

    static member HelloOption (name: string option) =
        let name = name |> Option.defaultValue "None"
        $"Hello {name} !"

let getMethod name =
    let method = typeof<Scripting>.GetMethod(name)
    method

[<Test>]
let invokeScalar() =
    let method = getMethod "Hello"
    let invocable = Invocable(method)

    let args = Value.Map (Map ["name", Value.String "Pierre"])
    let result = invocable.Invoke<string> args
    result |> should equal "Hello Pierre"

[<Test>]
let invokeRecord() =
    let method = getMethod "HelloArgs"
    let invocable = Invocable(method)
    
    let args = Value.Map (Map ["args", Value.Map (Map ["Name", Value.String "Pierre"])])
    let result = invocable.Invoke<string> args
    result |> should equal "Hello Pierre !"

[<Test>]
let invokeOptionNone() =
    let method = getMethod "HelloOption"
    let invocable = Invocable(method)

    let args = Value.Map Map.empty
    let result = invocable.Invoke<string> args
    result |> should equal "Hello None !"

[<Test>]
let invokeOptionSome() =
    let method = getMethod "HelloOption"
    let invocable = Invocable(method)

    let args = Value.Map (Map ["name", Value.String "Pierre"])
    let result = invocable.Invoke<string> args
    result |> should equal "Hello Pierre !"
