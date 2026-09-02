using AiHelpers.Data.Enums;

namespace AiHelpers.Data.Entities;

/// <summary>
/// A Helper-specific question asked of the user at run time, before the Helper runs - e.g. "Which
/// portfolio holder should be consulted?" for a committee report Helper. Answers are folded into
/// the run: Text answers become extra labelled context in the input, Document answers are sent as
/// additional attachments alongside anything the user uploads themselves.
/// </summary>
public class HelperContextQuestion
{
    public int Id { get; set; }

    public int HelperDefinitionId { get; set; }
    public HelperDefinition HelperDefinition { get; set; } = null!;

    public required string Label { get; set; }
    public ContextQuestionType Type { get; set; } = ContextQuestionType.Text;
    public bool IsMandatory { get; set; }

    /// <summary>Optional instruction telling the model how to use this specific answer, folded in
    /// alongside it at run time - e.g. "Use this to determine who signs off the report."</summary>
    public string? UsageInstruction { get; set; }

    /// <summary>Only meaningful when Type is Select - the picklist, as a JSON string array (e.g.
    /// ["Option A","Option B"]). A plain JSON column rather than a child table, matching this
    /// app's existing convention for small, always-loaded-with-the-parent lists (see
    /// PersonalityPrompt.AnswersJson) - nothing here ever needs to be queried independently of its
    /// question.</summary>
    public string? OptionsJson { get; set; }

    /// <summary>Display/collection order - questions are asked in this order.</summary>
    public int SortOrder { get; set; }
}
