using System.Text.RegularExpressions;
using AiHelpers.Contracts;
using AiHelpers.Data;
using AiHelpers.Data.Entities;
using AiHelpers.Data.Enums;
using AiHelpers.Providers;

namespace AiHelpers.Services;

/// <summary>
/// Ports V1's WSO2 proxy business logic (spend check -> call model -> log cost) as plain C#
/// against the app's own database, instead of SQL-mediated async polling - see
/// project_ai_helpers_v1_architecture memory for the original flow this replaces.
/// </summary>
public class HelperInvocationService(AppDbContext db, IEnumerable<ILlmProviderAdapter> adapters, ISpendStatusService spendStatus) : IHelperInvocationService
{
    public async Task<HelperInvocationOutcome> RunAsync(HelperDefinition helper, string userInput, string userEmail, IReadOnlyList<Attachment>? attachments = null, CancellationToken cancellationToken = default)
    {
        if (helper.IsExternal)
        {
            return new HelperInvocationOutcome { ErrorMessage = "External helpers aren't supported yet." };
        }

        if (helper.LlmDefinition is null)
        {
            return new HelperInvocationOutcome { ErrorMessage = "This Helper has no model configured." };
        }

        var (spend, cap) = await spendStatus.RefreshAsync(userEmail, cancellationToken);
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
                UserInput = userInput,
                Attachments = BuildAttachments(helper, attachments)
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

        // Recompute rather than reuse the pre-call (spend, cap) - this run's own cost has just
        // been logged, so the pre-call figures would under-report by exactly this call's cost.
        // Also pushes the update out via SpendStatusService.Changed so the top status bar reflects
        // it immediately, not just whatever this method returns to its caller.
        var (updatedSpend, updatedCap) = await spendStatus.RefreshAsync(userEmail, cancellationToken);

        var response = new HelperResponse
        {
            SuggestedFileName = helper.Name,
            Documents = [new Document { Type = DocumentType.Html, Name = helper.Name, Content = ExtractContent(result.Text) }]
        };

        return new HelperInvocationOutcome { Response = response, Spend = updatedSpend, Cap = updatedCap };
    }

    /// <summary>
    /// V1 sent a Helper's configured Knowledge document (a reference/template file, e.g. the
    /// exact report template a "Committee Report Generator"-style Helper must adhere to) as a
    /// real Converse API document content block - V2 had this in the schema
    /// (HasKnowledge/KnowledgeData/KnowledgeFileType) but never actually sent it. Prepended ahead
    /// of any user-uploaded attachments, matching V1's behaviour of always including it when
    /// configured, not depending on the user separately re-attaching it each run.
    /// </summary>
    private static IReadOnlyList<Attachment> BuildAttachments(HelperDefinition helper, IReadOnlyList<Attachment>? uploaded)
    {
        if (!helper.HasKnowledge || string.IsNullOrWhiteSpace(helper.KnowledgeData))
        {
            return uploaded ?? [];
        }

        var classification = AttachmentClassifier.ClassifyExtension(helper.KnowledgeFileType ?? "");
        if (classification is not { } c)
        {
            return uploaded ?? [];
        }

        var knowledgeAttachment = new Attachment
        {
            Name = $"{helper.Name} reference document",
            Kind = c.Kind,
            Format = c.Format,
            Bytes = Convert.FromBase64String(helper.KnowledgeData)
        };

        return uploaded is { Count: > 0 } ? [knowledgeAttachment, .. uploaded] : [knowledgeAttachment];
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
}
