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

            TerrabuildException.Raise($"{errorCode}: {msg}.", ex)


let authenticate token =
    checkAuthError "please check permissions with your administrator" (fun () -> Auth.authenticate token)


let create token space =
    match token, space with
    | Some token , Some space ->
        checkAuthError
            $"please check permissions with your administrator to access space {space}"
            (fun() -> Client(token, space) :> Contracts.IApiClient |> Some)
    | _ ->
        None
    