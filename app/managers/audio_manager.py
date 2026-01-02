"""
Audio Manager for device detection and volume control.

Handles ALSA and PulseAudio device enumeration, mixer control detection,
and volume get/set operations. Also supports PortAudio devices for test
tones via the sounddevice library.

On HAOS (Home Assistant OS), uses PulseAudio via pactl.
On standalone Docker, uses ALSA via aplay/amixer.
"""

import logging
import re
import subprocess

from environment import is_hassio

# sounddevice/numpy are imported lazily in _play_test_tone_portaudio()
# to avoid PortAudio C extension segfaults on Alpine Linux during module load

logger = logging.getLogger(__name__)

# Type alias for audio device info
AudioDevice = dict[str, str]

# =============================================================================
# CONSTANTS
# =============================================================================

# Default fallback when no controls can be detected
DEFAULT_MIXER_CONTROLS = ["Master", "PCM"]

# Controls to try when reading volume (includes Capture for status reporting)
VOLUME_READ_CONTROLS = ["Master", "PCM", "Speaker", "Headphone", "Digital", "Capture"]

# Controls to try when setting volume (excludes Capture - it's for input levels)
VOLUME_WRITE_CONTROLS = ["Master", "PCM", "Speaker", "Headphone", "Digital"]

# Virtual/software devices that don't support hardware volume control
VIRTUAL_AUDIO_DEVICES = ["null", "pulse", "dmix", "default"]

# Default volume percentage for virtual devices or when detection fails
DEFAULT_VOLUME_PERCENT = 75

# =============================================================================
# REGEX PATTERNS FOR ALSA OUTPUT PARSING
# =============================================================================
# These patterns parse output from ALSA command-line tools (aplay, amixer).
# They are designed to be resilient to minor formatting variations.

# Extracts card number from ALSA hardware device identifiers.
# Matches: "hw:0,0" -> "0", "hw:1,3" -> "1", "plughw:2,0" -> "2"
# Format: "hw:" followed by one or more digits (card number),
#         optionally followed by comma and device number.
# Used by: get_mixer_controls(), get_volume(), set_volume()
ALSA_CARD_NUMBER_PATTERN = re.compile(r"hw:([0-9]+)")

# Extracts mixer control name from amixer scontrols output.
# Matches: "Simple mixer control 'Master',0" -> "Master"
# Format: Single-quoted string within the amixer control listing.
# Note: Control names may contain spaces (e.g., 'Front Speaker').
# Used by: get_mixer_controls()
ALSA_CONTROL_NAME_PATTERN = re.compile(r"'([^']+)'")

# Extracts volume percentage from amixer sget/sset output.
# Matches: "[75%]" -> "75", "[0%]" -> "0", "[100%]" -> "100"
# Format: Square brackets containing digits followed by percent sign.
# Note: amixer outputs volume in multiple formats; we extract percentage.
# Example full line: "Front Left: Playback 65 [75%] [-16.50dB] [on]"
# Used by: get_volume()
ALSA_VOLUME_PERCENT_PATTERN = re.compile(r"\[(\d+)%\]")


class AudioManager:
    """
    Manages audio device detection and volume control.

    Provides methods to enumerate ALSA audio devices, detect available
    mixer controls, and get/set volume levels. Handles virtual devices
    gracefully by returning sensible defaults.

    Attributes:
        windows_mode: Whether running in Windows compatibility mode.
    """

    def __init__(self, windows_mode: bool = False) -> None:
        """
        Initialize the AudioManager.

        Args:
            windows_mode: If True, return simulated devices instead of
                         querying ALSA (for development on Windows).
        """
        self.windows_mode = windows_mode
        if windows_mode:
            logger.warning("AudioManager running in Windows compatibility mode")

    def get_devices(self) -> list[AudioDevice]:
        """
        Get list of available audio devices.

        On HAOS: Uses PulseAudio via `pactl list sinks short`.
        On standalone Docker: Uses ALSA via `aplay -l`.
        Always includes fallback virtual devices.

        Returns:
            List of audio device dictionaries, each containing:
            - id: Device identifier (PulseAudio sink name or ALSA device like 'hw:0,0')
            - name: Human-readable device name
            - card: Card identifier
            - device: Device number
        """
        if self.windows_mode:
            logger.info("Windows mode detected - returning simulated audio devices")
            return [
                {"id": "default", "name": "Default Audio Device (Windows)", "card": "0", "device": "0"},
                {"id": "pulse", "name": "PulseAudio (Network)", "card": "pulse", "device": "0"},
                {"id": "tcp:host.docker.internal:4713", "name": "Network Audio Stream", "card": "net", "device": "0"},
            ]

        # Use PulseAudio on HAOS, ALSA on standalone Docker
        if is_hassio():
            return self._get_pulseaudio_devices()
        else:
            return self._get_alsa_devices()

    def _get_pulseaudio_devices(self) -> list[AudioDevice]:
        """
        Get PulseAudio sinks for HAOS environment.

        Uses `pactl list sinks short` to enumerate available audio outputs.

        Returns:
            List of audio device dictionaries with PulseAudio sink names.
        """
        # Fallback devices for PulseAudio
        fallback_devices = [
            {"id": "null", "name": "Null Audio Device (Silent)", "card": "null", "device": "0"},
            {"id": "default", "name": "Default PulseAudio Output", "card": "default", "device": "0"},
        ]

        try:
            logger.debug("Attempting to detect PulseAudio sinks with pactl")
            result = subprocess.run(
                ["pactl", "list", "sinks", "short"],
                capture_output=True,
                text=True,
                timeout=10,
            )

            logger.debug(f"pactl list sinks short output:\n{result.stdout}")
            if result.stderr:
                logger.debug(f"pactl stderr:\n{result.stderr}")

            devices = []
            # Parse output: "0\talsa_output.pci-0000_00_1f.3.analog-stereo\tmodule-alsa-card.c\ts16le 2ch 44100Hz\tIDLE"
            for line in result.stdout.strip().split("\n"):
                if not line.strip():
                    continue
                parts = line.split("\t")
                if len(parts) >= 2:
                    sink_index = parts[0].strip()
                    sink_name = parts[1].strip()
                    # Create a friendly display name
                    display_name = sink_name.replace("alsa_output.", "").replace("_", " ").replace(".", " ")
                    devices.append(
                        {
                            "id": sink_name,
                            "name": f"{display_name} (sink {sink_index})",
                            "card": sink_index,
                            "device": "0",
                        }
                    )
                    logger.debug(f"Found PulseAudio sink: {sink_name}")

            if devices:
                logger.info(f"Found {len(devices)} PulseAudio sinks")
                return fallback_devices + devices
            else:
                logger.warning("No PulseAudio sinks found, using fallback devices only")
                return fallback_devices

        except subprocess.TimeoutExpired:
            logger.warning("Timeout running pactl, using fallback devices")
            return fallback_devices
        except FileNotFoundError:
            logger.warning("pactl command not found, using fallback devices only")
            return fallback_devices
        except Exception as e:
            logger.error(f"Unexpected error getting PulseAudio devices: {e}")
            return fallback_devices

    def _get_alsa_devices(self) -> list[AudioDevice]:
        """
        Get ALSA devices for standalone Docker environment.

        Uses `aplay -l` to enumerate hardware audio devices.

        Returns:
            List of audio device dictionaries with ALSA device identifiers.
        """
        # Always provide fallback devices
        fallback_devices = [
            {"id": "null", "name": "Null Audio Device (Silent)", "card": "null", "device": "0"},
            {"id": "default", "name": "Default Audio Device", "card": "0", "device": "0"},
            {"id": "dmix", "name": "Software Mixing Device", "card": "dmix", "device": "0"},
        ]

        try:
            logger.debug("Attempting to detect hardware audio devices with aplay -l")
            result = subprocess.run(["aplay", "-l"], capture_output=True, text=True, check=True)
            devices = []

            logger.debug(f"aplay -l output:\n{result.stdout}")

            # Parse actual audio devices
            for line in result.stdout.split("\n"):
                if "card" in line and ":" in line:
                    # Parse line like "card 0: PCH [HDA Intel PCH], device 0: ALC887-VD Analog [ALC887-VD Analog]"
                    parts = line.split(":")
                    if len(parts) >= 2:
                        card_info = parts[0].strip()
                        device_info = parts[1].strip()
                        # Extract card and device numbers
                        try:
                            card_num = card_info.split()[1]
                            if "device" in line:
                                device_num = line.split("device")[1].split(":")[0].strip()
                                device_id = f"hw:{card_num},{device_num}"
                                device_name = device_info.split("[")[0].strip() if "[" in device_info else device_info
                                devices.append(
                                    {
                                        "id": device_id,
                                        "name": f"{device_name} ({device_id})",
                                        "card": card_num,
                                        "device": device_num,
                                    }
                                )
                                logger.debug(f"Found hardware device: {device_name} -> {device_id}")
                        except (IndexError, ValueError) as e:
                            logger.warning(f"Error parsing audio device line: {line} - {e}")
                            continue

            # If we found real devices, add them to fallback devices
            if devices:
                logger.info(f"Found {len(devices)} hardware audio devices")
                return fallback_devices + devices
            else:
                logger.warning("No hardware audio devices found in aplay output, using fallback devices only")
                return fallback_devices

        except subprocess.CalledProcessError as e:
            logger.warning(f"Could not get audio devices list (aplay failed): {e}")
            return fallback_devices
        except FileNotFoundError:
            logger.warning("aplay command not found, using fallback devices only")
            return fallback_devices
        except Exception as e:
            logger.error(f"Unexpected error getting audio devices: {e}")
            return fallback_devices

    def get_mixer_controls(self, device: str) -> list[str]:
        """
        Get available ALSA mixer controls for a device.

        Queries amixer for the list of simple controls available on the
        specified sound card.

        Args:
            device: ALSA device identifier (e.g., 'hw:0,0').

        Returns:
            List of control names (e.g., ['Master', 'PCM', 'Headphone']).
            Returns DEFAULT_MIXER_CONTROLS for virtual devices.
        """
        if self.windows_mode or device in VIRTUAL_AUDIO_DEVICES:
            return DEFAULT_MIXER_CONTROLS.copy()

        try:
            # Extract card number from device ID (e.g., "hw:0,0" -> "0")
            card_match = ALSA_CARD_NUMBER_PATTERN.search(device)
            if not card_match:
                return DEFAULT_MIXER_CONTROLS.copy()

            card_num = card_match.group(1)
            result = subprocess.run(
                ["amixer", "-c", card_num, "scontrols"],
                capture_output=True,
                text=True,
                check=True,
            )

            controls = []
            for line in result.stdout.split("\n"):
                if "Simple mixer control" in line:
                    # Extract control name (e.g., "Simple mixer control 'Master',0" -> "Master")
                    match = ALSA_CONTROL_NAME_PATTERN.search(line)
                    if match:
                        controls.append(match.group(1))

            return controls if controls else DEFAULT_MIXER_CONTROLS.copy()

        except (subprocess.CalledProcessError, FileNotFoundError) as e:
            logger.warning(f"Could not get mixer controls for device {device}: {e}")
            return DEFAULT_MIXER_CONTROLS.copy()

    def get_volume(self, device: str, control: str = "Master") -> int:
        """
        Get the current volume for an audio device.

        Queries the ALSA mixer to read the current volume level. Tries multiple
        control names (Master, PCM, etc.) until one works.

        Args:
            device: ALSA device identifier (e.g., 'hw:0,0').
            control: Preferred control name (default: 'Master').

        Returns:
            Volume level as integer percentage (0-100).
            Returns DEFAULT_VOLUME_PERCENT for virtual devices or on error.
        """
        if self.windows_mode or device in VIRTUAL_AUDIO_DEVICES:
            logger.debug(f"Virtual device {device}, returning default volume")
            return DEFAULT_VOLUME_PERCENT

        try:
            # Extract card number from device ID (e.g., "hw:0,0" -> "0")
            card_match = ALSA_CARD_NUMBER_PATTERN.search(device)
            if not card_match:
                logger.debug(f"No card number found in device {device}, returning default volume")
                return DEFAULT_VOLUME_PERCENT

            card_num = card_match.group(1)

            for control_name in VOLUME_READ_CONTROLS:
                try:
                    result = subprocess.run(
                        ["amixer", "-c", card_num, "sget", control_name],
                        capture_output=True,
                        text=True,
                        check=True,
                    )

                    # Parse volume percentage from output (e.g., "[75%]" -> 75)
                    volume_match = ALSA_VOLUME_PERCENT_PATTERN.search(result.stdout)
                    if volume_match:
                        volume = int(volume_match.group(1))
                        logger.debug(f"Got volume {volume}% for device {device} control {control_name}")
                        return volume
                except subprocess.CalledProcessError:
                    continue  # Try next control name

            # If no controls worked, return default
            logger.warning(f"Could not find working volume control for device {device}")
            return DEFAULT_VOLUME_PERCENT

        except (subprocess.CalledProcessError, FileNotFoundError, ValueError) as e:
            logger.warning(f"Could not get volume for device {device}: {e}")
            return DEFAULT_VOLUME_PERCENT

    def set_volume(self, device: str, volume: int, control: str = "Master") -> tuple[bool, str]:
        """
        Set the volume for an audio device.

        Uses amixer to set the volume level on the ALSA mixer. Tries multiple
        control names (Master, PCM, etc.) until one works.

        Args:
            device: ALSA device identifier (e.g., 'hw:0,0').
            volume: Volume level as integer percentage (0-100).
            control: Preferred control name (default: 'Master').

        Returns:
            Tuple of (success: bool, message: str).
        """
        if not 0 <= volume <= 100:
            return False, "Volume must be between 0 and 100"

        if self.windows_mode or device in VIRTUAL_AUDIO_DEVICES:
            logger.info(f"Virtual device {device}, volume {volume}% stored (no hardware control)")
            return True, f"Volume set to {volume}% (virtual device)"

        try:
            # Extract card number from device ID (e.g., "hw:0,0" -> "0")
            card_match = ALSA_CARD_NUMBER_PATTERN.search(device)
            if not card_match:
                logger.debug(f"No card number found in device {device}, storing volume only")
                return True, f"Volume set to {volume}% (no hardware control)"

            card_num = card_match.group(1)

            for control_name in VOLUME_WRITE_CONTROLS:
                try:
                    subprocess.run(
                        ["amixer", "-c", card_num, "sset", control_name, f"{volume}%"],
                        capture_output=True,
                        text=True,
                        check=True,
                    )

                    logger.info(f"Set volume to {volume}% for device {device} control {control_name}")
                    return True, f"Volume set to {volume}% ({control_name})"
                except subprocess.CalledProcessError as e:
                    logger.debug(f"Control {control_name} failed for device {device}: {e}")
                    continue  # Try next control name

            # If no controls worked
            logger.warning(f"Could not find working volume control for device {device}")
            return False, f"No working volume controls found for device {device}"

        except subprocess.CalledProcessError as e:
            # Handle both string and bytes stderr
            if hasattr(e, "stderr") and e.stderr:
                if isinstance(e.stderr, bytes):
                    error_msg = e.stderr.decode()
                else:
                    error_msg = str(e.stderr)
            else:
                error_msg = str(e)
            logger.warning(f"Could not set volume for device {device}: {error_msg}")
            return False, f"Could not set volume: {error_msg}"
        except FileNotFoundError:
            logger.warning("amixer command not found")
            return False, "Audio mixer control not available"

    def is_virtual_device(self, device: str) -> bool:
        """
        Check if a device is a virtual/software device.

        Args:
            device: ALSA device identifier.

        Returns:
            True if the device is virtual (null, pulse, dmix, default).
        """
        return device in VIRTUAL_AUDIO_DEVICES

    def _is_pulseaudio_sink(self, device: str) -> bool:
        """
        Check if a device identifier looks like a PulseAudio sink name.

        PulseAudio sinks typically have names like:
        - alsa_output.pci-0000_00_1f.3.analog-stereo
        - bluez_sink.6C_5C_3D_3B_15_3F.a2dp_sink
        - combined

        Args:
            device: Device identifier to check.

        Returns:
            True if device looks like a PulseAudio sink name.
        """
        # PulseAudio sink patterns
        pulse_prefixes = ("alsa_output.", "bluez_sink.", "combined", "null_sink")
        if any(device.startswith(prefix) for prefix in pulse_prefixes):
            return True
        # Generic check: contains underscores and dots (typical PA naming)
        return "_" in device and "." in device and not device.startswith("hw:")

    def play_test_tone(
        self,
        device: str,
        duration_secs: float = 2.0,
        frequency_hz: int = 440,
    ) -> tuple[bool, str]:
        """
        Play a test tone on an audio device to verify output mapping.

        Routes to the appropriate backend based on device type:
        - ALSA devices (hw:X,Y): Uses speaker-test utility
        - PortAudio devices (numeric index): Uses sounddevice library
        - PulseAudio sinks: Uses paplay with generated tone

        Args:
            device: Device identifier. Either an ALSA device (e.g., 'hw:0,0'),
                   a PortAudio device index (e.g., '0', '1', '2'), or a
                   PulseAudio sink name (e.g., 'alsa_output.pci-...').
            duration_secs: How long to play the tone (default: 2 seconds).
            frequency_hz: Tone frequency in Hz (default: 440Hz, A4 note).

        Returns:
            Tuple of (success: bool, message: str).

        Note:
            - Virtual devices (null, dmix) will play silently or not at all
            - PortAudio support requires sounddevice and numpy packages
            - The tone plays synchronously and blocks until complete
        """
        if self.windows_mode:
            return False, "Test tone not available in Windows compatibility mode"

        # Check if this looks like a PortAudio device index (just a number)
        if device.isdigit():
            return self._play_test_tone_portaudio(int(device), duration_secs, frequency_hz)

        if device == "null":
            return True, "Test tone sent to null device (silent)"

        # Check if this is a PulseAudio sink name
        if self._is_pulseaudio_sink(device) or is_hassio():
            return self._play_test_tone_pulseaudio(device, duration_secs, frequency_hz)

        # Use ALSA speaker-test for ALSA devices
        return self._play_test_tone_alsa(device, duration_secs, frequency_hz)

    def _play_test_tone_portaudio(
        self,
        device_index: int,
        duration_secs: float,
        frequency_hz: int,
    ) -> tuple[bool, str]:
        """
        Play a test tone on a PortAudio device using sounddevice.

        Generates a sine wave and plays it through the specified PortAudio
        device index. Used for testing Sendspin audio outputs.

        Args:
            device_index: PortAudio device index (0, 1, 2, etc.).
            duration_secs: How long to play the tone.
            frequency_hz: Tone frequency in Hz.

        Returns:
            Tuple of (success: bool, message: str).
        """
        # Lazy import to avoid PortAudio C extension segfaults on Alpine/HAOS during module load
        try:
            import numpy as np
            import sounddevice as sd
        except ImportError:
            return False, "PortAudio test requires sounddevice package (not installed)"
        except Exception as e:
            return False, f"Failed to initialize PortAudio: {e}"

        try:
            # Get device info to verify it exists and get sample rate
            try:
                device_info = sd.query_devices(device_index, "output")
                sample_rate = int(device_info["default_samplerate"])
                device_name = device_info["name"]
            except (sd.PortAudioError, ValueError) as e:
                return False, f"Invalid PortAudio device {device_index}: {e}"

            logger.info(
                f"Playing test tone on PortAudio device {device_index} "
                f"({device_name}) at {frequency_hz}Hz for {duration_secs}s"
            )

            # Generate sine wave
            t = np.linspace(0, duration_secs, int(sample_rate * duration_secs), dtype=np.float32)
            tone = 0.5 * np.sin(2 * np.pi * frequency_hz * t)

            # Play the tone (blocking)
            sd.play(tone, samplerate=sample_rate, device=device_index)
            sd.wait()

            logger.info(f"Test tone completed on PortAudio device {device_index}")
            return True, f"Test tone played on {device_name}"

        except sd.PortAudioError as e:
            logger.warning(f"PortAudio error on device {device_index}: {e}")
            return False, f"PortAudio error: {e}"
        except Exception as e:
            logger.error(f"Error playing test tone on PortAudio device {device_index}: {e}")
            return False, f"Error: {str(e)}"

    def _play_test_tone_pulseaudio(
        self,
        device: str,
        duration_secs: float,
        frequency_hz: int,
    ) -> tuple[bool, str]:
        """
        Play a test tone on a PulseAudio sink using paplay.

        Generates a WAV file with a sine wave and plays it through the
        specified PulseAudio sink. Used for testing audio outputs on HAOS.

        Args:
            device: PulseAudio sink name (e.g., 'alsa_output.pci-...', 'bluez_sink.XXX').
            duration_secs: How long to play the tone.
            frequency_hz: Tone frequency in Hz.

        Returns:
            Tuple of (success: bool, message: str).
        """
        import struct
        import tempfile
        import wave

        try:
            # Generate WAV file with sine wave
            sample_rate = 44100
            num_samples = int(sample_rate * duration_secs)

            # Create temporary WAV file
            with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp_file:
                wav_path = tmp_file.name

            # Generate and write WAV data
            import math

            with wave.open(wav_path, "w") as wav_file:
                wav_file.setnchannels(2)  # Stereo
                wav_file.setsampwidth(2)  # 16-bit
                wav_file.setframerate(sample_rate)

                # Generate sine wave samples
                for i in range(num_samples):
                    # Sine wave at specified frequency, 50% volume
                    value = int(16383 * math.sin(2 * math.pi * frequency_hz * i / sample_rate))
                    # Pack as stereo (left and right channels)
                    packed = struct.pack("<hh", value, value)
                    wav_file.writeframes(packed)

            logger.info(f"Playing test tone on PulseAudio sink {device} at {frequency_hz}Hz for {duration_secs}s")

            # Build paplay command
            # paplay --device=<sink_name> <wav_file>
            cmd = ["paplay"]

            # Only add --device if it's not a default/generic name
            if device and device not in ("default", "pulse"):
                cmd.extend(["--device", device])

            cmd.append(wav_path)

            logger.debug(f"Running: {' '.join(cmd)}")

            # Run paplay with timeout
            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=duration_secs + 10,
            )

            # Clean up temp file
            import contextlib
            import os

            with contextlib.suppress(OSError):
                os.unlink(wav_path)

            if result.returncode == 0:
                logger.info(f"Test tone completed on PulseAudio sink {device}")
                return True, f"Test tone played on {device}"
            else:
                error_msg = result.stderr.strip() if result.stderr else "Unknown error"
                logger.warning(f"paplay failed for {device}: {error_msg}")
                return False, f"Test failed: {error_msg}"

        except subprocess.TimeoutExpired:
            logger.warning(f"Test tone timed out on PulseAudio sink {device}")
            return False, "Test tone timed out"
        except FileNotFoundError:
            logger.warning("paplay command not found")
            return False, "paplay utility not available (PulseAudio not installed)"
        except Exception as e:
            logger.error(f"Error playing test tone on PulseAudio sink {device}: {e}")
            return False, f"Error: {str(e)}"

    def _play_test_tone_alsa(
        self,
        device: str,
        duration_secs: float,
        frequency_hz: int,
    ) -> tuple[bool, str]:
        """
        Play a test tone on an ALSA device using speaker-test.

        Uses the ALSA speaker-test utility to generate a sine wave on the
        specified device. Used for testing Squeezelite audio outputs.

        Args:
            device: ALSA device identifier (e.g., 'hw:0,0', 'default').
            duration_secs: How long to play the tone.
            frequency_hz: Tone frequency in Hz.

        Returns:
            Tuple of (success: bool, message: str).
        """
        try:
            # speaker-test options:
            # -D device: ALSA device to use
            # -t sine: Generate sine wave
            # -f freq: Frequency in Hz
            # -l 1: Play 1 loop per channel
            # -c 2: Use 2 channels (stereo)

            cmd = [
                "speaker-test",
                "-D",
                device,
                "-t",
                "sine",
                "-f",
                str(frequency_hz),
                "-l",
                "1",
                "-c",
                "2",
            ]

            logger.info(f"Playing test tone on ALSA device {device}: {' '.join(cmd)}")

            # Run with timeout to prevent hanging
            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=duration_secs + 5,
            )

            if result.returncode == 0:
                logger.info(f"Test tone completed on ALSA device {device}")
                return True, f"Test tone played on {device}"
            else:
                error_msg = result.stderr.strip() if result.stderr else "Unknown error"
                logger.warning(f"speaker-test failed for {device}: {error_msg}")
                return False, f"Test failed: {error_msg}"

        except subprocess.TimeoutExpired:
            logger.warning(f"Test tone timed out on device {device}")
            return False, "Test tone timed out"
        except FileNotFoundError:
            logger.warning("speaker-test command not found")
            return False, "speaker-test utility not available"
        except Exception as e:
            logger.error(f"Error playing test tone on {device}: {e}")
            return False, f"Error: {str(e)}"
