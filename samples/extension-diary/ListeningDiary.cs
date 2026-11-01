using YKSPlayer.Sdk.Extensions;

namespace ListeningDiary;

/// <summary>
/// Sample extension: a small diary of today's listening, shown as a panel in the player.
/// Demonstrates events (track changes), a panel, a command and host notifications.
/// </summary>
public sealed class Diary : Extension
{
    private readonly List<(DateTime At, TrackInfo Track)> _entries = [];
    private DateTime _since = DateTime.Now;

    public override IReadOnlyList<PanelInfo> Panels => [new("today", "今日のリスニング日記")];

    public override IReadOnlyList<CommandInfo> Commands => [new("new-page", "日記を新しいページにする", "")];

    public override async Task OnTrackChangedAsync(TrackInfo track)
    {
        lock (_entries) _entries.Add((DateTime.Now, track));
        await Host.InvalidatePanelAsync("today");
    }

    public override async Task<string?> ExecuteCommandAsync(string id)
    {
        if (id != "new-page") return null;
        lock (_entries) _entries.Clear();
        _since = DateTime.Now;
        await Host.InvalidatePanelAsync("today");
        return "新しいページを開きました";
    }

    public override Task<Panel?> RenderPanelAsync(string id)
    {
        List<(DateTime At, TrackInfo Track)> e;
        lock (_entries) e = [.. _entries];
        var minutes = e.Sum(x => x.Track.DurationSeconds) / 60;
        var artists = e.Select(x => x.Track.Artist).Where(a => a.Length > 0).Distinct().Count();
        var blocks = new List<Block>
        {
            Block.Heading(e.Count == 0 ? "まだ何も聴いていません" : $"{e.Count} 曲 · 約 {minutes:0} 分"),
            Block.KeyValue("はじめた時刻", _since.ToString("HH:mm")),
            Block.KeyValue("アーティスト", $"{artists} 組"),
            Block.Bar("今日の目標 60 分", Math.Min(1, minutes / 60)),
        };
        if (e.Count > 0)
            blocks.Add(Block.List(e.TakeLast(6).Reverse().Select(x => $"{x.At:HH:mm}  {x.Track.Title} — {x.Track.Artist}").ToList()));
        blocks.Add(Block.Button("新しいページ", "new-page"));
        return Task.FromResult<Panel?>(new Panel("今日のリスニング日記", blocks));
    }
}
