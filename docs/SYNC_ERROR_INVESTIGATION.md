# Sync Error Investigation Notes

## Problem Statement

The dev branch shows a persistent ~200ms sync error in the SDK logs:
```
Sync drift: error=-198.93ms, elapsed=6038ms, readTime=6237ms, latencyComp=50ms
```

This error appears regardless of:
- Resampler type (polyphase or linear)
- Sample rate conversion (native 48kHz or upsampled to 192kHz)
- Audio backend (ALSA direct or PulseAudio)

---

## Key Findings

### 1. Resampler is NOT the Cause

A/B testing with both resamplers shows identical ~200ms sync error:
- `UnifiedPolyphaseResampler` (dev branch): ~200ms error
- `ResamplingAudioSampleSource` (main branch linear): ~200ms error

This definitively rules out the resampler as the source.

### 2. SDK's latencyComp is Informational Only

The SDK logs `latencyComp=XXms` but does **NOT** use it in the sync error formula.

**Sync error formula observed:**
```
error = elapsed - readTime
```

Where:
- `elapsed` = wall clock time since pipeline started
- `readTime` = timestamp of audio samples we've read from the buffer

The `latencyComp` (from `player.OutputLatencyMs`) is logged but not subtracted.

### 3. StaticDelayMs Affects Scheduling, Not Error Calculation

Setting `clockSync.StaticDelayMs = -200` shifts **when** samples are scheduled to play, but does NOT change how the sync error is calculated. The error still shows ~200ms.

### 4. Inflating OutputLatencyMs Makes It Worse

When we set `OutputLatencyMs = 200` (buffer + startup fill):
- SDK sees `latencyComp=200ms` in logs
- SDK reads samples 200ms further ahead
- Sync error shows ~-200ms (reading ahead by output latency)
- Error is "correct" from SDK's perspective, but not helpful

### 5. ALSA Has Two Sources of Delay

1. **Buffer latency** (~50ms): Time for audio in ALSA's ring buffer
2. **Startup fill time** (~150ms): ALSA auto-starts after buffer is ~75% full

Total real-world delay: ~200ms before audio reaches speakers.

### 6. Main Branch Behavior

Main branch uses PulseAudio and shows:
- Sync error ~0ms in logs
- But audio plays at "wrong times" (user observation)

This suggests PulseAudio may be hiding/compensating for something that ALSA exposes.

---

## What We Tried

| Approach | Result |
|----------|--------|
| A/B test resamplers | Both show same error - not resampler |
| Set StaticDelayMs = -200 | No effect on error calculation |
| Inflate OutputLatencyMs to 200ms | SDK reads further ahead, error unchanged |
| Native rate mode (no upsampling) | Same error |

---

## SDK Sync Error Understanding

The SDK calculates sync error as the difference between:
- How much wall clock time has passed
- How much audio (in time) we've read from the buffer

A **negative** error means we've read MORE audio than wall time has passed (reading ahead).

For proper sync with output latency L:
- We SHOULD be reading L ms ahead
- So error of -L would actually be "in sync"
- But SDK doesn't adjust for this

---

## Open Questions

1. **Why does main branch show 0ms error?**
   - PulseAudio's `pa_simple_get_latency()` returns dynamic latency
   - Does PulseAudio internally adjust timing?

2. **Where should the latency compensation happen?**
   - In the SDK's sync calculation?
   - In how we start the pipeline?
   - In when we begin reading samples?

3. **Is the error measurement wrong, or is playback actually late?**
   - Need external measurement to verify actual audio timing

---

## Next Investigation Areas

1. **Compare pipeline startup** between main and dev
2. **Check if SDK has a "start offset" concept** we're not using
3. **External latency measurement** with oscilloscope or audio loopback
4. **Review SDK source** for how latencyComp should be used
5. **Check if PulseAudio backend** reports latency differently

---

## Files Relevant to This Issue

- `src/MultiRoomAudio/Audio/Alsa/AlsaPlayer.cs` - ALSA output, OutputLatencyMs
- `src/MultiRoomAudio/Audio/PulseAudio/PulseAudioPlayer.cs` - PulseAudio output
- `src/MultiRoomAudio/Services/PlayerManagerService.cs` - Pipeline setup, ClockSync
- `src/MultiRoomAudio/Audio/UnifiedPolyphaseResampler.cs` - Dev resampler
- `src/MultiRoomAudio/Audio/ResamplingAudioSampleSource.cs` - Main branch resampler

---

## Log Snippets for Reference

### Typical sync drift warning:
```
Sync drift: error=-196.04ms, elapsed=5462ms, readTime=5658ms, latencyComp=50ms, drift=+0.2μs/s, correction=none
```

### ALSA buffer info:
```
ALSA actual buffer: 2400 frames (50ms), period: 600 frames
```

### Pipeline start:
```
Starting playback: buffer=288ms, sync offset=-1767232404661.88ms, output latency=50ms
```

---

## Resolution: ALSA Startup Latency Calibration

### What Was Implemented

We added per-device startup latency calibration to `AlsaPlayer.cs`:

1. During `InitializeAsync()`, after opening the ALSA device, we measure the actual startup latency
2. The calibration writes silence until ALSA transitions from PREPARED to RUNNING state
3. This measured time (~150ms on typical devices) is added to the buffer latency
4. `OutputLatencyMs` now returns the combined latency (buffer + startup)

### New Log Output

```
ALSA actual buffer: 2400 frames (50ms), period: 600 frames
ALSA calibration complete: startup=147ms, buffer=50ms, total=197ms
ALSA player initialized. Device: default, Format: S32_LE, Rate: 192000Hz, Total Latency: 197ms
```

### SDK Fix Required

The sync error will still show ~200ms in logs because the **SDK ignores latencyComp** in its error calculation. The calibration provides accurate latency data, but the SDK needs to be updated to use it.

**See**: [SDK_LATENCY_FIX_PROPOSAL.md](SDK_LATENCY_FIX_PROPOSAL.md) for detailed recommendations to the SDK team.

### Summary

| Component | Status |
|-----------|--------|
| ALSA calibration | Implemented |
| Accurate latency reporting | Implemented |
| SDK sync error fix | Pending (SDK team) |
