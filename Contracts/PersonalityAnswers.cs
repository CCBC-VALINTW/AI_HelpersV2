namespace AiHelpers.Contracts;

/// <summary>
/// The raw questionnaire answers behind a generated personality profile - persisted as JSON on
/// PersonalityPrompt.AnswersJson purely so a returning user's sliders/radios can be restored,
/// separate from PersonalityPrompt.Prompt (the composed text actually sent to the model).
/// </summary>
public class PersonalityAnswers
{
    public string? TonePreference { get; set; }
    public int FormalityLevel { get; set; } = 3;
    public int AnalyticalCreative { get; set; } = 3;
    public int SeriousPlayful { get; set; } = 3;
    public int ConciseElaborate { get; set; } = 3;
    public int ConventionalUnconventional { get; set; } = 3;
    public string? VocabularyStyle { get; set; }
    public string? HumourUse { get; set; }
    public string? MetaphorUse { get; set; }
    public string? SentenceStructure { get; set; }
    public string? ContractionPreference { get; set; }
    public string? ResponseStructure { get; set; }
    public HashSet<string> AvoidCommon { get; set; } = [];
    public string AvoidCustom { get; set; } = "";
    public HashSet<string> EncourageCommon { get; set; } = [];
    public string EncourageCustom { get; set; } = "";
}
