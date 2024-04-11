namespace Api
open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.HttpsPolicy
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.IdentityModel.Tokens
open System.Text

type Startup(config: IConfiguration) =

    member _.ConfigureServices(services: IServiceCollection) =
        let sectionSettings = config.GetSection("AppSettings")
        services.Configure<AppSettings>(sectionSettings) |> ignore
        let appSettings = sectionSettings.Get<AppSettings>()

        services.AddSingleton(appSettings) |> ignore
        services
            .AddAuthentication()
            .AddJwtBearer(fun options ->
                options.RequireHttpsMetadata <- false
                options.SaveToken <- false
                options.TokenValidationParameters <- TokenValidationParameters(
                    IssuerSigningKey = SymmetricSecurityKey(Encoding.ASCII.GetBytes(appSettings.Auth.Secret)),
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = appSettings.Auth.Issuer,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero)) |> ignore

        services.AddControllers() |> ignore

    member _.Configure (app: IApplicationBuilder, env: IWebHostEnvironment) =
        if (env.IsDevelopment()) then
            app.UseDeveloperExceptionPage() |> ignore

        app
            .UseAuthentication()
            .UseAuthorization() |> ignore

        app
            .UseHttpsRedirection()
            .UseRouting()
            .UseEndpoints(fun endpoints ->
                endpoints.MapControllers() |> ignore) |> ignore

