let config = Configuration.read "tests"
// printfn $"{config}"

let graph = Graph.buildGraph config "push"
// printfn $"{graph}"

let buildInfo = Build.run config graph
printfn $"{buildInfo}"
