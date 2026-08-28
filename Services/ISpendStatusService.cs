namespace AiHelpers.Services;

/// <summary>
/// Scoped (one instance per Blazor Server circuit, i.e. per signed-in user) so the top status
/// bar can show a live monthly spend/cap figure that updates the moment a run completes,
/// without every component re-querying the database itself or waiting for a navigation.
/// </summary>
public interface ISpendStatusService
{
    decimal CurrentSpend { get; }
    decimal CurrentCap { get; }
    bool Loaded { get; }
    event Action? Changed;
    Task<(decimal Spend, decimal Cap)> RefreshAsync(string userEmail, CancellationToken cancellationToken = default);
}
