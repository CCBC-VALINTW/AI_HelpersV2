namespace AiHelpers.Data.Entities;

/// <summary>
/// A per-user, per-named prompt fragment describing how they'd like Helper output personalised -
/// a user can save several (e.g. "Formal reports", "Casual emails") and pick one per run, rather
/// than being limited to a single always-on personality.
/// </summary>
public class PersonalityPrompt
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string Name { get; set; }
    public required string Prompt { get; set; }

    /// <summary>At most one default per user (enforced by a filtered unique index) - pre-selected on the Helper run page.</summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// JSON-serialised AiHelpers.Contracts.PersonalityAnswers - the raw questionnaire answers
    /// behind Prompt, kept only so the Personality Profile page can restore a returning user's
    /// selections. Null for any row that predates the questionnaire page (Prompt is still shown/
    /// used as-is in that case, just with the form starting from defaults on revisit).
    /// </summary>
    public string? AnswersJson { get; set; }
}
