module JWT
open System.Security.Claims
open System.IdentityModel.Tokens.Jwt
open System
open Microsoft.IdentityModel.Tokens
open System.Text

let createToken (organization: string) (email: string) (secret: string) =
    let claims = [ 
        Claim("organization", organization)
        Claim("email", email)
    ]

    let now = DateTime.UtcNow

    let key = SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
    let creds = SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
    let jwtToken = JwtSecurityToken(
        claims = claims,
        notBefore = now,
        expires = now.AddMinutes(60),
        signingCredentials = creds
    )
    JwtSecurityTokenHandler().WriteToken(jwtToken)
 