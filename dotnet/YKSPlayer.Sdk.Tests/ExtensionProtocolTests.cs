using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;
using YKSPlayer.Sdk;
using YKSPlayer.Sdk.Extensions;

namespace YKSPlayer.Sdk.Tests;

public class ExtensionProtocolTests
{
    private sealed class Echo : Extension
    {
        public TrackInfo? Last;
        public override IReadOnlyList<PanelInfo> Panels => [new("p", "Panel")];
        public override IReadOnlyList<CommandInfo> Commands => [new("c", "Command")];
        public override bool ProvidesLyrics => true;

        public override async Task OnTrackChangedAsync(TrackInfo track)
        {
            Last = track;
            await Host.InvalidatePanelAsync("p");
        }

        public override Task<string?> ExecuteCommandAsync(string id) => Task.FromResult<string?>($"ran {id}");

        public override Task<Panel?> RenderPanelAsync(string id) =>
            Task.FromResult<Panel?>(new Panel("Now", [Block.KeyValue("title", Last?.Title ?? "-")]));

        public override Task<LyricsResult?> GetLyricsAsync(LyricsQuery q, CancellationToken ct) =>
            Task.FromResult<LyricsResult?>(q.Title == "Song" ? new LyricsResult("[00:01.00]hello") : null);
    }

    [Fact]
    public async Task FullConversation()
    {
        // Two pipes: app -> extension, extension -> app.
        using var toExtServer = new AnonymousPipeServerStream(PipeDirection.Out);
        using var toExtClient = new AnonymousPipeClientStream(PipeDirection.In, toExtServer.ClientSafePipeHandle);
        using var toAppServer = new AnonymousPipeServerStream(PipeDirection.Out);
        using var toAppClient = new AnonymousPipeClientStream(PipeDirection.In, toAppServer.ClientSafePipeHandle);

        var ext = new Echo();
        var run = Task.Run(() => ExtensionRunner.RunAsync(ext, toExtClient, toAppServer));

        await using var app = new JsonRpcPeer(toAppClient, toExtServer);
        var invalidated = new TaskCompletionSource<string>();
        app.OnNotification = (m, p) =>
        {
            if (m == Methods.InvalidatePanel) invalidated.TrySetResult(p!["id"]!.GetValue<string>());
            return Task.CompletedTask;
        };
        app.Start();
        var t = TimeSpan.FromSeconds(5);

        var init = (await app.RequestAsync(Methods.Initialize, new InitializeParams(1, "2.0.0", ["ui.panel"]), t)).Deserialize<InitializeResult>(PluginManifest.JsonOptions)!;
        Assert.Equal("p", init.Panels[0].Id);
        Assert.Contains("lyrics", init.Providers);

        await app.NotifyAsync(Methods.TrackChanged, new TrackInfo("夜明け", "YKS", "A", 200));
        Assert.Equal("p", await invalidated.Task.WaitAsync(t));

        var panel = (await app.RequestAsync(Methods.RenderPanel, new { id = "p" }, t)).Deserialize<Panel>(PluginManifest.JsonOptions)!;
        Assert.Equal("夜明け", panel.Blocks[0].Value);

        Assert.Equal("ran c", (await app.RequestAsync(Methods.ExecuteCommand, new { id = "c" }, t))!.GetValue<string>());
        var lyrics = await app.RequestAsync(Methods.GetLyrics, new LyricsQuery("Song", "", "", 1), t);
        Assert.Equal("[00:01.00]hello", lyrics!["lrc"]!.GetValue<string>());

        var err = await Assert.ThrowsAsync<JsonRpcException>(() => app.RequestAsync("nope", null, t));
        Assert.Contains("not found", err.Message);

        await app.RequestAsync(Methods.Shutdown, null, t);
        await run.WaitAsync(t);
    }
}
