namespace AiHelpers.Data.Entities;

/// <summary>
/// A document sent from a Helper run into the standalone "final polish" document editor
/// (/documents/{Id}) and persisted as its own record - HelperDetail.razor's own in-place
/// refine loop never persists its _output itself, this is what makes a chosen result durable,
/// downloadable, and re-editable afterwards.
/// </summary>
public class GeneratedDocument
{
    public int Id { get; set; }

    /// <summary>The Helper run this document originated from. Nullable so the record survives if
    /// that Helper is later deleted - same SetNull reasoning as AccountingEntry/CallbackEntry/
    /// Feedback (preserve the historical record rather than cascade it away).</summary>
    public int? HelperDefinitionId { get; set; }
    public HelperDefinition? HelperDefinition { get; set; }

    public required string CreatedByEmail { get; set; }

    public required string Title { get; set; }

    /// <summary>Sanitized HTML (HtmlContentSanitizer) - the single source of truth both loaded into
    /// the editor and walked by DocumentExportService for the Word/PDF/HTML exports. Re-sanitized
    /// on every save, same as HelperDetail's output editor's ApplyEdits.</summary>
    public required string HtmlContent { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastModifiedAtUtc { get; set; }
}
