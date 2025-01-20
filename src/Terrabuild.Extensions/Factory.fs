module Terrabuild.Extensions.Factory

// well-know provided extensions
// do not forget to add reference when adding new implementation
let systemScripts =
    Map [
        "@cargo", typeof<Cargo>
        "@docker", typeof<Docker>
        "@dotnet", typeof<Dotnet>
        "@gradle", typeof<Gradle>
        "@make", typeof<Make>
        "@npm", typeof<Npm>
        "@null", typeof<Null>
        "@shell", typeof<Shell>
        "@openapi", typeof<OpenApi>
        "@terraform", typeof<Terraform>
        "@yarn", typeof<Yarn>
    ]
