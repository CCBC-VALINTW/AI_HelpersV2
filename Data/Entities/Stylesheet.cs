namespace AiHelpers.Data.Entities;

public class Stylesheet
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Css { get; set; }
    public string? StyleInstructions { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<HelperDefinition> Helpers { get; set; } = [];
}
