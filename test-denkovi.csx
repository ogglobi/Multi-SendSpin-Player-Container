#!/usr/bin/env dotnet-script
// Test script for Denkovi FTDI relay board on macOS
// Run with: dotnet script test-denkovi.csx

#r "nuget: System.Runtime.InteropServices, 4.3.0"

using System.Runtime.InteropServices;
using System.Reflection;

// Constants
const int FTDI_VENDOR_ID = 0x0403;
const int FT245RL_PRODUCT_ID = 0x6001;
const byte PIN_MASK_ALL_OUTPUT = 0xFF;
const byte BITMODE_RESET = 0x00;
const byte BITMODE_BITBANG = 0x01;

// Native library resolution for macOS
static IntPtr ResolveFtdiLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
{
    if (libraryName != "libftdi1")
        return IntPtr.Zero;

    IntPtr handle;

    // Try Homebrew paths
    string[] macPaths = new[]
    {
        "/opt/homebrew/lib/libftdi1.dylib",      // Apple Silicon Homebrew
        "/opt/homebrew/lib/libftdi1.2.dylib",
        "/usr/local/lib/libftdi1.dylib",         // Intel Homebrew
        "libftdi1.dylib"
    };

    foreach (var path in macPaths)
    {
        if (NativeLibrary.TryLoad(path, out handle))
        {
            Console.WriteLine($"Loaded libftdi1 from: {path}");
            return handle;
        }
    }

    return IntPtr.Zero;
}

// Register resolver
NativeLibrary.SetDllImportResolver(typeof(Program).Assembly, ResolveFtdiLibrary);

// P/Invoke declarations
[StructLayout(LayoutKind.Sequential)]
struct ftdi_device_list
{
    public IntPtr next;
    public IntPtr dev;
}

[DllImport("libftdi1", CallingConvention = CallingConvention.Cdecl)]
static extern IntPtr ftdi_new();

[DllImport("libftdi1", CallingConvention = CallingConvention.Cdecl)]
static extern void ftdi_free(IntPtr ftdi);

[DllImport("libftdi1", CallingConvention = CallingConvention.Cdecl)]
static extern int ftdi_usb_find_all(IntPtr ftdi, ref IntPtr devlist, int vendor, int product);

[DllImport("libftdi1", CallingConvention = CallingConvention.Cdecl)]
static extern void ftdi_list_free(ref IntPtr devlist);

[DllImport("libftdi1", CallingConvention = CallingConvention.Cdecl)]
static extern int ftdi_usb_open(IntPtr ftdi, int vendor, int product);

[DllImport("libftdi1", CallingConvention = CallingConvention.Cdecl)]
static extern int ftdi_usb_close(IntPtr ftdi);

[DllImport("libftdi1", CallingConvention = CallingConvention.Cdecl)]
static extern int ftdi_usb_reset(IntPtr ftdi);

[DllImport("libftdi1", CallingConvention = CallingConvention.Cdecl)]
static extern int ftdi_usb_purge_buffers(IntPtr ftdi);

[DllImport("libftdi1", CallingConvention = CallingConvention.Cdecl)]
static extern int ftdi_set_baudrate(IntPtr ftdi, int baudrate);

[DllImport("libftdi1", CallingConvention = CallingConvention.Cdecl)]
static extern int ftdi_set_bitmode(IntPtr ftdi, byte bitmask, byte mode);

[DllImport("libftdi1", CallingConvention = CallingConvention.Cdecl)]
static extern int ftdi_write_data(IntPtr ftdi, byte[] buf, int size);

[DllImport("libftdi1", CallingConvention = CallingConvention.Cdecl)]
static extern int ftdi_read_pins(IntPtr ftdi, out byte pins);

[DllImport("libftdi1", CallingConvention = CallingConvention.Cdecl)]
static extern IntPtr ftdi_get_error_string(IntPtr ftdi);

string GetError(IntPtr ctx)
{
    if (ctx == IntPtr.Zero) return "No context";
    IntPtr errorPtr = ftdi_get_error_string(ctx);
    return errorPtr == IntPtr.Zero ? "Unknown error" : Marshal.PtrToStringAnsi(errorPtr) ?? "Unknown error";
}

Console.WriteLine("=== Denkovi FTDI Relay Board Test ===\n");

// Create context
IntPtr ctx = ftdi_new();
if (ctx == IntPtr.Zero)
{
    Console.WriteLine("ERROR: Failed to create FTDI context");
    return 1;
}

Console.WriteLine("✓ FTDI context created");

// Enumerate devices
IntPtr devList = IntPtr.Zero;
int count = ftdi_usb_find_all(ctx, ref devList, FTDI_VENDOR_ID, FT245RL_PRODUCT_ID);
Console.WriteLine($"✓ Found {count} FTDI device(s)");

if (devList != IntPtr.Zero)
    ftdi_list_free(ref devList);

if (count <= 0)
{
    Console.WriteLine("ERROR: No FTDI devices found");
    ftdi_free(ctx);
    return 1;
}

// Open device
int result = ftdi_usb_open(ctx, FTDI_VENDOR_ID, FT245RL_PRODUCT_ID);
if (result < 0)
{
    Console.WriteLine($"ERROR: Failed to open device: {result} - {GetError(ctx)}");
    ftdi_free(ctx);
    return 1;
}

Console.WriteLine("✓ Device opened successfully");

// Reset and configure
result = ftdi_usb_reset(ctx);
Console.WriteLine($"  Reset: {(result >= 0 ? "OK" : $"Warning: {result}")}");

result = ftdi_usb_purge_buffers(ctx);
Console.WriteLine($"  Purge buffers: {(result >= 0 ? "OK" : $"Warning: {result}")}");

result = ftdi_set_baudrate(ctx, 9600);
Console.WriteLine($"  Set baud rate: {(result >= 0 ? "OK" : $"Warning: {result}")}");

result = ftdi_set_bitmode(ctx, PIN_MASK_ALL_OUTPUT, BITMODE_BITBANG);
if (result < 0)
{
    Console.WriteLine($"ERROR: Failed to set bitbang mode: {result} - {GetError(ctx)}");
    ftdi_usb_close(ctx);
    ftdi_free(ctx);
    return 1;
}
Console.WriteLine("✓ Bitbang mode enabled");

// Turn all relays off first
byte[] buffer = new byte[1];
buffer[0] = 0x00;
result = ftdi_write_data(ctx, buffer, 1);
Console.WriteLine($"\n✓ All relays OFF (wrote 0x00, result: {result})");

// Read current pin state
byte pins;
result = ftdi_read_pins(ctx, out pins);
Console.WriteLine($"  Current pin state: 0x{pins:X2} (read result: {result})");

Console.WriteLine("\n=== Testing Individual Relays ===");
Console.WriteLine("Press Enter after each relay to continue, or 'q' to quit...\n");

// Test each relay 1-8
for (int channel = 1; channel <= 8; channel++)
{
    // Turn ON this relay
    byte onState = (byte)(1 << (channel - 1));
    buffer[0] = onState;
    result = ftdi_write_data(ctx, buffer, 1);

    // Read back state
    ftdi_read_pins(ctx, out pins);

    Console.WriteLine($"Relay {channel} ON  (wrote 0x{onState:X2}, read back: 0x{pins:X2})");

    var key = Console.ReadLine();
    if (key?.ToLower() == "q") break;

    // Turn OFF this relay
    buffer[0] = 0x00;
    result = ftdi_write_data(ctx, buffer, 1);
    ftdi_read_pins(ctx, out pins);
    Console.WriteLine($"Relay {channel} OFF (wrote 0x00, read back: 0x{pins:X2})\n");
}

// Test all relays on
Console.WriteLine("\n=== All Relays ON Test ===");
buffer[0] = 0xFF;
result = ftdi_write_data(ctx, buffer, 1);
ftdi_read_pins(ctx, out pins);
Console.WriteLine($"All relays ON (wrote 0xFF, read back: 0x{pins:X2})");
Console.WriteLine("Press Enter to turn all off...");
Console.ReadLine();

// All off
buffer[0] = 0x00;
result = ftdi_write_data(ctx, buffer, 1);
ftdi_read_pins(ctx, out pins);
Console.WriteLine($"All relays OFF (wrote 0x00, read back: 0x{pins:X2})");

// Cleanup
ftdi_set_bitmode(ctx, 0x00, BITMODE_RESET);
ftdi_usb_close(ctx);
ftdi_free(ctx);

Console.WriteLine("\n✓ Test complete, device closed");
return 0;
