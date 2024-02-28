open Queil.FSharp.FscHost

let loadQueil () =
    let options = Options.Default

    let output =
        File "script.fsx"
        |> CompilerHost.getAssembly options
        |> Async.RunSynchronously

    let assembly = output.Assembly.Value
    let tpe = assembly.GetType("Script")
    let f = tpe.GetMethod("sayHello")
    for arg in f.GetParameters() do
        printfn $"{arg.Name}:{arg.ParameterType.ToString()}"
    let r = f.Invoke(null, [| "Toto" |])
    printfn $"{r}"

let loadScripting () =
    let assembly = Scripting.loadScript "script.fsx"
    let tpe = assembly.GetType("Script")
    let f = tpe.GetMethod("sayHello")
    for arg in f.GetParameters() do
        printfn $"{arg.Name}:{arg.ParameterType.ToString()}"
    let r = f.Invoke(null, [| "Toto" |])
    printfn $"{r}"

loadScripting()



// let tpe = assembly |> Member.get<string -> string> "Script.sayHello"
// let r = tpe("Toto")
// printfn $"R = {r}"



// let o = assembly.GetType("Script")
// let m = o.GetMethod("sayHello")
// printfn $"MethodName = {m.Name}"
// let r = m.Invoke(null, [| "Pierre" |]) :?> string
// printfn $"Result = {r}"


// for m in o.GetMembers() do
//     printfn $"Member {m.ToString()}"

