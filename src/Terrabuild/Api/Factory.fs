module Api.Factory
open System.Net
open Errors

let checkAuthError msg f  =
    try
        f()
    with
        | ex ->
            let errorCode =
                match ex.InnerException with
                | :? WebException as innerEx ->
                    match innerEx.Response with
                    | :? HttpWebResponse as hwr -> hwr.StatusCode.ToString()
                    | _ -> ex.Message
                | _ -> ex.Message

            forwardExternalError($"{errorCode}: {msg}.", ex)


let create workspaceId token options =
    match workspaceId, token with
    | Some workspaceId , Some token ->
        checkAuthError
            $"please check permissions with your administrator to access workspace {workspaceId}"
            (fun() -> Client(workspaceId, token, options) :> Contracts.IApiClient |> Some)
    | _ ->
        None
