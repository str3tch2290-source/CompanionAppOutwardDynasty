using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Companion.Protocol;

namespace Companion.Core.Networking;

public sealed class TcpControlServer : IDisposable
{
    private readonly CompanionConfig _cfg;
    private readonly Func<ControlConnection, Task> _onConnection;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public TcpControlServer(CompanionConfig cfg, Func<ControlConnection, Task> onConnection)
    {
        _cfg = cfg;
        _onConnection = onConnection;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _cfg.TcpPort);
        _listener.Start();

        _ = Task.Run(async () =>
        {
            while (!_cts!.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync(_cts.Token); }
                catch { break; }

                _ = Task.Run(async () =>
                {
                    var conn = new ControlConnection(client);
                    await _onConnection(conn);
                });
            }
        });
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
    }
}

public sealed class ControlConnection : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    public ControlConnection(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
        _reader = new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);
        _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };
    }

    public EndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint;

    public async Task SendAsync(Envelope env, CancellationToken ct = default)
    {
        var line = env.ToJson();
        await _writer.WriteLineAsync(line.AsMemory(), ct);
    }

    public async Task<string?> ReadLineAsync(CancellationToken ct = default)
    {
        // StreamReader has no ct-aware ReadLineAsync until later; basic workaround:
        var t = _reader.ReadLineAsync();
        var done = await Task.WhenAny(t, Task.Delay(Timeout.InfiniteTimeSpan, ct));
        if (done != t) return null;
        return await t;
    }

    public void Dispose()
    {
        try { _writer.Dispose(); } catch { }
        try { _reader.Dispose(); } catch { }
        try { _stream.Dispose(); } catch { }
        try { _client.Dispose(); } catch { }
    }
}
