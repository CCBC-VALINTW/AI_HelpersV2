namespace AiHelpers.Data.Entities;

public class Feedback
{
    public int Id { get; set; }

    public required string Email { get; set; }
    public byte Overall { get; set; }
    public bool DidSaveTime { get; set; }
    public int? HoursSaved { get; set; }
    public byte? MinutesSaved { get; set; }
    public string? Successes { get; set; }
    public string? Failures { get; set; }
    public string? Improvements { get; set; }
    public string? OtherComments { get; set; }

    public int? HelperDefinitionId { get; set; }
    public HelperDefinition? HelperDefinition { get; set; }
}
