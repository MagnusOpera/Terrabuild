module Ansi

// see https://en.wikipedia.org/wiki/ANSI_escape_code

let ESC = "\u001b"
let CSI = ESC + "["

let cursorUp (x: int) = $"{CSI}{x}A"
let cursorDown (x: int) = $"{CSI}{x}B"
let cursorHome = $"{CSI}1G"
let csi (x: int) = $"{CSI}{x}m"
let clearScreen = $"{CSI}2J"
let clearScreen2 = $"{ESC}c"

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
    let trashcan = "🗑️"
    let bolt = "⚡️"
    let bug = "🪲"
    let warning = "⚠️"
    let poop = "💩"
    let skull = "☠️"
    let noentry = "⛔️"
    let cyclone = "🌀"
    let prohibited = "🚫"
    let gear = "⚙️"
    let clockwise = "↻"
    let bang = "!"
    let recycle = "♻︎"
    let log = "𝍌"
    let rightarrow = "▶"
    let brain = "🧠"
    let sun_cloud = "🌤️"
    let think = "🤔"
    let eyes = "👀"
    let pretzel = "🥨"
    let green_checkmark = "✅"
    let red_cross = "❌"
    let snowflake = "❄️"
    let question_mark = "❓"
    let bang_mark = "❗️"
    let coffee = "☕️"
    let construction = "🚧"
    let tombstone = "🪦"
    let clock = "🕒"
    let down = "↓"
    let up = "↑"

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

    let cursorHide = $"{CSI}?25l"
    let cursorShow = $"{CSI}?25h"
