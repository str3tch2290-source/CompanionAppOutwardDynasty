using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Companion.Core.Networking;

public sealed class UdpTelemetryServer : IDisposable
{
    private readonly int _port;
    private UdpClient? _udp;
    private CancellationTokenSource? _cts;

    public event Action<JsonDocument, IPEndPoint>? PacketReceived;

    public UdpTelemetryServer(int port)
    {
        _port = port;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _udp = new UdpClient(_port);
        _ = Task.Run(async () =>
        {
            while (!_cts!.IsCancellationRequested)
            {
                UdpReceiveResult res;
                try { res = await _udp.ReceiveAsync(_cts.Token); }
                catch { break; }

                try
                {
                    var json = JsonDocument.Parse(res.Buffer);
                    PacketReceived?.Invoke(json, res.RemoteEndPoint);
                }
                catch { /* ignore malformed */ }
            }
        });
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _udp?.Dispose(); } catch { }
    }
}
