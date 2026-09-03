namespace AiHelpers.Data.Entities;

/// <summary>
/// A short-lived (7-day) snapshot of a Helper run's raw output, captured automatically on every
/// successful call regardless of whether the user goes on to "Send to editor" - a safety net for
/// walking away from the screen, an accidental navigation, or anything else that loses the run
/// page's own in-memory state, so the output can be retrieved without re-running the LLM call.
/// Not a replacement for GeneratedDocument - that's a deliberate, permanent save; this is
/// automatic and temporary, purged well before most people would think to look for it if they
/// don't need it.
///
/// V1's AI_CallbackTable did something structurally similar (bridging GovService's inability to
/// hold a connection open across a long LLM call), but for a completely different reason - V2
/// doesn't need that async bridge at all (Blazor Server awaits directly). This is purely a
/// recovery mechanism, reusing the name/table for continuity rather than because the shape or
/// purpose carried over - see the git history this class replaced for the original polling-status
/// fields (Status, a Content-only payload) that no longer make sense here.
/// </summary>
public class CallbackEntry
{
    public int Id { get; set; }

    /// <summary>The retrieval page's URL uses this, not Id - unguessable-enough alongside the
    /// owner-email scoping already required to view one.</summary>
    public Guid CallbackGuid { get; set; } = Guid.NewGuid();

    public required string CreatedByEmail { get; set; }

    public int? HelperDefinitionId { get; set; }
    public HelperDefinition? HelperDefinition { get; set; }
    /// <summary>Snapshotted at creation time, kept even if the Helper is later renamed or
    /// deleted - same reasoning as AccountingEntry.HelperName.</summary>
    public string? HelperName { get; set; }

    public required string OutputHtml { get; set; }
    public string? SuggestedFileName { get; set; }

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    /// <summary>Bedrock's own Converse API stopReason (e.g. end_turn, max_tokens) - genuine
    /// model-response diagnostic info, not the V1 polling-protocol status this field name used to
    /// hold.</summary>
    public string? StopReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
