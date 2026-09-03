using Depicta.Api.Contracts;
using Depicta.Api.Domain.Enums;
using Depicta.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Depicta.Api.Controllers;

/// <summary>
/// The review queue and the human-in-the-loop actions. Approve is the ONLY path that results
/// in a write to WordPress, and only for the items in the request body.
/// </summary>
[ApiController]
[Route("api/media")]
public class MediaController : ControllerBase
{
    private readonly MediaService _media;
    public MediaController(MediaService media) => _media = media;

    /// <summary>Paged review queue. Filter by state (default PendingReview).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<MediaItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MediaItemDto>>> List(
        [FromQuery] MediaItemState? state = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _media.ListAsync(state, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MediaItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MediaItemDto>> Get(Guid id, CancellationToken ct)
    {
        var item = await _media.GetAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Approve one or more items → write their (possibly edited) alt to WordPress.</summary>
    [HttpPost("approve")]
    [ProducesResponseType(typeof(BulkActionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkActionResult>> Approve([FromBody] ApproveRequest request, CancellationToken ct)
    {
        if (request.Items is null || request.Items.Count == 0) return BadRequest("No items.");
        try { return Ok(await _media.ApproveAsync(request.Items, ct)); }
        catch (SiteNotConnectedException e) { return BadRequest(e.Message); }
    }

    /// <summary>Reject one or more proposals (no write happens).</summary>
    [HttpPost("reject")]
    [ProducesResponseType(typeof(BulkActionResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<BulkActionResult>> Reject([FromBody] RejectRequest request, CancellationToken ct)
        => Ok(await _media.RejectAsync(request.MediaItemIds, ct));

    /// <summary>Re-queue items for a fresh Vision proposal.</summary>
    [HttpPost("regenerate")]
    [ProducesResponseType(typeof(BulkActionResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<BulkActionResult>> Regenerate([FromBody] RegenerateRequest request, CancellationToken ct)
        => Ok(await _media.RegenerateAsync(request.MediaItemIds, ct));

    /// <summary>1-click revert: rewrite the previous alt back to WordPress.</summary>
    [HttpPost("{id:guid}/revert")]
    [ProducesResponseType(typeof(MediaItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MediaItemDto>> Revert(Guid id, CancellationToken ct)
    {
        try { return Ok(await _media.RevertAsync(id, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (SiteNotConnectedException e) { return BadRequest(e.Message); }
        catch (InvalidOperationException e) { return Conflict(e.Message); }
    }
}
