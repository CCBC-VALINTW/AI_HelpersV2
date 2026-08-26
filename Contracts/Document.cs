using AiHelpers.Data.Enums;

namespace AiHelpers.Contracts;

/// <summary>One generated file within a HelperResponse.</summary>
public class Document
{
    public required DocumentType Type { get; set; }
    public required string Name { get; set; }

    /// <summary>Raw text for Html/PlainText/Json/Csv; base64 for binary types (Docx/Pdf/Xlsx).</summary>
    public required string Content { get; set; }
}
