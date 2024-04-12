open Api.Controllers

let token = { Token = "tagada" }
let input = Token token
printfn $"{FSharpJson.Serialize input}"

// let token = JWT.createToken "test" "test@example.com" "dwoqhdoqhdoqhdqhdoqhdockljszdkcbzcoiheoicjqwpikcj"
// printfn $"{token}"
