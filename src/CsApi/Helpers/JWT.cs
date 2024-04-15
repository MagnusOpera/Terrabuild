
namespace Api.Helpers;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System;
using Microsoft.IdentityModel.Tokens;
using System.Text;

public static class JWT {

    public static string CreateToken(string organization, string email, string secret) {
        Claim[] claims = [new Claim("organization", organization), new Claim("email", email)];
        var now = DateTime.UtcNow;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);
        var jwtToken = new JwtSecurityToken(claims: claims,
                                            notBefore: now,
                                            expires: now.AddMinutes(60),
                                            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(jwtToken);
    }
}

