open Helpers

let config = Configuration.read "tests"
// printfn $"{config}"

let graph = Graph.buildGraph config "publish"
// printfn $"{graph}"

Build.run "tests" graph



// let build = Json.DeserializeFile<Configuration.BuildConfiguration> "tests/BUILD"
// printfn $"{build}"

// let project = Json.DeserializeFile<Configuration.ProjectConfiguration> "tests/projects/project1/PROJECT"
// printfn $"{project}"
