using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Companion.Core.Networking;

public sealed class LanDiscovery : IDisposable
{
    private readonly CompanionConfig _cfg;
    private UdpClient? _recv;
    private UdpClient? _send;
    private CancellationTokenSource? _cts;

    public LanDiscovery(CompanionConfig cfg)
    {
        _cfg = cfg;
    }

    public void Start()
    {
        if (!_cfg.EnableLanDiscovery) return;

        _cts = new CancellationTokenSource();

        _recv = new UdpClient(_cfg.DiscoveryPort);
        _recv.EnableBroadcast = true;

        _send = new UdpClient();
        _send.EnableBroadcast = true;

        // reply to discovery pings
        _ = Task.Run(async () =>
        {
            while (!_cts!.IsCancellationRequested)
            {
                UdpReceiveResult res;
                try { res = await _recv.ReceiveAsync(_cts.Token); }
                catch { break; }

                var msg = Encoding.UTF8.GetString(res.Buffer);
                if (!msg.StartsWith("ODM_DISCOVER")) continue;

                var reply = new
                {
                    name = _cfg.ServerName,
                    tcpPort = _cfg.TcpPort,
                    udpPort = _cfg.UdpPort,
                    discoveryPort = _cfg.DiscoveryPort,
                    v = 1
                };
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reply));
                try { await _recv.SendAsync(bytes, bytes.Length, res.RemoteEndPoint); } catch { }
            }
        });

        // periodic broadcast beacon (helps UIs list without ping)
        _ = Task.Run(async () =>
        {
            var ep = new IPEndPoint(IPAddress.Broadcast, _cfg.DiscoveryPort);
            while (!_cts!.IsCancellationRequested)
            {
                var beacon = new
                {
                    beacon = "ODM_BEACON",
                    name = _cfg.ServerName,
                    tcpPort = _cfg.TcpPort,
                    udpPort = _cfg.UdpPort,
                    v = 1
                };
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(beacon));
                try { await _send!.SendAsync(bytes, bytes.Length, ep); } catch { }
                try { await Task.Delay(TimeSpan.FromSeconds(2), _cts.Token); } catch { break; }
            }
        });
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _recv?.Dispose(); } catch { }
        try { _send?.Dispose(); } catch { }
    }
}
