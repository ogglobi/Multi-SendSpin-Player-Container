using System.Runtime.InteropServices;

namespace MultiRoomAudio.Audio.PulseAudio;

/// <summary>
/// P/Invoke bindings for the PulseAudio simple API (libpulse-simple.so).
/// This is a minimal, blocking API suitable for simple playback applications.
/// </summary>
/// <remarks>
/// The pa_simple API provides a synchronous interface to PulseAudio, which is
/// simpler to use than the async API but blocks during operations. This is
/// acceptable for our use case since we run playback on a dedicated thread.
///
/// Reference: https://freedesktop.org/software/pulseaudio/doxygen/simple.html
/// </remarks>
internal static class PulseAudioNative
{
    private const string LibPulseSimple = "libpulse-simple.so.0";
    private const string LibPulse = "libpulse.so.0";

    /// <summary>
    /// Sample format for audio data.
    /// </summary>
    public enum SampleFormat
    {
        U8 = 0,           // Unsigned 8 bit PCM
        ALAW = 1,         // 8 bit a-Law
        ULAW = 2,         // 8 bit mu-Law
        S16LE = 3,        // Signed 16 bit PCM, little endian
        S16BE = 4,        // Signed 16 bit PCM, big endian
        FLOAT32LE = 5,    // 32 bit IEEE floating point, little endian
        FLOAT32BE = 6,    // 32 bit IEEE floating point, big endian
        S32LE = 7,        // Signed 32 bit PCM, little endian
        S32BE = 8,        // Signed 32 bit PCM, big endian
        S24LE = 9,        // Signed 24 bit PCM packed, little endian
        S24BE = 10,       // Signed 24 bit PCM packed, big endian
        S24_32LE = 11,    // Signed 24 bit PCM in LSB of 32 bit words, little endian
        S24_32BE = 12,    // Signed 24 bit PCM in LSB of 32 bit words, big endian
        MAX = 13,
        Invalid = -1
    }

    /// <summary>
    /// Stream direction.
    /// </summary>
    public enum StreamDirection
    {
        NoDirection = 0,
        Playback = 1,
        Record = 2,
        Upload = 3
    }

    /// <summary>
    /// Sample specification structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SampleSpec
    {
        public SampleFormat Format;
        public uint Rate;
        public byte Channels;
    }

    /// <summary>
    /// Buffer attributes for stream configuration.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BufferAttr
    {
        /// <summary>Maximum length of the buffer in bytes.</summary>
        public uint MaxLength;
        /// <summary>Target length of the buffer in bytes (playback only).</summary>
        public uint TLength;
        /// <summary>Pre-buffering in bytes.</summary>
        public uint PreBuf;
        /// <summary>Minimum request size in bytes.</summary>
        public uint MinReq;
        /// <summary>Fragment size in bytes (record only).</summary>
        public uint FragSize;
    }

    /// <summary>
    /// Create a new connection to the PulseAudio server for simple playback or recording.
    /// </summary>
    /// <param name="server">Server name, or NULL for default.</param>
    /// <param name="name">A descriptive name for this client.</param>
    /// <param name="dir">Stream direction (playback or record).</param>
    /// <param name="dev">Sink/source name, or NULL for default.</param>
    /// <param name="streamName">A descriptive name for this stream.</param>
    /// <param name="ss">Sample specification.</param>
    /// <param name="map">Channel map, or NULL for default.</param>
    /// <param name="attr">Buffer attributes, or NULL for default.</param>
    /// <param name="error">Pointer to store error code on failure.</param>
    /// <returns>Handle to the connection, or NULL on failure.</returns>
    [DllImport(LibPulseSimple, EntryPoint = "pa_simple_new")]
    public static extern IntPtr SimpleNew(
        string? server,
        string name,
        StreamDirection dir,
        string? dev,
        string streamName,
        ref SampleSpec ss,
        IntPtr map,  // pa_channel_map*, NULL for default
        IntPtr attr, // pa_buffer_attr*, NULL for default
        out int error);

    /// <summary>
    /// Create a new connection with buffer attributes.
    /// </summary>
    [DllImport(LibPulseSimple, EntryPoint = "pa_simple_new")]
    public static extern IntPtr SimpleNewWithAttr(
        string? server,
        string name,
        StreamDirection dir,
        string? dev,
        string streamName,
        ref SampleSpec ss,
        IntPtr map,
        ref BufferAttr attr,
        out int error);

    /// <summary>
    /// Close and free the connection.
    /// </summary>
    [DllImport(LibPulseSimple, EntryPoint = "pa_simple_free")]
    public static extern void SimpleFree(IntPtr s);

    /// <summary>
    /// Write audio data to the server.
    /// </summary>
    /// <param name="s">The connection handle.</param>
    /// <param name="data">Pointer to audio data.</param>
    /// <param name="bytes">Number of bytes to write.</param>
    /// <param name="error">Pointer to store error code on failure.</param>
    /// <returns>0 on success, negative on error.</returns>
    [DllImport(LibPulseSimple, EntryPoint = "pa_simple_write")]
    public static extern int SimpleWrite(IntPtr s, IntPtr data, UIntPtr bytes, out int error);

    /// <summary>
    /// Wait until all data in the buffer has been played.
    /// </summary>
    [DllImport(LibPulseSimple, EntryPoint = "pa_simple_drain")]
    public static extern int SimpleDrain(IntPtr s, out int error);

    /// <summary>
    /// Flush the playback buffer.
    /// </summary>
    [DllImport(LibPulseSimple, EntryPoint = "pa_simple_flush")]
    public static extern int SimpleFlush(IntPtr s, out int error);

    /// <summary>
    /// Get the playback latency in microseconds.
    /// </summary>
    [DllImport(LibPulseSimple, EntryPoint = "pa_simple_get_latency")]
    public static extern ulong SimpleGetLatency(IntPtr s, out int error);

    /// <summary>
    /// Get a human-readable error message for an error code.
    /// </summary>
    [DllImport(LibPulse, EntryPoint = "pa_strerror")]
    public static extern IntPtr Strerror(int error);

    /// <summary>
    /// Get the error message as a managed string.
    /// </summary>
    public static string GetErrorMessage(int error)
    {
        var ptr = Strerror(error);
        return Marshal.PtrToStringAnsi(ptr) ?? $"Unknown error {error}";
    }

    /// <summary>
    /// Calculate bytes per frame for a sample spec.
    /// </summary>
    public static int BytesPerFrame(ref SampleSpec ss)
    {
        var bytesPerSample = ss.Format switch
        {
            SampleFormat.U8 or SampleFormat.ALAW or SampleFormat.ULAW => 1,
            SampleFormat.S16LE or SampleFormat.S16BE => 2,
            SampleFormat.S24LE or SampleFormat.S24BE => 3,
            SampleFormat.FLOAT32LE or SampleFormat.FLOAT32BE or
            SampleFormat.S32LE or SampleFormat.S32BE or
            SampleFormat.S24_32LE or SampleFormat.S24_32BE => 4,
            _ => 4
        };
        return bytesPerSample * ss.Channels;
    }

    /// <summary>
    /// Calculate bytes for a given duration in milliseconds.
    /// </summary>
    public static uint BytesForMs(ref SampleSpec ss, int milliseconds)
    {
        var bytesPerFrame = BytesPerFrame(ref ss);
        return (uint)(ss.Rate * bytesPerFrame * milliseconds / 1000);
    }
}
