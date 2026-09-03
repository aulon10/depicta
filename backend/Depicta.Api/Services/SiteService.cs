using Depicta.Api.Contracts;
using Depicta.Api.Domain.Entities;
using Depicta.Api.Infrastructure;
using Depicta.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace Depicta.Api.Services;

/// <summary>Manages the single connected WordPress site (v1). Encrypts credentials at rest.</summary>
public sealed class SiteService
{
    private readonly DepictaDbContext _db;
    private readonly ICredentialProtector _protector;

    public SiteService(DepictaDbContext db, ICredentialProtector protector)
    {
        _db = db;
        _protector = protector;
    }

    public async Task<Site?> GetAsync(CancellationToken ct) =>
        await _db.Sites.AsNoTracking().FirstOrDefaultAsync(ct);

    /// <summary>The tracked site entity for internal use (scan/apply). Throws if not connected.</summary>
    public async Task<Site> RequireAsync(CancellationToken ct) =>
        await _db.Sites.FirstOrDefaultAsync(ct)
        ?? throw new SiteNotConnectedException();

    public async Task<SiteDto> ConnectAsync(ConnectSiteRequest req, CancellationToken ct)
    {
        var site = await _db.Sites.FirstOrDefaultAsync(ct);
        if (site is null)
        {
            site = new Site
            {
                Url = req.Url.TrimEnd('/'),
                WpUsername = req.WpUsername,
                EncryptedAppPassword = _protector.Protect(req.AppPassword),
                EncryptedPluginSecret = _protector.Protect(req.PluginSecret),
            };
            _db.Sites.Add(site);
        }
        else
        {
            site.Url = req.Url.TrimEnd('/');
            site.WpUsername = req.WpUsername;
            site.EncryptedAppPassword = _protector.Protect(req.AppPassword);
            site.EncryptedPluginSecret = _protector.Protect(req.PluginSecret);
        }

        await _db.SaveChangesAsync(ct);
        return site.ToDto();
    }

    public async Task<bool> DisconnectAsync(CancellationToken ct)
    {
        var site = await _db.Sites.FirstOrDefaultAsync(ct);
        if (site is null) return false;
        _db.Sites.Remove(site); // cascades scans + media items
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class SiteNotConnectedException : Exception
{
    public SiteNotConnectedException() : base("No WordPress site is connected.") { }
}
