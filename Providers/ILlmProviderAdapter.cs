using AiHelpers.Data.Entities;
using AiHelpers.Data.Enums;

namespace AiHelpers.Providers;

public interface ILlmProviderAdapter
{
    LlmProvider Provider { get; }
    Task<LlmInvocationResult> InvokeAsync(LlmInvocationRequest request, CancellationToken cancellationToken = default);
}

public class LlmInvocationRequest
{
    public required HelperDefinition Helper { get; set; }
    public required LlmDefinition Model { get; set; }
    public required string UserInput { get; set; }
    public IReadOnlyList<Attachment> Attachments { get; set; } = [];
}

public enum AttachmentKind { Document, Image }

/// <summary>
/// A user-uploaded document/image, or a Helper's own configured Knowledge document, to send
/// alongside UserInput. Format values match the Converse API's own vocabulary directly (document:
/// pdf/csv/doc/docx/xls/xlsx/html/txt/md, image: png/jpeg/gif/webp) so adapters don't need to
/// re-derive them from a file extension.
/// </summary>
public class Attachment
{
    public required string Name { get; set; }
    public required AttachmentKind Kind { get; set; }
    public required string Format { get; set; }
    public required byte[] Bytes { get; set; }
}

/// <summary>
/// Shared file-extension classification for attachments - used both for user-uploaded files
/// (HelperDetail.razor) and a Helper's own Knowledge document (HelperInvocationService), so the
/// Converse format vocabulary lives in one place.
/// </summary>
public static class AttachmentClassifier
{
    public static (AttachmentKind Kind, string Format)? ClassifyExtension(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "pdf" or "csv" or "doc" or "docx" or "xls" or "xlsx" or "txt" or "md" => (AttachmentKind.Document, ext),
            "html" or "htm" => (AttachmentKind.Document, "html"),
            // WebVTT (Teams meeting transcripts) - "vtt" isn't a Converse API document format, but
            // the content itself is already plain, human-readable text (timestamps + cue lines),
            // so it's sent as "txt" rather than needing any real parsing/extraction step.
            "vtt" => (AttachmentKind.Document, "txt"),
            "jpg" or "jpeg" => (AttachmentKind.Image, "jpeg"),
            "png" or "gif" or "webp" => (AttachmentKind.Image, ext),
            _ => null
        };
    }
}

public class LlmInvocationResult
{
    public required string Text { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public string? StopReason { get; set; }
}
