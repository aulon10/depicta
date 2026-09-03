using Depicta.Api.Contracts;
using Depicta.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Depicta.Api.Controllers;

/// <summary>Connect / inspect the single WordPress site (v1).</summary>
[ApiController]
[Route("api/site")]
public class SitesController : ControllerBase
{
    private readonly SiteService _sites;
    public SitesController(SiteService sites) => _sites = sites;

    /// <summary>Connect the site. Encrypts and stores the app password + plugin secret.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(SiteDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SiteDto>> Connect([FromBody] ConnectSiteRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Url) || string.IsNullOrWhiteSpace(request.AppPassword))
            return BadRequest("Url and AppPassword are required.");
        return Ok(await _sites.ConnectAsync(request, ct));
    }

    /// <summary>Return the connected site (never the secrets), or 404 if not connected yet.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(SiteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SiteDto>> Get(CancellationToken ct)
    {
        var site = await _sites.GetAsync(ct);
        return site is null ? NotFound() : Ok(site.ToDto());
    }

    /// <summary>Disconnect and wipe stored credentials.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disconnect(CancellationToken ct) =>
        await _sites.DisconnectAsync(ct) ? NoContent() : NotFound();
}
