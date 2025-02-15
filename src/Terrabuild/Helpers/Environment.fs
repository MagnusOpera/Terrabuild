module Environment

let envVar (varName: string) =
    varName |> System.Environment.GetEnvironmentVariable

let currentDir() = System.Environment.CurrentDirectory
