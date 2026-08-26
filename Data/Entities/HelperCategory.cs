namespace AiHelpers.Data.Entities;

public class HelperCategory
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    public ICollection<HelperDefinition> Helpers { get; set; } = [];
}
