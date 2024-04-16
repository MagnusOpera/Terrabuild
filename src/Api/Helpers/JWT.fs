module JWT
open System.Security.Claims
open System.IdentityModel.Tokens.Jwt
open System
open Microsoft.IdentityModel.Tokens
open System.Text

let createToken (organization: string) (email: string) (secret: string) (durationMn: int) (settings: Api.AuthSettings)=
    let claims = [ 
        Claim("organization", organization)
        Claim("email", email)
    ]

    let startsOn = DateTime.UtcNow
    let expiresOn = startsOn.AddMinutes(durationMn)

    let key = SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
    let creds = SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
    let jwtToken = JwtSecurityToken(
        issuer = settings.Issuer,
        claims = claims,
        notBefore = startsOn,
        expires = expiresOn,
        signingCredentials = creds
    )
    JwtSecurityTokenHandler().WriteToken(jwtToken)

let getToken (auth: string) =
    JwtSecurityTokenHandler().ReadJwtToken(auth)

let getOrganization (token: JwtSecurityToken) =
    token.Claims |> Seq.tryFind (fun x -> x.Type = "organization")
