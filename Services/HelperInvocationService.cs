using System.Text.RegularExpressions;
using AiHelpers.Contracts;
using AiHelpers.Data;
using AiHelpers.Data.Entities;
using AiHelpers.Data.Enums;
using AiHelpers.Providers;
using Microsoft.EntityFrameworkCore;

namespace AiHelpers.Services;

/// <summary>
/// Ports V1's WSO2 proxy business logic (spend check -> call model -> log cost) as plain C#
/// against the app's own database, instead of SQL-mediated async polling - see
/// project_ai_helpers_v1_architecture memory for the original flow this replaces.
/// </summary>
public class HelperInvocationService(AppDbContext db, IEnumerable<ILlmProviderAdapter> adapters) : IHelperInvocationService
{
    public async Task<HelperInvocationOutcome> RunAsync(HelperDefinition helper, string userInput, string userEmail, CancellationToken cancellationToken = default)
    {
        if (helper.IsExternal)
        {
            return new HelperInvocationOutcome { ErrorMessage = "External helpers aren't supported yet." };
        }

        if (helper.LlmDefinition is null)
        {
            return new HelperInvocationOutcome { ErrorMessage = "This Helper has no model configured." };
        }

        var (spend, cap) = await GetSpendAndCapAsync(userEmail, cancellationToken);
        if (spend >= cap)
        {
            return new HelperInvocationOutcome { BudgetExceeded = true, Spend = spend, Cap = cap };
        }

        var adapter = adapters.FirstOrDefault(a => a.Provider == helper.LlmDefinition.Provider);
        if (adapter is null)
        {
            return new HelperInvocationOutcome { ErrorMessage = $"No adapter available yet for {helper.LlmDefinition.Provider}." };
        }

        LlmInvocationResult result;
        try
        {
            result = await adapter.InvokeAsync(new LlmInvocationRequest
            {
                Helper = helper,
                Model = helper.LlmDefinition,
                UserInput = userInput
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            return new HelperInvocationOutcome { ErrorMessage = ex.Message, Spend = spend, Cap = cap };
        }

        // V1's AI_LogAccountingCost stored procedure computed this as integer division
        // (@InputTokens/1000) before multiplying by cost, silently truncating to zero cost for
        // any call under 1000 tokens. Doing the division in decimal here instead.
        var cost = (result.InputTokens / 1000m) * (helper.LlmDefinition.InputTokenCost ?? 0m)
            + (result.OutputTokens / 1000m) * (helper.LlmDefinition.OutputTokenCost ?? 0m);

        db.AccountingEntries.Add(new AccountingEntry
        {
            UserId = userEmail,
            HelperDefinitionId = helper.Id,
            HelperName = helper.Name,
            Cost = cost,
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        var response = new HelperResponse
        {
            SuggestedFileName = helper.Name,
            Documents = [new Document { Type = DocumentType.Html, Name = helper.Name, Content = ExtractContent(result.Text) }]
        };

        return new HelperInvocationOutcome { Response = response, Spend = spend, Cap = cap };
    }

    /// <summary>
    /// Defensive backstop for when a model wraps its response in a markdown code fence plus
    /// chat-style commentary (an intro line, a follow-up question) despite being told not to -
    /// prompt-following alone isn't fully reliable. If a fenced block is present anywhere in the
    /// response, keeps only its contents and discards the surrounding commentary; otherwise
    /// returns the response as-is.
    /// </summary>
    private static string ExtractContent(string text)
    {
        var match = Regex.Match(text, "```[a-zA-Z]*\\s*\\n(.*?)\\n```", RegexOptions.Singleline);
        return (match.Success ? match.Groups[1].Value : text).Trim();
    }

    private async Task<(decimal Spend, decimal Cap)> GetSpendAndCapAsync(string userEmail, CancellationToken cancellationToken)
    {
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var rawSpend = await db.AccountingEntries
            .Where(a => a.UserId == userEmail && a.Timestamp >= monthStart)
            .SumAsync(a => (decimal?)a.Cost, cancellationToken) ?? 0m;

        var cap = await db.SpendCaps
            .Where(s => s.UserId == userEmail)
            .Select(s => (decimal?)s.MonthlyCapAmount)
            .FirstOrDefaultAsync(cancellationToken) ?? 1.0m;

        // Matches V1: only 80% of actual spend counts against the cap.
        return (rawSpend * 0.8m, cap);
    }
}
