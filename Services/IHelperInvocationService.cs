using AiHelpers.Contracts;
using AiHelpers.Data.Entities;
using AiHelpers.Providers;

namespace AiHelpers.Services;

public interface IHelperInvocationService
{
    Task<HelperInvocationOutcome> RunAsync(HelperDefinition helper, string userInput, string userEmail, IReadOnlyList<Attachment>? attachments = null, CancellationToken cancellationToken = default);
}

public class HelperInvocationOutcome
{
    public bool BudgetExceeded { get; set; }
    public decimal Spend { get; set; }
    public decimal Cap { get; set; }
    public HelperResponse? Response { get; set; }
    public string? ErrorMessage { get; set; }
}
