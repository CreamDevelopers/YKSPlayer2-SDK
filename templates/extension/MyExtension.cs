using YKSPlayer.Sdk.Extensions;

namespace MyExtension;

/// <summary>A panel that shows the current track. Grow it from here: commands, lyrics, ...</summary>
public sealed class MyExtension : Extension
{
    private TrackInfo? _track;

    public override IReadOnlyList<PanelInfo> Panels => [new("main", "MyExtension")];

    public override async Task OnTrackChangedAsync(TrackInfo track)
    {
        _track = track;
        await Host.InvalidatePanelAsync("main");
    }

    public override Task<Panel?> RenderPanelAsync(string id) => Task.FromResult<Panel?>(new Panel("MyExtension",
    [
        Block.Heading(_track?.Title ?? "Nothing playing"),
        Block.KeyValue("Artist", _track?.Artist ?? "-"),
    ]));
}
