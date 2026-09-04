namespace AiHelpers.Services;

/// <summary>
/// Single source of truth for "is this email an admin" - previously duplicated independently in
/// Program.cs's AdminOnly policy and EntraCurrentUserService.IsAdminAsync (copy-pasted, not
/// shared), which could silently drift out of sync. Two tiers, both checked: the permanent
/// AdminEmails list in appsettings.json (nothing in this app's own UI can ever remove it - a
/// deliberate break-glass fallback) plus AdminUser rows, managed at /admin (Admins tab).
/// </summary>
public interface IAdminAccessService
{
    Task<bool> IsAdminAsync(string? email, CancellationToken cancellationToken = default);
}
