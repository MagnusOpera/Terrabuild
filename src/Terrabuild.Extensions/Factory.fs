module Terrabuild.Extensions.Factory

// well-know provided extensions
// do not forget to add reference when adding new implementation
let systemScripts =
    Map [
        "@cargo", (typeof<Cargo>, Some "rust")
        "@docker", (typeof<Docker>, None)
        "@dotnet", (typeof<Dotnet>, Some "mcr.microsoft.com/dotnet/sdk")
        "@gradle", (typeof<Gradle>, Some "gradle")
        "@make", (typeof<Make>, None)
        "@npm", (typeof<Npm>, Some "node")
        "@null", (typeof<Null>, None)
        "@shell", (typeof<Shell>, None)
        "@openapi", (typeof<OpenApi>, Some "openapitools/openapi-generator-cli")
        "@terraform", (typeof<Terraform>, Some "hashicorp/terraform") 
        "@yarn", (typeof<Yarn>, Some "nonde")
    ]
