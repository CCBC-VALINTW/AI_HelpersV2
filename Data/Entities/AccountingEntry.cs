namespace AiHelpers.Data.Entities;

/// <summary>Records the cost of a single Helper invocation, for per-user/per-month reporting.</summary>
public class AccountingEntry
{
    public int Id { get; set; }

    public required string UserId { get; set; }

    public int? HelperDefinitionId { get; set; }
    public HelperDefinition? HelperDefinition { get; set; }

    /// <summary>Snapshot of the Helper name at call time, kept even if the Helper is later renamed or deleted.</summary>
    public string? HelperName { get; set; }

    public decimal Cost { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
