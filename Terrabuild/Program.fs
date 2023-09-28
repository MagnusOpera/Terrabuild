let config = Configuration.read "tests"
// printfn $"{config}"

let graph = Graph.buildGraph config "build"
// printfn $"{graph}"

let buildInfo = Build.run config graph
printfn $"{buildInfo}"
