module Terminal
open System

let flush () =
    Console.Out.Flush()

let write (str: string) =
    Console.Out.Write(str)

let writeLine (str: string) =
    Console.Out.WriteLine(str)

let hideCursor() =
    Ansi.Styles.cursorHide |> write |> flush

let showCursor() =
    Ansi.Styles.cursorShow |> write |> flush
