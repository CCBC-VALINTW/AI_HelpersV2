using System.Security.Claims;

namespace AiHelpers.Services;

/// <summary>
/// Async by design: Blazor Server interactive circuits outlive the original HTTP request, so
/// the signed-in user must be read from AuthenticationStateProvider (which involves an await),
/// not HttpContext.User, which only works for the initial request.
/// </summary>
public interface ICurrentUserService
{
    Task<string> GetEmailAsync();
    Task<string> GetDisplayNameAsync();
    Task<bool> IsAdminAsync();
    Task<ClaimsPrincipal> GetPrincipalAsync();
}
