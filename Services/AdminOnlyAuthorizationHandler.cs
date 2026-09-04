using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace AiHelpers.Services;

public class AdminOnlyRequirement : IAuthorizationRequirement;

/// <summary>
/// Backs the "AdminOnly" policy (Program.cs) via a custom handler rather than
/// policy.RequireAssertion, because that callback is synchronous and IAdminAccessService needs to
/// query the database. Duplicates the same 3-claim email-resolution fallback chain
/// EntraCurrentUserService.GetEmailAsync uses, since this runs as part of ASP.NET Core's own
/// authorization pipeline (outside any Blazor circuit, no AuthenticationStateProvider available
/// here) and must read the claim straight off the ClaimsPrincipal instead.
/// </summary>
public class AdminOnlyAuthorizationHandler(IAdminAccessService adminAccess) : AuthorizationHandler<AdminOnlyRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminOnlyRequirement requirement)
    {
        var email = context.User.FindFirstValue(ClaimTypes.Email)
            ?? context.User.FindFirstValue("preferred_username")
            ?? context.User.FindFirstValue(ClaimTypes.Upn);

        if (await adminAccess.IsAdminAsync(email))
        {
            context.Succeed(requirement);
        }
    }
}
