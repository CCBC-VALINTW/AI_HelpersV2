using AiHelpers.Data.Entities;
using AiHelpers.Data.Enums;

namespace AiHelpers.Providers;

public interface ILlmProviderAdapter
{
    LlmProvider Provider { get; }
    Task<LlmInvocationResult> InvokeAsync(LlmInvocationRequest request, CancellationToken cancellationToken = default);
}

public class LlmInvocationRequest
{
    public required HelperDefinition Helper { get; set; }
    public required LlmDefinition Model { get; set; }
    public required string UserInput { get; set; }
}

public class LlmInvocationResult
{
    public required string Text { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public string? StopReason { get; set; }
}
