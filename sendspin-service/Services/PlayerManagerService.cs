using System.Collections.Concurrent;
using Sendspin.SDK.Audio;
using Sendspin.SDK.Client;
using Sendspin.SDK.Connection;
using Sendspin.SDK.Discovery;
using Sendspin.SDK.Models;
using Sendspin.SDK.Synchronization;
using SendspinService.Audio;
using SendspinService.Models;

namespace SendspinService.Services;

/// <summary>
/// Manages the lifecycle of all Sendspin players.
/// Handles creation, connection, state management, and disposal.
/// </summary>
public class PlayerManagerService : IHostedService, IDisposable
{
    private readonly ILogger<PlayerManagerService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, PlayerContext> _players = new();
    private readonly MdnsServerDiscovery _serverDiscovery;
    private bool _disposed;

    /// <summary>
    /// Internal context holding all objects for a player instance.
    /// </summary>
    private record PlayerContext(
        ISendspinClient Client,
        SendspinConnection Connection,
        IAudioPipeline Pipeline,
        IAudioPlayer Player,
        IClockSynchronizer ClockSync,
        ClientCapabilities Capabilities,
        PlayerConfig Config,
        DateTime CreatedAt,
        CancellationTokenSource Cts
    )
    {
        public PlayerState State { get; set; } = PlayerState.Created;
        public string? ErrorMessage { get; set; }
        public DateTime? ConnectedAt { get; set; }
        public long SamplesPlayed { get; set; }
    }

    public PlayerManagerService(
        ILogger<PlayerManagerService> logger,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _serverDiscovery = new MdnsServerDiscovery(
            loggerFactory.CreateLogger<MdnsServerDiscovery>());
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PlayerManagerService started");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PlayerManagerService stopping, disposing all players...");

        var tasks = _players.Keys.Select(name => StopPlayerAsync(name)).ToArray();
        await Task.WhenAll(tasks);

        _logger.LogInformation("PlayerManagerService stopped");
    }

    /// <summary>
    /// Creates and starts a new player with the given configuration.
    /// </summary>
    public async Task<PlayerResponse> CreatePlayerAsync(PlayerCreateRequest request, CancellationToken ct = default)
    {
        if (_players.ContainsKey(request.Name))
        {
            throw new InvalidOperationException($"Player '{request.Name}' already exists");
        }

        // Validate device if specified
        if (!PortAudioDeviceEnumerator.ValidateDevice(request.Device, out var deviceError))
        {
            throw new ArgumentException(deviceError);
        }

        _logger.LogInformation("Creating player '{Name}' with device '{Device}'",
            request.Name, request.Device ?? "default");

        try
        {
            // 1. Create capabilities with player role
            var capabilities = new ClientCapabilities
            {
                ClientId = request.ClientId ?? GenerateClientId(request.Name),
                ClientName = request.Name,
                Roles = new List<string> { "player@v1" },
                AudioFormats = GetDefaultFormats(),
                BufferCapacity = 32_000_000 // 32MB
            };

            // 2. Create clock synchronizer
            var clockSync = new KalmanClockSynchronizer(
                _loggerFactory.CreateLogger<KalmanClockSynchronizer>());

            // 3. Create audio player (PortAudio for Linux)
            var player = new PortAudioPlayer(
                _loggerFactory.CreateLogger<PortAudioPlayer>(),
                request.Device);

            // 4. Create audio pipeline with proper factories
            var decoderFactory = new AudioDecoderFactory();
            var pipeline = new AudioPipeline(
                _loggerFactory.CreateLogger<AudioPipeline>(),
                decoderFactory,
                clockSync,
                bufferFactory: (format, sync) => new TimedAudioBuffer(format, sync, bufferCapacityMs: 8000),
                playerFactory: () => player,
                sourceFactory: (buffer, timeFunc) => new BufferedAudioSampleSource(buffer, timeFunc));

            // 5. Create WebSocket connection
            var connection = new SendspinConnection(
                _loggerFactory.CreateLogger<SendspinConnection>());

            // 6. Create SDK client
            var client = new SendspinClientService(
                _loggerFactory.CreateLogger<SendspinClientService>(),
                connection,
                clockSync,
                capabilities,
                pipeline);

            // 7. Create config for tracking
            var config = new PlayerConfig
            {
                Name = request.Name,
                DeviceId = request.Device,
                ClientId = capabilities.ClientId,
                ServerUrl = request.ServerUrl,
                Volume = request.Volume,
                DelayMs = request.DelayMs
            };

            // 8. Create context
            var cts = new CancellationTokenSource();
            var context = new PlayerContext(
                client, connection, pipeline, player, clockSync, capabilities, config,
                DateTime.UtcNow, cts)
            {
                State = PlayerState.Created
            };

            // 9. Wire up events
            WireEvents(request.Name, context);

            // 10. Store context
            _players[request.Name] = context;

            // 11. Start connection in background
            _ = ConnectPlayerAsync(request.Name, context, ct);

            return CreateResponse(request.Name, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create player '{Name}'", request.Name);
            throw;
        }
    }

    /// <summary>
    /// Gets the current status of a player.
    /// </summary>
    public PlayerResponse? GetPlayer(string name)
    {
        if (!_players.TryGetValue(name, out var context))
            return null;

        return CreateResponse(name, context);
    }

    /// <summary>
    /// Gets all active players.
    /// </summary>
    public PlayersListResponse GetAllPlayers()
    {
        var players = _players
            .Select(kvp => CreateResponse(kvp.Key, kvp.Value))
            .ToList();

        return new PlayersListResponse(players, players.Count);
    }

    /// <summary>
    /// Hot-switches the audio device for a player without stopping playback.
    /// </summary>
    public async Task<bool> SwitchDeviceAsync(string name, string? newDeviceId, CancellationToken ct = default)
    {
        if (!_players.TryGetValue(name, out var context))
            return false;

        // Validate new device
        if (!PortAudioDeviceEnumerator.ValidateDevice(newDeviceId, out var error))
        {
            throw new ArgumentException(error);
        }

        _logger.LogInformation("Hot-switching player '{Name}' to device '{Device}'",
            name, newDeviceId ?? "default");

        try
        {
            await context.Pipeline.SwitchDeviceAsync(newDeviceId, ct);
            context.Config.DeviceId = newDeviceId;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch device for player '{Name}'", name);
            throw;
        }
    }

    /// <summary>
    /// Sets the volume for a player (0-100).
    /// </summary>
    public async Task<bool> SetVolumeAsync(string name, int volume, CancellationToken ct = default)
    {
        if (!_players.TryGetValue(name, out var context))
            return false;

        volume = Math.Clamp(volume, 0, 100);
        _logger.LogDebug("Setting volume for '{Name}' to {Volume}", name, volume);

        try
        {
            await context.Client.SetVolumeAsync(volume);
            context.Config.Volume = volume;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set volume for player '{Name}'", name);
            throw;
        }
    }

    /// <summary>
    /// Sets the mute state for a player.
    /// </summary>
    public bool SetMuted(string name, bool muted)
    {
        if (!_players.TryGetValue(name, out var context))
            return false;

        context.Pipeline.SetMuted(muted);
        return true;
    }

    /// <summary>
    /// Stops and removes a player.
    /// </summary>
    public async Task<bool> StopPlayerAsync(string name)
    {
        if (!_players.TryRemove(name, out var context))
            return false;

        _logger.LogInformation("Stopping player '{Name}'", name);

        try
        {
            context.Cts.Cancel();

            await context.Client.DisconnectAsync("Player stopped");
            await context.Pipeline.StopAsync();
            await context.Client.DisposeAsync();
            await context.Pipeline.DisposeAsync();
            await context.Player.DisposeAsync();
            await context.Connection.DisposeAsync();

            _logger.LogInformation("Player '{Name}' stopped and disposed", name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping player '{Name}'", name);
            return true; // Still consider it stopped
        }
    }

    /// <summary>
    /// Restarts a player (stops and recreates with same config).
    /// </summary>
    public async Task<PlayerResponse?> RestartPlayerAsync(string name, CancellationToken ct = default)
    {
        if (!_players.TryGetValue(name, out var context))
            return null;

        var config = context.Config;

        await StopPlayerAsync(name);

        var request = new PlayerCreateRequest
        {
            Name = config.Name,
            Device = config.DeviceId,
            ClientId = config.ClientId,
            ServerUrl = config.ServerUrl,
            Volume = config.Volume,
            DelayMs = config.DelayMs
        };

        return await CreatePlayerAsync(request, ct);
    }

    /// <summary>
    /// Pauses playback for a player.
    /// </summary>
    public void PausePlayer(string name)
    {
        if (_players.TryGetValue(name, out var context))
        {
            context.Player.Pause();
        }
    }

    /// <summary>
    /// Resumes playback for a player.
    /// </summary>
    public void ResumePlayer(string name)
    {
        if (_players.TryGetValue(name, out var context))
        {
            context.Player.Play();
        }
    }

    private async Task ConnectPlayerAsync(string name, PlayerContext context, CancellationToken ct)
    {
        try
        {
            context.State = PlayerState.Starting;

            // Discover server if URL not provided
            Uri? serverUri = null;
            if (!string.IsNullOrEmpty(context.Config.ServerUrl))
            {
                serverUri = new Uri(context.Config.ServerUrl);
            }
            else
            {
                _logger.LogInformation("Discovering Sendspin servers via mDNS...");
                var servers = await _serverDiscovery.ScanAsync(
                    TimeSpan.FromSeconds(5), ct);

                var server = servers.FirstOrDefault();
                if (server == null)
                {
                    throw new InvalidOperationException("No Sendspin servers found via mDNS discovery");
                }

                // Construct WebSocket URI from discovered server
                var host = server.IpAddresses.FirstOrDefault() ?? server.Host;
                serverUri = new Uri($"ws://{host}:{server.Port}/sendspin");
                _logger.LogInformation("Found server: {ServerName} at {Uri}",
                    server.Name, serverUri);
            }

            // Connect
            context.State = PlayerState.Connecting;
            await context.Client.ConnectAsync(serverUri!, ct);

            context.State = PlayerState.Connected;
            context.ConnectedAt = DateTime.UtcNow;

            _logger.LogInformation("Player '{Name}' connected to server", name);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Player '{Name}' connection cancelled", name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect player '{Name}'", name);
            context.State = PlayerState.Error;
            context.ErrorMessage = ex.Message;
        }
    }

    private void WireEvents(string name, PlayerContext context)
    {
        context.Client.ConnectionStateChanged += (_, args) =>
        {
            _logger.LogDebug("Player '{Name}' connection state: {State}", name, args.NewState);

            context.State = args.NewState switch
            {
                ConnectionState.Connected => PlayerState.Connected,
                ConnectionState.Disconnected => PlayerState.Stopped,
                _ => context.State
            };
        };

        context.Pipeline.StateChanged += (_, state) =>
        {
            _logger.LogDebug("Player '{Name}' pipeline state: {State}", name, state);

            if (state == AudioPipelineState.Playing)
                context.State = PlayerState.Playing;
            else if (state == AudioPipelineState.Buffering)
                context.State = PlayerState.Buffering;
        };

        context.Pipeline.ErrorOccurred += (_, error) =>
        {
            _logger.LogError(error.Exception, "Player '{Name}' pipeline error: {Message}",
                name, error.Message);
            context.ErrorMessage = error.Message;
        };

        context.Player.ErrorOccurred += (_, error) =>
        {
            _logger.LogError(error.Exception, "Player '{Name}' audio error: {Message}",
                name, error.Message);
            context.ErrorMessage = error.Message;
        };
    }

    private PlayerResponse CreateResponse(string name, PlayerContext context)
    {
        var bufferStats = context.Pipeline.BufferStats;

        return new PlayerResponse(
            Name: name,
            State: context.State,
            Device: context.Config.DeviceId,
            ClientId: context.Capabilities.ClientId,
            ServerUrl: context.Config.ServerUrl,
            Volume: context.Config.Volume,
            IsMuted: context.Player.IsMuted,
            DelayMs: context.Config.DelayMs,
            OutputLatencyMs: context.Player.OutputLatencyMs,
            CreatedAt: context.CreatedAt,
            ConnectedAt: context.ConnectedAt,
            ErrorMessage: context.ErrorMessage,
            IsClockSynced: context.Client.IsClockSynced,
            Metrics: bufferStats != null ? new PlayerMetrics(
                BufferLevel: (int)bufferStats.BufferedMs,
                BufferCapacity: (int)bufferStats.TargetMs,
                SamplesPlayed: context.SamplesPlayed,
                Underruns: bufferStats.UnderrunCount,
                Overruns: bufferStats.OverrunCount
            ) : null
        );
    }

    private static string GenerateClientId(string name)
    {
        // Generate deterministic client ID from name (matches CLI behavior)
        var hash = name.GetHashCode().ToString("x8");
        var safeName = new string(name.ToLower()
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .ToArray());

        return $"sendspin-{safeName}-{hash[..4]}";
    }

    private static List<AudioFormat> GetDefaultFormats()
    {
        return new List<AudioFormat>
        {
            new AudioFormat { Codec = "opus", SampleRate = 48000, Channels = 2, Bitrate = 256 },
            new AudioFormat { Codec = "pcm", SampleRate = 48000, Channels = 2, BitDepth = 16 },
            new AudioFormat { Codec = "flac", SampleRate = 48000, Channels = 2 }
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var context in _players.Values)
        {
            context.Cts.Cancel();
            try
            {
                context.Client.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
                context.Pipeline.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
                context.Player.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
                context.Connection.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
            }
            catch { }
        }

        _players.Clear();
    }
}
