using AiHelpers.Data.Enums;

namespace AiHelpers.Data.Entities;

/// <summary>
/// The reusable "what to fetch and how" half of what a Helper editor thinks of as a Data Source -
/// split out of HelperDataQuery (2026-09-07) so the same query can be attached to more than one
/// Helper without retyping it. HelperDataQuery is now just the thin per-Helper attachment (which
/// Helper, what SortOrder, what UsageInstruction for THAT Helper specifically) pointing at one of
/// these; everything about how the data is actually pulled - the SQL, the connection, the shape -
/// lives here exactly once, so editing it updates every Helper that uses it.
///
/// OwningHelperId (2026-09-07) is the real edit-in-place authority, not OwnerEmail - only the
/// Helper this was originally designed for (see its own doc comment) can modify this definition
/// directly; every other Helper attaching it (even one owned by the very same person) can only
/// fork it. This is deliberately per-Helper, not per-person: two Helpers owned by the same user
/// must not be able to silently corrupt each other's shared Data Source just because one person
/// happens to be editing both - see HelperEditor.razor's SaveAsync for the fork logic itself.
///
/// IsShared defaults to false - some Data Sources are genuinely Helper-specific, or query
/// restricted data that shouldn't be discoverable from an unrelated Helper's editor, so reuse is
/// opt-in, not automatic.
/// </summary>
public class DataSourceDefinition
{
    public int Id { get; set; }

    /// <summary>The Helper this Data Source was originally designed for - the only Helper whose
    /// editor can modify this definition's mechanics (or its IsShared flag) in place; any other
    /// Helper attaching it that changes anything gets its own fork instead (see
    /// HelperEditor.razor's SaveAsync). Null for a definition whose owning Helper has since been
    /// deleted - nobody can edit it in place any more (a safe default, not a bug), though it keeps
    /// working for whatever Helper(s) are already attached and stays manageable from Admin → Data
    /// Sources.</summary>
    public int? OwningHelperId { get; set; }
    public HelperDefinition? OwningHelper { get; set; }

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

    /// <summary>Whether DataResultCompactor's denormalised-join reshaping is applied before this
    /// result is sent to the model - defaults on since it's pure reshaping (no information loss),
    /// but left switchable per query in case a specific result genuinely needs the flat shape
    /// preserved (e.g. a downstream prompt instruction that assumes one row per line).</summary>
    public bool CompactionEnabled { get; set; } = true;

    /// <summary>Who authored this definition (or its most recent fork) - purely informational/audit
    /// now (shown in Admin → Data Sources and the "shared by" picker text), NOT what gates editing
    /// in place - see OwningHelperId for that. Falls back to a "system" sentinel (never a real,
    /// possibly-fabricated email) for a definition whose owning Helper itself had no OwnerEmail at
    /// migration time.</summary>
    public required string OwnerEmail { get; set; }

    /// <summary>When true, this Data Source appears in every Helper editor's "use an existing Data
    /// Source" picker, not just the Helper(s) it's already attached to. Off by default - see the
    /// class doc comment for why reuse is opt-in.</summary>
    public bool IsShared { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<HelperDataQuery> Attachments { get; set; } = [];
}
