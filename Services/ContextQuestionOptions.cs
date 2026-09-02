using System.Text.Json;

namespace AiHelpers.Services;

/// <summary>
/// Reads/writes HelperContextQuestion.OptionsJson (a Select question's picklist) - shared between
/// HelperEditor.razor (editing the list as one option per line) and HelperDetail.razor/
/// HelperEditor's preview pane (rendering it as a real &lt;select&gt;), so the two can't drift on
/// the JSON shape.
/// </summary>
public static class ContextQuestionOptions
{
    public static List<string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Splits one-option-per-line editor input into the stored list - blank lines dropped,
    /// each trimmed.</summary>
    public static List<string> ParseLines(string? linesText) =>
        (linesText ?? "")
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

    public static string? Serialize(IReadOnlyList<string> options) =>
        options.Count == 0 ? null : JsonSerializer.Serialize(options);
}
