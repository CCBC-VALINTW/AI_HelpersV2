namespace AiHelpers.Data.Entities;

/// <summary>
/// Additional admins, managed at /admin (Admins tab) - on top of the permanent AdminEmails list in
/// appsettings.json (see IAdminAccessService), which nothing in this app's own UI can ever
/// remove. That two-tier split is deliberate: it means this table can never be the only thing
/// standing between the app and a total admin lockout, even if it's emptied by mistake.
/// </summary>
public class AdminUser
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
