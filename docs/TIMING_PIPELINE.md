# MultiRoomAudio Timing Pipeline

This document explains how MultiRoomAudio handles each step of the timing adjustments for synchronized multi-room audio playback.

---

## The Timing Challenge

Audio comes from Music Assistant **timestamped** - each audio chunk has a specific time it should be played. We have an **unknown amount of latency** that our software + hardware stack introduces:

```
┌──────────────────────┐
│   Music Assistant    │
│   Server Clock       │──────────────────┐
└──────────────────────┘                  │
         │                                │
         │ "Play this audio at            │ Clock Sync
         │  timestamp T=12345.678s"       │ (NTP-like)
         │                                │
         ▼                                ▼
┌──────────────────────────────────────────────────────┐
│                 MultiRoomAudio                        │
│                                                       │
│  ┌─────────────┐    ┌──────────────┐    ┌─────────┐  │
│  │ Network     │───►│ Decode &     │───►│ ALSA    │  │
│  │ Buffer      │    │ Resample     │    │ Output  │  │
│  │ (latency 1) │    │ (latency 2)  │    │ (lat 3) │  │
│  └─────────────┘    └──────────────┘    └─────────┘  │
│                                                       │
│  Total latency = lat1 + lat2 + lat3 + ???            │
└──────────────────────────────────────────────────────┘
         │
         ▼
    ┌─────────┐
    │ Speaker │  ← Audio plays at actual time T'
    └─────────┘

    If T' ≠ T, we're out of sync!
```

---

## Step 1: Clock Synchronization

**Purpose**: Establish a shared time reference between server and client.

### How It Works

The SDK's `KalmanClockSynchronizer` implements an NTP-like protocol:

1. **Ping-Pong Measurement**:
   ```
   Client sends ping at local time T1
   Server receives at server time T2
   Server sends pong at server time T3
   Client receives at local time T4

   Round-trip time = (T4 - T1) - (T3 - T2)
   Clock offset ≈ ((T2 - T1) + (T3 - T4)) / 2
   ```

2. **Kalman Filter**: Refines offset estimate over multiple measurements
3. **Drift Estimation**: Tracks how fast our clock diverges from server's

### Our Configuration

```csharp
var clockSync = new KalmanClockSynchronizer(logger);
```

### Key Metrics (from Stats for Nerds)

| Metric | What It Means |
|--------|---------------|
| `ClockOffsetMs` | Our clock vs server (can be huge, just needs to be stable) |
| `UncertaintyMs` | Confidence interval (lower = more precise) |
| `DriftRatePpm` | Parts-per-million clock drift (typical: 1-50 ppm) |
| `IsDriftReliable` | True after enough measurements for stable drift estimate |
| `MeasurementCount` | Number of sync measurements taken |

### What "Converged" Means

The SDK considers sync "minimal" after just 2 measurements (`HasMinimalSync`), enabling fast startup. Full convergence takes more measurements for lower uncertainty.

---

## Step 2: Audio Buffering with Timestamps

**Purpose**: Hold audio until it's time to play, handle jitter.

### How It Works

The SDK's `TimedAudioBuffer` receives audio chunks with timestamps:

```
┌───────────────────────────────────────────────────────┐
│               TimedAudioBuffer                         │
│                                                        │
│  Capacity: 8000ms    Target: 250ms                    │
│                                                        │
│  ┌─────────────────────────────────────────────────┐  │
│  │ T=100ms │ T=110ms │ T=120ms │ ... │ T=350ms │   │  │
│  └─────────────────────────────────────────────────┘  │
│           ▲                               ▲           │
│           │                               │           │
│        Read pointer              Write pointer        │
│        (current playback)        (incoming audio)     │
└───────────────────────────────────────────────────────┘
```

### Buffer Behavior

1. **On Write**: Audio chunks are added with their server timestamps
2. **On Read**: Buffer checks current synchronized time
   - If read timestamp < current time: **We're behind** → speed up
   - If read timestamp > current time: **We're ahead** → slow down or wait

### Sendspin Protocol Specifics

- Server can send audio up to **5 seconds ahead**
- Our "Target" (250ms) is really a **minimum buffer** for smooth playback
- Buffer levels of 4000-5000ms are normal during stable playback

### Our Configuration

```csharp
bufferCapacityMs: 8000,  // Maximum buffer size
syncOptions: PulseAudioSyncOptions  // Custom sync tuning
buffer.TargetBufferMilliseconds = 250;  // Faster startup
```

---

## Step 3: Sync Error Detection

**Purpose**: Determine how far off our playback timing is.

### How Sync Error Is Calculated

```
Sync Error = Wall Clock Elapsed Time - Samples Consumed Time

Where:
- Wall Clock Elapsed = Current sync time - Playback start time
- Samples Consumed Time = Total samples read / Sample rate
```

**Example**:
```
Started playing at T=0
After 10 seconds (wall clock):
- If we've read 480,000 samples at 48kHz = 10.0 seconds of audio
- Sync error = 10.0s - 10.0s = 0ms ✓

- If we've read 479,040 samples = 9.98 seconds of audio
- Sync error = 10.0s - 9.98s = +20ms (we're behind, playing old audio)

- If we've read 480,960 samples = 10.02 seconds of audio
- Sync error = 10.0s - 10.02s = -20ms (we're ahead, playing too fast)
```

### Important Note on Output Latency

**Output latency is NOT used in sync error calculation!**

The `OutputLatencyMs` we report (from ALSA buffer size) is used for:
- Diagnostics in Stats for Nerds
- Knowing when audio will actually reach the speaker

But the SDK calculates sync error purely from sample counts vs wall clock time.

---

## Step 4: Sync Correction (Three Tiers)

**Purpose**: Correct timing errors when detected.

### Tier 1: Deadband (No Correction)

If sync error is within deadband, do nothing:
```csharp
EntryDeadbandMicroseconds = 5_000,  // 5ms to enter correction
ExitDeadbandMicroseconds = 2_000,   // 2ms to exit correction
```

### Tier 2: Rate Adjustment (Smooth Correction)

For errors between deadband and resampling threshold:
```csharp
MaxSpeedCorrection = 0.04,  // ±4% speed adjustment
```

The SDK fires `TargetPlaybackRateChanged` events, and our `UnifiedPolyphaseResampler` adjusts:
- Rate 1.04 = play 4% faster (catch up)
- Rate 0.96 = play 4% slower (slow down)

### Tier 3: Frame Drop/Insert (Aggressive Correction)

For very large errors (>200ms in our config):
```csharp
ResamplingThresholdMicroseconds = 200_000,  // 200ms
```

**We effectively disable this** by setting such a high threshold. The SDK would:
- Drop frames to catch up
- Insert silence/repeat frames to slow down

But this causes clicks, so we rely purely on rate adjustment.

### Re-anchoring

For catastrophic drift (>500ms), reset and start fresh:
```csharp
ReanchorThresholdMicroseconds = 500_000,  // 500ms
```

---

## Step 5: Sample Rate Conversion + Rate Adjustment

**Purpose**: Convert sample rates AND apply sync correction in one pass.

### Why Unified Resampling?

Previous dual-resampler approach had issues:
```
OLD: 48kHz → [SRC to 192kHz] → [Linear interp for ±4%] → ALSA
     Problem: Linear interpolation on 192kHz signal = aliasing = warbling
```

New unified approach:
```
NEW: 48kHz → [Polyphase resampler: 4x * rate adjustment] → ALSA
     Single high-quality operation, no aliasing
```

### How It Works

```csharp
public UnifiedPolyphaseResampler(
    source,
    inputRate: 48000,
    outputRate: 192000,
    timedBuffer,  // Subscribes to TargetPlaybackRateChanged
    logger
)
```

The resampler:
1. Subscribes to SDK's `TargetPlaybackRateChanged` event
2. Calculates effective ratio: `(192000 / 48000) * playbackRate`
3. Uses polyphase filter bank for high-quality conversion
4. Interpolates between filter phases for fractional ratios

### Quality Presets

| Preset | Phases | Taps | Memory |
|--------|--------|------|--------|
| HighestQuality | 128 | 48 | ~48KB |
| MediumQuality | 64 | 32 | ~16KB (DEFAULT) |
| LowResource | 32 | 24 | ~6KB |

---

## Step 6: ALSA Output

**Purpose**: Write audio to the sound card.

### Our Push-Based Model

```csharp
private void PlaybackLoop()
{
    while (_isPlaying)
    {
        // 1. Read from resampler (which reads from buffer)
        var samplesRead = source.Read(buffer, 0, buffer.Length);

        // 2. Apply volume
        for (int i = 0; i < samplesRead; i++)
            buffer[i] *= volume;

        // 3. Convert to output format (float → S32_LE)
        BitDepthConverter.Convert(floatBuffer, byteBuffer, bitDepth);

        // 4. Write to ALSA (BLOCKING)
        AlsaNative.WriteInterleaved(pcmHandle, ptr, frames);
    }
}
```

### Latency Reporting

We query actual ALSA buffer size after configuration:
```csharp
AlsaNative.GetParams(pcmHandle, out bufferSize, out periodSize);
OutputLatencyMs = (bufferSize * 1000) / sampleRate;
```

ALSA may allocate larger buffers than requested for:
- USB audio devices
- dmix/PulseAudio plugins
- Virtual/networked devices

---

## Step 7: Delay Offset (User Fine-Tuning)

**Purpose**: Allow manual adjustment for speaker placement, etc.

### How It Works

```csharp
// Set static delay offset
clockSync.StaticDelayMs = -50;  // Play 50ms earlier
```

This shifts the effective playback schedule:
- Positive values: Delay playback (play later)
- Negative values: Advance playback (play earlier)

### Use Cases

- Speaker closer to listener → negative delay
- Speaker farther from listener → positive delay
- Compensate for DSP processing in DAC/receiver

---

## Complete Data Flow

```
┌────────────────────────────────────────────────────────────────────────────┐
│                          TIMING PIPELINE                                    │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   Music Assistant                                                           │
│        │                                                                    │
│        │ WebSocket (Sendspin Protocol)                                      │
│        │ Audio chunks with timestamps                                       │
│        ▼                                                                    │
│   ┌─────────────────────────────────────────────────────────────────────┐  │
│   │  SendSpin.SDK                                                        │  │
│   │                                                                      │  │
│   │  ┌──────────────┐   ┌───────────────────┐   ┌──────────────────┐    │  │
│   │  │ ClockSync    │   │ TimedAudioBuffer  │   │ Sync Correction  │    │  │
│   │  │              │   │                   │   │                  │    │  │
│   │  │ Offset: Xms  │◄──│ Stores audio with │──►│ Tier 1: Deadband │    │  │
│   │  │ Drift: Yppm  │   │ timestamps        │   │ Tier 2: Rate Adj │    │  │
│   │  │ Converged: ✓ │   │ Buffer: 4000ms    │   │ Tier 3: Drop/Ins │    │  │
│   │  └──────────────┘   └───────────────────┘   └────────┬─────────┘    │  │
│   │                                                       │              │  │
│   └───────────────────────────────────────────────────────┼──────────────┘  │
│                                                           │                 │
│                                                           │ TargetPlayback  │
│                                                           │ RateChanged     │
│                                                           ▼                 │
│   ┌─────────────────────────────────────────────────────────────────────┐  │
│   │  UnifiedPolyphaseResampler (MultiRoomAudio)                          │  │
│   │                                                                      │  │
│   │  Input: 48kHz   Output: 192kHz   Rate: 1.02x   Effective: 4.08x     │  │
│   │                                                                      │  │
│   │  64 phases × 32 taps (MediumQuality)                                │  │
│   └─────────────────────────────────────────────────────────────────────┘  │
│        │                                                                    │
│        │ Float32 PCM at output rate                                         │
│        ▼                                                                    │
│   ┌─────────────────────────────────────────────────────────────────────┐  │
│   │  AlsaPlayer (MultiRoomAudio)                                         │  │
│   │                                                                      │  │
│   │  PlaybackLoop: Read → Volume → Convert → WriteInterleaved (block)   │  │
│   │                                                                      │  │
│   │  Output: S32_LE   Latency: 50ms (queried from ALSA)                 │  │
│   └─────────────────────────────────────────────────────────────────────┘  │
│        │                                                                    │
│        ▼                                                                    │
│   ┌─────────────────────────────────────────────────────────────────────┐  │
│   │  USB DAC / Sound Card                                                │  │
│   │                                                                      │  │
│   │  Hardware buffer: ~10-50ms depending on device                      │  │
│   └─────────────────────────────────────────────────────────────────────┘  │
│        │                                                                    │
│        ▼                                                                    │
│   ┌─────────────────────────────────────────────────────────────────────┐  │
│   │  Speaker                                                             │  │
│   │                                                                      │  │
│   │  ♪ Audio plays synchronized with other rooms ♪                      │  │
│   └─────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## Key Metrics to Monitor (Stats for Nerds)

| Section | Metric | Healthy Value | Concern |
|---------|--------|---------------|---------|
| **Sync Status** | Sync Error | ±5ms (green) | >20ms (red) |
| **Sync Status** | Playback Rate | 0.98-1.02 | Stuck at 0.96 or 1.04 |
| **Buffer** | Buffered | 2000-5000ms | <100ms or growing unbounded |
| **Buffer** | Underruns | 0 | Any value > 0 |
| **Clock Sync** | Uncertainty | <1ms | >5ms |
| **Clock Sync** | Drift Rate | <50 ppm | >100 ppm |
| **Throughput** | Dropped (sync) | 0 | Large numbers |

---

## Troubleshooting Common Issues

### Constant Sync Error (e.g., -200ms)

**Symptom**: Sync error never reaches zero, playback rate stuck at extreme.

**Likely Causes**:
1. ALSA latency mismatch (now fixed in v2.0.14)
2. Push vs pull timing model difference
3. Buffer state at playback start

**Investigation**: Check if error is stable. A stable error might indicate a systematic offset in our timing model.

### Growing Buffer with Stable Error

**Symptom**: Buffer grows to 4000-5000ms, error stays constant.

**Explanation**: This is actually NORMAL. The server sends audio ahead, and we buffer it. The Sendspin protocol allows up to 5 seconds of look-ahead.

### Warbling Audio

**Symptom**: Pitch wobble during playback.

**Fix**: Already fixed in v2.0.13 with unified resampler. If still occurring, check resampler integration.

---

## Related Documents

- [WINDOWSSPIN_COMPARISON.md](WINDOWSSPIN_COMPARISON.md) - Detailed comparison with reference implementation
- [AUDIO_PIPELINE.md](AUDIO_PIPELINE.md) - Technical pipeline architecture
- [ARCHITECTURE.md](ARCHITECTURE.md) - Overall system design
