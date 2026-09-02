using AiHelpers.Data;
using AiHelpers.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiHelpers.Services;

/// <summary>
/// Silent, lightweight "who's opening this and how often" visibility - deliberately not a full
/// audit trail (Will's own explicit scoping). Called from MainLayout.OnInitializedAsync, an
/// always-mounted component whose own initialization can genuinely race a routed page's
/// OnInitializedAsync on the same circuit - same reasoning as SpendStatusService, so this uses its
/// own independent DbContext via IDbContextFactory rather than a shared injected AppDbContext.
/// </summary>
public class AccessLogService(IDbContextFactory<AppDbContext> dbFactory) : IAccessLogService
{
    // "Not looking for a full audit" (Will's own words) - old rows are opportunistically purged on
    // every write rather than kept forever, so this stays a rolling usage-frequency picture rather
    // than an ever-growing table, without needing any new background-job infrastructure just for
    // cleanup.
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(30);

    public async Task LogAsync(string email, CancellationToken cancellationToken = default)
    {
        // Never let this break a page load - it's a nice-to-have side effect, not core
        // functionality, and "silent" was the explicit ask - silent on failure too.
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            db.AccessLogEntries.Add(new AccessLogEntry { Email = email });
            await db.SaveChangesAsync(cancellationToken);

            var cutoff = DateTime.UtcNow - RetentionWindow;
            await db.AccessLogEntries
                .Where(a => a.TimestampUtc < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }
        catch
        {
            // Silent by design - see doc comment above.
        }
    }
}
