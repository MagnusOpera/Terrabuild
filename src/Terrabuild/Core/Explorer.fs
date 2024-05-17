module Explorer
open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Hosting.Server
open Microsoft.AspNetCore.Hosting.Server.Features

let serve wsDir =

    let graphHandler = Func<string, string, string>(fun environment target ->
        let options = { Configuration.Options.WhatIf = false
                        Configuration.Options.Debug = false
                        Configuration.Options.Force = false
                        Configuration.Options.MaxConcurrency = 0
                        Configuration.Options.Retry = false
                        Configuration.Options.StartedAt = DateTime.UtcNow }
        let variables = Map.empty
        let labels = None
        let targets = Set.singleton target

        let sourceControl = SourceControls.Factory.create()
        let config = Configuration.read wsDir environment variables sourceControl options
        let graph = Graph.buildGraph config labels targets
        let mermaid = Graph.graph graph |> String.join "\n"
        mermaid)

    let builder = WebApplication.CreateBuilder()
    let app = builder.Build()
    app.MapGet("/graph/{env}/{target}", graphHandler) |> ignore
    app.Lifetime.ApplicationStarted.Register(fun () ->
        let server = app.Services.GetRequiredService<IServer>()
        let addressFeature = server.Features.Get<IServerAddressesFeature>()
        let address = Uri(addressFeature.Addresses |> Seq.head)

        let graphUrl =
            DotNetEnv.Env.GetString("TERRABUILD_GRAPH_URL", "https://graph.terrabuild.io")
            |> Uri
        let graphUrl = Uri(graphUrl, $"?port={address.Port}").ToString()
        System.Diagnostics.Process.Start("open", graphUrl) |> ignore) |> ignore

    app.Run("http://*:0")
