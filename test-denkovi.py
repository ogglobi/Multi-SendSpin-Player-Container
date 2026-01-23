#!/usr/bin/env python3
"""
Interactive test script for Denkovi FTDI relay board on macOS.
Uses ctypes to interface with libftdi1.

Run with: python3 test-denkovi.py
"""

import ctypes
import time
import sys

# Load libftdi1
try:
    libftdi = ctypes.CDLL("/opt/homebrew/lib/libftdi1.dylib")
except OSError:
    try:
        libftdi = ctypes.CDLL("/usr/local/lib/libftdi1.dylib")
    except OSError:
        print("ERROR: Could not load libftdi1.dylib")
        print("Install with: brew install libftdi")
        sys.exit(1)

# Constants
FTDI_VENDOR_ID = 0x0403
FT245RL_PRODUCT_ID = 0x6001
BITMODE_BITBANG = 0x01
BITMODE_RESET = 0x00
PIN_MASK_ALL_OUTPUT = 0xFF

# Set up function prototypes
libftdi.ftdi_new.restype = ctypes.c_void_p
libftdi.ftdi_new.argtypes = []

libftdi.ftdi_free.restype = None
libftdi.ftdi_free.argtypes = [ctypes.c_void_p]

libftdi.ftdi_usb_open.restype = ctypes.c_int
libftdi.ftdi_usb_open.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_int]

libftdi.ftdi_usb_close.restype = ctypes.c_int
libftdi.ftdi_usb_close.argtypes = [ctypes.c_void_p]

libftdi.ftdi_usb_reset.restype = ctypes.c_int
libftdi.ftdi_usb_reset.argtypes = [ctypes.c_void_p]

libftdi.ftdi_usb_purge_buffers.restype = ctypes.c_int
libftdi.ftdi_usb_purge_buffers.argtypes = [ctypes.c_void_p]

libftdi.ftdi_set_baudrate.restype = ctypes.c_int
libftdi.ftdi_set_baudrate.argtypes = [ctypes.c_void_p, ctypes.c_int]

libftdi.ftdi_set_bitmode.restype = ctypes.c_int
libftdi.ftdi_set_bitmode.argtypes = [ctypes.c_void_p, ctypes.c_ubyte, ctypes.c_ubyte]

libftdi.ftdi_write_data.restype = ctypes.c_int
libftdi.ftdi_write_data.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_ubyte), ctypes.c_int]

libftdi.ftdi_read_pins.restype = ctypes.c_int
libftdi.ftdi_read_pins.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_ubyte)]

libftdi.ftdi_get_error_string.restype = ctypes.c_char_p
libftdi.ftdi_get_error_string.argtypes = [ctypes.c_void_p]


class DenkoviRelay:
    def __init__(self):
        self.ctx = None
        self.current_state = 0x00

    def open(self):
        """Open connection to the relay board."""
        self.ctx = libftdi.ftdi_new()
        if not self.ctx:
            raise Exception("Failed to create FTDI context")

        result = libftdi.ftdi_usb_open(self.ctx, FTDI_VENDOR_ID, FT245RL_PRODUCT_ID)
        if result < 0:
            error = libftdi.ftdi_get_error_string(self.ctx).decode()
            libftdi.ftdi_free(self.ctx)
            self.ctx = None
            raise Exception(f"Failed to open device: {result} - {error}")

        # Initialize
        libftdi.ftdi_usb_reset(self.ctx)
        libftdi.ftdi_usb_purge_buffers(self.ctx)
        libftdi.ftdi_set_baudrate(self.ctx, 9600)

        result = libftdi.ftdi_set_bitmode(self.ctx, PIN_MASK_ALL_OUTPUT, BITMODE_BITBANG)
        if result < 0:
            error = libftdi.ftdi_get_error_string(self.ctx).decode()
            self.close()
            raise Exception(f"Failed to set bitbang mode: {result} - {error}")

        # Turn all relays off
        self._write_state(0x00)
        return True

    def close(self):
        """Close the connection."""
        if self.ctx:
            self._write_state(0x00)  # All off
            libftdi.ftdi_set_bitmode(self.ctx, 0x00, BITMODE_RESET)
            libftdi.ftdi_usb_close(self.ctx)
            libftdi.ftdi_free(self.ctx)
            self.ctx = None

    def _write_state(self, state):
        """Write relay state to the board."""
        buf = (ctypes.c_ubyte * 1)(state & 0xFF)
        result = libftdi.ftdi_write_data(self.ctx, buf, 1)
        if result >= 0:
            self.current_state = state
        return result >= 0

    def read_pins(self):
        """Read current pin state from hardware."""
        pins = ctypes.c_ubyte()
        result = libftdi.ftdi_read_pins(self.ctx, ctypes.byref(pins))
        return pins.value if result >= 0 else None

    def set_relay(self, channel, on):
        """Set a specific relay (1-8) on or off."""
        if channel < 1 or channel > 8:
            raise ValueError("Channel must be 1-8")

        bit = 1 << (channel - 1)
        if on:
            new_state = self.current_state | bit
        else:
            new_state = self.current_state & ~bit

        return self._write_state(new_state)

    def get_relay(self, channel):
        """Get the current state of a relay."""
        if channel < 1 or channel > 8:
            raise ValueError("Channel must be 1-8")
        bit = 1 << (channel - 1)
        return (self.current_state & bit) != 0

    def all_on(self):
        """Turn all relays on."""
        return self._write_state(0xFF)

    def all_off(self):
        """Turn all relays off."""
        return self._write_state(0x00)

    def get_state_string(self):
        """Get a string representation of all relay states."""
        states = []
        for i in range(1, 9):
            state = "ON " if self.get_relay(i) else "OFF"
            states.append(f"R{i}:{state}")
        return " | ".join(states)


def main():
    print("=" * 60)
    print("     Denkovi FTDI Relay Board Test (8-channel)")
    print("=" * 60)
    print()

    relay = DenkoviRelay()

    try:
        print("Opening device...")
        relay.open()
        print("✓ Device opened successfully!")
        print()

        # Read initial state
        pins = relay.read_pins()
        print(f"Initial pin state: 0x{pins:02X}" if pins is not None else "Could not read pins")
        print(f"Software state: 0x{relay.current_state:02X}")
        print()

        while True:
            print("-" * 60)
            print(f"Current: {relay.get_state_string()}")
            print("-" * 60)
            print()
            print("Commands:")
            print("  1-8     : Toggle relay 1-8")
            print("  on N    : Turn relay N on")
            print("  off N   : Turn relay N off")
            print("  all on  : Turn all relays on")
            print("  all off : Turn all relays off")
            print("  cycle   : Cycle through each relay")
            print("  read    : Read hardware pin state")
            print("  q       : Quit")
            print()

            cmd = input("Command: ").strip().lower()

            if cmd == 'q' or cmd == 'quit':
                break
            elif cmd == 'all on':
                relay.all_on()
                print("All relays ON")
            elif cmd == 'all off':
                relay.all_off()
                print("All relays OFF")
            elif cmd == 'read':
                pins = relay.read_pins()
                if pins is not None:
                    print(f"Hardware pin state: 0x{pins:02X} (binary: {pins:08b})")
                    print(f"Software state:     0x{relay.current_state:02X} (binary: {relay.current_state:08b})")
                else:
                    print("Could not read pins")
            elif cmd == 'cycle':
                print("Cycling through relays...")
                for i in range(1, 9):
                    relay.set_relay(i, True)
                    print(f"  Relay {i} ON")
                    time.sleep(0.5)
                    relay.set_relay(i, False)
                print("Cycle complete")
            elif cmd.startswith('on '):
                try:
                    n = int(cmd[3:])
                    relay.set_relay(n, True)
                    print(f"Relay {n} ON")
                except (ValueError, IndexError):
                    print("Usage: on N (where N is 1-8)")
            elif cmd.startswith('off '):
                try:
                    n = int(cmd[4:])
                    relay.set_relay(n, False)
                    print(f"Relay {n} OFF")
                except (ValueError, IndexError):
                    print("Usage: off N (where N is 1-8)")
            elif cmd.isdigit():
                n = int(cmd)
                if 1 <= n <= 8:
                    current = relay.get_relay(n)
                    relay.set_relay(n, not current)
                    print(f"Relay {n} toggled to {'ON' if not current else 'OFF'}")
                else:
                    print("Invalid relay number (must be 1-8)")
            elif cmd:
                print(f"Unknown command: {cmd}")

            print()

    except Exception as e:
        print(f"ERROR: {e}")
        return 1
    finally:
        print("\nClosing device...")
        relay.close()
        print("✓ Done")

    return 0


if __name__ == "__main__":
    sys.exit(main())
