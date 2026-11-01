using System.Text.Json;
using System.Text.Json.Serialization;

namespace YKSPlayer.Sdk.Extensions;

// Wire types added in SDK 1.1 (extension protocol 1, additive): library, queue, playlists, settings,
// track commands, streaming sources, drawn visualizers and metadata. See docs/extension-protocol.md.

/// <summary>A track as extensions see it. <see cref="Path"/> only with library.read; <see cref="Uri"/> for streams.</summary>
public sealed record TrackRef(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("album")] string Album,
    [property: JsonPropertyName("durationSeconds")] double DurationSeconds,
    [property: JsonPropertyName("path")] string? Path = null,
    [property: JsonPropertyName("uri")] string? Uri = null);

public sealed record PlaylistRef(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("count")] int Count);

public sealed record QueueInfo(
    /// <summary>Index of the current track in <see cref="Tracks"/>, -1 when none.</summary>
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("tracks")] IReadOnlyList<TrackRef> Tracks);

public sealed record PlaybackStatus(
    /// <summary>"playing", "paused" or "stopped".</summary>
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("position")] double Position,
    [property: JsonPropertyName("duration")] double Duration,
    [property: JsonPropertyName("volume")] double Volume,
    [property: JsonPropertyName("track")] TrackRef? Track);

/// <summary>A command shown in the context menu of tracks (library, queue). Its value is the track ids as JSON.</summary>
public sealed record TrackCommandInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("glyph")] string? Glyph = null);

// ---------------------------------------------------------------- streaming sources

/// <summary>A browsable source of streams (internet radio, a cloud library, ...), shown on the Sources page.</summary>
public sealed record SourceInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("glyph")] string? Glyph = null);

/// <summary>An entry in a source: a folder to open or a track to play.</summary>
public sealed record SourceItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    /// <summary>"folder" or "track".</summary>
    [property: JsonPropertyName("kind")] string Kind = "track",
    [property: JsonPropertyName("subtitle")] string? Subtitle = null,
    /// <summary>For tracks: http(s) URL of the audio. Leave null to be asked later (source/resolve), e.g. for expiring links.</summary>
    [property: JsonPropertyName("uri")] string? Uri = null,
    [property: JsonPropertyName("artist")] string? Artist = null,
    [property: JsonPropertyName("album")] string? Album = null,
    /// <summary>0 for live streams.</summary>
    [property: JsonPropertyName("durationSeconds")] double DurationSeconds = 0,
    /// <summary>Cover: an https URL or a file in the extension's data folder.</summary>
    [property: JsonPropertyName("artwork")] string? Artwork = null)
{
    public static SourceItem Folder(string id, string title, string? subtitle = null) => new(id, title, "folder", subtitle);
    public static SourceItem Track(string id, string title, string? uri, string? artist = null, double durationSeconds = 0, string? artwork = null) =>
        new(id, title, "track", artist, uri, artist, null, durationSeconds, artwork);
}

// ---------------------------------------------------------------- drawn visualizers

public sealed record VisualizerInfoRef(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name);

/// <summary>What the player hands a visualizer each frame (about 30 times a second).</summary>
public sealed record VizFrame(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("width")] float Width,
    [property: JsonPropertyName("height")] float Height,
    [property: JsonPropertyName("time")] double Time,
    /// <summary>32 spectrum bands, 0..1, low to high.</summary>
    [property: JsonPropertyName("bands")] float[] Bands,
    /// <summary>Beat pulse and chorus flash, 0..1.</summary>
    [property: JsonPropertyName("pulse")] float Pulse,
    [property: JsonPropertyName("flash")] float Flash,
    /// <summary>Theme colours as "#rrggbb".</summary>
    [property: JsonPropertyName("accent")] string Accent,
    [property: JsonPropertyName("glow")] string Glow);

/// <summary>
/// One drawing operation. Coordinates are in device-independent pixels from the top left; colours "#rrggbb" or
/// "#rrggbbaa". op: "rect", "line", "circle", "poly", "text".
/// </summary>
public sealed record DrawOp(
    [property: JsonPropertyName("op")] string Op,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("x")] float X = 0,
    [property: JsonPropertyName("y")] float Y = 0,
    [property: JsonPropertyName("w")] float W = 0,
    [property: JsonPropertyName("h")] float H = 0,
    /// <summary>circle radius, rect corner radius.</summary>
    [property: JsonPropertyName("r")] float R = 0,
    /// <summary>Stroke width; 0 fills the shape.</summary>
    [property: JsonPropertyName("stroke")] float Stroke = 0,
    /// <summary>poly: x0,y0,x1,y1,...; line: x1,y1,x2,y2.</summary>
    [property: JsonPropertyName("points")] float[]? Points = null,
    [property: JsonPropertyName("closed")] bool Closed = false,
    [property: JsonPropertyName("text")] string? Text = null,
    [property: JsonPropertyName("size")] float Size = 14);

/// <summary>Builds a frame's drawing operations.</summary>
public sealed class DrawList
{
    private readonly List<DrawOp> _ops = [];
    public IReadOnlyList<DrawOp> Ops => _ops;

    public DrawList Rect(float x, float y, float w, float h, string color, float radius = 0, float stroke = 0)
    {
        _ops.Add(new DrawOp("rect", color, x, y, w, h, radius, stroke));
        return this;
    }

    public DrawList Circle(float x, float y, float r, string color, float stroke = 0)
    {
        _ops.Add(new DrawOp("circle", color, x, y, R: r, Stroke: stroke));
        return this;
    }

    public DrawList Line(float x1, float y1, float x2, float y2, string color, float width = 1)
    {
        _ops.Add(new DrawOp("line", color, Stroke: width, Points: [x1, y1, x2, y2]));
        return this;
    }

    public DrawList Poly(IEnumerable<(float X, float Y)> points, string color, float stroke = 0, bool closed = true)
    {
        _ops.Add(new DrawOp("poly", color, Stroke: stroke, Points: [.. points.SelectMany(p => new[] { p.X, p.Y })], Closed: closed));
        return this;
    }

    public DrawList Text(float x, float y, string text, string color, float size = 14)
    {
        _ops.Add(new DrawOp("text", color, x, y, Text: text, Size: size));
        return this;
    }

    /// <summary>"#rrggbb" + alpha (0..1) -> "#rrggbbaa".</summary>
    public static string WithAlpha(string rgb, double alpha) => rgb[..7] + ((int)Math.Round(Math.Clamp(alpha, 0, 1) * 255)).ToString("x2");
}

// ---------------------------------------------------------------- metadata

public sealed record MetadataQuery(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("album")] string Album,
    [property: JsonPropertyName("durationSeconds")] double DurationSeconds,
    /// <summary>Whether the track already has a cover.</summary>
    [property: JsonPropertyName("hasCover")] bool HasCover);

/// <summary>What a metadata provider found. The player only fills what is missing.</summary>
public sealed record MetadataResult(
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("artist")] string? Artist = null,
    [property: JsonPropertyName("album")] string? Album = null,
    /// <summary>JPEG or PNG: a file in the extension's data folder.</summary>
    [property: JsonPropertyName("coverFile")] string? CoverFile = null);

// ---------------------------------------------------------------- settings

/// <summary>Current values of the settings the manifest declares (manifest "settings"), by id.</summary>
public sealed class ExtensionSettings(IReadOnlyDictionary<string, JsonElement> values)
{
    public IReadOnlyDictionary<string, JsonElement> Values => values;
    public bool Bool(string id, bool fallback = false) => values.TryGetValue(id, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : fallback;
    public double Number(string id, double fallback = 0) => values.TryGetValue(id, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;
    public string Text(string id, string fallback = "") => values.TryGetValue(id, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;
}
