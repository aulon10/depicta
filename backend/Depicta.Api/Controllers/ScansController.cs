using Depicta.Api.Contracts;
using Depicta.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Depicta.Api.Controllers;

/// <summary>Trigger and inspect media scans.</summary>
[ApiController]
[Route("api/scans")]
public class ScansController : ControllerBase
{
    private readonly ScanService _scans;
    public ScansController(ScanService scans) => _scans = scans;

    /// <summary>Start a scan: pulls media missing alt from WP, records items, wakes the worker.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ScanRunDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ScanRunDto>> Start(CancellationToken ct)
    {
        try
        {
            var run = await _scans.StartAsync(ct);
            return AcceptedAtAction(nameof(Get), new { id = run.Id }, run);
        }
        catch (SiteNotConnectedException e) { return BadRequest(e.Message); }
        catch (ScanFailedException e) { return StatusCode(StatusCodes.Status502BadGateway, e.Message); }
    }

    /// <summary>Recent scan runs (most recent first).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ScanRunDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ScanRunDto>>> List([FromQuery] int take = 20, CancellationToken ct = default)
        => Ok(await _scans.ListAsync(take, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ScanRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScanRunDto>> Get(Guid id, CancellationToken ct)
    {
        var run = await _scans.GetAsync(id, ct);
        return run is null ? NotFound() : Ok(run);
    }
}
