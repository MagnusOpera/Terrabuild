module Auth


let login space token =
    let api = Api.Factory.create (Some space) (Some token)
    Cache.addAuthToken space token

let logout space =
    Cache.removeAuthToken space
