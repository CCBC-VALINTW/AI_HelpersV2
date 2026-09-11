namespace AiHelpers.Services;

/// <summary>
/// Result of wwwroot/js/outputActions.js's rasterizeSvgs - the markup with each inline SVG replaced
/// by a PNG &lt;img&gt;, plus how many graphics that actually worked for.
/// <para>
/// Converted/Failed are worth surfacing rather than discarding: a failed graphic keeps its original
/// SVG, which Word will ignore, so the difference between "all your charts came through" and "one of
/// them silently didn't" is something the person exporting needs told.
/// </para>
/// </summary>
public sealed record RasterizedHtml(string Html, int Converted, int Failed);
