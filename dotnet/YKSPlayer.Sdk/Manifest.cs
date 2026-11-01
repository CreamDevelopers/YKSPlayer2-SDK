using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace YKSPlayer.Sdk;

/// <summary>Kinds of plugins. See docs/manifest.md.</summary>
public static class PluginKinds
{
    public const string Decoder = "decoder";
    public const string Dsp = "dsp";
    public const string Visualizer = "visualizer";
    public const string Extension = "extension";
    /// <summary>Colours and an optional background image, declared in a JSON file (no code).</summary>
    public const string Theme = "theme";
    public static readonly string[] All = [Decoder, Dsp, Visualizer, Extension, Theme];
}

/// <summary>Permissions a plugin can ask for. The player shows them before enabling a plugin.</summary>
public static class Permissions
{
    public const string AudioDecode = "audio.decode";
    public const string AudioProcess = "audio.process";
    public const string PlaybackRead = "playback.read";
    public const string PlaybackControl = "playback.control";
    public const string LibraryRead = "library.read";
    public const string LyricsProvide = "lyrics.provide";
    public const string MetadataProvide = "metadata.provide";
    /// <summary>Receive the lyrics of the playing track to translate them (they usually go to a translation service, so net too).</summary>
    public const string LyricsTranslate = "lyrics.translate";
    /// <summary>Receive the lyrics of the playing track to spell out how they sound.</summary>
    public const string LyricsPronounce = "lyrics.pronounce";
    public const string UiPanel = "ui.panel";
    public const string UiCommand = "ui.command";
    /// <summary>Start helper programs (e.g. a downloader) inside the extension's sandbox; they inherit its limits.</summary>
    public const string ProcessRun = "process.run";
    /// <summary>Add audio files from the extension's data folder to the library.</summary>
    public const string LibraryWrite = "library.write";
    /// <summary>Offer streams (radio, cloud music) on the Sources page; playing them needs net too.</summary>
    public const string SourceProvide = "source.provide";
    /// <summary>Draw a visualizer from code (drawing operations sent each frame).</summary>
    public const string VisualizerDraw = "visualizer.draw";
    /// <summary>Network access. "net" alone means any host; "net:example.com" names one.</summary>
    public const string Net = "net";

    public static readonly string[] Known =
        [AudioDecode, AudioProcess, PlaybackRead, PlaybackControl, LibraryRead, LyricsProvide, MetadataProvide, UiPanel, UiCommand, Net, ProcessRun, LibraryWrite, SourceProvide, VisualizerDraw, LyricsTranslate, LyricsPronounce];

    public static bool IsKnown(string p) => Known.Contains(p) || p.StartsWith("net:", StringComparison.Ordinal);
}

public sealed class EngineRequirements
{
    /// <summary>SemVer range of YKSPlayer versions, e.g. ">=2.0.0 &lt;3.0.0".</summary>
    [JsonPropertyName("yksplayer")] public string YksPlayer { get; set; } = ">=2.0.0 <3.0.0";
    /// <summary>Native plugin ABI (yks_plugin.h YKS_PLUGIN_ABI). Required for decoder and dsp plugins.</summary>
    [JsonPropertyName("pluginAbi")] public int? PluginAbi { get; set; }
}

public sealed class DecoderInfo
{
    [JsonPropertyName("extensions")] public List<string> Extensions { get; set; } = [];
}

public sealed class VisualizerInfo
{
    /// <summary>Number of spectrum bins the shader samples (8..256).</summary>
    [JsonPropertyName("bins")] public int Bins { get; set; } = 64;
}

public sealed class ExtensionInfo
{
    /// <summary>"dotnet": entry is a .NET assembly run by YKSPlayer's extension host. "exe": entry is a program speaking the protocol on stdio.</summary>
    [JsonPropertyName("runtime")] public string Runtime { get; set; } = "dotnet";
}

/// <summary>One setting of an extension, shown as a switch, slider, text box or drop-down on its card.</summary>
public sealed class SettingDefinition
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    /// <summary>"bool", "number", "text" or "choice".</summary>
    [JsonPropertyName("type")] public string Type { get; set; } = "bool";
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("default")] public System.Text.Json.JsonElement? Default { get; set; }
    [JsonPropertyName("min")] public double? Min { get; set; }
    [JsonPropertyName("max")] public double? Max { get; set; }
    [JsonPropertyName("step")] public double? Step { get; set; }
    [JsonPropertyName("choices")] public List<SettingChoice> Choices { get; set; } = [];
}

public sealed class SettingChoice
{
    [JsonPropertyName("value")] public string Value { get; set; } = "";
    [JsonPropertyName("label")] public string Label { get; set; } = "";
}

/// <summary>yks-plugin.json.</summary>
public sealed partial class PluginManifest
{
    public const int CurrentSchema = 1;
    public const string FileName = "yks-plugin.json";
    public const string SignatureFileName = "yks-plugin.sig";

    [JsonPropertyName("schema")] public int Schema { get; set; } = CurrentSchema;
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("publisher")] public string Publisher { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("license")] public string License { get; set; } = "";
    [JsonPropertyName("repository")] public string? Repository { get; set; }
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("entry")] public string Entry { get; set; } = "";
    /// <summary>Optional square icon inside the package: .svg (preferred) or .png. Drawn as a full tile, so it brings its own background.</summary>
    [JsonPropertyName("icon")] public string? Icon { get; set; }
    [JsonPropertyName("engines")] public EngineRequirements Engines { get; set; } = new();
    [JsonPropertyName("permissions")] public List<string> Permissions { get; set; } = [];
    [JsonPropertyName("dependencies")] public Dictionary<string, string> Dependencies { get; set; } = [];
    [JsonPropertyName("decoder")] public DecoderInfo? Decoder { get; set; }
    [JsonPropertyName("visualizer")] public VisualizerInfo? Visualizer { get; set; }
    [JsonPropertyName("extension")] public ExtensionInfo? Extension { get; set; }
    [JsonPropertyName("categories")] public List<string> Categories { get; set; } = [];
    [JsonPropertyName("credits")] public string? Credits { get; set; }
    /// <summary>Settings the player shows on the plugin's card; values reach the extension (settings/changed).</summary>
    [JsonPropertyName("settings")] public List<SettingDefinition> Settings { get; set; } = [];
    /// <summary>Relative path -> lower-case hex SHA-256. Written by the packer; covers every file in the package.</summary>
    [JsonPropertyName("files")] public SortedDictionary<string, string> Files { get; set; } = new(StringComparer.Ordinal);

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static PluginManifest Parse(string json) =>
        JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions) ?? throw new FormatException("Empty manifest");

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Problems that make the manifest invalid (empty when it is fine).</summary>
    public IReadOnlyList<string> Validate()
    {
        var e = new List<string>();
        if (Schema != CurrentSchema) e.Add($"schema must be {CurrentSchema}");
        if (!IdPattern().IsMatch(Id)) e.Add("id must look like 'publisher.name' (lower case letters, digits, '-', dots)");
        if (string.IsNullOrWhiteSpace(Name)) e.Add("name is required");
        if (!SemVersion.TryParse(Version, out _)) e.Add("version must be SemVer (e.g. 1.2.0)");
        if (string.IsNullOrWhiteSpace(Publisher)) e.Add("publisher is required");
        if (!PluginKinds.All.Contains(Kind)) e.Add($"kind must be one of {string.Join(", ", PluginKinds.All)}");
        if (string.IsNullOrWhiteSpace(Entry) || !IsSafeRelativePath(Entry)) e.Add("entry must be a relative path inside the package");
        if (Icon is not null && (!IsSafeRelativePath(Icon) || !IconExtensions.Contains(Path.GetExtension(Icon).ToLowerInvariant())))
            e.Add("icon must be a .svg or .png path inside the package");
        if (!VersionRange.TryParse(Engines.YksPlayer, out _)) e.Add("engines.yksplayer must be a SemVer range such as \">=2.0.0 <3.0.0\"");
        foreach (var p in Permissions.Where(p => !Sdk.Permissions.IsKnown(p))) e.Add($"unknown permission '{p}'");
        foreach (var (dep, range) in Dependencies)
        {
            if (!IdPattern().IsMatch(dep)) e.Add($"dependency id '{dep}' is invalid");
            if (!VersionRange.TryParse(range, out _)) e.Add($"dependency range '{range}' is invalid");
        }
        switch (Kind)
        {
            case PluginKinds.Decoder:
                if (Engines.PluginAbi is null) e.Add("engines.pluginAbi is required for native plugins");
                if (Decoder is null || Decoder.Extensions.Count == 0) e.Add("decoder.extensions lists the file types the decoder handles");
                else if (Decoder.Extensions.Any(x => !x.StartsWith('.') || x.Length < 2)) e.Add("decoder extensions start with a dot, e.g. \".mod\"");
                if (!Entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) e.Add("a decoder entry is a .dll");
                break;
            case PluginKinds.Dsp:
                if (Engines.PluginAbi is null) e.Add("engines.pluginAbi is required for native plugins");
                if (!Entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) e.Add("an effect entry is a .dll");
                break;
            case PluginKinds.Visualizer:
                if (!Entry.EndsWith(".cso", StringComparison.OrdinalIgnoreCase)) e.Add("a visualizer entry is a compiled pixel shader (.cso)");
                if (Visualizer is { Bins: < 8 or > 256 }) e.Add("visualizer.bins must be between 8 and 256");
                break;
            case PluginKinds.Theme:
                if (!Entry.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) e.Add("a theme entry is its theme JSON file");
                break;
            case PluginKinds.Extension:
                var rt = Extension?.Runtime ?? "dotnet";
                if (rt is not ("dotnet" or "exe")) e.Add("extension.runtime is 'dotnet' or 'exe'");
                if (rt == "dotnet" && !Entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) e.Add("a .NET extension entry is the assembly (.dll)");
                if (rt == "exe" && !Entry.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) e.Add("an exe extension entry is an .exe");
                break;
        }
        foreach (var f in Files.Keys.Where(f => !IsSafeRelativePath(f))) e.Add($"file path '{f}' is not allowed");
        foreach (var d in Settings)
        {
            if (string.IsNullOrWhiteSpace(d.Id) || string.IsNullOrWhiteSpace(d.Label)) e.Add("every setting needs an id and a label");
            if (d.Type is not ("bool" or "number" or "text" or "choice")) e.Add($"setting '{d.Id}': type is bool, number, text or choice");
            if (d.Type == "choice" && d.Choices.Count == 0) e.Add($"setting '{d.Id}': a choice needs choices");
        }
        if (Settings.Select(d => d.Id).Distinct().Count() != Settings.Count) e.Add("setting ids must be unique");
        return e;
    }

    public static readonly string[] IconExtensions = [".svg", ".png"];
    /// <summary>Largest icon file the player will draw.</summary>
    public const int MaxIconBytes = 256 * 1024;

    /// <summary>No rooted paths, no "..", forward slashes only.</summary>
    public static bool IsSafeRelativePath(string p) =>
        p.Length > 0 && !p.Contains('\\') && !p.StartsWith('/') && !p.Contains(':') && p.Split('/').All(s => s.Length > 0 && s != "." && s != "..");

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]*(\.[a-z0-9][a-z0-9-]*)+$")]
    private static partial Regex IdPattern();
}
