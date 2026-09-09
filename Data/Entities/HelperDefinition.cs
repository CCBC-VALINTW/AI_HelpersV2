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

    /// <summary>Helper-specific questions asked of the user before running, in SortOrder.</summary>
    public ICollection<HelperContextQuestion> ContextQuestions { get; set; } = [];

    /// <summary>Optional framing text shown above the context questions on the run page, explaining
    /// why they're being asked - e.g. "This helps me build you a fully rounded report from the outset."</summary>
    public string? ContextQuestionsIntro { get; set; }

    /// <summary>When true, the run page hides the general input editor, attachment picker, and web
    /// URL field entirely - the whole call is built purely from ContextQuestions answers. Meant
    /// for a Helper with a tightly-scoped, predictable input shape (e.g. "always exactly these
    /// three fields"), where free-form input would only invite noise.</summary>
    public bool IsContextDriven { get; set; }

    /// <summary>This Helper's attached Data Sources (each a reference to a, possibly shared,
    /// DataSourceDefinition), run automatically on every call - see HelperDataQuery's own doc
    /// comment for why this isn't a context-question type.</summary>
    public ICollection<HelperDataQuery> DataQueries { get; set; } = [];

    public bool HasKnowledge { get; set; }
    public string? KnowledgeData { get; set; }
    public string? KnowledgeFileType { get; set; }
    public string? KnowledgePrompt { get; set; }

    /// <summary>When true (opt-in, off by default), the Knowledge document is NOT attached to a
    /// real run at all - KnowledgeDistilledText is folded into the system prompt as plain text
    /// instead, so a real run stops paying to resend/re-process the raw file's tokens every single
    /// time. Off by default so nothing changes for a Helper already relying on the model seeing the
    /// full original document until its owner deliberately opts in - see KnowledgeDistilledText's
    /// own doc comment for the real tradeoff this makes.</summary>
    public bool KnowledgeOptimizationEnabled { get; set; }

    /// <summary>A condensed distillation of KnowledgeData, produced by a one-off AI analysis pass
    /// (see HelperEditor.razor's "Analyze &amp; Distill" action) - never generated automatically,
    /// and never used unless KnowledgeOptimizationEnabled is also true. Deliberately lossy: this
    /// trades some fidelity to the original document for a large, ongoing token saving on every
    /// real run - the same shape of tradeoff as DataResultCompactor's compaction, just for
    /// unstructured reference documents instead of query results. Null until analysis has actually
    /// been run once.</summary>
    public string? KnowledgeDistilledText { get; set; }

    /// <summary>An optional example of the finished document this Helper should produce (docx,
    /// pdf, html, etc.) - held purely so its owner can ask the AI to derive an OutputFormat
    /// instruction from it (see HelperEditor.razor's "Suggest Output format from this example").
    /// Deliberately separate from KnowledgeData: a Helper can need a live reference document
    /// AND a one-off output example at the same time, and unlike Knowledge, this is NEVER
    /// attached to a real run - HelperInvocationService has no reason to ever read this field.</summary>
    public string? OutputTemplateData { get; set; }
    public string? OutputTemplateFileType { get; set; }

    /// <summary>A layout description derived from OutputTemplateData by a one-off AI analysis pass
    /// (see HelperEditor.razor's "Analyse layout of this example") - which sections appear, in what
    /// order, and what data belongs in each. Sent to the model as its own system-prompt entry
    /// alongside OutputFormat rather than being merged into it: OutputFormat stays purely
    /// owner-authored, and because each analysis is derived only from the example document (never
    /// from this field's own previous value), swapping the example and re-analysing replaces this
    /// cleanly instead of compounding successive templates' influence on top of each other. Null
    /// until analysis has actually been run, and cleared whenever the example document is replaced
    /// or removed, since a layout description of a document that's no longer there is worse than
    /// none.</summary>
    public string? OutputTemplateInstruction { get; set; }

    /// <summary>When true, this Helper proxies to an external URL rather than calling the LLM directly.</summary>
    public bool IsExternal { get; set; }
    public string? ExternalUrl { get; set; }

    /// <summary>
    /// A JSON Schema the model's structured output must conform to, written into
    /// HelperResponse.Data. Null means this Helper doesn't produce structured data - not to be
    /// confused with OutputFormat, which is a free-text prompt instruction, not a real schema.
    /// Ignored by the adapter when LlmDefinition.SupportsStructuredOutput is false.
    /// </summary>
    public string? OutputSchemaJson { get; set; }

    /// <summary>Set on every save from the Helper Editor (see HelperEditor.razor's SaveAsync) -
    /// V1 had no equivalent column, so a Helper migrated from it gets this set to the migration
    /// time instead (see Tools/DataMigration/Program.cs) rather than left blank.</summary>
    public DateTime LastModifiedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<AccountingEntry> AccountingEntries { get; set; } = [];
    public ICollection<CallbackEntry> CallbackEntries { get; set; } = [];
    public ICollection<Feedback> FeedbackEntries { get; set; } = [];
}
