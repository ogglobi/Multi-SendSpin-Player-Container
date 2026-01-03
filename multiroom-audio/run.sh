#!/usr/bin/env bashio
# Home Assistant Add-on startup script
# Reads configuration from /data/options.json and starts the application

set -e

# Ensure PATH includes all common binary locations
# pip on Alpine may install to /usr/local/bin or Python-specific paths
export PATH="/usr/local/bin:/usr/bin:/bin:/usr/local/sbin:/usr/sbin:/sbin:$PATH"

# Read options from Home Assistant add-on config
CONFIG_PATH="/data/options.json"

if [ -f "$CONFIG_PATH" ]; then
    LOG_LEVEL=$(bashio::config 'log_level' 'info')
else
    LOG_LEVEL="info"
fi

# Export environment variables for the Python app
export LOG_LEVEL="${LOG_LEVEL}"
export CONFIG_PATH="/data"
export LOG_PATH="/share/multiroom-audio/logs"
export AUDIO_BACKEND="pulse"
export WEB_PORT="8095"
export FLASK_ENV="production"

# Ensure directories exist
mkdir -p /share/multiroom-audio/logs

# Log startup information
bashio::log.info "Starting Multi-Room Audio Controller..."
bashio::log.info "Log level: ${LOG_LEVEL}"
bashio::log.info "Audio backend: pulse (HAOS)"

# List available audio devices
bashio::log.info "Detecting audio devices..."
if command -v pactl &> /dev/null; then
    bashio::log.info "PulseAudio sinks:"
    pactl list sinks short 2>/dev/null || bashio::log.warning "Could not list PulseAudio sinks"
fi

# Check for player binaries and log their locations
bashio::log.info "Checking player binaries..."
bashio::log.info "PATH: ${PATH}"

SQUEEZELITE_PATH=$(which squeezelite 2>/dev/null || true)
if [ -n "$SQUEEZELITE_PATH" ]; then
    bashio::log.info "✓ squeezelite found at: ${SQUEEZELITE_PATH}"
else
    bashio::log.warning "✗ squeezelite not found in PATH"
    # Check common locations manually
    for loc in /usr/bin/squeezelite /usr/local/bin/squeezelite; do
        if [ -x "$loc" ]; then
            bashio::log.info "  Found at $loc (adding to PATH)"
            export PATH="$(dirname $loc):$PATH"
        fi
    done
fi

SNAPCLIENT_PATH=$(which snapclient 2>/dev/null || true)
if [ -n "$SNAPCLIENT_PATH" ]; then
    bashio::log.info "✓ snapclient found at: ${SNAPCLIENT_PATH}"
else
    bashio::log.warning "✗ snapclient not found (optional)"
fi

SENDSPIN_PATH=$(which sendspin 2>/dev/null || true)
if [ -n "$SENDSPIN_PATH" ]; then
    bashio::log.info "✓ sendspin found at: ${SENDSPIN_PATH}"
else
    bashio::log.warning "✗ sendspin not found in PATH"
    # Check Python script locations
    for loc in /usr/bin/sendspin /usr/local/bin/sendspin; do
        if [ -x "$loc" ]; then
            bashio::log.info "  Found at $loc (adding to PATH)"
            export PATH="$(dirname $loc):$PATH"
        fi
    done
fi

bashio::log.info "Final PATH: ${PATH}"

# Start the Flask application
bashio::log.info "Starting web interface on port 8095..."
cd /app

# Use Flask's built-in server directly
# Note: We avoid gunicorn due to fork-related segfaults with SocketIO and audio libraries.
# Flask's dev server is sufficient for home automation with limited concurrent users.
exec python3 app.py
