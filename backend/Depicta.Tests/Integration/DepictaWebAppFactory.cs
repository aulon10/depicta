using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Depicta.Tests.Integration;

/// <summary>
/// Boots the real API in-memory for HTTP-level tests. Runs in Development (so the dev admin
/// credentials + signing-key fallback apply) but points the database at a throwaway SQLite file
/// and uses ephemeral Data Protection keys, so tests never touch the developer's real db.
/// </summary>
public sealed class DepictaWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbFile = $"depicta_test_{Guid.NewGuid():N}.db";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "sqlite",
                ["ConnectionStrings:Depicta"] = $"Data Source={_dbFile}",
                ["DataProtection:KeyPath"] = "",   // ephemeral keys — no files written
                ["Vision:Claude:ApiKey"] = "",     // worker stays idle; no external calls
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { if (File.Exists(_dbFile)) File.Delete(_dbFile); } catch { /* best effort */ }
    }
}
