using System.Text.Json.Nodes;

namespace AiHelpers.Contracts;

/// <summary>
/// The result of running a Helper. Mixed mode is allowed - a Helper can return generated
/// Documents, a structured Data payload (when HelperDefinition.OutputSchemaJson is set), or
/// both in the same response.
/// </summary>
public class HelperResponse
{
    public string? SuggestedFileName { get; set; }
    public string? Category { get; set; }

    public List<Document> Documents { get; set; } = [];

    /// <summary>Present when the Helper defines OutputSchemaJson and the model returned matching structured output.</summary>
    public JsonNode? Data { get; set; }
}
