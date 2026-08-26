namespace AiHelpers.Data.Entities;

public class Stylesheet
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Css { get; set; }
    public string? StyleInstructions { get; set; }

    public ICollection<HelperDefinition> Helpers { get; set; } = [];
}
