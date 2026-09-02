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

    /// <summary>The single source of truth both loaded into the editor and walked by
    /// DocumentExportService for the Word/PDF/HTML exports. Never has stylesheet CSS or a rendDoc
    /// wrapper baked into it - see StylesheetId.</summary>
    public required string HtmlContent { get; set; }

    /// <summary>The stylesheet applied when this document is rendered in the structured editor's
    /// preview - composed in at render time by structuredEditor.js, same "never mutate the stored
    /// content" principle as HelperDetail.razor's own OutputDocumentBuilder. Nullable/SetNull so
    /// the document survives if the stylesheet is later deleted, same as HelperDefinitionId.</summary>
    public int? StylesheetId { get; set; }
    public Stylesheet? Stylesheet { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastModifiedAtUtc { get; set; }
}
