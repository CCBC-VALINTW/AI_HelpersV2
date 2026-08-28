using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.Identity.Web;

namespace AiHelpers.Services;

public class GraphOrganisationalInfoService(
    HttpClient httpClient,
    ITokenAcquisition tokenAcquisition,
    ILogger<GraphOrganisationalInfoService> logger) : IOrganisationalInfoService
{
    public async Task<string?> TryGetOrganisationalInfoAsync(ClaimsPrincipal user)
    {
        try
        {
            var token = await tokenAcquisition.GetAccessTokenForUserAsync(["User.Read"], user: user);

            using var request = new HttpRequestMessage(HttpMethod.Get,
                "me?$select=displayName,jobTitle,companyName,department,mail,userPrincipalName,businessPhones,mobilePhone");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Graph /me lookup failed with status {Status}", response.StatusCode);
                return null;
            }

            var profile = await response.Content.ReadFromJsonAsync<GraphProfile>();
            if (profile is null) return null;

            var phone = profile.BusinessPhones?.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p))
                ?? profile.MobilePhone;
            var email = profile.Mail ?? profile.UserPrincipalName;

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(profile.DisplayName)) parts.Add($"Name: {profile.DisplayName}");
            if (!string.IsNullOrWhiteSpace(profile.JobTitle)) parts.Add($"Job title: {profile.JobTitle}");
            if (!string.IsNullOrWhiteSpace(profile.CompanyName)) parts.Add($"Company: {profile.CompanyName}");
            if (!string.IsNullOrWhiteSpace(profile.Department)) parts.Add($"Department: {profile.Department}");
            if (!string.IsNullOrWhiteSpace(email)) parts.Add($"Email: {email}");
            if (!string.IsNullOrWhiteSpace(phone)) parts.Add($"Telephone: {phone}");

            return parts.Count == 0 ? null : string.Join(". ", parts) + ".";
        }
        catch (Exception ex)
        {
            // Directory lookups are a nice-to-have pre-fill, not a hard requirement - missing
            // consent, an expired cache entry, or Graph being briefly unavailable should never
            // break the run page, so any failure here just means no pre-fill happens.
            logger.LogWarning(ex, "Could not fetch organisational info from Microsoft Graph.");
            return null;
        }
    }

    private class GraphProfile
    {
        [JsonPropertyName("displayName")] public string? DisplayName { get; set; }
        [JsonPropertyName("jobTitle")] public string? JobTitle { get; set; }
        [JsonPropertyName("companyName")] public string? CompanyName { get; set; }
        [JsonPropertyName("department")] public string? Department { get; set; }
        [JsonPropertyName("mail")] public string? Mail { get; set; }
        [JsonPropertyName("userPrincipalName")] public string? UserPrincipalName { get; set; }
        [JsonPropertyName("businessPhones")] public string[]? BusinessPhones { get; set; }
        [JsonPropertyName("mobilePhone")] public string? MobilePhone { get; set; }
    }
}
