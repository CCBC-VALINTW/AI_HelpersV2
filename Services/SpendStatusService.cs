using AiHelpers.Data;
using Microsoft.EntityFrameworkCore;

namespace AiHelpers.Services;

public class SpendStatusService(IDbContextFactory<AppDbContext> dbFactory) : ISpendStatusService
{
    // AccountingEntry.UsdCost is always real, unconverted USD (what AWS actually bills) - this app
    // displays/caps in GBP ("we use real money here"), so it's converted here, once, at the point
    // spend gets aggregated. Deliberately a fixed, rough rate rather than a live lookup - Will's
    // own reasoning: the real exchange rate only matters at the point the actual monthly AWS
    // invoice is paid, not per-call, so a live rate here would just be spurious precision. Review
    // periodically if it drifts far from reality.
    private const decimal UsdToGbpRate = 0.8m;

    // V1 business rule: only 80% of a user's (now GBP-converted) spend counts against their cap,
    // giving everyone a built-in ~25% headroom buffer. Coincidentally the same numeric value as
    // UsdToGbpRate above - the two are completely unrelated (one's a currency conversion, this is
    // a spending-policy discount) and must stay as two separate named constants, not merged into
    // one, or a future change to either would silently change the other's behaviour too.
    private const decimal CapCountingRate = 0.8m;

    public decimal CurrentSpend { get; private set; }
    public decimal CurrentCap { get; private set; } = 1.0m;
    public bool Loaded { get; private set; }

    public event Action? Changed;

    public async Task<(decimal Spend, decimal Cap)> RefreshAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        // Its own short-lived context, not the circuit-scoped AppDbContext other components
        // inject directly - see the registration comment in Program.cs for why.
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var rawSpendUsd = await db.AccountingEntries
            .Where(a => a.UserId == userEmail && a.Timestamp >= monthStart)
            .SumAsync(a => (decimal?)a.UsdCost, cancellationToken) ?? 0m;

        var cap = await db.SpendCaps
            .Where(s => s.UserId == userEmail)
            .Select(s => (decimal?)s.MonthlyCapAmount)
            .FirstOrDefaultAsync(cancellationToken) ?? 1.0m;

        CurrentSpend = rawSpendUsd * UsdToGbpRate * CapCountingRate;
        CurrentCap = cap;
        Loaded = true;

        Changed?.Invoke();
        return (CurrentSpend, CurrentCap);
    }
}
