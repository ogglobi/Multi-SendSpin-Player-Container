using Sendspin.SDK.Audio;
using Sendspin.SDK.Models;
using static MultiRoomAudio.Audio.PulseAudio.PulseAudioNative;

namespace MultiRoomAudio.Audio.PulseAudio;

/// <summary>
/// IAudioPlayer implementation using direct PulseAudio pa_simple API.
/// Designed for both HAOS and Docker environments where PulseAudio is available.
/// </summary>
/// <remarks>
/// This player uses PulseAudio's simple synchronous API which avoids the
/// ALSA bridge layer that causes underruns in HAOS. Audio data is written
/// directly to the PulseAudio server on a dedicated thread.
/// </remarks>
public class PulseAudioPlayer : IAudioPlayer
{
    private readonly ILogger<PulseAudioPlayer> _logger;
    private readonly object _lock = new();

    private IntPtr _paHandle = IntPtr.Zero;
    private IAudioSampleSource? _sampleSource;
    private AudioFormat? _currentFormat;
    private string? _sinkName;
    private bool _disposed;

    private Thread? _playbackThread;
    private CancellationTokenSource? _playbackCts;
    private volatile bool _isPlaying;
    private volatile bool _isPaused;

    // Pre-allocated buffers for the playback thread
    private float[]? _sampleBuffer;
    private byte[]? _byteBuffer;

    /// <summary>
    /// Buffer size in milliseconds. Larger values increase latency but reduce underruns.
    /// </summary>
    private const int BufferMs = 50;

    /// <summary>
    /// Frames per write operation. At 48kHz, 1024 frames = ~21ms of audio.
    /// </summary>
    private const int FramesPerWrite = 1024;

    public AudioPlayerState State { get; private set; } = AudioPlayerState.Uninitialized;

    private volatile float _volume = 1.0f;
    private volatile bool _isMuted;

    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 1f);
    }

    public bool IsMuted
    {
        get => _isMuted;
        set => _isMuted = value;
    }

    public int OutputLatencyMs { get; private set; }

    public event EventHandler<AudioPlayerState>? StateChanged;
    public event EventHandler<AudioPlayerError>? ErrorOccurred;

    /// <summary>
    /// Initializes a new instance of the PulseAudioPlayer.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="sinkName">
    /// Optional PulseAudio sink name. If null, uses the default sink.
    /// Can be a sink name like "alsa_output.usb-..." or sink index.
    /// </param>
    public PulseAudioPlayer(ILogger<PulseAudioPlayer> logger, string? sinkName = null)
    {
        _logger = logger;
        _sinkName = sinkName;
    }

    public Task InitializeAsync(AudioFormat format, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            try
            {
                _logger.LogInformation(
                    "Initializing PulseAudio player with format: {SampleRate}Hz, {Channels}ch, sink: {Sink}",
                    format.SampleRate, format.Channels, _sinkName ?? "default");

                // Create sample spec for PulseAudio
                var sampleSpec = new SampleSpec
                {
                    Format = SampleFormat.FLOAT32LE,
                    Rate = (uint)format.SampleRate,
                    Channels = (byte)format.Channels
                };

                // Configure buffer attributes for low latency
                var targetLatencyBytes = BytesForMs(ref sampleSpec, BufferMs);
                var bufferAttr = new BufferAttr
                {
                    MaxLength = uint.MaxValue,  // Let server decide
                    TLength = targetLatencyBytes,
                    PreBuf = targetLatencyBytes / 2,
                    MinReq = uint.MaxValue,     // Let server decide
                    FragSize = uint.MaxValue    // Not used for playback
                };

                // Connect to PulseAudio
                _paHandle = SimpleNewWithAttr(
                    server: null,  // Use default server (PULSE_SERVER env var)
                    name: "MultiRoomAudio",
                    dir: StreamDirection.Playback,
                    dev: _sinkName,  // null = default sink
                    streamName: "Sendspin Audio",
                    ss: ref sampleSpec,
                    map: IntPtr.Zero,
                    attr: ref bufferAttr,
                    error: out var error);

                if (_paHandle == IntPtr.Zero)
                {
                    var errorMsg = GetErrorMessage(error);
                    throw new InvalidOperationException($"Failed to connect to PulseAudio: {errorMsg}");
                }

                _currentFormat = format;

                // Get actual latency
                var latencyUs = SimpleGetLatency(_paHandle, out var latencyError);
                if (latencyError != 0)
                {
                    _logger.LogWarning(
                        "Could not query PulseAudio latency: {Error}. Using fallback {BufferMs}ms",
                        GetErrorMessage(latencyError), BufferMs);
                    OutputLatencyMs = BufferMs;
                }
                else
                {
                    OutputLatencyMs = (int)(latencyUs / 1000);
                    if (OutputLatencyMs <= 0)
                    {
                        _logger.LogDebug(
                            "PulseAudio reported invalid latency {Latency}μs. Using fallback {BufferMs}ms",
                            latencyUs, BufferMs);
                        OutputLatencyMs = BufferMs;
                    }
                }

                // Pre-allocate buffers
                var samplesPerWrite = FramesPerWrite * format.Channels;
                _sampleBuffer = new float[samplesPerWrite];
                _byteBuffer = new byte[samplesPerWrite * sizeof(float)];

                SetState(AudioPlayerState.Stopped);

                _logger.LogInformation(
                    "PulseAudio player initialized. Sink: {Sink}, Latency: {Latency}ms",
                    _sinkName ?? "default", OutputLatencyMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize PulseAudio player");
                SetState(AudioPlayerState.Error);
                OnError("Initialization failed", ex);
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public void SetSampleSource(IAudioSampleSource source)
    {
        lock (_lock)
        {
            _sampleSource = source;
            _logger.LogDebug("Sample source set");
        }
    }

    public void Play()
    {
        lock (_lock)
        {
            if (_paHandle == IntPtr.Zero)
            {
                _logger.LogWarning("Cannot play - not initialized");
                return;
            }

            if (_isPlaying && !_isPaused)
            {
                _logger.LogDebug("Already playing");
                return;
            }

            if (_isPaused)
            {
                // Resume from pause
                _isPaused = false;
                SetState(AudioPlayerState.Playing);
                _logger.LogInformation("Playback resumed");
                return;
            }

            // Start playback thread
            _playbackCts = new CancellationTokenSource();
            _isPlaying = true;
            _isPaused = false;

            _playbackThread = new Thread(PlaybackLoop)
            {
                Name = "PulseAudio-Playback",
                IsBackground = true
            };

            try
            {
                _playbackThread.Priority = ThreadPriority.AboveNormal;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex,
                    "Could not set thread priority to AboveNormal. Audio may have higher latency. " +
                    "This is normal in containers without elevated privileges.");
            }

            _playbackThread.Start();

            SetState(AudioPlayerState.Playing);
            _logger.LogInformation("Playback started");
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (!_isPlaying)
                return;

            _isPaused = true;
            SetState(AudioPlayerState.Paused);
            _logger.LogInformation("Playback paused");
        }
    }

    public void Stop()
    {
        Thread? threadToJoin = null;

        lock (_lock)
        {
            if (!_isPlaying)
                return;

            _isPlaying = false;
            _isPaused = false;
            _playbackCts?.Cancel();
            threadToJoin = _playbackThread;
        }

        // Wait for thread outside lock - increased timeout for safety
        if (threadToJoin != null)
        {
            if (!threadToJoin.Join(TimeSpan.FromSeconds(5)))
            {
                _logger.LogWarning(
                    "Playback thread did not exit within 5 seconds, may be hung in native call");
            }
        }

        lock (_lock)
        {
            _playbackThread = null;
            _playbackCts?.Dispose();
            _playbackCts = null;

            // Flush any remaining audio
            if (_paHandle != IntPtr.Zero)
            {
                SimpleFlush(_paHandle, out _);
            }

            SetState(AudioPlayerState.Stopped);
            _logger.LogInformation("Playback stopped");
        }
    }

    public async Task SwitchDeviceAsync(string? deviceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Switching PulseAudio sink to: {Sink}", deviceId ?? "default");

        var wasPlaying = State == AudioPlayerState.Playing;
        var savedSource = _sampleSource;
        var savedFormat = _currentFormat;
        var savedSinkName = _sinkName;
        IntPtr oldHandle = IntPtr.Zero;

        // Stop current playback
        Stop();

        // Save the old handle but don't free it yet (in case we need to restore)
        lock (_lock)
        {
            oldHandle = _paHandle;
            _paHandle = IntPtr.Zero;
        }

        // Update sink name
        _sinkName = deviceId;

        // Reinitialize with new sink
        if (savedFormat != null)
        {
            try
            {
                await InitializeAsync(savedFormat, cancellationToken);

                // Only free old handle after successful initialization
                if (oldHandle != IntPtr.Zero)
                {
                    SimpleFree(oldHandle);
                    oldHandle = IntPtr.Zero;
                }

                if (savedSource != null)
                {
                    SetSampleSource(savedSource);
                }

                if (wasPlaying)
                {
                    Play();
                }

                _logger.LogInformation("Device switch complete");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Device switch failed, restoring previous state");

                // Restore the old handle on failure
                lock (_lock)
                {
                    if (_paHandle == IntPtr.Zero && oldHandle != IntPtr.Zero)
                    {
                        _paHandle = oldHandle;
                        oldHandle = IntPtr.Zero; // Prevent cleanup
                        _sinkName = savedSinkName;
                        SetState(AudioPlayerState.Stopped);
                    }
                }

                OnError("Device switch failed", ex);
                throw;
            }
            finally
            {
                // Clean up old handle if not restored
                if (oldHandle != IntPtr.Zero)
                {
                    SimpleFree(oldHandle);
                }
            }
        }
    }

    private void PlaybackLoop()
    {
        _logger.LogDebug("Playback thread started");

        try
        {
            var ct = _playbackCts?.Token ?? CancellationToken.None;

            while (_isPlaying && !ct.IsCancellationRequested)
            {
                if (_isPaused)
                {
                    // When paused, sleep briefly and check again
                    Thread.Sleep(10);
                    continue;
                }

                var source = _sampleSource;
                var buffer = _sampleBuffer;
                var byteBuffer = _byteBuffer;

                if (source == null || buffer == null || byteBuffer == null || _paHandle == IntPtr.Zero)
                {
                    // No source or not initialized - output silence
                    Thread.Sleep(10);
                    continue;
                }

                // Read samples from source
                var samplesRead = source.Read(buffer, 0, buffer.Length);

                if (samplesRead == 0)
                {
                    // No data available - small sleep to avoid busy loop
                    Thread.Sleep(1);
                    continue;
                }

                // Apply volume and mute
                var vol = IsMuted ? 0f : Volume;
                for (int i = 0; i < samplesRead; i++)
                {
                    buffer[i] *= vol;
                }

                // Convert float samples to bytes
                Buffer.BlockCopy(buffer, 0, byteBuffer, 0, samplesRead * sizeof(float));

                // Write to PulseAudio
                unsafe
                {
                    fixed (byte* ptr = byteBuffer)
                    {
                        var result = SimpleWrite(
                            _paHandle,
                            (IntPtr)ptr,
                            (UIntPtr)(samplesRead * sizeof(float)),
                            out var error);

                        if (result < 0)
                        {
                            var errorMsg = GetErrorMessage(error);
                            _logger.LogError("PulseAudio write error: {Error}", errorMsg);

                            // Don't spam errors - brief pause before retry
                            Thread.Sleep(10);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in playback thread");
            OnError("Playback error", ex);
        }

        _logger.LogDebug("Playback thread exited");
    }

    private void SetState(AudioPlayerState newState)
    {
        if (State != newState)
        {
            var oldState = State;
            State = newState;
            _logger.LogDebug("State changed: {OldState} -> {NewState}", oldState, newState);
            StateChanged?.Invoke(this, newState);
        }
    }

    private void OnError(string message, Exception? ex = null)
    {
        ErrorOccurred?.Invoke(this, new AudioPlayerError(message, ex));
    }

    public ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            if (_disposed)
                return ValueTask.CompletedTask;

            _disposed = true;
        }

        // Stop playback (waits for thread)
        Stop();

        IntPtr handleToFree;
        lock (_lock)
        {
            // Capture the handle and set to zero BEFORE freeing
            // This ensures the playback thread sees the zero and skips writes
            handleToFree = _paHandle;
            _paHandle = IntPtr.Zero;

            _sampleBuffer = null;
            _byteBuffer = null;
        }

        // Free the handle outside the lock (safe - playback thread has stopped)
        if (handleToFree != IntPtr.Zero)
        {
            SimpleFree(handleToFree);
        }

        _logger.LogInformation("PulseAudio player disposed");
        return ValueTask.CompletedTask;
    }
}
