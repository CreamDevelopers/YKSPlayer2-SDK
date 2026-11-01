using System.Text.Json;
using System.Text.Json.Nodes;

namespace YKSPlayer.Sdk.Extensions;

/// <summary>
/// Serves an <see cref="Extension"/> over the protocol. YKSPlayer's extension host calls this for "dotnet"
/// extensions; an "exe" extension calls it from Main:
/// <code>await ExtensionRunner.RunAsync(new MyExtension(), Console.OpenStandardInput(), Console.OpenStandardOutput());</code>
/// </summary>
public static class ExtensionRunner
{
    public static async Task RunAsync(Extension extension, Stream input, Stream output)
    {
        await using var peer = new JsonRpcPeer(input, output);
        var host = new Host(peer);
        extension.Attach(host);
        var done = new TaskCompletionSource();
        peer.OnRequest = async (method, prm, ct) =>
        {
            switch (method)
            {
                case Methods.Initialize:
                    var init = prm.Deserialize<InitializeParams>(PluginManifest.JsonOptions) ?? throw new JsonRpcException("bad initialize");
                    if (init.ApiVersion != Methods.ApiVersion) throw new JsonRpcException($"API version {init.ApiVersion} is not supported");
                    host.Permissions = init.Permissions;
                    host.DataDirectory = init.DataDirectory;
                    host.Settings = new ExtensionSettings(init.Settings ?? new Dictionary<string, System.Text.Json.JsonElement>());
                    await extension.OnStartAsync();
                    return JsonRpcPeer.ToNode(new InitializeResult(extension.Commands, extension.Panels,
                        Providers(extension),
                        extension.TrackCommands, extension.Sources, extension.Visualizers, extension.Events));
                case Methods.ExecuteCommand when prm is not null && prm["trackIds"] is JsonArray ids:
                    return JsonValue.Create(await extension.ExecuteTrackCommandAsync(prm["id"]?.GetValue<string>() ?? "",
                        [.. ids.Select(n => n?.GetValue<long>() ?? 0)]));
                case Methods.Browse:
                    return JsonRpcPeer.ToNode(await extension.BrowseAsync(prm?["source"]?.GetValue<string>() ?? "", prm?["path"]?.GetValue<string>() ?? ""));
                case Methods.Resolve:
                    return JsonValue.Create(await extension.ResolveAsync(prm?["source"]?.GetValue<string>() ?? "", prm?["id"]?.GetValue<string>() ?? ""));
                case Methods.RenderViz:
                    var frame = prm.Deserialize<VizFrame>(PluginManifest.JsonOptions) ?? throw new JsonRpcException("bad frame");
                    return JsonRpcPeer.ToNode((await extension.RenderVisualizerAsync(frame))?.Ops ?? []);
                case Methods.GetMetadata:
                    var mq = prm.Deserialize<MetadataQuery>(PluginManifest.JsonOptions) ?? throw new JsonRpcException("bad metadata query");
                    return JsonRpcPeer.ToNode(await extension.GetMetadataAsync(mq, ct));
                case Methods.ExecuteCommand:
                    return JsonValue.Create(await extension.ExecuteCommandAsync(prm?["id"]?.GetValue<string>() ?? "", prm?["value"]?.GetValue<string>()));
                case Methods.RenderPanel:
                    return JsonRpcPeer.ToNode(await extension.RenderPanelAsync(prm?["id"]?.GetValue<string>() ?? ""));
                case Methods.GetLyrics:
                    var q = prm.Deserialize<LyricsQuery>(PluginManifest.JsonOptions) ?? throw new JsonRpcException("bad lyrics query");
                    return JsonRpcPeer.ToNode(await extension.GetLyricsAsync(q, ct));
                case Methods.TranslateLyrics:
                    var tq = prm.Deserialize<LyricsTranslateQuery>(PluginManifest.JsonOptions) ?? throw new JsonRpcException("bad translate query");
                    return JsonRpcPeer.ToNode(await extension.TranslateLyricsAsync(tq, ct));
                case Methods.PronounceLyrics:
                    var pq = prm.Deserialize<LyricsPronounceQuery>(PluginManifest.JsonOptions) ?? throw new JsonRpcException("bad pronounce query");
                    return JsonRpcPeer.ToNode(await extension.PronounceLyricsAsync(pq, ct));
                case Methods.Shutdown:
                    done.TrySetResult();
                    return null;
                default:
                    throw new JsonRpcException($"Method {method} not found");
            }
        };
        peer.OnNotification = async (method, prm) =>
        {
            try
            {
                switch (method)
                {
                    case Methods.TrackChanged:
                        if (prm.Deserialize<TrackInfo>(PluginManifest.JsonOptions) is { } t) await extension.OnTrackChangedAsync(t);
                        break;
                    case Methods.StateChanged:
                        await extension.OnStateChangedAsync(prm?["state"]?.GetValue<string>() ?? "");
                        break;
                    case Methods.SettingsChanged:
                        host.Settings = new ExtensionSettings(prm.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(PluginManifest.JsonOptions) ?? []);
                        await extension.OnSettingsChangedAsync();
                        break;
                    case Methods.Position:
                        await extension.OnPositionAsync(prm?["position"]?.GetValue<double>() ?? 0, prm?["duration"]?.GetValue<double>() ?? 0);
                        break;
                    case Methods.QueueChanged:
                        await extension.OnQueueChangedAsync();
                        break;
                }
            }
            catch (Exception e)
            {
                await host.LogAsync($"{method} failed: {e.Message}");
            }
        };
        peer.Start();
        await Task.WhenAny(peer.Completion, done.Task);
    }

    private static List<string> Providers(Extension e)
    {
        var p = new List<string>();
        if (e.ProvidesLyrics) p.Add("lyrics");
        if (e.ProvidesMetadata) p.Add("metadata");
        if (e.TranslatesLyrics) p.Add("translation");
        if (e.PronouncesLyrics) p.Add("pronunciation");
        return p;
    }

    private sealed class Host(JsonRpcPeer peer) : IExtensionHost
    {
        public IReadOnlyList<string> Permissions { get; set; } = [];
        public Task InvalidatePanelAsync(string panelId) => peer.NotifyAsync(Methods.InvalidatePanel, new { id = panelId });
        public Task NotifyAsync(string message) => peer.NotifyAsync(Methods.Notify, new { message });
        public Task ControlPlaybackAsync(string action) => peer.NotifyAsync(Methods.Control, new { action });
        public Task LogAsync(string message) => peer.NotifyAsync(Methods.Log, new { message });
        public string? DataDirectory { get; set; }
        public ExtensionSettings Settings { get; set; } = new(new Dictionary<string, System.Text.Json.JsonElement>());

        private async Task<T> Ask<T>(string method, object? prm)
        {
            var r = await peer.RequestAsync(method, prm, TimeSpan.FromSeconds(30));
            return r.Deserialize<T>(PluginManifest.JsonOptions) ?? throw new JsonRpcException($"{method}: empty reply");
        }

        private Task Tell(string method, object? prm) => peer.RequestAsync(method, prm, TimeSpan.FromSeconds(30));

        public Task<IReadOnlyList<TrackRef>> QueryLibraryAsync(string? text, int limit, int offset) =>
            Ask<IReadOnlyList<TrackRef>>(Methods.LibraryQuery, new { text, limit, offset });
        public Task<IReadOnlyList<PlaylistRef>> PlaylistsAsync() => Ask<IReadOnlyList<PlaylistRef>>(Methods.LibraryPlaylists, null);
        public Task<IReadOnlyList<TrackRef>> PlaylistTracksAsync(long playlistId) => Ask<IReadOnlyList<TrackRef>>(Methods.PlaylistTracks, new { id = playlistId });
        public Task<long> CreatePlaylistAsync(string name, IReadOnlyList<long> trackIds) => Ask<long>(Methods.PlaylistCreate, new { name, trackIds });
        public Task SetPlaylistTracksAsync(long playlistId, IReadOnlyList<long> trackIds) => Tell(Methods.PlaylistSetTracks, new { id = playlistId, trackIds });
        public Task<QueueInfo> QueueAsync() => Ask<QueueInfo>(Methods.QueueGet, null);
        public Task<PlaybackStatus> StatusAsync() => Ask<PlaybackStatus>(Methods.Status, null);
        public Task PlayAsync(IReadOnlyList<long> trackIds, int start) => Tell(Methods.QueuePlay, new { trackIds, start });
        public Task EnqueueAsync(IReadOnlyList<long> trackIds, bool next) => Tell(Methods.QueueAdd, new { trackIds, next });
        public Task PlayItemsAsync(IReadOnlyList<SourceItem> items, int start) => Tell(Methods.PlayItems, new { items, start });
        public Task SeekAsync(double seconds) => Tell(Methods.Seek, new { seconds });
        public Task SetVolumeAsync(double volume) => Tell(Methods.Volume, new { volume });
        public Task OpenUrlAsync(string url) => Tell(Methods.OpenUrl, new { url });

        public async Task<int> AddToLibraryAsync(IReadOnlyList<string> paths, bool play)
        {
            var r = await peer.RequestAsync(Methods.LibraryAdd, new { paths, play }, TimeSpan.FromSeconds(30));
            return r?.GetValue<int>() ?? 0;
        }
    }
}
