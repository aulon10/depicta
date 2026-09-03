using Depicta.Api.Contracts;
using Depicta.Api.Security.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Depicta.Api.Controllers;

/// <summary>Admin authentication. The only anonymous surface of the API.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthOptions _opts;
    private readonly TokenService _tokens;
    private readonly ILogger<AuthController> _log;

    public AuthController(AuthOptions opts, TokenService tokens, ILogger<AuthController> log)
    {
        _opts = opts;
        _tokens = tokens;
        _log = log;
    }

    /// <summary>Exchange admin username + password for a short-lived bearer token.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        // Verify the username and password independently but always run the (costly) hash check so an
        // attacker can't distinguish "unknown user" from "wrong password" by response timing.
        var userMatch = CryptoEquals(request.Username, _opts.Admin.Username);
        var passwordOk = PasswordHasher.Verify(request.Password ?? "", _opts.Admin.PasswordHash);

        if (!userMatch || !passwordOk)
        {
            _log.LogWarning("Failed admin login attempt for user {User} from {Ip}",
                request.Username, HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { error = "Invalid username or password." });
        }

        var (token, expiresAt) = _tokens.Issue(_opts.Admin.Username);
        return Ok(new LoginResponse(token, expiresAt, _opts.Admin.Username));
    }

    /// <summary>Returns the authenticated admin — a cheap way for the client to validate its token.</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me() => Ok(new { username = User.Identity?.Name });

    private static bool CryptoEquals(string? a, string? b)
    {
        var x = System.Text.Encoding.UTF8.GetBytes(a ?? "");
        var y = System.Text.Encoding.UTF8.GetBytes(b ?? "");
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Security.Cryptography.SHA256.HashData(x),
            System.Security.Cryptography.SHA256.HashData(y));
    }
}
