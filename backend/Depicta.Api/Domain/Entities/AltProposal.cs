namespace Depicta.Api.Domain.Entities;

/// <summary>
/// A Vision-generated alt text suggestion for a <see cref="MediaItem"/>. Kept as its own
/// row (rather than a field on MediaItem) so regeneration keeps a history and so we can
/// record which model/prompt produced each suggestion.
/// </summary>
public class AltProposal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MediaItemId { get; set; }
    public MediaItem MediaItem { get; set; } = null!;

    /// <summary>The AI-proposed alt text (already validated: <= 125 chars, no "image of").</summary>
    public required string ProposedAlt { get; set; }

    /// <summary>What the owner actually approved, if they edited before approving.</summary>
    public string? EditedAlt { get; set; }

    /// <summary>Provider key, e.g. "claude" or "openai".</summary>
    public required string Provider { get; set; }

    /// <summary>Concrete model id, e.g. "claude-vision-*" / "gpt-4o".</summary>
    public required string Model { get; set; }

    /// <summary>Version tag of the system prompt used, for reproducibility.</summary>
    public string? PromptVersion { get; set; }

    /// <summary>0..1 self-reported confidence from the model, if provided.</summary>
    public double? Confidence { get; set; }

    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The text that will actually be written: the owner's edit, else the proposal.</summary>
    public string EffectiveAlt => string.IsNullOrWhiteSpace(EditedAlt) ? ProposedAlt : EditedAlt;
}
