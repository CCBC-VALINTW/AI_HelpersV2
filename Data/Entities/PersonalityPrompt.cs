namespace AiHelpers.Data.Entities;

/// <summary>A per-user prompt fragment describing how they'd like Helper output personalised.</summary>
public class PersonalityPrompt
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string Prompt { get; set; }
}
