namespace AiHelpers.Data.Entities;

/// <summary>
/// Attaches one DataSourceDefinition to one Helper - the thin per-Helper join row left behind once
/// the actual query/connection/format/etc. were split out into DataSourceDefinition (2026-09-07,
/// to let a Data Source be reused across Helpers - see that class's own doc comment). Everything
/// here is genuinely per-attachment, not part of the shared definition: which Helper, where in its
/// list (SortOrder), and how THIS Helper specifically should use the result (UsageInstruction) -
/// two Helpers attaching the same shared Data Source can reasonably want different guidance text
/// for how to use the same underlying data.
///
/// Run automatically on every call, no user interaction needed - see DataSourceDefinition's own
/// doc comment for why this isn't a context-question type.
/// </summary>
public class HelperDataQuery
{
    public int Id { get; set; }

    public int HelperDefinitionId { get; set; }
    public HelperDefinition HelperDefinition { get; set; } = null!;

    public int DataSourceDefinitionId { get; set; }
    public DataSourceDefinition DataSourceDefinition { get; set; } = null!;

    /// <summary>Optional instruction telling the model how THIS Helper should use this specific
    /// result, same pattern as HelperContextQuestion.UsageInstruction. Deliberately not part of
    /// DataSourceDefinition - a shared Data Source can reasonably need different framing for each
    /// Helper that attaches it.</summary>
    public string? UsageInstruction { get; set; }

    public int SortOrder { get; set; }
}
