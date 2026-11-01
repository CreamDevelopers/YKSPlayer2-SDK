using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YKSPlayer.Sdk.Extensions;

/// <summary>
/// Minimal JSON-RPC 2.0 peer over newline-delimited JSON. Used on both sides: by extensions (ExtensionRunner)
/// and by the player. Incoming requests are dispatched to <see cref="OnRequest"/>; replies are matched by id.
/// </summary>
public sealed class JsonRpcPeer : IAsyncDisposable
{
    private readonly StreamReader _in;
    private readonly Stream _out;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonNode?>> _pending = new();
    private long _nextId;
    private Task? _loop;
    private readonly CancellationTokenSource _cts = new();

    public JsonRpcPeer(Stream input, Stream output)
    {
        _in = new StreamReader(input, new UTF8Encoding(false));
        _out = output;
    }

    /// <summary>(method, params) -> result. Throw <see cref="JsonRpcException"/> to answer with an error.</summary>
    public Func<string, JsonNode?, CancellationToken, Task<JsonNode?>>? OnRequest { get; set; }

    /// <summary>Notifications (no reply expected).</summary>
    public Func<string, JsonNode?, Task>? OnNotification { get; set; }

    /// <summary>Completes when the other side closes the stream (or Stop is called).</summary>
    public Task Completion => _loop ?? Task.CompletedTask;

    public void Start() => _loop = Task.Run(ReadLoopAsync);

    public async Task<JsonNode?> RequestAsync(string method, object? @params, TimeSpan timeout, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        try
        {
            await WriteAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = ToNode(@params) });
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            timeoutCts.CancelAfter(timeout);
            await using (timeoutCts.Token.Register(() => tcs.TrySetCanceled()))
                return await tcs.Task;
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    public Task NotifyAsync(string method, object? @params) =>
        WriteAsync(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method, ["params"] = ToNode(@params) });

    public static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        JsonNode n => n,
        _ => JsonSerializer.SerializeToNode(value, value.GetType(), PluginManifest.JsonOptions),
    };

    private async Task WriteAsync(JsonObject msg)
    {
        var line = msg.ToJsonString() + "\n";
        var bytes = Encoding.UTF8.GetBytes(line);
        await _writeLock.WaitAsync();
        try
        {
            await _out.WriteAsync(bytes);
            await _out.FlushAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var line = await _in.ReadLineAsync(_cts.Token);
                if (line is null) break;
                if (line.Length == 0) continue;
                if (line.Length > 4 * 1024 * 1024) continue;  // refuse absurd messages
                JsonNode? msg;
                try
                {
                    msg = JsonNode.Parse(line);
                }
                catch (JsonException)
                {
                    continue;
                }
                if (msg is not JsonObject o) continue;
                if (o.ContainsKey("method"))
                {
                    var method = o["method"]!.GetValue<string>();
                    var prm = o["params"];
                    if (o.TryGetPropertyValue("id", out var idNode) && idNode is not null)
                        _ = Task.Run(() => AnswerAsync(idNode.DeepClone(), method, prm?.DeepClone()));
                    else if (OnNotification is { } n)
                        _ = Task.Run(() => n(method, prm?.DeepClone()));
                }
                else if (o.TryGetPropertyValue("id", out var rid) && rid is not null && _pending.TryGetValue(rid.GetValue<long>(), out var tcs))
                {
                    if (o["error"] is JsonObject err) tcs.TrySetException(new JsonRpcException(err["message"]?.GetValue<string>() ?? "error"));
                    else tcs.TrySetResult(o["result"]?.DeepClone());
                }
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or OperationCanceledException)
        {
        }
        foreach (var p in _pending.Values) p.TrySetException(new IOException("The connection closed"));
    }

    private async Task AnswerAsync(JsonNode id, string method, JsonNode? prm)
    {
        var reply = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id };
        try
        {
            var result = OnRequest is null ? throw new JsonRpcException($"Method {method} not found") : await OnRequest(method, prm, _cts.Token);
            reply["result"] = result;
        }
        catch (Exception e)
        {
            reply["error"] = new JsonObject { ["code"] = e is JsonRpcException ? -32000 : -32603, ["message"] = e.Message };
        }
        try
        {
            await WriteAsync(reply);
        }
        catch (IOException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_loop is not null)
        {
            try
            {
                await _loop.WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch (TimeoutException)
            {
            }
        }
        _cts.Dispose();
    }
}

public sealed class JsonRpcException(string message) : Exception(message);
