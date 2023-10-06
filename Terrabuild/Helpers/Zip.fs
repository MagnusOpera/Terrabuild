module Zip
open System.IO.Compression
open System.IO

let rec addAny (archive: ZipArchive) entryName sourceName = 
    let fileName = IO.getFilename sourceName
    if File.GetAttributes(sourceName).HasFlag(FileAttributes.Directory) then
        addDirectory archive (IO.combinePath entryName fileName) sourceName
    else
        archive.CreateEntryFromFile(sourceName, Path.Combine(entryName, fileName), CompressionLevel.SmallestSize) |> ignore

and addDirectory (archive: ZipArchive) entryName sourceDirName =
    let files = Directory.EnumerateFiles(sourceDirName, "*", SearchOption.AllDirectories)
    files |> Seq.iter (fun file -> addAny archive entryName file)

let createArchive (baseDirectory: string) (entries: string seq) =
    let tmpFile = IO.getTempFilename()
    use zipFile = new FileStream(tmpFile, FileMode.Create)
    use archive = new ZipArchive(zipFile, ZipArchiveMode.Create)
    for entry in entries do
        let relative = Path.GetRelativePath(baseDirectory, entry)
        archive.CreateEntryFromFile(entry, relative, CompressionLevel.SmallestSize) |> ignore
    tmpFile

let restoreArchive (filename: string) (projectDirectory: string) =
    ZipFile.ExtractToDirectory(filename, projectDirectory)
