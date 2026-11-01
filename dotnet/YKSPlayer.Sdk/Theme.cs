using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace YKSPlayer.Sdk;

/// <summary>
/// A theme plugin's entry file (kind "theme"): fixed colours instead of the ones taken from the cover, and an
/// optional background picture. Colours are "#rrggbb".
/// </summary>
public sealed partial class ThemeDefinition
{
    /// <summary>Deep background behind everything (keep it dark for the white text).</summary>
    [JsonPropertyName("background")] public string Background { get; set; } = "#1B1530";
    [JsonPropertyName("base")] public string Base { get; set; } = "#3B2A5C";
    /// <summary>Buttons, links, the visualizer.</summary>
    [JsonPropertyName("accent")] public string Accent { get; set; } = "#FF8A7A";
    [JsonPropertyName("glow")] public string Glow { get; set; } = "#FFC58A";
    [JsonPropertyName("text")] public string Text { get; set; } = "#F6F1FF";
    /// <summary>Colours of the drifting light (2 to 6).</summary>
    [JsonPropertyName("blobs")] public List<string> Blobs { get; set; } = ["#5B3B8C", "#C2577A", "#FF9E7A", "#2E2A6B"];
    /// <summary>Optional picture (.jpg/.png) in the package, drawn over the background colour.</summary>
    [JsonPropertyName("backgroundImage")] public string? BackgroundImage { get; set; }
    /// <summary>0..1, how strongly the picture shows.</summary>
    [JsonPropertyName("backgroundOpacity")] public double BackgroundOpacity { get; set; } = 0.5;

    public static ThemeDefinition Parse(string json) =>
        JsonSerializer.Deserialize<ThemeDefinition>(json, PluginManifest.JsonOptions) ?? throw new FormatException("Empty theme");

    public IReadOnlyList<string> Validate()
    {
        var e = new List<string>();
        foreach (var (name, c) in new[] { ("background", Background), ("base", Base), ("accent", Accent), ("glow", Glow), ("text", Text) })
            if (!Hex().IsMatch(c)) e.Add($"{name} must be a colour like #1b1530");
        if (Blobs.Count is < 2 or > 6 || Blobs.Any(b => !Hex().IsMatch(b))) e.Add("blobs: 2 to 6 colours like #5b3b8c");
        if (BackgroundImage is { } img && (!PluginManifest.IsSafeRelativePath(img) || Path.GetExtension(img).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png")))
            e.Add("backgroundImage must be a .jpg or .png inside the package");
        if (BackgroundOpacity is < 0 or > 1) e.Add("backgroundOpacity is between 0 and 1");
        return e;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex Hex();
}
