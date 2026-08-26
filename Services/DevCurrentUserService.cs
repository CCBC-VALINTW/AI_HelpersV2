namespace AiHelpers.Services;

/// <summary>
/// Stand-in for real Entra ID / SSO authentication (which V1 got for free via GovService).
/// Pages should depend on ICurrentUserService rather than this type directly, so swapping
/// in real auth later is a DI registration change, not a page rewrite.
/// </summary>
public class DevCurrentUserService : ICurrentUserService
{
    public string Email => "will.valintine@conwy.gov.uk";
    public string DisplayName => "Will Valintine";
}
