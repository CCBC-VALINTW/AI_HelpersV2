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
    //
    // Real bug fixed 2026-09-07: this used to also multiply by a second constant, CapCountingRate
    // (also 0.8), on the theory that V1 only counted 80% of spend against a user's cap as a
    // deliberate ~25% headroom policy. That was a misreading - V1's actual query (`WSO2 EI
    // Bedrock Proxy API definition.txt`: `SUM(DollarCost) * 0.8` compared directly against
    // `MonthlyCap`, a GBP-denominated field per Spend Caps' own "£" label) shows the *0.8 there
    // was always this exact same USD-to-GBP conversion, not a separate discount - V1 never had a
    // headroom concept at all. Applying both constants together silently double-converted
    // currency (effectively *0.64), understating real spend by 36% and letting a user run real
    // spend up to ~1.56x their actual cap before being blocked (this figure also gates
    // HelperInvocationService's spend >= cap check, not just this display). CapCountingRate has
    // been removed entirely, not just renamed - there was never a real second rule to keep.
    private const decimal UsdToGbpRate = 0.8m;

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

        CurrentSpend = rawSpendUsd * UsdToGbpRate;
        CurrentCap = cap;
        Loaded = true;

        Changed?.Invoke();
        return (CurrentSpend, CurrentCap);
    }
}
