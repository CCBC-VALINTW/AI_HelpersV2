namespace AiHelpers.Data.Entities;

/// <summary>A stored knowledge-base article, e.g. a saved Helper output shared for reuse.</summary>
public class ArticleStoreItem
{
    public int Id { get; set; }
    public string? Category { get; set; }
    public string? Access { get; set; }
    public required string Title { get; set; }
    public required string ArticleHtml { get; set; }
}
