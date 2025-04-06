module Environment

let IsLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux)

let envVar (varName: string) =
    varName |> System.Environment.GetEnvironmentVariable |> Option.ofObj

let currentDir() = System.Environment.CurrentDirectory
