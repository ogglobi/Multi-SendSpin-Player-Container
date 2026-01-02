"""
Process Manager for subprocess lifecycle management.

Handles starting, stopping, and monitoring subprocesses for audio players.
Provider-agnostic - just manages processes given commands to run.

Log streaming: All subprocess stdout/stderr is streamed to the main process
stdout so it appears in HAOS add-on logs. Logs are also written to files
for historical reference.
"""

import contextlib
import logging
import os
import signal
import subprocess
import sys
import threading
import time
from typing import IO

logger = logging.getLogger(__name__)


def _get_preexec_fn():
    """
    Get the appropriate preexec_fn for subprocess.Popen.

    Returns a function that calls os.setsid on Unix-like systems to create
    a new process group, allowing us to kill the entire group when stopping
    players. Returns None on Windows or if setsid is not available.
    """
    # Skip on Windows
    if sys.platform == "win32":
        return None

    # Check if setsid is available
    if not hasattr(os, "setsid"):
        logger.warning("os.setsid not available on this platform")
        return None

    def safe_setsid():
        """Wrapper around os.setsid with error handling."""
        import contextlib

        with contextlib.suppress(OSError):
            # OSError can happen if we're already a session leader
            # Silently continue - this is expected in some container environments
            os.setsid()

    return safe_setsid


def _stream_output(
    stream: IO[bytes],
    name: str,
    stream_type: str,
    log_file: IO[str] | None = None,
    stop_event: threading.Event | None = None,
) -> None:
    """
    Stream subprocess output to stdout and optionally to a log file.

    This function runs in a background thread and continuously reads from
    a subprocess pipe, writing each line to stdout (for HAOS visibility)
    and optionally to a log file (for historical reference).

    Args:
        stream: The subprocess stdout or stderr pipe to read from.
        name: Player name for log prefix.
        stream_type: Either "stdout" or "stderr" for log formatting.
        log_file: Optional file handle to write logs to.
        stop_event: Optional event to signal the thread to stop.
    """
    prefix = f"[{name}]"
    if stream_type == "stderr":
        prefix = f"[{name}:ERR]"

    try:
        while True:
            # Check stop event before blocking on readline
            if stop_event and stop_event.is_set():
                break

            try:
                line = stream.readline()
            except (OSError, ValueError):
                # Stream closed
                break

            # Empty bytes means EOF
            if not line:
                break

            # Decode and strip
            try:
                decoded = line.decode("utf-8", errors="replace").rstrip()
            except (AttributeError, TypeError):
                # Handle mock objects in tests that return non-bytes
                break

            if decoded:
                # Write to stdout so HAOS can see it
                with contextlib.suppress(Exception):
                    print(f"{prefix} {decoded}", flush=True)

                # Also write to log file if provided
                if log_file:
                    with contextlib.suppress(Exception):
                        log_file.write(f"{decoded}\n")
                        log_file.flush()
    except Exception as e:
        logger.debug(f"Stream reader for {name} ended: {e}")
    finally:
        with contextlib.suppress(Exception):
            stream.close()


# =============================================================================
# CONSTANTS
# =============================================================================

# Delay after starting a process to check if it failed immediately
PROCESS_STARTUP_DELAY_SECS = 0.5

# Timeout when waiting for process to stop gracefully (SIGTERM)
PROCESS_STOP_TIMEOUT_SECS = 5

# Timeout when waiting for process to be killed forcefully (SIGKILL)
PROCESS_KILL_TIMEOUT_SECS = 2


class ProcessManager:
    """
    Manages subprocess lifecycle for audio players.

    Provider-agnostic process management - handles starting, stopping,
    and monitoring subprocesses given commands to run. Does not know
    about specific player implementations.

    Subprocess output is streamed to stdout (for HAOS visibility) and
    also written to log files (for historical reference).

    Attributes:
        processes: Dictionary mapping player names to their Popen instances.
        log_dir: Directory for process log files.
        _stream_threads: Dictionary mapping player names to their streaming threads.
        _log_files: Dictionary mapping player names to their open log file handles.
        _stop_events: Dictionary mapping player names to their stop events.
    """

    def __init__(self, log_dir: str = "/app/logs") -> None:
        """
        Initialize the ProcessManager.

        Args:
            log_dir: Directory for process log files.
        """
        self.processes: dict[str, subprocess.Popen[bytes]] = {}
        self.log_dir = log_dir

        # Tracking for log streaming
        self._stream_threads: dict[str, list[threading.Thread]] = {}
        self._log_files: dict[str, IO[str]] = {}
        self._stop_events: dict[str, threading.Event] = {}

        # Ensure log directory exists
        os.makedirs(log_dir, exist_ok=True)
        logger.info(f"ProcessManager initialized with log_dir: {log_dir}")

    def start(
        self,
        name: str,
        command: list[str],
        fallback_command: list[str] | None = None,
    ) -> tuple[bool, str]:
        """
        Start a subprocess for a player.

        Launches a new subprocess with the given command. If the process
        fails immediately and a fallback command is provided, tries the
        fallback.

        Args:
            name: Unique name for this process (used as key).
            command: Command and arguments to run.
            fallback_command: Optional fallback command if primary fails.

        Returns:
            Tuple of (success: bool, message: str).

        Side Effects:
            - Adds process to self.processes dict
            - Creates process group for signal handling
        """
        if name in self.processes and self.processes[name].poll() is None:
            return False, f"Process '{name}' is already running"

        logger.info(f"Starting process '{name}' with command: {' '.join(command)}")

        try:
            # Clean up any previous log streaming resources
            self._cleanup_streaming(name)

            # Start the process in its own process group
            preexec = _get_preexec_fn()
            logger.debug(f"Starting subprocess with preexec_fn={preexec is not None}")

            process = subprocess.Popen(
                command,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                preexec_fn=preexec,
            )

            self.processes[name] = process

            # Give the process a moment to start and check if it fails immediately
            time.sleep(PROCESS_STARTUP_DELAY_SECS)

            if process.poll() is not None:
                # Process terminated immediately, check error
                stdout, stderr = process.communicate()
                error_msg = stderr.decode() if stderr else "Unknown error"
                logger.error(f"Process '{name}' failed to start: {error_msg}")
                # Print to stdout so HAOS can see it
                print(f"[{name}:ERR] Failed to start: {error_msg}", flush=True)

                # Try fallback if provided
                if fallback_command:
                    logger.info(f"Trying fallback command for '{name}'")
                    return self._start_fallback(name, fallback_command)

                return False, f"Process failed to start: {error_msg}"

            # Process started successfully - set up log streaming
            self._setup_streaming(name, process)

            logger.info(f"Started process '{name}' with PID {process.pid}")
            print(f"[{name}] Started with PID {process.pid}", flush=True)
            return True, f"Process '{name}' started successfully"

        except FileNotFoundError:
            binary = command[0] if command else "unknown"
            logger.error(f"Binary '{binary}' not found")
            print(f"[{name}:ERR] Binary '{binary}' not found", flush=True)
            return False, f"Binary '{binary}' not found"
        except Exception as e:
            logger.error(f"Error starting process '{name}': {e}")
            print(f"[{name}:ERR] Error starting: {e}", flush=True)
            return False, f"Error starting process: {e}"

    def _start_fallback(self, name: str, command: list[str]) -> tuple[bool, str]:
        """
        Start a fallback process after primary failed.

        Args:
            name: Process name.
            command: Fallback command to run.

        Returns:
            Tuple of (success: bool, message: str).
        """
        logger.info(f"Fallback command for '{name}': {' '.join(command)}")
        print(f"[{name}] Trying fallback configuration...", flush=True)

        try:
            process = subprocess.Popen(
                command,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                preexec_fn=_get_preexec_fn(),
            )

            self.processes[name] = process
            time.sleep(PROCESS_STARTUP_DELAY_SECS)

            if process.poll() is not None:
                stdout, stderr = process.communicate()
                error_msg = stderr.decode() if stderr else "Unknown error"
                print(f"[{name}:ERR] Fallback also failed: {error_msg}", flush=True)
                return False, f"Fallback also failed: {error_msg}"

            # Process started successfully - set up log streaming
            self._setup_streaming(name, process)

            logger.info(f"Started process '{name}' with fallback (PID {process.pid})")
            print(f"[{name}] Started with fallback (PID {process.pid})", flush=True)
            return True, f"Process '{name}' started with fallback configuration"

        except Exception as e:
            logger.error(f"Error starting fallback for '{name}': {e}")
            print(f"[{name}:ERR] Fallback error: {e}", flush=True)
            return False, f"Error starting fallback: {e}"

    def stop(self, name: str) -> tuple[bool, str]:
        """
        Stop a subprocess.

        Sends SIGTERM to gracefully stop the process. If the process doesn't
        terminate within PROCESS_STOP_TIMEOUT_SECS, sends SIGKILL.

        Args:
            name: Name of the process to stop.

        Returns:
            Tuple of (success: bool, message: str).

        Side Effects:
            - Removes process from self.processes dict
            - Cleans up log streaming threads and files
        """
        if name not in self.processes:
            return False, f"Process '{name}' not found"

        process = self.processes[name]
        if process.poll() is not None:
            # Process already terminated
            del self.processes[name]
            self._cleanup_streaming(name)
            return False, f"Process '{name}' was not running"

        try:
            print(f"[{name}] Stopping...", flush=True)

            # Send SIGTERM to the process group
            os.killpg(os.getpgid(process.pid), signal.SIGTERM)  # type: ignore[attr-defined]

            # Wait for process to terminate
            process.wait(timeout=PROCESS_STOP_TIMEOUT_SECS)
            del self.processes[name]
            self._cleanup_streaming(name)
            logger.info(f"Stopped process '{name}'")
            print(f"[{name}] Stopped", flush=True)
            return True, f"Process '{name}' stopped successfully"

        except subprocess.TimeoutExpired:
            # Force kill if it doesn't respond to SIGTERM
            try:
                os.killpg(os.getpgid(process.pid), signal.SIGKILL)  # type: ignore[attr-defined]
                process.wait(timeout=PROCESS_KILL_TIMEOUT_SECS)
            except Exception:
                pass
            del self.processes[name]
            self._cleanup_streaming(name)
            logger.info(f"Force stopped process '{name}'")
            print(f"[{name}] Force stopped", flush=True)
            return True, f"Process '{name}' force stopped"
        except Exception as e:
            logger.error(f"Error stopping process '{name}': {e}")
            print(f"[{name}:ERR] Error stopping: {e}", flush=True)
            return False, f"Error stopping process: {e}"

    def is_running(self, name: str) -> bool:
        """
        Check if a process is running.

        Args:
            name: Name of the process.

        Returns:
            True if the process is running, False otherwise.
        """
        if name not in self.processes:
            return False

        process = self.processes[name]
        return process.poll() is None

    def get_all_statuses(self, player_names: list[str]) -> dict[str, bool]:
        """
        Get running status of all specified players.

        Args:
            player_names: List of player names to check.

        Returns:
            Dictionary mapping player names to their running status.
        """
        statuses = {}
        for name in player_names:
            statuses[name] = self.is_running(name)
        return statuses

    def get_process(self, name: str) -> subprocess.Popen | None:
        """
        Get the Popen object for a process.

        Args:
            name: Name of the process.

        Returns:
            Popen object if found and running, None otherwise.
        """
        if name in self.processes and self.processes[name].poll() is None:
            return self.processes[name]
        return None

    def cleanup_dead_processes(self) -> list[str]:
        """
        Remove terminated processes from tracking.

        Checks all tracked processes and removes any that have terminated.

        Returns:
            List of names of processes that were cleaned up.
        """
        cleaned = []
        for name in list(self.processes.keys()):
            if self.processes[name].poll() is not None:
                del self.processes[name]
                self._cleanup_streaming(name)
                cleaned.append(name)
                logger.debug(f"Cleaned up terminated process '{name}'")
        return cleaned

    def stop_all(self) -> int:
        """
        Stop all running processes.

        Returns:
            Number of processes that were stopped.
        """
        stopped = 0
        for name in list(self.processes.keys()):
            success, _ = self.stop(name)
            if success:
                stopped += 1
        return stopped

    def get_log_path(self, name: str) -> str:
        """
        Get the log file path for a process.

        Args:
            name: Name of the process.

        Returns:
            Path to the log file.
        """
        return os.path.join(self.log_dir, f"{name}.log")

    def _setup_streaming(self, name: str, process: subprocess.Popen[bytes]) -> None:
        """
        Set up log streaming threads for a process.

        Creates background threads to read stdout/stderr from the process
        and forward them to stdout (for HAOS visibility) and a log file.

        Args:
            name: Process name for log prefixing.
            process: The subprocess to stream output from.
        """
        # Create stop event for clean shutdown
        stop_event = threading.Event()
        self._stop_events[name] = stop_event

        # Open log file for this process
        log_path = self.get_log_path(name)
        try:
            log_file = open(log_path, "a", encoding="utf-8")  # noqa: SIM115
            self._log_files[name] = log_file
            logger.debug(f"Opened log file for '{name}': {log_path}")
        except Exception as e:
            logger.warning(f"Could not open log file for '{name}': {e}")
            log_file = None

        # Create streaming threads
        threads = []

        if process.stdout:
            stdout_thread = threading.Thread(
                target=_stream_output,
                args=(process.stdout, name, "stdout", log_file, stop_event),
                daemon=True,
                name=f"{name}-stdout",
            )
            stdout_thread.start()
            threads.append(stdout_thread)

        if process.stderr:
            stderr_thread = threading.Thread(
                target=_stream_output,
                args=(process.stderr, name, "stderr", log_file, stop_event),
                daemon=True,
                name=f"{name}-stderr",
            )
            stderr_thread.start()
            threads.append(stderr_thread)

        self._stream_threads[name] = threads
        logger.debug(f"Started {len(threads)} streaming threads for '{name}'")

    def _cleanup_streaming(self, name: str) -> None:
        """
        Clean up log streaming resources for a process.

        Signals streaming threads to stop, closes log files, and removes
        tracking entries.

        Args:
            name: Process name to clean up.
        """
        # Signal threads to stop
        if name in self._stop_events:
            self._stop_events[name].set()
            del self._stop_events[name]

        # Wait briefly for threads to finish (they're daemon threads so won't block)
        if name in self._stream_threads:
            for thread in self._stream_threads[name]:
                thread.join(timeout=0.5)
            del self._stream_threads[name]

        # Close log file
        if name in self._log_files:
            with contextlib.suppress(Exception):
                self._log_files[name].close()
            del self._log_files[name]

        logger.debug(f"Cleaned up streaming resources for '{name}'")
