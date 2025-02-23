module Auth
open System


type SpaceAuth = {
    Id: Guid
    Token: string
}

[<RequireQualifiedAccess>]
type Configuration = {
    SpaceAuths: SpaceAuth list
}


let private removeAuthToken (workspaceId: Guid) =
    let configFile = FS.combinePath Cache.terrabuildHome "config.json"
    let config =
        if configFile |> FS.fileExists then configFile |> IO.readTextFile |> Json.Deserialize<Configuration>
        else { Configuration.SpaceAuths = List.empty }

    let config = { config with SpaceAuths = config.SpaceAuths |> List.filter (fun sa -> sa.Id = workspaceId )}

    config
    |> Json.Serialize
    |> IO.writeTextFile configFile


let private addAuthToken (workspaceId: Guid) (token: string) =
    let configFile = FS.combinePath Cache.terrabuildHome "config.json"
    let config =
        if configFile |> FS.fileExists then configFile |> IO.readTextFile |> Json.Deserialize<Configuration>
        else { SpaceAuths = [] }

    let config = { config with SpaceAuths = { Id = workspaceId; Token = token } :: config.SpaceAuths }

    config
    |> Json.Serialize
    |> IO.writeTextFile configFile


let readAuthToken (workspaceId: Guid) =
    let configFile = FS.combinePath Cache.terrabuildHome "config.json"
    let config =
        if configFile |> FS.fileExists then configFile |> IO.readTextFile |> Json.Deserialize<Configuration>
        else { Configuration.SpaceAuths = List.empty }

    match config.SpaceAuths |> List.tryFind (fun sa -> sa.Id = workspaceId) with
    | Some spaceAuth -> Some spaceAuth.Token
    | _ -> None



let login workspaceId token =
    Api.Factory.create (Some workspaceId) (Some token) |> ignore
    addAuthToken workspaceId token

let logout space =
    removeAuthToken space
