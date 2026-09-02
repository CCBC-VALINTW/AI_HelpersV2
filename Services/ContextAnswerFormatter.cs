using AiHelpers.Data.Enums;
using AiHelpers.Providers;

namespace AiHelpers.Services;

/// <summary>
/// Folds a set of context-question answers into the "read all of these FIRST" block prepended to
/// a Helper's input - shared between the real run page (HelperDetail.razor, answering the actual
/// persisted HelperContextQuestion rows) and the Helper Editor's preview facility
/// (HelperEditor.razor, answering the current in-form draft questions before they're saved), so
/// the two can't silently drift on formatting.
/// </summary>
public static class ContextAnswerFormatter
{
    public record Answer(string Label, ContextQuestionType Type, string? UsageInstruction, string? Text, Attachment? Document, bool BoolValue);

    public static string? BuildContextBlock(IEnumerable<Answer> answers)
    {
        var contextParts = new List<string>();
        foreach (var answer in answers)
        {
            var instruction = string.IsNullOrWhiteSpace(answer.UsageInstruction) ? null : answer.UsageInstruction;

            if ((answer.Type == ContextQuestionType.Text || answer.Type == ContextQuestionType.Select) && !string.IsNullOrWhiteSpace(answer.Text))
            {
                contextParts.Add(instruction is null
                    ? $"{answer.Label}: {answer.Text}"
                    : $"{answer.Label}: {answer.Text} ({instruction})");
            }
            else if ((answer.Type == ContextQuestionType.Document || answer.Type == ContextQuestionType.WebUrl) &&
                answer.Document is not null && instruction is not null)
            {
                // WebUrl answers arrive as the exact same kind of Attachment as a Document answer
                // (see HelperDetail.razor's OnContextUrlFetch, which reuses UrlFetchService the
                // same way the main input's own URL field does) - only the wording differs, since
                // "attached document" would misdescribe a fetched web page.
                var noun = answer.Type == ContextQuestionType.WebUrl ? "fetched page" : "attached document";
                contextParts.Add($"Regarding the {noun} \"{answer.Document.Name}\" ({answer.Label}): {instruction}");
            }
            else if (answer.Type == ContextQuestionType.Boolean)
            {
                // Always included, unlike Text/Document - "No" is meaningful information too,
                // there's no "blank" state to skip the way an empty text box has.
                var value = answer.BoolValue ? "Yes" : "No";
                contextParts.Add(instruction is null
                    ? $"{answer.Label}: {value}"
                    : $"{answer.Label}: {value} ({instruction})");
            }
        }

        if (contextParts.Count == 0) return null;

        return "Context answers provided below - read all of these FIRST, before drafting your response, " +
            "as they may change which sections or features your output should include:\n" +
            string.Join("\n", contextParts);
    }
}
