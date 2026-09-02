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

    /// <summary>The provider's real, unconverted cost of this call - AWS Bedrock bills in USD, so
    /// this is always USD, regardless of what currency spend/caps are displayed in. Named to match
    /// V1's own column (DollarCost) rather than the ambiguous "Cost" this briefly became in V2 -
    /// GBP conversion happens only where this gets aggregated/displayed (SpendStatusService), not
    /// baked into the stored figure, so a future price update never needs pre-converting.</summary>
    public decimal UsdCost { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
