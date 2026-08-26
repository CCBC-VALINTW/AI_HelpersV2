using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AiHelpers.Data.Entities;
using AiHelpers.Data.Enums;
using AiHelpers.Services;

namespace AiHelpers.Providers.Bedrock;

/// <summary>
/// Calls AWS Bedrock's Converse API directly (no WSO2 proxy - see
/// project_ai_helpers_v1_architecture memory for why V1 needed one and V2 doesn't).
/// Only bearer-token auth (AWS's Bedrock API key feature) is implemented, since that's the
/// credential style actually in use. Access key/secret would need SigV4 request signing - not
/// worth building and testing without real IAM credentials to verify it against.
/// </summary>
public class BedrockAdapter(HttpClient httpClient, ICredentialStore credentialStore) : ILlmProviderAdapter
{
    public LlmProvider Provider => LlmProvider.AwsBedrock;

    public async Task<LlmInvocationResult> InvokeAsync(LlmInvocationRequest request, CancellationToken cancellationToken = default)
    {
        var credential = await credentialStore.GetDefaultAsync<AwsCredentialPayload>(LlmProvider.AwsBedrock, cancellationToken)
            ?? throw new InvalidOperationException("No AWS Bedrock credential is configured. Set one at /admin/credentials.");

        if (credential.BearerToken is null)
        {
            throw new NotSupportedException("Only bearer-token AWS credentials are supported right now - access key/secret needs SigV4 signing, not yet implemented.");
        }

        var body = BuildRequestBody(request.Helper, request.Model, request.UserInput);

        var url = $"https://bedrock-runtime.{credential.Region}.amazonaws.com/model/{Uri.EscapeDataString(request.Model.Identifier)}/converse";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credential.BearerToken);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseJson = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // AWS error bodies aren't consistent about casing here - seen both "message" and
            // "Message" depending on which layer generates the error.
            var message = responseJson?["message"]?.GetValue<string>()
                ?? responseJson?["Message"]?.GetValue<string>()
                ?? response.ReasonPhrase ?? "Unknown error";
            throw new InvalidOperationException($"Bedrock call failed ({(int)response.StatusCode}): {message}");
        }

        var contentBlocks = responseJson?["output"]?["message"]?["content"]?.AsArray() ?? [];
        var text = string.Concat(contentBlocks
            .Select(block => block?["text"]?.GetValue<string>())
            .Where(t => t is not null));

        return new LlmInvocationResult
        {
            Text = text,
            InputTokens = responseJson?["usage"]?["inputTokens"]?.GetValue<int>() ?? 0,
            OutputTokens = responseJson?["usage"]?["outputTokens"]?.GetValue<int>() ?? 0,
            StopReason = responseJson?["stopReason"]?.GetValue<string>()
        };
    }

    private static JsonObject BuildRequestBody(HelperDefinition helper, LlmDefinition model, string userInput)
    {
        var systemPrompts = new[]
        {
            helper.PrimaryPurpose,
            helper.Methodology,
            helper.StyleTone,
            helper.OutputFormat,
            helper.TargetAudience,
            helper.SpecialInstructions
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        var body = new JsonObject
        {
            ["system"] = new JsonArray(systemPrompts.Select(p => (JsonNode)new JsonObject { ["text"] = p }).ToArray()),
            ["messages"] = new JsonArray(
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray(new JsonObject { ["text"] = userInput })
                })
        };

        // When Effort engages reasoning, drop Creativity/Adherence entirely rather than send
        // them alongside thinking config - Anthropic's extended-thinking mode on Bedrock
        // rejects temperature/top_p being set alongside it, this isn't just our own preference.
        if (helper.Effort is { } effort && model.SupportsReasoning)
        {
            var maxBudget = model.ReasoningTokens ?? 4096;
            var budgetTokens = effort switch
            {
                EffortLevel.Low => (int)(maxBudget * 0.25),
                EffortLevel.Medium => (int)(maxBudget * 0.5),
                EffortLevel.High => maxBudget,
                _ => maxBudget
            };

            body["additionalModelRequestFields"] = new JsonObject
            {
                ["thinking"] = new JsonObject
                {
                    ["type"] = "enabled",
                    ["budget_tokens"] = budgetTokens
                }
            };
        }
        else
        {
            var temperature = helper.Creativity ?? model.DefaultCreativity;
            var topP = helper.Adherence ?? model.DefaultAdherence;
            body["inferenceConfig"] = new JsonObject
            {
                ["temperature"] = temperature,
                ["topP"] = topP
            };
        }

        return body;
    }
}
