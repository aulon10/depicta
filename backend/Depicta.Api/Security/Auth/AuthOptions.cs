namespace Depicta.Api.Security.Auth;

/// <summary>
/// Authentication configuration, bound from the "Auth" section. Secrets (the signing key and the
/// admin password hash) are supplied via environment variables / user-secrets in every real
/// environment — appsettings ships them empty. See README "Security".
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Symmetric HMAC-SHA256 signing key for the JWT. Must be at least 32 bytes of entropy.</summary>
    public string SigningKey { get; set; } = "";

    public string Issuer { get; set; } = "depicta";
    public string Audience { get; set; } = "depicta-app";

    /// <summary>Access-token lifetime in minutes.</summary>
    public int TokenMinutes { get; set; } = 120;

    public AdminAccount Admin { get; set; } = new();

    public sealed class AdminAccount
    {
        public string Username { get; set; } = "admin";

        /// <summary>PBKDF2 hash produced by <see cref="PasswordHasher"/> ("v1.iterations.salt.hash").</summary>
        public string PasswordHash { get; set; } = "";
    }
}
