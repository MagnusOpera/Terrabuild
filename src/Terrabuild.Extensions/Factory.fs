module Terrabuild.Extensions.Factory

// well-know provided extensions
// do not forget to add reference when adding new implementation
let systemScripts =
    Map [
        "@docker", typeof<Terrabuild.Extensions.Docker>
        "@dotnet", typeof<Terrabuild.Extensions.Dotnet>
        "@make", typeof<Terrabuild.Extensions.Make>
        "@npm", typeof<Terrabuild.Extensions.Npm>
        "@null", typeof<Terrabuild.Extensions.Null>
        "@shell", typeof<Terrabuild.Extensions.Shell>
        "@terraform", typeof<Terrabuild.Extensions.Terraform>
    ]
