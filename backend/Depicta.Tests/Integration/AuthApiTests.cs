using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Depicta.Tests.Integration;

/// <summary>
/// End-to-end HTTP checks of the auth boundary added in Phase 1: the API is closed by default,
/// login issues a token, and the token opens the protected endpoints.
/// </summary>
public class AuthApiTests : IClassFixture<DepictaWebAppFactory>
{
    private readonly DepictaWebAppFactory _factory;
    public AuthApiTests(DepictaWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Protected_endpoint_returns_401_without_a_token()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/media");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_a_token()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "depicta-dev-2026" });

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginDto>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("admin", body.Username);
    }

    [Fact]
    public async Task Login_with_bad_credentials_is_unauthorized()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task A_valid_token_unlocks_the_protected_endpoint()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "depicta-dev-2026" });
        var body = await login.Content.ReadFromJsonAsync<LoginDto>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.Token);

        var res = await client.GetAsync("/api/media");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    private sealed record LoginDto(string Token, DateTimeOffset ExpiresAt, string Username);
}
