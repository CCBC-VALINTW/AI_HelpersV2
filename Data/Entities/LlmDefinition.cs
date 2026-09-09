using AiHelpers.Data.Enums;

namespace AiHelpers.Data.Entities;

public class LlmDefinition
{
    public int Id { get; set; }

    /// <summary>Which adapter calls this model - determines how Effort/Creativity/Adherence get translated.</summary>
    public LlmProvider Provider { get; set; } = LlmProvider.AwsBedrock;

    /// <summary>The provider's native model identifier, e.g. a Bedrock model ID.</summary>
    public required string Identifier { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }

    public int? MaxTokens { get; set; }
    /// <summary>Fallback Adherence (top_p) when a Helper doesn't specify its own. Not currently
    /// sent to Bedrock - see BedrockAdapter for why - kept for a possible future provider.</summary>
    public decimal DefaultAdherence { get; set; }
    /// <summary>Fallback Creativity (temperature) when a Helper doesn't specify its own. Not
    /// currently sent to Bedrock - see BedrockAdapter for why - kept for a possible future provider.</summary>
    public decimal DefaultCreativity { get; set; }

    public bool SupportsText { get; set; }
    public bool SupportsDocument { get; set; }
    public bool SupportsImage { get; set; }
    /// <summary>Whether this model can be forced to emit JSON conforming to a supplied schema
    /// (e.g. via Bedrock's tool-forcing), rather than relying on prompting alone.</summary>
    public bool SupportsStructuredOutput { get; set; }

    /// <summary>USD cost per 1,000,000 tokens - matches how providers (AWS Bedrock included)
    /// publish pricing today. Renamed from InputTokenCost/OutputTokenCost (which held a per-1,000
    /// rate) when providers moved to quoting per-million; existing values were multiplied by 1,000
    /// in the same migration that renamed these columns - see
    /// Migrations/*_RenameTokenCostsToPerMillion.</summary>
    public decimal? InputCostPerMillionTokens { get; set; }
    public decimal? OutputCostPerMillionTokens { get; set; }

    public bool SupportsReasoning { get; set; }
    public int? ReasoningTokens { get; set; }
    /// <summary>Anthropic's newer "adaptive thinking" (Claude Sonnet 5 confirmed so far) can't be
    /// disabled and isn't controlled by a token budget - effort is a separate dial instead. Only
    /// meaningful when SupportsReasoning is true; see BedrockAdapter for the two different request
    /// shapes this selects between. Defaults to false (the older enable/budget_tokens shape) so
    /// every existing reasoning-capable row keeps its current behaviour unless explicitly flipped.</summary>
    public bool UsesAdaptiveThinking { get; set; }

    public ModelResidency Residency { get; set; } = ModelResidency.Global;

    public ICollection<HelperDefinition> Helpers { get; set; } = [];
}
