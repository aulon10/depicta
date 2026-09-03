using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Depicta.Api.Infrastructure;

/// <summary>
/// Design-time factory so the EF tools (`dotnet ef migrations add`, `database update`) can build
/// the context without booting the whole app. Migrations target <b>PostgreSQL</b> — the production
/// database. Local dev runs on SQLite via <c>EnsureCreated()</c> and needs no migrations.
///
/// Generating a migration doesn't need a live database. For `database update`, point
/// <c>DEPICTA_DESIGN_CONNECTION</c> at a real PostgreSQL instance (otherwise the placeholder below
/// is used).
/// </summary>
public sealed class DepictaDbContextDesignFactory : IDesignTimeDbContextFactory<DepictaDbContext>
{
    public DepictaDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("DEPICTA_DESIGN_CONNECTION")
                   ?? "Host=localhost;Database=depicta;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<DepictaDbContext>()
            .UseNpgsql(conn)
            .Options;

        return new DepictaDbContext(options);
    }
}
