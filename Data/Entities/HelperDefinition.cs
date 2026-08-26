using AiHelpers.Data.Enums;

namespace AiHelpers.Data.Entities;

/// <summary>
/// A configured AI "Helper" - a named LLM prompt/config combination a user or the
/// organisation can run. Equivalent to V1's AI_HelperDefinitions table.
/// </summary>
public class HelperDefinition
{
    public int Id { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>Null only when IsExternal is true - external helpers proxy to a URL instead of calling an LLM.</summary>
    public int? LlmDefinitionId { get; set; }
    public LlmDefinition? LlmDefinition { get; set; }

    /// <summary>Generic reasoning-effort dial. Null means reasoning is off for this Helper; the
    /// provider adapter ignores it entirely when LlmDefinition.SupportsReasoning is false.</summary>
    public EffortLevel? Effort { get; set; }

    /// <summary>Maps to temperature. Ignored by the adapter when reasoning is engaged for a model
    /// where reasoning mode overrides sampling controls.</summary>
    public decimal? Creativity { get; set; }
    /// <summary>Maps to top_p. Same reasoning-mode override rule as Creativity.</summary>
    public decimal? Adherence { get; set; }

    /// <summary>How far the user is permitted to adjust Creativity from the default, if at all.</summary>
    public decimal? CreativityAdjustmentAllowance { get; set; }
    /// <summary>How far the user is permitted to adjust Adherence from the default, if at all.</summary>
    public decimal? AdherenceAdjustmentAllowance { get; set; }

    public string? PrimaryPurpose { get; set; }
    public string? Methodology { get; set; }
    public string? StyleTone { get; set; }
    public string? OutputFormat { get; set; }
    public string? TargetAudience { get; set; }
    public string? SpecialInstructions { get; set; }

    /// <summary>Email of the user who owns this Helper, when Scope is Personal.</summary>
    public string? OwnerEmail { get; set; }
    public HelperScope Scope { get; set; } = HelperScope.Personal;

    public int? HelperCategoryId { get; set; }
    public HelperCategory? HelperCategory { get; set; }

    public int? DefaultStylesheetId { get; set; }
    public Stylesheet? DefaultStylesheet { get; set; }

    public bool AllowContext { get; set; }
    public string? ContextPrompt { get; set; }

    public bool HasKnowledge { get; set; }
    public string? KnowledgeData { get; set; }
    public string? KnowledgeFileType { get; set; }
    public string? KnowledgePrompt { get; set; }

    /// <summary>When true, this Helper proxies to an external URL rather than calling the LLM directly.</summary>
    public bool IsExternal { get; set; }
    public string? ExternalUrl { get; set; }

    public ICollection<AccountingEntry> AccountingEntries { get; set; } = [];
    public ICollection<CallbackEntry> CallbackEntries { get; set; } = [];
    public ICollection<Feedback> FeedbackEntries { get; set; } = [];
}
