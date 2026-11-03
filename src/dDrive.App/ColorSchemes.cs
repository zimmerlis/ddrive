using System.Windows.Media;

namespace dDrive.App;

public sealed record ColorScheme(string Id, string Label, System.Windows.Media.Color AccentColor);

public static class ColorSchemes
{
    public static readonly ColorScheme Ocean = new("ocean", "Ozean", System.Windows.Media.Color.FromRgb(73, 151, 176));
    public static readonly ColorScheme Forest = new("forest", "Wald", System.Windows.Media.Color.FromRgb(46, 125, 50));
    public static readonly ColorScheme Graphite = new("graphite", "Graphit", System.Windows.Media.Color.FromRgb(96, 110, 128));

    public static IReadOnlyList<ColorScheme> All { get; } = [Ocean, Forest, Graphite];

    public static ColorScheme FromId(string? id) =>
        All.FirstOrDefault(scheme => string.Equals(scheme.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Ocean;
}
