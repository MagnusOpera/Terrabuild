let config = Configuration.read "tests"
// printfn $"{config}"

let graph = Graph.buildGraph config "build"
// printfn $"{graph}"

let buildInfo = Build.run config graph
printfn $"{buildInfo}"




// let build = Json.DeserializeFile<Configuration.BuildConfiguration> "tests/BUILD"
// printfn $"{build}"

// let project = Json.DeserializeFile<Configuration.ProjectConfiguration> "tests/projects/project1/PROJECT"
// printfn $"{project}"
