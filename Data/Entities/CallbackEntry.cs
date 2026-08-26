using AiHelpers.Data.Enums;

namespace AiHelpers.Data.Entities;

/// <summary>
/// An async job record. In V1 this was the only way to bridge a long-running LLM call
/// back to GovService, which cannot hold a connection open or stream. Retained for data
/// migration and for any GovService-facing endpoints kept during transition; native V2
/// UI can call the LLM directly and stream instead of polling this table.
/// </summary>
public class CallbackEntry
{
    public int Id { get; set; }

    public Guid CallbackGuid { get; set; }
    public string? Content { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public CallbackStatus Status { get; set; } = CallbackStatus.Initiated;

    /// <summary>Email of the user who initiated the call.</summary>
    public string? Initiator { get; set; }
    public string? StopReason { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }

    public int? HelperDefinitionId { get; set; }
    public HelperDefinition? HelperDefinition { get; set; }
    public string? HelperName { get; set; }
}
