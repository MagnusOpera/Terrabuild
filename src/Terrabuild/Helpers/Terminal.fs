module Terminal
open System
open System.Linq
open NeoSmart.Unicode

let private terms = [
    "^xterm" // xterm, PuTTY, Mintty
    "^rxvt" // RXVT
    "^eterm" // Eterm
    "^screen" // GNU screen, tmux
    "tmux" // tmux
    "^vt100" // DEC VT series
    "^vt102" // DEC VT series
    "^vt220" // DEC VT series
    "^vt320" // DEC VT series
    "ansi" // ANSI
    "scoansi" // SCO ANSI
    "cygwin" // Cygwin, MinGW
    "linux" // Linux console
    "konsole" // Konsole
    "bvterm" // Bitvise SSH Client
    "^st-256color" // Suckless Simple Terminal, st
    "alacritty" // Alacritty
]

let supportAnsi =
    match System.Environment.GetEnvironmentVariable("TERM") |> Option.ofObj with
    | Some currTerm ->
        terms |> List.exists (fun term -> 
            match currTerm with
            | String.Regex term _ -> true
            | _ -> false)
    | _ -> false


let flush () =
    Console.Out.Flush()

let center (content: string) =
    let size = 2
    let s = content.AsUnicodeSequence().AsString.Count()

    // ssssXXssss
    let lpadding = (max (size - s) 0) / 2
    let rpadding = max (size - lpadding - s) 0
    String(' ', lpadding) + content + String(' ', rpadding)

let write (str: string) =
    Console.Out.Write(str)

let writeLine (str: string) =
    Console.Out.WriteLine(str)

let hideCursor() =
    if supportAnsi then
        Ansi.Styles.cursorHide |> write |> flush

let showCursor() =
    if supportAnsi then
        Ansi.Styles.cursorShow |> write |> flush
