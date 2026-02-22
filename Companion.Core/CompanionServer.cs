using System.Text.Json;
using Companion.Core.Networking;
using Companion.Core.Maps;
using Companion.Protocol;

namespace Companion.Core;

public sealed class CompanionServer : IDisposable
{
    public CompanionConfig Config { get; }
    public LobbyState Lobby { get; private set; }
    public VoteEngine Votes { get; } = new();

    private readonly object _gate = new();

    private TcpControlServer? _tcp;
    private UdpTelemetryServer? _udp;
    private LanDiscovery? _discovery;

    private HistoryStore? _history;
    private SnapshotStore? _snapshots;
    private TerritoryGridStore? _territory;

    public TerritoryGridStore Territory => _territory ??= new TerritoryGridStore(Lobby.DynastyId);

    public event Action<string>? Log;
    public event Action? StateChanged;

    public CompanionServer(CompanionConfig cfg)
    {
        Config = cfg;
        Lobby = new LobbyState(GenerateLobbyCode());
    }

    public void Start()
    {
        Directory.CreateDirectory(AppPaths.BaseDir);

        _tcp = new TcpControlServer(Config, HandleConnectionAsync);
        _tcp.Start();

        _udp = new UdpTelemetryServer(Config.UdpPort);
        _udp.PacketReceived += (_, __) => { /* future: update map markers */ };
        _udp.Start();

        _discovery = new LanDiscovery(Config);
        _discovery.Start();

        Log?.Invoke($"TCP listening on {Config.TcpPort}, UDP {Config.UdpPort}, discovery {Config.DiscoveryPort}");
        RaiseStateChanged();

        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(1000);
                BroadcastSessionState();
                TryResolveVote();
            }
        });
    }

    public void CreateOrSetLobby(string lobbyCode)
    {
        lock (_gate)
        {
            Lobby = new LobbyState(lobbyCode);
            Log?.Invoke($"Lobby set to {lobbyCode}");
        }
        RaiseStateChanged();
    }

    public void SetDynasty(string dynastyId)
    {
        lock (_gate)
        {
            Lobby.SetDynasty(dynastyId);
            _history?.Dispose();
            _history = new HistoryStore(dynastyId);
            _snapshots = new SnapshotStore(dynastyId);
            _territory = new TerritoryGridStore(dynastyId);
        }
        Log?.Invoke($"Dynasty set to {dynastyId}");
        RaiseStateChanged();
    }

    public void BeginHostMigrationVote(string reason = "host_down", int deadlineSec = 30)
    {
        List<string> candidates;
        lock (_gate)
        {
            candidates = Lobby.Clients.Keys.ToList();
            Lobby.SetPaused(true);
        }
        Broadcast(new Envelope { Op = "pause", Payload = JsonSerializer.SerializeToElement(new PausePayload { Mode="hard", Reason="host_migration" }, Envelope.JsonOptions) });
        Votes.Begin(reason, candidates, TimeSpan.FromSeconds(deadlineSec));
        Broadcast(new Envelope { Op = "vote_begin", Payload = JsonSerializer.SerializeToElement(new VoteBeginPayload { Reason=reason, Candidates=candidates, DeadlineSec=deadlineSec }, Envelope.JsonOptions) });
        RaiseStateChanged();
    }

    private void TryResolveVote()
    {
        if (!Votes.HasActive) return;
        HashSet<string> voters;
        lock (_gate) voters = Lobby.Clients.Keys.ToHashSet();
        var resolved = Votes.TryResolve(voters);
        if (resolved is null) return;

        var (winner, tally) = resolved.Value;
        lock (_gate) Lobby.SetHost(winner);

        Broadcast(new Envelope { Op="vote_result", Payload = JsonSerializer.SerializeToElement(new VoteResultPayload { Winner=winner, Tally=tally }, Envelope.JsonOptions) });
        Broadcast(new Envelope { Op="host_assign", Payload = JsonSerializer.SerializeToElement(new HostAssignPayload { Host=winner }, Envelope.JsonOptions) });

        // transfer latest snapshot (A)
        if (_snapshots is not null)
        {
            var latest = _snapshots.LoadLatest();
            if (latest is not null)
            {
                var (hash, doc) = latest.Value;
                var payload = new SnapshotTransferPayload
                {
                    DynastyId = Lobby.DynastyId,
                    Hash = hash,
                    Time = new Dictionary<string,int>(), // optional; stored in index too
                    Snapshot = doc.RootElement
                };
                // send directly to winner
                SendTo(winner, new Envelope { Op="snapshot_transfer", Payload = JsonSerializer.SerializeToElement(payload, Envelope.JsonOptions) });
            }
        }

        // remain paused until new host says host_ready
        RaiseStateChanged();
    }

    private void BroadcastSessionState()
    {
        Envelope env;
        lock (_gate)
        {
            env = new Envelope
            {
                Op = "session_state",
                Payload = JsonSerializer.SerializeToElement(Lobby.ToSessionStatePayload(), Envelope.JsonOptions)
            };
        }
        Broadcast(env);
    }

    private readonly Dictionary<string, ControlConnection> _connections = new();

    private async Task HandleConnectionAsync(ControlConnection conn)
    {
        Log?.Invoke($"Client connected: {conn.RemoteEndPoint}");

        string? playerId = null;

        try
        {
            using var cts = new CancellationTokenSource();
            while (true)
            {
                var line = await conn.ReadLineAsync(cts.Token);
                if (line is null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                Envelope env;
                try { env = Envelope.FromJson(line); }
                catch { continue; }

                // soft versioning
                if (env.Version != ProtocolVersion.Current)
                {
                    await conn.SendAsync(new Envelope
                    {
                        Op = "warn_version",
                        Payload = JsonSerializer.SerializeToElement(new WarnVersionPayload
                        {
                            ServerVersion = ProtocolVersion.Current,
                            ClientVersion = env.Version,
                            Message = "Protocol version mismatch; attempting soft compatibility."
                        }, Envelope.JsonOptions)
                    });
                }

                if (env.Op == "hello")
                {
                    var hello = env.PayloadAs<HelloPayload>();
                    if (hello is null) continue;

                    // auth token check
                    if (!string.Equals(hello.Token, Lobby.LobbyCode, StringComparison.OrdinalIgnoreCase))
                    {
                        await conn.SendAsync(new Envelope { Op="error", Payload = JsonSerializer.SerializeToElement(new { message="bad_token" }) });
                        break;
                    }

                    playerId = env.Sender ?? Guid.NewGuid().ToString("N");
                    lock (_gate)
                    {
                        var client = new ConnectedClient
                        {
                            PlayerId = playerId,
                            Name = hello.Name,
                            Role = hello.Role
                        };
                        Lobby.Clients[playerId] = client;
                        _connections[playerId] = conn;

                        if (hello.Role == "host" && Lobby.HostPlayerId is null)
                            Lobby.SetHost(playerId);

                        // lazily init stores once we know dynastyId (host can set later), but ensure something exists
                        _history ??= new HistoryStore(Lobby.DynastyId);
                        _snapshots ??= new SnapshotStore(Lobby.DynastyId);
                        _territory ??= new TerritoryGridStore(Lobby.DynastyId);
                    }

                    await conn.SendAsync(new Envelope
                    {
                        Op = "hello_ack",
                        Payload = JsonSerializer.SerializeToElement(new HelloAckPayload
                        {
                            SessionId = Guid.NewGuid().ToString("N"),
                            UdpPort = Config.UdpPort,
                            ServerTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                        }, Envelope.JsonOptions)
                    });

                    RaiseStateChanged();
                    continue;
                }

                if (playerId is null) continue;

                lock (_gate)
                {
                    if (Lobby.Clients.TryGetValue(playerId, out var c))
                        c.LastSeenAt = DateTimeOffset.UtcNow;
                }

                switch (env.Op)
                {
                    case "vote_cast":
                        {
                            var vc = env.PayloadAs<VoteCastPayload>();
                            if (vc is null) break;
                            Votes.Cast(playerId, vc.Candidate);
                            break;
                        }
                    case "host_ready":
                        {
                            lock (_gate) Lobby.SetPaused(false);
                            Broadcast(new Envelope { Op="unpause", Payload = JsonSerializer.SerializeToElement(new { }) });
                            RaiseStateChanged();
                            break;
                        }
                    case "set_dynasty":
                        {
                            var payload = env.Payload.Deserialize<Dictionary<string,string>>(Envelope.JsonOptions);
                            if (payload is not null && payload.TryGetValue("dynastyId", out var d))
                                SetDynasty(d);
                            break;
                        }
                    case "history_event":
                        {
                            var he = env.PayloadAs<HistoryEventPayload>();
                            if (he is null) break;
                            _history ??= new HistoryStore(he.DynastyId);
                            _history.Append(env.UnixTime, he.Type, env.Sender, he.Data);
                            break;
                        }
                    case "snapshot_push":
                        {
                            var sp = env.PayloadAs<SnapshotPushPayload>();
                            if (sp is null) break;
                            SetDynasty(sp.DynastyId);
                            var (hash, _) = _snapshots!.SaveSnapshot(sp.Snapshot, sp.Time);
                            await conn.SendAsync(new Envelope { Op="snapshot_ack", Payload = JsonSerializer.SerializeToElement(new SnapshotAckPayload { Hash=hash, Stored=true }, Envelope.JsonOptions) });
                            break;
                        }
                    case "territory_delta":
                        {
                            var td = env.PayloadAs<TerritoryDeltaPayload>();
                            if (td is null) break;

                            // Apply to territory grid (128x128 default). We treat q,r as x,y indices.
                            _territory ??= new TerritoryGridStore(Lobby.DynastyId);
                            var cells = td.Cells.Select(c => (c.Q, c.R, OwnerId.Make(c.Owner.Type, c.Owner.Id)));
                            _territory.ApplyCells(td.Region, cells);

                            // History log
                            _history ??= new HistoryStore(Lobby.DynastyId);
                            _history.Append(env.UnixTime, "territory_change", env.Sender, env.Payload);
                            break;
                        }
                }
            }
        }
        finally
        {
            if (playerId is not null)
            {
                lock (_gate)
                {
                    Lobby.Clients.Remove(playerId);
                    _connections.Remove(playerId);
                }
                RaiseStateChanged();
            }
            Log?.Invoke($"Client disconnected: {conn.RemoteEndPoint}");
        }
    }

    private void Broadcast(Envelope env)
    {
        List<ControlConnection> conns;
        lock (_gate) conns = _connections.Values.Distinct().ToList();
        foreach (var c in conns)
            _ = c.SendAsync(env);
    }

    private void SendTo(string playerId, Envelope env)
    {
        ControlConnection? c = null;
        lock (_gate) _connections.TryGetValue(playerId, out c);
        if (c is null) return;
        _ = c.SendAsync(env);
    }

    private void RaiseStateChanged() => StateChanged?.Invoke();

    private static string GenerateLobbyCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng = Random.Shared;
        return new string(Enumerable.Range(0, 6).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }

    public void Dispose()
    {
        _history?.Dispose();
        _udp?.Dispose();
        _discovery?.Dispose();
        _tcp?.Dispose();
    }
}
