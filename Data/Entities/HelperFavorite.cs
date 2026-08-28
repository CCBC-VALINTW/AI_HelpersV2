namespace AiHelpers.Data.Entities;

public class HelperFavorite
{
    public int Id { get; set; }
    public required string UserEmail { get; set; }
    public int HelperDefinitionId { get; set; }
    public HelperDefinition HelperDefinition { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
