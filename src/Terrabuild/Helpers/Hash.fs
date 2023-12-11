module Hash
open System.IO
open System.Security.Cryptography
open System

let computeFilesSha files =
    use ms = new MemoryStream()

    let computeFileSha file =
        let sha256 = SHA256.Create()
        use hFile = File.Open(file, FileMode.Open)
        sha256.ComputeHash hFile |> ms.Write

    files
    |> Seq.iter computeFileSha

    ms.Position <- 0
    let sha256 = SHA256.Create()
    let hash = ms |> sha256.ComputeHash |> Convert.ToHexString
    hash
