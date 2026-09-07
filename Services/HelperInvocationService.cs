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
///
/// Real bug fixed 2026-09-07: a real Helper call can run for many minutes (long documents have
/// taken 20+), and this used to share the plain injected, circuit-scoped AppDbContext other
/// components/pages also use for the lifetime of the whole browser tab. Navigating to any other
/// page mid-run - not closing the tab, just clicking a link - meant that page's own
/// OnInitializedAsync raced this method's own SaveChangesAsync calls (AccountingEntry,
/// CallbackEntry, DataQueryExecutionLog) on the SAME DbContext instance, which EF Core does not
/// support concurrently. Whichever save lost the race threw "A second operation was started on
/// this context instance before a previous operation completed" - and since
/// LogCallbackEntryAsync/LogDataQueryExecutionAsync both deliberately swallow their own
/// SaveChangesAsync failures (by design, so a logging failure never breaks an otherwise-successful
/// run), this failed completely silently: no error, but the CallbackEntry a user would later look
/// for in Recent Runs was simply never written. Same root cause SpendStatusService already hit
/// and fixed once before. Fixed the same way: RunAsync now creates its own independent,
/// short-lived DbContext via IDbContextFactory instead of sharing the injected one, so it no
/// longer contends with whatever page the user has since navigated to.
/// </summary>
public class HelperInvocationService(IDbContextFactory<AppDbContext> dbFactory, IEnumerable<ILlmProviderAdapter> adapters, ISpendStatusService spendStatus, IDataQueryService dataQueryService) : IHelperInvocationService
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

        // Own independent context for this run, not the injected circuit-scoped one - see the
        // class doc comment for why. Held for the whole run (not re-acquired per save) so this
        // stays one consistent unit of work, same as the shared context used to be, just no
        // longer contending with whatever else the circuit gets up to while this is in flight.
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var (effectiveInput, dataQueryError) = await ResolveDataQueriesAsync(db, helper, userInput, userEmail, cancellationToken);
        if (dataQueryError is not null)
        {
            return new HelperInvocationOutcome { ErrorMessage = dataQueryError, Spend = spend, Cap = cap };
        }

        LlmInvocationResult result;
        try
        {
            result = await adapter.InvokeAsync(new LlmInvocationRequest
            {
                Helper = helper,
                Model = helper.LlmDefinition,
                UserInput = effectiveInput,
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
        // Deliberately left in USD (LlmDefinition.InputTokenCost/OutputTokenCost are the
        // provider's own USD list prices) - GBP conversion happens only where this is
        // aggregated/displayed (SpendStatusService.UsdToGbpRate), not baked in here, so this
        // stays a straight, unconverted record of what the call actually cost AWS.
        var costUsd = (result.InputTokens / 1000m) * (helper.LlmDefinition.InputTokenCost ?? 0m)
            + (result.OutputTokens / 1000m) * (helper.LlmDefinition.OutputTokenCost ?? 0m);

        db.AccountingEntries.Add(new AccountingEntry
        {
            UserId = userEmail,
            HelperDefinitionId = helper.Id,
            HelperName = helper.Name,
            UsdCost = costUsd,
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        // Recompute rather than reuse the pre-call (spend, cap) - this run's own cost has just
        // been logged, so the pre-call figures would under-report by exactly this call's cost.
        // Also pushes the update out via SpendStatusService.Changed so the top status bar reflects
        // it immediately, not just whatever this method returns to its caller. SpendStatusService
        // already uses its own IDbContextFactory-sourced context internally, so this call was
        // never part of the concurrency risk this fix addresses.
        var (updatedSpend, updatedCap) = await spendStatus.RefreshAsync(userEmail, cancellationToken);

        var (content, suggestedDescription) = ExtractContent(result.Text);
        var suggestedFileName = FileNameSanitizer.Sanitize(
            string.IsNullOrWhiteSpace(suggestedDescription) ? helper.Name : $"{helper.Name} - {suggestedDescription}",
            fallback: helper.Name);

        await LogCallbackEntryAsync(db, helper, userEmail, content, suggestedFileName, result, cancellationToken);

        var response = new HelperResponse
        {
            SuggestedFileName = suggestedFileName,
            Documents = [new Document { Type = DocumentType.Html, Name = suggestedFileName, Content = content }]
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
    /// Runs every HelperDataQuery attached to this Helper, same "silently included, no user
    /// interaction needed" shape as BuildAttachments' own Knowledge-document handling above - the
    /// caller (HelperDetail.razor) never needs to know these exist, same as it never needs to know
    /// about Knowledge. Requires helper.DataQueries (and each query's DataConnection) to already be
    /// loaded - a Helper fetched without that Include just runs with an empty collection, same as
    /// any other unloaded EF navigation, rather than throwing.
    ///
    /// A failed or disabled data source aborts the whole run (returns the error instead of a
    /// prepended block) rather than silently running with missing data - an explicit design
    /// choice: a Helper built to depend on live data producing output that quietly omits that data
    /// is worse than a clear failure. Every attempt is logged to DataQueryExecutionLog regardless
    /// of outcome, both for debugging ("why did this look wrong") and as part of this feature's
    /// own security auditability story.
    /// </summary>
    private async Task<(string EffectiveInput, string? Error)> ResolveDataQueriesAsync(AppDbContext db, HelperDefinition helper, string userInput, string userEmail, CancellationToken cancellationToken)
    {
        if (helper.DataQueries.Count == 0) return (userInput, null);

        var blocks = new List<string>();
        foreach (var dataQuery in helper.DataQueries.OrderBy(q => q.SortOrder))
        {
            var definition = dataQuery.DataSourceDefinition;
            if (!definition.DataConnection.IsEnabled)
            {
                await LogDataQueryExecutionAsync(db, dataQuery, userEmail, success: false, rowCount: null, truncated: false, durationMs: 0,
                    errorMessage: "Connection is disabled.", cancellationToken);
                return (userInput, $"This Helper's data source \"{definition.Label}\" is currently disabled - contact an admin.");
            }

            var result = await dataQueryService.ExecuteAsync(definition.DataConnection, definition.Query, definition.MaxRows, definition.OutputFormat, definition.CompactionEnabled, cancellationToken);
            await LogDataQueryExecutionAsync(db, dataQuery, userEmail, result.Success, result.RowCount, result.Truncated, result.DurationMs, result.ErrorMessage, cancellationToken);

            if (!result.Success)
            {
                return (userInput, $"This Helper's data source \"{definition.Label}\" failed: {result.ErrorMessage}");
            }

            var heading = string.IsNullOrWhiteSpace(dataQuery.UsageInstruction)
                ? definition.Label
                : $"{definition.Label} ({dataQuery.UsageInstruction})";
            blocks.Add($"## {heading}\n{result.Content}");
        }

        var dataBlock = "Live data retrieved for this request - read this FIRST, before drafting your response:\n\n" +
            string.Join("\n\n", blocks);
        return ($"{dataBlock}\n\n{userInput}", null);
    }

    private async Task LogDataQueryExecutionAsync(AppDbContext db, HelperDataQuery dataQuery, string userEmail, bool success, int? rowCount, bool truncated, int durationMs, string? errorMessage, CancellationToken cancellationToken)
    {
        // Never let a logging failure break (or crash - see the real incident this comment
        // replaced) the run itself - same "silent by design" reasoning as AccessLogService.LogAsync.
        // This is a nice-to-have audit trail, not core functionality; a Helper run that actually
        // succeeded must never fail purely because its own logging step hit a problem.
        var logEntry = new DataQueryExecutionLog
        {
            // Null, not 0, for a HelperEditor preview run - BuildPreviewHelper's DataQueries are
            // draft objects built in memory, never persisted, so Id is the CLR default (0), which
            // isn't a real row and violates the FK (the actual incident this was fixed from - an
            // unhandled exception here took down the whole circuit). Real, saved queries still
            // get logged against their actual Id - only an unsaved draft's execution is logged
            // "loose", same as how AccountingEntry already logs a preview run's real spend
            // without needing to reference a persisted GeneratedDocument.
            HelperDataQueryId = dataQuery.Id == 0 ? null : dataQuery.Id,
            Label = dataQuery.DataSourceDefinition.Label,
            UserId = userEmail,
            Succeeded = success,
            RowCount = rowCount,
            Truncated = truncated,
            DurationMs = durationMs,
            ErrorMessage = errorMessage
        };
        db.DataQueryExecutionLogs.Add(logEntry);
        try
        {
            // Saved immediately, not batched with the run's other SaveChangesAsync calls - a
            // query that aborts the run still needs its own failure logged, and an early return
            // here must not silently lose that.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Detach, not just swallow - even though db here is now this run's own short-lived
            // context (see the class doc comment), a failed entity left in the change tracker
            // would still poison every later SaveChangesAsync call made against this SAME context
            // for the rest of this run (the accounting write, the CallbackEntry write below).
            // Detaching removes it from tracking entirely, so this failure stays contained to
            // this one log entry.
            db.Entry(logEntry).State = EntityState.Detached;
        }
    }

    // 7 days - explicitly a recovery window, not real retention; GeneratedDocument (an actual
    // deliberate save) is what's meant to last.
    private static readonly TimeSpan CallbackEntryRetention = TimeSpan.FromDays(7);

    /// <summary>
    /// Snapshots this run's raw output so it can be recovered without re-running the LLM call -
    /// captured unconditionally on every successful run (including a Helper Editor preview),
    /// regardless of whether the caller goes on to persist it as a real GeneratedDocument. See
    /// CallbackEntry's own doc comment for the full "why".
    /// </summary>
    private async Task LogCallbackEntryAsync(AppDbContext db, HelperDefinition helper, string userEmail, string content, string suggestedFileName, LlmInvocationResult result, CancellationToken cancellationToken)
    {
        var entry = new CallbackEntry
        {
            CreatedByEmail = userEmail,
            HelperDefinitionId = helper.Id,
            HelperName = helper.Name,
            OutputHtml = content,
            SuggestedFileName = suggestedFileName,
            InputTokens = result.InputTokens,
            OutputTokens = result.OutputTokens,
            StopReason = result.StopReason
        };
        db.CallbackEntries.Add(entry);
        try
        {
            await db.SaveChangesAsync(cancellationToken);

            // Opportunistic purge on every write, same pattern as AccessLogService - no separate
            // background-job infrastructure needed just for cleanup, and this stays a rolling
            // recovery window rather than an ever-growing table.
            var cutoff = DateTime.UtcNow - CallbackEntryRetention;
            await db.CallbackEntries
                .Where(c => c.CreatedAtUtc < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }
        catch
        {
            // Never let this break the run itself - see LogDataQueryExecutionAsync's own doc
            // comment for why detaching (not just swallowing) still matters even against this
            // run's own private context.
            db.Entry(entry).State = EntityState.Detached;
        }
    }

    private static readonly Regex SuggestedFileNameMarker =
        new(@"\s*<!--\s*SUGGESTED_FILENAME:\s*(.*?)\s*-->\s*", RegexOptions.IgnoreCase | RegexOptions.Singleline);

    /// <summary>
    /// Defensive backstop for when a model wraps its response in a markdown code fence plus
    /// chat-style commentary (an intro line, a follow-up question) despite being told not to -
    /// prompt-following alone isn't fully reliable. If a fenced block is present anywhere in the
    /// response, keeps only its contents and discards the surrounding commentary; otherwise
    /// returns the response as-is. Also extracts and strips BedrockAdapter's SUGGESTED_FILENAME
    /// marker first (see its own doc comment) - done before the fence check so the marker is never
    /// mistaken for part of a fenced block's own content, regardless of which side of the fence it
    /// ends up on.
    /// </summary>
    private static (string Content, string? SuggestedDescription) ExtractContent(string text)
    {
        var markerMatch = SuggestedFileNameMarker.Match(text);
        var suggestedDescription = markerMatch.Success ? markerMatch.Groups[1].Value.Trim() : null;
        var withoutMarker = markerMatch.Success ? SuggestedFileNameMarker.Replace(text, "\n", 1) : text;

        var fenceMatch = Regex.Match(withoutMarker, "```[a-zA-Z]*\\s*\\n(.*?)\\n```", RegexOptions.Singleline);
        var content = (fenceMatch.Success ? fenceMatch.Groups[1].Value : withoutMarker).Trim();

        return (content, string.IsNullOrWhiteSpace(suggestedDescription) ? null : suggestedDescription);
    }
}
