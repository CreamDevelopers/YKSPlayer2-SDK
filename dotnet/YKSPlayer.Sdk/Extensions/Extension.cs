namespace YKSPlayer.Sdk.Extensions;

/// <summary>What an extension may ask of the player. Calls the extension is not permitted to make are refused.</summary>
public interface IExtensionHost
{
    /// <summary>Permissions the user granted (a subset of the manifest's).</summary>
    IReadOnlyList<string> Permissions { get; }

    /// <summary>Asks the player to render a panel again (needs ui.panel).</summary>
    Task InvalidatePanelAsync(string panelId);

    /// <summary>Shows a short message (needs ui.panel or ui.command).</summary>
    Task NotifyAsync(string message);

    /// <summary>"play", "pause", "next", "previous" (needs playback.control).</summary>
    Task ControlPlaybackAsync(string action);

    Task LogAsync(string message);

    /// <summary>Values of the settings declared in the manifest (updated before OnSettingsChangedAsync).</summary>
    ExtensionSettings Settings { get; }

    // Library and playlists (library.read to read, library.write to change)
    Task<IReadOnlyList<TrackRef>> QueryLibraryAsync(string? text = null, int limit = 50, int offset = 0);
    Task<IReadOnlyList<PlaylistRef>> PlaylistsAsync();
    Task<IReadOnlyList<TrackRef>> PlaylistTracksAsync(long playlistId);
    Task<long> CreatePlaylistAsync(string name, IReadOnlyList<long> trackIds);
    Task SetPlaylistTracksAsync(long playlistId, IReadOnlyList<long> trackIds);

    // Queue and playback (playback.read to read, playback.control to change)
    Task<QueueInfo> QueueAsync();
    Task<PlaybackStatus> StatusAsync();
    /// <summary>Replaces the queue with library tracks and plays from <paramref name="start"/>.</summary>
    Task PlayAsync(IReadOnlyList<long> trackIds, int start = 0);
    Task EnqueueAsync(IReadOnlyList<long> trackIds, bool next = false);
    /// <summary>Plays stream items (http(s) URLs), e.g. from a source; needs playback.control and net.</summary>
    Task PlayItemsAsync(IReadOnlyList<SourceItem> items, int start = 0);
    Task SeekAsync(double seconds);
    Task SetVolumeAsync(double volume);

    /// <summary>Opens an https page in the user's browser (needs ui.command).</summary>
    Task OpenUrlAsync(string url);

    /// <summary>The extension's own writable folder (null for older players).</summary>
    string? DataDirectory { get; }

    /// <summary>
    /// Adds audio files to the library and optionally plays them (needs library.write). Only files inside
    /// <see cref="DataDirectory"/> are accepted. Returns how many were added.
    /// </summary>
    Task<int> AddToLibraryAsync(IReadOnlyList<string> paths, bool play);
}

/// <summary>
/// Base class for YKSPlayer extensions. Override what you need; everything has a harmless default.
/// The player runs extensions in a separate low-integrity process and talks to them over JSON-RPC, so a slow or
/// crashing extension never affects playback.
/// </summary>
public abstract class Extension
{
    protected IExtensionHost Host { get; private set; } = null!;

    internal void Attach(IExtensionHost host) => Host = host;

    public virtual IReadOnlyList<CommandInfo> Commands => [];
    public virtual IReadOnlyList<PanelInfo> Panels => [];
    public virtual bool ProvidesLyrics => false;
    /// <summary>Fill in missing covers and names (needs metadata.provide).</summary>
    public virtual bool ProvidesMetadata => false;
    /// <summary>Translate lyrics; the player shows them under each line (needs lyrics.translate).</summary>
    public virtual bool TranslatesLyrics => false;
    /// <summary>Spell lyrics as they sound (katakana); shown under each line, apart from translations (needs lyrics.pronounce).</summary>
    public virtual bool PronouncesLyrics => false;
    public virtual IReadOnlyList<TrackCommandInfo> TrackCommands => [];
    public virtual IReadOnlyList<SourceInfo> Sources => [];
    public virtual IReadOnlyList<VisualizerInfoRef> Visualizers => [];
    /// <summary>"position", "queue": extra notifications to receive.</summary>
    public virtual IReadOnlyList<string> Events => [];

    public virtual Task OnSettingsChangedAsync() => Task.CompletedTask;
    public virtual Task OnPositionAsync(double position, double duration) => Task.CompletedTask;
    public virtual Task OnQueueChangedAsync() => Task.CompletedTask;

    /// <summary>A track command from the context menu, with the library ids of the selected tracks.</summary>
    public virtual Task<string?> ExecuteTrackCommandAsync(string id, IReadOnlyList<long> trackIds) => Task.FromResult<string?>(null);

    /// <summary>Lists a folder of a source ("" is the top). Needs source.provide.</summary>
    public virtual Task<IReadOnlyList<SourceItem>> BrowseAsync(string sourceId, string path) => Task.FromResult<IReadOnlyList<SourceItem>>([]);

    /// <summary>The playable URL of a track listed without one.</summary>
    public virtual Task<string?> ResolveAsync(string sourceId, string itemId) => Task.FromResult<string?>(null);

    /// <summary>Draws one frame of a visualizer (needs visualizer.draw). Keep it under ~10 ms.</summary>
    public virtual Task<DrawList?> RenderVisualizerAsync(VizFrame frame) => Task.FromResult<DrawList?>(null);

    public virtual Task<MetadataResult?> GetMetadataAsync(MetadataQuery query, CancellationToken ct) => Task.FromResult<MetadataResult?>(null);

    public virtual Task OnStartAsync() => Task.CompletedTask;
    public virtual Task OnTrackChangedAsync(TrackInfo track) => Task.CompletedTask;
    public virtual Task OnStateChangedAsync(string state) => Task.CompletedTask;

    /// <summary>Runs a command; return a message to show, or null.</summary>
    public virtual Task<string?> ExecuteCommandAsync(string id) => Task.FromResult<string?>(null);

    /// <summary>A command with a value: the text of an input block, or "on"/"off" from a toggle.</summary>
    public virtual Task<string?> ExecuteCommandAsync(string id, string? value) => ExecuteCommandAsync(id);

    public virtual Task<Panel?> RenderPanelAsync(string id) => Task.FromResult<Panel?>(null);

    /// <summary>Lyrics for a track that has none of its own (needs lyrics.provide).</summary>
    public virtual Task<LyricsResult?> GetLyricsAsync(LyricsQuery query, CancellationToken ct) => Task.FromResult<LyricsResult?>(null);

    /// <summary>Translations of the lyrics of the playing track, one per line (needs lyrics.translate).</summary>
    public virtual Task<LyricsTranslateResult?> TranslateLyricsAsync(LyricsTranslateQuery query, CancellationToken ct) => Task.FromResult<LyricsTranslateResult?>(null);

    /// <summary>How each line of the playing track's lyrics sounds, one per line (needs lyrics.pronounce).</summary>
    public virtual Task<LyricsPronounceResult?> PronounceLyricsAsync(LyricsPronounceQuery query, CancellationToken ct) => Task.FromResult<LyricsPronounceResult?>(null);
}
