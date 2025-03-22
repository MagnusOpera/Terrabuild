module Terrabuild.Configuration.Tests.HCL

open System.IO
open NUnit.Framework

[<Test>]
let parseProject() =
    let content = File.ReadAllText("TestFiles/PROJECT")
    let file = FrontEnd.HCL.parse content
    printfn $"{file}"

[<Test>]
let parseProject2() =
    let content = File.ReadAllText("TestFiles/PROJECT2")
    let file = FrontEnd.HCL.parse content
    printfn $"{file}"

[<Test>]
let parseWorkspace() =
    let content = File.ReadAllText("TestFiles/WORKSPACE")
    let file = FrontEnd.HCL.parse content
    printfn $"{file}"

[<Test>]
let parseWorkspace2() =
    let content = File.ReadAllText("TestFiles/WORKSPACE2")
    let file =FrontEnd.HCL.parse content
    printfn $"{file}"
