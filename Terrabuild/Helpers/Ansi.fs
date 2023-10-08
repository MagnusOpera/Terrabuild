module Ansi

// see https://en.wikipedia.org/wiki/ANSI_escape_code

let ESC = "\u001b"
let CSI = ESC + "["

let cursorUp (x: int) = $"{CSI}{x}A"
let cursorDown (x: int) = $"{CSI}{x}B"
let cursorHome = "\r"
let csi (x: int) = $"{CSI}{x}m"

module Emojis =
    let crossmark = "✘"
    let checkmark = "✔"
    let party = "🎉"
    let rocket = "🚀"
    let bomb = "💣"
    let explosion = "💥"
    let thumbUp = "👍"
    let thumbDown = "👎"
    let happy = "😃"
    let sad = "🙁"
    let box = "📦"
    let popcorn = "🍿"

module Styles =
    let reset = csi 0
    let bold = csi 1
    let slowBlink = csi 5
    let invert = csi 7

    let black = csi 30
    let red = csi 31
    let green = csi 32
    let yellow = csi 33
    let blue = csi 34
    let magenta = csi 35
    let cyan = csi 36
    let white = csi 37


