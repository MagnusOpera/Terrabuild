module AspNetExtensions

let inline addAuthentication (services: Microsoft.Extensions.DependencyInjection.IServiceCollection) : Microsoft.AspNetCore.Authentication.AuthenticationBuilder =
    Microsoft.Extensions.DependencyInjection.AuthenticationServiceCollectionExtensions.AddAuthentication(services)

#nowarn "0077" // op_Explicit
let inline (!>) (x : ^a) : ^b = (((^a or ^b) : (static member op_Explicit : ^a -> ^b) x))

let inline (!<) (x : ^a) : ^b = (((^a or ^b) : (static member op_Implicit : ^a -> ^b) x))

let inline reply msg = !> msg
