namespace MICGeoVisionPro.Desktop.ViewModels;

public sealed record LithologySegment(
    string Label,
    double Top,
    double Height,
    string Fill,
    string ToolTip);

public sealed record DepthTick(double Top, string Label);

public static class LithologyPalette
{
    private static readonly (string Keyword, string Color)[] Known =
    [
        ("terre végétale", "#6B4F3A"),
        ("topsoil", "#6B4F3A"),
        ("tourbe", "#4E3B2A"),
        ("peat", "#4E3B2A"),
        ("remblai", "#8C7B6B"),
        ("fill", "#8C7B6B"),
        ("enrobé", "#3F3F46"),
        ("asphalt", "#3F3F46"),
        ("béton", "#B9BDC1"),
        ("concrete", "#B9BDC1"),
        ("craie", "#E6E8E3"),
        ("chalk", "#E6E8E3"),
        ("calcaire", "#A8C8D8"),
        ("limestone", "#A8C8D8"),
        ("grès", "#D9B382"),
        ("sandstone", "#D9B382"),
        ("marne", "#8FA9A0"),
        ("marl", "#8FA9A0"),
        ("argilite", "#5E6B73"),
        ("shale", "#5E6B73"),
        ("granite", "#C08A7C"),
        ("granit", "#C08A7C"),
        ("gravier", "#9A8C7A"),
        ("gravel", "#9A8C7A"),
        ("limon", "#C9B79C"),
        ("silt", "#C9B79C"),
        ("sable", "#E3CF9F"),
        ("sand", "#E3CF9F"),
        ("argile", "#B08968"),
        ("clay", "#B08968"),
        ("eau", "#4A90C2"),
        ("water", "#4A90C2"),
        ("substratum", "#6D7B8A"),
        ("roche", "#6D7B8A"),
        ("bedrock", "#6D7B8A")
    ];

    private static readonly string[] Fallback =
    [
        "#7FA6C9", "#C9A27F", "#9CC9A2", "#C9C27F",
        "#A69CC9", "#C99CB4", "#7FC9C0", "#B4A77F"
    ];

    public static string ColorFor(string? lithology)
    {
        var label = lithology?.Trim().ToLowerInvariant() ?? string.Empty;
        foreach (var (keyword, color) in Known)
        {
            if (label.Contains(keyword, StringComparison.Ordinal))
            {
                return color;
            }
        }

        if (label.Length == 0) return "#8C99A6";

        var hash = 0;
        foreach (var character in label)
        {
            hash = (hash * 31 + character) & int.MaxValue;
        }

        return Fallback[hash % Fallback.Length];
    }
}
