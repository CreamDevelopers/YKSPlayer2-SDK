using System.Text.Json.Serialization;

namespace YKSPlayer.Sdk.Extensions;

// Wire types of the extension protocol (docs/extension-protocol.md). JSON-RPC 2.0, one message per line on stdio.

public sealed record TrackInfo(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("album")] string Album,
    [property: JsonPropertyName("durationSeconds")] double DurationSeconds,
    /// <summary>Only sent to extensions granted library.read.</summary>
    [property: JsonPropertyName("path")] string? Path = null);

public sealed record CommandInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    /// <summary>Segoe Fluent Icons glyph, e.g. "".</summary>
    [property: JsonPropertyName("glyph")] string? Glyph = null);

public sealed record PanelInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title);

/// <summary>A panel's content. The player renders the blocks with its own controls and colours.</summary>
public sealed record Panel(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("blocks")] IReadOnlyList<Block> Blocks);

/// <summary>One element of a panel. Exactly the fields of its <see cref="Type"/> are used.</summary>
public sealed record Block(
    /// <summary>"heading", "text", "keyValue", "list", "button", "progress", "input", "toggle", "image", "slider", "choice", "link".</summary>
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string? Text = null,
    [property: JsonPropertyName("key")] string? Key = null,
    [property: JsonPropertyName("value")] string? Value = null,
    [property: JsonPropertyName("items")] IReadOnlyList<string>? Items = null,
    /// <summary>For buttons: the command to run.</summary>
    [property: JsonPropertyName("command")] string? Command = null,
    /// <summary>For progress: 0..1.</summary>
    [property: JsonPropertyName("progress")] double? Progress = null,
    /// <summary>For inputs: grey hint text; for toggles: whether it is on.</summary>
    [property: JsonPropertyName("placeholder")] string? Placeholder = null,
    [property: JsonPropertyName("on")] bool? On = null,
    /// <summary>For links: an https URL; for images: an https URL or a file in the extension's data folder.</summary>
    [property: JsonPropertyName("url")] string? Url = null,
    /// <summary>For sliders.</summary>
    [property: JsonPropertyName("min")] double? Min = null,
    [property: JsonPropertyName("max")] double? Max = null)
{
    public static Block Heading(string text) => new("heading", Text: text);
    public static Block Paragraph(string text) => new("text", Text: text);
    public static Block KeyValue(string key, string value) => new("keyValue", Key: key, Value: value);
    public static Block List(IReadOnlyList<string> items) => new("list", Items: items);
    public static Block Button(string text, string command) => new("button", Text: text, Command: command);
    public static Block Bar(string text, double progress) => new("progress", Text: text, Progress: progress);
    /// <summary>A text box with a button; the command receives the text as its value.</summary>
    public static Block Input(string placeholder, string buttonText, string command) => new("input", Text: buttonText, Command: command, Placeholder: placeholder);
    /// <summary>An on/off switch; the command receives "on" or "off".</summary>
    public static Block Toggle(string text, bool on, string command) => new("toggle", Text: text, Command: command, On: on);
    /// <summary>A picture (up to 320 px tall): https URL or a file in the extension's data folder.</summary>
    public static Block Image(string url) => new("image", Url: url);
    /// <summary>A slider; the command receives the new value (invariant culture) when the user lets go.</summary>
    public static Block Slider(string text, double value, double min, double max, string command) =>
        new("slider", Text: text, Value: value.ToString(System.Globalization.CultureInfo.InvariantCulture), Command: command, Min: min, Max: max);
    /// <summary>A drop-down; the command receives the chosen item.</summary>
    public static Block Choice(string text, IReadOnlyList<string> items, string selected, string command) =>
        new("choice", Text: text, Items: items, Value: selected, Command: command);
    /// <summary>A link that opens an https page in the browser.</summary>
    public static Block Link(string text, string url) => new("link", Text: text, Url: url);
}

public sealed record LyricsQuery(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("album")] string Album,
    [property: JsonPropertyName("durationSeconds")] double DurationSeconds);

/// <summary>Lyrics in LRC format ("[mm:ss.xx]line"); plain text is shown unsynced.</summary>
public sealed record LyricsResult([property: JsonPropertyName("lrc")] string Lrc);

/// <summary>Lyric lines to translate, in order; empty lines are sent as "" and should come back as "".</summary>
public sealed record LyricsTranslateQuery(
    [property: JsonPropertyName("lines")] IReadOnlyList<string> Lines,
    /// <summary>Language to translate into: a BCP 47 tag such as "ja", "en", "zh-Hans".</summary>
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("artist")] string Artist);

/// <summary>One translation per line of the query, in the same order ("" where there is none).</summary>
public sealed record LyricsTranslateResult([property: JsonPropertyName("lines")] IReadOnlyList<string> Lines);

/// <summary>Lyric lines to spell out as they sound, in order; empty lines are sent as "" and should come back as "".</summary>
public sealed record LyricsPronounceQuery(
    [property: JsonPropertyName("lines")] IReadOnlyList<string> Lines,
    /// <summary>Script to write the sounds in: "ja-Kana" (katakana).</summary>
    [property: JsonPropertyName("script")] string Script,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("artist")] string Artist);

/// <summary>One reading per line of the query, in the same order ("" where there is none).</summary>
public sealed record LyricsPronounceResult([property: JsonPropertyName("lines")] IReadOnlyList<string> Lines);

public sealed record InitializeParams(
    [property: JsonPropertyName("apiVersion")] int ApiVersion,
    [property: JsonPropertyName("appVersion")] string AppVersion,
    [property: JsonPropertyName("permissions")] IReadOnlyList<string> Permissions,
    /// <summary>A folder only this extension uses, writable from its sandbox (settings, downloads, caches).</summary>
    [property: JsonPropertyName("dataDirectory")] string? DataDirectory = null,
    /// <summary>Current values of the manifest's settings.</summary>
    [property: JsonPropertyName("settings")] IReadOnlyDictionary<string, System.Text.Json.JsonElement>? Settings = null);

public sealed record InitializeResult(
    [property: JsonPropertyName("commands")] IReadOnlyList<CommandInfo> Commands,
    [property: JsonPropertyName("panels")] IReadOnlyList<PanelInfo> Panels,
    /// <summary>"lyrics", "metadata", "translation", "pronunciation".</summary>
    [property: JsonPropertyName("providers")] IReadOnlyList<string> Providers,
    [property: JsonPropertyName("trackCommands")] IReadOnlyList<TrackCommandInfo>? TrackCommands = null,
    [property: JsonPropertyName("sources")] IReadOnlyList<SourceInfo>? Sources = null,
    [property: JsonPropertyName("visualizers")] IReadOnlyList<VisualizerInfoRef>? Visualizers = null,
    /// <summary>Optional notifications to receive: "position" (once a second while playing), "queue".</summary>
    [property: JsonPropertyName("events")] IReadOnlyList<string>? Events = null);

public static class Methods
{
    public const int ApiVersion = 1;

    // app -> extension
    public const string Initialize = "initialize";
    public const string Shutdown = "shutdown";
    public const string TrackChanged = "playback/trackChanged";
    public const string StateChanged = "playback/stateChanged";
    public const string ExecuteCommand = "command/execute";
    public const string RenderPanel = "panel/render";
    public const string GetLyrics = "lyrics/get";

    // extension -> app
    public const string InvalidatePanel = "panel/invalidate";
    public const string Notify = "ui/notify";
    public const string Control = "playback/control";
    public const string Log = "log";
    /// <summary>Request: {"paths": [...], "play": bool} -> number of files added (needs library.write).</summary>
    public const string LibraryAdd = "library/add";

    // SDK 1.1, extension -> app
    public const string LibraryQuery = "library/query";
    public const string LibraryPlaylists = "library/playlists";
    public const string PlaylistTracks = "library/playlistTracks";
    public const string PlaylistCreate = "playlists/create";
    public const string PlaylistSetTracks = "playlists/setTracks";
    public const string QueueGet = "queue/get";
    public const string QueuePlay = "queue/play";
    public const string QueueAdd = "queue/add";
    public const string PlayItems = "queue/playItems";
    public const string Status = "playback/status";
    public const string Seek = "playback/seek";
    public const string Volume = "playback/volume";
    public const string OpenUrl = "ui/openUrl";

    // SDK 1.1, app -> extension
    public const string SettingsChanged = "settings/changed";
    public const string Position = "playback/position";
    public const string QueueChanged = "queue/changed";
    public const string Browse = "source/browse";
    public const string Resolve = "source/resolve";
    public const string RenderViz = "viz/render";
    public const string GetMetadata = "metadata/get";

    // SDK 1.2, app -> extension
    public const string TranslateLyrics = "lyrics/translate";
    public const string PronounceLyrics = "lyrics/pronounce";
}
