using System.Runtime.InteropServices;
using MultiRoomAudio.Models;

namespace MultiRoomAudio.Relay;

/// <summary>
/// FTDI D2XX wrapper for controlling 8-channel relay boards using bit-bang mode.
/// Supports the Denkovi USB 8 Relay Board with FT245RL chip.
/// </summary>
/// <remarks>
/// The FT245RL uses FTDI's Bit-Bang Mode where each of the 8 data pins
/// directly controls a relay. Writing a byte value immediately sets
/// all 8 relay states (bit 0 = relay 1, bit 7 = relay 8).
///
/// On Linux, requires libftd2xx.so to be installed.
/// The ftdi_sio kernel module must be unloaded or blocked via udev rules.
/// </remarks>
public sealed class FtdiRelayBoard : IDisposable
{
    private IntPtr _handle = IntPtr.Zero;
    private byte _currentState;
    private bool _disposed;
    private readonly ILogger<FtdiRelayBoard>? _logger;
    private readonly object _lock = new();

    // FTDI D2XX Status codes
    private const int FT_OK = 0;
    private const int FT_INVALID_HANDLE = 1;
    private const int FT_DEVICE_NOT_FOUND = 2;
    private const int FT_DEVICE_NOT_OPENED = 3;

    // Bit mode constants
    private const byte FT_BITMODE_RESET = 0x00;
    private const byte FT_BITMODE_ASYNC_BITBANG = 0x01;

    // All pins as outputs (0xFF = all 8 bits)
    private const byte PIN_MASK_ALL_OUTPUT = 0xFF;

    #region P/Invoke Declarations

    // Library name varies by platform
    private const string FtdiLibrary = "libftd2xx";

    [DllImport(FtdiLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FT_Open(int deviceNumber, ref IntPtr handle);

    [DllImport(FtdiLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FT_OpenEx(string description, int flags, ref IntPtr handle);

    [DllImport(FtdiLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FT_Close(IntPtr handle);

    [DllImport(FtdiLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FT_SetBitMode(IntPtr handle, byte mask, byte mode);

    [DllImport(FtdiLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FT_Write(IntPtr handle, byte[] buffer, int bytesToWrite, ref int bytesWritten);

    [DllImport(FtdiLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FT_GetBitMode(IntPtr handle, ref byte mode);

    [DllImport(FtdiLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FT_ResetDevice(IntPtr handle);

    [DllImport(FtdiLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FT_Purge(IntPtr handle, int mask);

    [DllImport(FtdiLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FT_SetBaudRate(IntPtr handle, int baudRate);

    [DllImport(FtdiLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FT_CreateDeviceInfoList(ref int numDevices);

    [DllImport(FtdiLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int FT_GetDeviceInfoDetail(
        int index,
        ref int flags,
        ref int type,
        ref int id,
        ref int locId,
        byte[] serialNumber,
        byte[] description,
        ref IntPtr handle);

    // Flags for FT_OpenEx
    private const int FT_OPEN_BY_SERIAL_NUMBER = 1;
    private const int FT_OPEN_BY_DESCRIPTION = 2;

    // Purge flags
    private const int FT_PURGE_RX = 1;
    private const int FT_PURGE_TX = 2;

    #endregion

    public FtdiRelayBoard(ILogger<FtdiRelayBoard>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if FTDI library is available on this system.
    /// </summary>
    public static bool IsLibraryAvailable()
    {
        try
        {
            int numDevices = 0;
            var result = FT_CreateDeviceInfoList(ref numDevices);
            // FT_OK or any error that indicates the library loaded
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Enumerate all connected FTDI devices.
    /// </summary>
    public static List<FtdiDeviceInfo> EnumerateDevices()
    {
        var devices = new List<FtdiDeviceInfo>();

        try
        {
            int numDevices = 0;
            var result = FT_CreateDeviceInfoList(ref numDevices);
            if (result != FT_OK || numDevices == 0)
                return devices;

            for (int i = 0; i < numDevices; i++)
            {
                int flags = 0, type = 0, id = 0, locId = 0;
                var serialNumber = new byte[64];
                var description = new byte[64];
                IntPtr handle = IntPtr.Zero;

                result = FT_GetDeviceInfoDetail(i, ref flags, ref type, ref id, ref locId,
                    serialNumber, description, ref handle);

                if (result == FT_OK)
                {
                    var serial = System.Text.Encoding.ASCII.GetString(serialNumber).TrimEnd('\0');
                    var desc = System.Text.Encoding.ASCII.GetString(description).TrimEnd('\0');
                    var isOpen = (flags & 0x01) != 0;

                    devices.Add(new FtdiDeviceInfo(i, serial, desc, isOpen));
                }
            }
        }
        catch (Exception)
        {
            // Library not available or error - return empty list
        }

        return devices;
    }

    /// <summary>
    /// Whether the relay board is currently connected.
    /// </summary>
    public bool IsConnected => _handle != IntPtr.Zero;

    /// <summary>
    /// Current relay states as a byte (bit 0 = relay 1).
    /// </summary>
    public byte CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _currentState;
            }
        }
    }

    /// <summary>
    /// Open connection to the first available FTDI device.
    /// </summary>
    public bool Open()
    {
        return Open(0);
    }

    /// <summary>
    /// Open connection to a specific FTDI device by index.
    /// </summary>
    public bool Open(int deviceIndex)
    {
        lock (_lock)
        {
            if (_handle != IntPtr.Zero)
            {
                _logger?.LogWarning("FTDI device already open");
                return true;
            }

            try
            {
                var result = FT_Open(deviceIndex, ref _handle);
                if (result != FT_OK)
                {
                    _logger?.LogError("Failed to open FTDI device {Index}: error {Result}", deviceIndex, result);
                    _handle = IntPtr.Zero;
                    return false;
                }

                if (!Initialize())
                {
                    Close();
                    return false;
                }

                _logger?.LogInformation("FTDI relay board opened (device {Index})", deviceIndex);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception opening FTDI device {Index}", deviceIndex);
                _handle = IntPtr.Zero;
                return false;
            }
        }
    }

    /// <summary>
    /// Open connection to a specific FTDI device by serial number.
    /// </summary>
    public bool OpenBySerial(string serialNumber)
    {
        lock (_lock)
        {
            if (_handle != IntPtr.Zero)
            {
                _logger?.LogWarning("FTDI device already open");
                return true;
            }

            try
            {
                var result = FT_OpenEx(serialNumber, FT_OPEN_BY_SERIAL_NUMBER, ref _handle);
                if (result != FT_OK)
                {
                    _logger?.LogError("Failed to open FTDI device with serial '{Serial}': error {Result}",
                        serialNumber, result);
                    _handle = IntPtr.Zero;
                    return false;
                }

                if (!Initialize())
                {
                    Close();
                    return false;
                }

                _logger?.LogInformation("FTDI relay board opened (serial: {Serial})", serialNumber);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception opening FTDI device with serial '{Serial}'", serialNumber);
                _handle = IntPtr.Zero;
                return false;
            }
        }
    }

    /// <summary>
    /// Initialize the device for relay control (bit-bang mode).
    /// </summary>
    private bool Initialize()
    {
        // Reset device
        var result = FT_ResetDevice(_handle);
        if (result != FT_OK)
        {
            _logger?.LogWarning("Failed to reset FTDI device: {Result}", result);
        }

        // Purge buffers
        result = FT_Purge(_handle, FT_PURGE_RX | FT_PURGE_TX);
        if (result != FT_OK)
        {
            _logger?.LogWarning("Failed to purge FTDI buffers: {Result}", result);
        }

        // Set baud rate (affects bit-bang timing)
        result = FT_SetBaudRate(_handle, 9600);
        if (result != FT_OK)
        {
            _logger?.LogWarning("Failed to set baud rate: {Result}", result);
        }

        // Enable async bit-bang mode with all pins as outputs
        result = FT_SetBitMode(_handle, PIN_MASK_ALL_OUTPUT, FT_BITMODE_ASYNC_BITBANG);
        if (result != FT_OK)
        {
            _logger?.LogError("Failed to set bit-bang mode: {Result}", result);
            return false;
        }

        // Initialize all relays to OFF
        _currentState = 0x00;
        return WriteState(_currentState);
    }

    /// <summary>
    /// Close connection to the relay board.
    /// </summary>
    public void Close()
    {
        lock (_lock)
        {
            if (_handle == IntPtr.Zero)
                return;

            try
            {
                // Turn off all relays before closing
                WriteState(0x00);

                // Reset bit mode
                FT_SetBitMode(_handle, 0x00, FT_BITMODE_RESET);

                // Close device
                FT_Close(_handle);
                _logger?.LogInformation("FTDI relay board closed");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error closing FTDI device");
            }
            finally
            {
                _handle = IntPtr.Zero;
                _currentState = 0x00;
            }
        }
    }

    /// <summary>
    /// Set the state of a specific relay channel.
    /// </summary>
    /// <param name="channel">Channel number (1-8).</param>
    /// <param name="on">True to turn on, false to turn off.</param>
    public bool SetRelay(int channel, bool on)
    {
        if (channel < 1 || channel > 8)
            throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be 1-8");

        lock (_lock)
        {
            if (_handle == IntPtr.Zero)
            {
                _logger?.LogWarning("Cannot set relay - device not open");
                return false;
            }

            // Convert channel (1-8) to bit position (0-7)
            byte bit = (byte)(1 << (channel - 1));

            byte newState;
            if (on)
                newState = (byte)(_currentState | bit);
            else
                newState = (byte)(_currentState & ~bit);

            if (newState == _currentState)
                return true; // No change needed

            if (WriteState(newState))
            {
                _currentState = newState;
                _logger?.LogDebug("Relay {Channel} set to {State}", channel, on ? "ON" : "OFF");
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Get the state of a specific relay channel.
    /// </summary>
    /// <param name="channel">Channel number (1-8).</param>
    public RelayState GetRelay(int channel)
    {
        if (channel < 1 || channel > 8)
            throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be 1-8");

        lock (_lock)
        {
            if (_handle == IntPtr.Zero)
                return RelayState.Unknown;

            byte bit = (byte)(1 << (channel - 1));
            return (_currentState & bit) != 0 ? RelayState.On : RelayState.Off;
        }
    }

    /// <summary>
    /// Set all relay states at once.
    /// </summary>
    /// <param name="states">Byte where each bit represents a relay (bit 0 = relay 1).</param>
    public bool SetAllRelays(byte states)
    {
        lock (_lock)
        {
            if (_handle == IntPtr.Zero)
            {
                _logger?.LogWarning("Cannot set relays - device not open");
                return false;
            }

            if (states == _currentState)
                return true;

            if (WriteState(states))
            {
                _currentState = states;
                _logger?.LogDebug("All relays set to 0x{State:X2}", states);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Turn off all relays.
    /// </summary>
    public bool AllOff()
    {
        return SetAllRelays(0x00);
    }

    /// <summary>
    /// Write a byte value to the relay board.
    /// </summary>
    private bool WriteState(byte state)
    {
        try
        {
            var buffer = new byte[] { state };
            int bytesWritten = 0;
            var result = FT_Write(_handle, buffer, 1, ref bytesWritten);

            if (result != FT_OK)
            {
                _logger?.LogError("Failed to write to FTDI device: {Result}", result);
                return false;
            }

            if (bytesWritten != 1)
            {
                _logger?.LogWarning("FTDI write returned {BytesWritten} bytes (expected 1)", bytesWritten);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception writing to FTDI device");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Close();
    }
}
