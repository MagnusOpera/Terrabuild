namespace Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Api;
using Microsoft.AspNetCore.Authorization;
using Api.Helpers;

public record CredentialsInput {
    public required string Organization { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
}

public record TokenInput {
    public required string Token { get; init; }
}

public record LoginOutput {
    public required string AuthToken { get; init; }
    public required string RefreshToken { get; init; }
}

[ApiController, Route("auth")]
public class AuthController(ILogger<AuthController> logger, AppSettings appSettings) : ControllerBase {

    [HttpPost("login"), AllowAnonymous]
    public ActionResult<LoginOutput> Login(CredentialsInput input) {
        var token = JWT.CreateToken(input.Organization, input.Email, appSettings.Auth.Secret);
        var response = new LoginOutput { AuthToken = token, RefreshToken = "troulala" };
        return Ok(response);
    }
}
