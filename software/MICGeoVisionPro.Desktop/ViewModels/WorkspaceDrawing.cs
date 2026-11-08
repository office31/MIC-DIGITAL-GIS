namespace MICGeoVisionPro.Desktop.ViewModels;

public enum WorkspaceView
{
    Borehole,
    Map,
    CrossSection
}

public sealed record MapMarker(
    double Left,
    double Top,
    double Size,
    string Label,
    bool IsSelected,
    string ToolTip);

public sealed record SectionLayer(
    double Top,
    double Height,
    string Fill,
    string ToolTip);

public sealed record SectionColumn(
    double Left,
    double Width,
    double Top,
    double Height,
    bool IsSelected,
    IReadOnlyList<SectionLayer> Layers);

public sealed record SectionTick(double Left, double Top, string Label);

public sealed record SectionLabel(double Left, double Top, string Label);
