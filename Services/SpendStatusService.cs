using AiHelpers.Data;
using Microsoft.EntityFrameworkCore;

namespace AiHelpers.Services;

public class SpendStatusService(IDbContextFactory<AppDbContext> dbFactory) : ISpendStatusService
{
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

        var rawSpend = await db.AccountingEntries
            .Where(a => a.UserId == userEmail && a.Timestamp >= monthStart)
            .SumAsync(a => (decimal?)a.Cost, cancellationToken) ?? 0m;

        var cap = await db.SpendCaps
            .Where(s => s.UserId == userEmail)
            .Select(s => (decimal?)s.MonthlyCapAmount)
            .FirstOrDefaultAsync(cancellationToken) ?? 1.0m;

        // Matches V1: only 80% of actual spend counts against the cap.
        CurrentSpend = rawSpend * 0.8m;
        CurrentCap = cap;
        Loaded = true;

        Changed?.Invoke();
        return (CurrentSpend, CurrentCap);
    }
}
