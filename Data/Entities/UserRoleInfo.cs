namespace AiHelpers.Data.Entities;

/// <summary>
/// A per-user free-text description of their organisational role (job title, service, section
/// etc.), mirroring PersonalityPrompt's pattern. V1 computed this automatically from directory
/// fields (name/email/job title/service/section) it had access to via GovService's own AD
/// integration - V2 doesn't currently pull those claims, so this is user-entered instead of
/// auto-populated.
/// </summary>
public class UserRoleInfo
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string Info { get; set; }
}
