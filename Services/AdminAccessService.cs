using AiHelpers.Data;
using Microsoft.EntityFrameworkCore;

namespace AiHelpers.Services;

/// <summary>
/// Uses IDbContextFactory, not an injected AppDbContext, so this works identically whether it's
/// called from a Blazor circuit-scoped component (EntraCurrentUserService) or from
/// AdminOnlyAuthorizationHandler, which runs outside any circuit and needs its own short-lived
/// context - same reasoning as SpendStatusService.
/// </summary>
public class AdminAccessService(IDbContextFactory<AppDbContext> dbFactory, IConfiguration configuration) : IAdminAccessService
{
    public async Task<bool> IsAdminAsync(string? email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        var configAdmins = configuration.GetSection("AdminEmails").Get<string[]>() ?? [];
        if (configAdmins.Contains(email, StringComparer.OrdinalIgnoreCase)) return true;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AdminUsers.AnyAsync(a => a.Email == email, cancellationToken);
    }
}
