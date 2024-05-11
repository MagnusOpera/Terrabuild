module Explorer
open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Hosting.Server
open Microsoft.AspNetCore.Hosting.Server.Features
open System.Threading

let serve wsDir =

    let handler environment target =
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
        mermaid
    let handler = Func<string, string, string>(fun env target -> handler env target)

    let builder = WebApplication.CreateBuilder()
    let app = builder.Build()
    app.MapGet("/graph/{env}/{target}", handler) |> ignore
    app.Lifetime.ApplicationStarted.Register(fun () ->
        let server = app.Services.GetRequiredService<IServer>()
        let addressFeature = server.Features.Get<IServerAddressesFeature>()
        let address = addressFeature.Addresses |> Seq.head
        printfn $"Port = {address}") |> ignore

    app.Run()
