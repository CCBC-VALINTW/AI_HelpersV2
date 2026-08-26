using AiHelpers.Data.Enums;

namespace AiHelpers.Data.Entities;

public class LlmDefinition
{
    public int Id { get; set; }

    /// <summary>Provider/model identifier, e.g. Bedrock model ID.</summary>
    public required string Identifier { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }

    public int? MaxTokens { get; set; }
    public decimal DefaultTopP { get; set; }
    public decimal DefaultTemperature { get; set; }

    public bool SupportsText { get; set; }
    public bool SupportsDocument { get; set; }
    public bool SupportsImage { get; set; }

    public decimal? InputTokenCost { get; set; }
    public decimal? OutputTokenCost { get; set; }

    public bool SupportsReasoning { get; set; }
    public int? ReasoningTokens { get; set; }

    public ModelResidency Residency { get; set; } = ModelResidency.Global;

    public ICollection<HelperDefinition> Helpers { get; set; } = [];
}
