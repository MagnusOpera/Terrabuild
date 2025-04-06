module Environment

let IsLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)

let getEnvVar (name: string) =
    System.Environment.GetEnvironmentVariable(name) |> Option.ofObj
