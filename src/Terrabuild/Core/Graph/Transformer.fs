module GraphTransformer
open Collections
open GraphDef
open System.Collections.Concurrent
open Serilog
open Errors

let transform (options: Configuration.Options) (graph: GraphDef.Graph) =
    Log.Debug("===== [Graph Transform] =====")

    let allNodes = ConcurrentDictionary<string, GraphDef.Node>()
    for (KeyValue(_, node)) in graph.Nodes do

        let node =
            match node.Usage with
            | NodeUsage.Build targetOperation ->

                let optContext = {
                    Terrabuild.Extensibility.ActionContext.Debug = options.Debug
                    Terrabuild.Extensibility.ActionContext.CI = options.CI.IsSome
                    Terrabuild.Extensibility.ActionContext.Command = targetOperation.Command
                    Terrabuild.Extensibility.ActionContext.BranchOrTag = options.BranchOrTag
                    Terrabuild.Extensibility.ActionContext.Hash = targetOperation.Hash
                }

                let parameters = 
                    match targetOperation.Context with
                    | Terrabuild.Expressions.Value.Map map ->
                        map
                        |> Map.add "context" (Terrabuild.Expressions.Value.Object optContext)
                        |> Terrabuild.Expressions.Value.Map
                    | _ -> TerrabuildException.Raise("Failed to get context (internal error)")

                let executionRequest =
                    match Extensions.invokeScriptMethod<Terrabuild.Extensibility.ActionExecutionRequest> optContext.Command parameters (Some targetOperation.Script) with
                    | Extensions.InvocationResult.Success executionRequest -> executionRequest
                    | _ -> TerrabuildException.Raise("Failed to get shell operation (extension error)")

                let ops =
                    executionRequest.Operations
                    |> List.map (fun operation -> {
                        ContaineredShellOperation.Container = targetOperation.Container
                        ContaineredShellOperation.ContainerVariables = targetOperation.ContainerVariables
                        ContaineredShellOperation.MetaCommand = $"{targetOperation.Extension} {targetOperation.Command}"
                        ContaineredShellOperation.Command = operation.Command
                        ContaineredShellOperation.Arguments = operation.Arguments
                    })

                { node with Operations = ops }
            | _ -> node

        allNodes.TryAdd(node.Id, node) |> ignore

    { graph with 
        Graph.Nodes = allNodes |> Map.ofDict }
