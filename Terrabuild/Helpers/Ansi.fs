module Ansi

// see https://en.wikipedia.org/wiki/ANSI_escape_code

let ESC = "\u001b"
let CSI = ESC + "["

let cursorUp (x: int) = $"{CSI}{x}A"
let cursorDown (x: int) = $"{CSI}{x}B"
let cursorHome = "\r"
let color (x: int) = $"{CSI}{x}m"

module Emojis =
    let crossmark = "✘"
    let checkmark = "✔"
    let party = "🎉"
    let rocket = "🚀"

module Styles =
    let black = color 30
    let red = color 31
    let green = color 32
    let yellow = color 33
    let blue = color 34
    let magenta = color 35
    let cyan = color 36
    let white = color 37

    let normal = $"{CSI}0m"
