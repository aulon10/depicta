using Depicta.Api.Contracts;
using Depicta.Api.Domain.Entities;

namespace Depicta.Api.Services;

/// <summary>Entity → DTO projections. Secrets are never mapped out.</summary>
public static class Mapping
{
    public static SiteDto ToDto(this Site s) => new(s.Id, s.Url, s.WpUsername, s.LastScannedAt);

    public static ScanRunDto ToDto(this ScanRun r) =>
        new(r.Id, r.Status, r.Discovered, r.AlreadyHadAlt, r.StartedAt, r.CompletedAt, r.Error);

    public static ProposalDto ToDto(this AltProposal p) =>
        new(p.Id, p.ProposedAlt, p.EditedAlt, p.Provider, p.Model, p.Confidence, p.CreatedAt);

    public static MediaItemDto ToDto(this MediaItem m)
    {
        var active = m.ActiveProposalId is null
            ? null
            : m.Proposals.FirstOrDefault(p => p.Id == m.ActiveProposalId);

        return new MediaItemDto(
            m.Id, m.WpAttachmentId, m.Url, m.Filename, m.Width, m.Height,
            m.CurrentAlt, m.State, active?.ToDto(), m.LastError, m.UpdatedAt);
    }
}
