using AiHelpers.Data.Enums;

namespace AiHelpers.Data.Entities;

/// <summary>
/// A Helper-specific, admin/owner-authored query against a DataConnection - executed
/// automatically on every run, same "silently included, no user interaction needed" shape as
/// HelperDefinition.HasKnowledge, not a user-facing context question. Deliberately not
/// parameterised from context-question answers (a real future extension, not this pass) - that's
/// exactly where untrusted input would start touching a query, and it deserves its own careful
/// design rather than being folded in implicitly here.
/// </summary>
public class HelperDataQuery
{
    public int Id { get; set; }

    public int HelperDefinitionId { get; set; }
    public HelperDefinition HelperDefinition { get; set; } = null!;

    public int DataConnectionId { get; set; }
    public DataConnection DataConnection { get; set; } = null!;

    /// <summary>Shown to the model as a heading above the query's result - e.g. "Current pothole
    /// reports".</summary>
    public required string Label { get; set; }

    public required string Query { get; set; }

    public DataQueryOutputFormat OutputFormat { get; set; } = DataQueryOutputFormat.Csv;

    /// <summary>Hard cap on rows read back - protects against an unbounded result set blowing out
    /// the request (and the model's context) the same way MaxDocumentBytes/MaxAttachments already
    /// guard uploads. Truncated results say so explicitly in the folded-in text, never silently.</summary>
    public int MaxRows { get; set; } = 500;

    /// <summary>Optional instruction telling the model how to use this specific result, same
    /// pattern as HelperContextQuestion.UsageInstruction.</summary>
    public string? UsageInstruction { get; set; }

    public int SortOrder { get; set; }
}
