using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace AiHelpers.Services;

public class EntraCurrentUserService(AuthenticationStateProvider authStateProvider, IAdminAccessService adminAccess) : ICurrentUserService
{
    public async Task<string> GetEmailAsync()
    {
        var user = await GetUserAsync();
        return user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("preferred_username")
            ?? user.FindFirstValue(ClaimTypes.Upn)
            ?? throw new InvalidOperationException("Signed-in user has no email/UPN claim.");
    }

    public async Task<string> GetDisplayNameAsync()
    {
        var user = await GetUserAsync();
        return user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("name") ?? await GetEmailAsync();
    }

    public async Task<bool> IsAdminAsync()
    {
        var email = await GetEmailAsync();
        return await adminAccess.IsAdminAsync(email);
    }

    public Task<ClaimsPrincipal> GetPrincipalAsync() => GetUserAsync();

    private async Task<ClaimsPrincipal> GetUserAsync()
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        return state.User;
    }
}
