module Compression
open System.IO.Compression
open System.IO
open System.Formats.Tar

let tar (outputFileName: string) (inputDirectoryName: string) =
    use output = File.Create(outputFileName)
    TarFile.CreateFromDirectory(inputDirectoryName, output, false)

let untar (outputDirectoryName: string) (inputFileName: string) =
    IO.createDirectory outputDirectoryName
    TarFile.ExtractToDirectory(inputFileName, outputDirectoryName, true)

let compress (outputFileName: string) (inputFileName: string) =
    use input = File.OpenRead(inputFileName)
    use output = File.Create(outputFileName)
    use compressor = new BrotliStream(output, CompressionLevel.SmallestSize)
    input.CopyTo(compressor);

let uncompress (outputFileName: string) (inputFileName: string) =
    use input = File.OpenRead(inputFileName)
    use output = File.Create(outputFileName)
    use decompressor = new BrotliStream(input, CompressionMode.Decompress)
    decompressor.CopyTo(output)
