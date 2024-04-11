module AspNetExtensions

let inline addAuthentication (services: Microsoft.Extensions.DependencyInjection.IServiceCollection) : Microsoft.AspNetCore.Authentication.AuthenticationBuilder =
    Microsoft.Extensions.DependencyInjection.AuthenticationServiceCollectionExtensions.AddAuthentication(services)
