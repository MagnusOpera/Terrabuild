module Auth


let login token =
    Api.Factory.authenticate token
    Cache.addAuthToken token

let logout () =
    Cache.removeAuthToken()
