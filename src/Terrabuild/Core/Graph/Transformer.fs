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
            | NodeUsage.Build _ ->
                let ops =
                    node.ConfigurationTarget.Operations
                    |> List.collect (fun operation ->
                        let optContext = {
                            Terrabuild.Extensibility.ActionContext.Debug = options.Debug
                            Terrabuild.Extensibility.ActionContext.CI = options.CI.IsSome
                            Terrabuild.Extensibility.ActionContext.Command = operation.Command
                            Terrabuild.Extensibility.ActionContext.BranchOrTag = options.BranchOrTag
                            Terrabuild.Extensibility.ActionContext.Hash = operation.Hash
                        }

                        let parameters = 
                            match operation.Context with
                            | Terrabuild.Expressions.Value.Map map ->
                                map
                                |> Map.add "context" (Terrabuild.Expressions.Value.Object optContext)
                                |> Terrabuild.Expressions.Value.Map
                            | _ -> TerrabuildException.Raise("Failed to get context (internal error)")

                        let executionRequest =
                            match Extensions.invokeScriptMethod<Terrabuild.Extensibility.ActionExecutionRequest> optContext.Command parameters (Some operation.Script) with
                            | Extensions.InvocationResult.Success executionRequest -> executionRequest
                            | _ -> TerrabuildException.Raise("Failed to get shell operation (extension error)")

                        executionRequest.Operations
                        |> List.map (fun shellOperation -> {
                            ContaineredShellOperation.Container = operation.Container
                            ContaineredShellOperation.ContainerVariables = operation.ContainerVariables
                            ContaineredShellOperation.MetaCommand = $"{operation.Extension} {operation.Command}"
                            ContaineredShellOperation.Command = shellOperation.Command
                            ContaineredShellOperation.Arguments = shellOperation.Arguments
                        })
                    )

                { node with Operations = ops }
            | _ -> node

        allNodes.TryAdd(node.Id, node) |> ignore

    { graph with 
        Graph.Nodes = allNodes |> Map.ofDict }
