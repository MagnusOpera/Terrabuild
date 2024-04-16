module Program
open Api

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
open Microsoft.AspNetCore.Identity
open Microsoft.AspNetCore.Authentication

// #nowarn "20"

let builder = WebApplication.CreateBuilder()
let config = builder.Configuration

let jsonSettings = config.GetSection("AppSettings") |> FSharpJson.ToJson
let appSettings = jsonSettings.ToJsonString() |> FSharpJson.Deserialize<AppSettings> 

builder.Services.AddSingleton(appSettings) |> ignore

// builder.Services.AddIdentity<IdentityUser, IdentityRole>() |> ignore

let defaultAuth (options: AuthenticationOptions) =
    options.DefaultAuthenticateScheme <- JwtBearerDefaults.AuthenticationScheme
    options.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme
    options.DefaultScheme <- JwtBearerDefaults.AuthenticationScheme

let jwtBearer (options: JwtBearerOptions) =
    options.RequireHttpsMetadata <- false
    options.SaveToken <- true
    options.TokenValidationParameters <- TokenValidationParameters(
        ValidateAudience = false,
        ValidateIssuer = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = appSettings.Auth.Issuer,
        IssuerSigningKey = SymmetricSecurityKey(Encoding.ASCII.GetBytes(appSettings.Auth.Secret)),
        ClockSkew = TimeSpan.Zero)

builder.Services
    .AddAuthentication(defaultAuth).AddJwtBearer(jwtBearer) |> ignore

builder.Services
    .AddEndpointsApiExplorer()
    .AddSwaggerGen()
    .AddControllers()
    .AddJsonOptions(fun options -> FSharpJson.Configure options.JsonSerializerOptions) |> ignore

let app = builder.Build()

if app.Environment.IsDevelopment() then
    app.UseSwagger().UseSwaggerUI() |> ignore

app.UseHttpsRedirection() |> ignore

app.UseAuthentication().UseAuthorization() |> ignore

app.MapControllers() |> ignore

app.Run()
