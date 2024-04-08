module Terrabuild.Extensions.Factory

// well-know provided extensions
// do not forget to add reference when adding new implementation
let systemScripts =
    Map [
        "@docker", typeof<Docker>
        "@dotnet", typeof<Dotnet>
        "@make", typeof<Make>
        "@npm", typeof<Npm>
        "@null", typeof<Null>
        "@shell", typeof<Shell>
        "@terraform", typeof<Terraform>
    ]
