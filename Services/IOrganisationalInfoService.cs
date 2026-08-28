using System.Security.Claims;

namespace AiHelpers.Services;

/// <summary>
/// Reads the signed-in user's job title/department/office from Microsoft Graph (their own
/// profile only - delegated User.Read). Best-effort: returns null on any failure (missing
/// consent, Graph unavailable, etc.) rather than throwing, since this only ever feeds a
/// pre-fill suggestion, never a required value.
/// </summary>
public interface IOrganisationalInfoService
{
    Task<string?> TryGetOrganisationalInfoAsync(ClaimsPrincipal user);
}
