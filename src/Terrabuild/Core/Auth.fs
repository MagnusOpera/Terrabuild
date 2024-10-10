module Auth


type SpaceAuth = {
    Space: string
    Token: string
}

[<RequireQualifiedAccess>]
type Configuration = {
    SpaceAuths: SpaceAuth list
}


let private removeAuthToken (space: string) =
    let configFile = FS.combinePath Cache.terrabuildHome "config.json"
    let config =
        if configFile |> FS.fileExists then configFile |> IO.readTextFile |> Json.Deserialize<Configuration>
        else { Configuration.SpaceAuths = List.empty }

    let config = { config with SpaceAuths = config.SpaceAuths |> List.filter (fun sa -> sa.Space = space )}

    config
    |> Json.Serialize
    |> IO.writeTextFile configFile


let private addAuthToken (space: string) (token: string) =
    let configFile = FS.combinePath Cache.terrabuildHome "config.json"
    let config =
        if configFile |> FS.fileExists then configFile |> IO.readTextFile |> Json.Deserialize<Configuration>
        else { SpaceAuths = [] }

    let config = { config with SpaceAuths = { Space = space; Token = token } :: config.SpaceAuths }

    config
    |> Json.Serialize
    |> IO.writeTextFile configFile


let readAuthToken (space: string) =
    let configFile = FS.combinePath Cache.terrabuildHome "config.json"
    let config =
        if configFile |> FS.fileExists then configFile |> IO.readTextFile |> Json.Deserialize<Configuration>
        else { Configuration.SpaceAuths = List.empty }

    match config.SpaceAuths |> List.tryFind (fun sa -> sa.Space = space) with
    | Some spaceAuth -> Some spaceAuth.Token
    | _ -> None



let login space token =
    Api.Factory.create (Some space) (Some token) |> ignore
    addAuthToken space token

let logout space =
    removeAuthToken space
