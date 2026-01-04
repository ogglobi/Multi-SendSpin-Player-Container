// Multi-Room Audio Controller - JavaScript App

// State
let players = {};
let devices = [];
let connection = null;

// XSS protection
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Initialize app
document.addEventListener('DOMContentLoaded', async () => {
    // Load initial data
    await Promise.all([
        refreshStatus(),
        refreshDevices()
    ]);

    // Set up volume slider preview
    const volumeSlider = document.getElementById('initialVolume');
    const volumeValue = document.getElementById('initialVolumeValue');
    if (volumeSlider && volumeValue) {
        volumeSlider.addEventListener('input', () => {
            volumeValue.textContent = volumeSlider.value + '%';
        });
    }

    // Set up SignalR connection
    setupSignalR();

    // Poll for status updates as fallback
    setInterval(refreshStatus, 5000);
});

// SignalR setup
function setupSignalR() {
    const statusBadge = document.getElementById('connection-status');

    // SignalR is optional - use polling as fallback
    if (typeof signalR === 'undefined') {
        console.log('SignalR not available, using polling');
        statusBadge.textContent = 'Polling';
        statusBadge.className = 'badge bg-info me-2';
        return;
    }

    connection = new signalR.HubConnectionBuilder()
        .withUrl('./hubs/status')
        .withAutomaticReconnect()
        .build();

    connection.on('PlayerStatusUpdate', (data) => {
        console.log('Status update:', data);
        if (data.players) {
            players = data.players;
            renderPlayers();
        }
    });

    connection.onreconnecting(() => {
        statusBadge.textContent = 'Reconnecting...';
        statusBadge.className = 'badge bg-warning me-2';
    });

    connection.onreconnected(() => {
        statusBadge.textContent = 'Connected';
        statusBadge.className = 'badge bg-success me-2';
    });

    connection.onclose(() => {
        statusBadge.textContent = 'Disconnected';
        statusBadge.className = 'badge bg-danger me-2';
    });

    connection.start()
        .then(() => {
            statusBadge.textContent = 'Connected';
            statusBadge.className = 'badge bg-success me-2';
        })
        .catch(err => {
            console.log('SignalR connection failed, using polling:', err);
            statusBadge.textContent = 'Polling';
            statusBadge.className = 'badge bg-info me-2';
        });
}

// API calls
async function refreshStatus() {
    try {
        const response = await fetch('./api/players');
        if (!response.ok) throw new Error('Failed to fetch players');

        const data = await response.json();
        players = {};
        (data.players || []).forEach(p => {
            players[p.name] = p;
        });

        renderPlayers();
    } catch (error) {
        console.error('Error refreshing status:', error);
        showAlert('Failed to load players', 'danger');
    }
}

async function refreshDevices() {
    try {
        const response = await fetch('./api/devices');
        if (!response.ok) throw new Error('Failed to fetch devices');

        const data = await response.json();
        devices = data.devices || [];

        // Update device selects
        const selects = document.querySelectorAll('#audioDevice, #editAudioDevice');
        selects.forEach(select => {
            const currentValue = select.value;
            select.innerHTML = '<option value="">Default Device</option>';
            devices.forEach(device => {
                const option = document.createElement('option');
                option.value = device.id;
                option.textContent = `${device.name}${device.isDefault ? ' (default)' : ''}`;
                select.appendChild(option);
            });
            if (currentValue) select.value = currentValue;
        });
    } catch (error) {
        console.error('Error refreshing devices:', error);
    }
}

async function addPlayer() {
    const name = document.getElementById('playerName').value.trim();
    const device = document.getElementById('audioDevice').value;
    const serverUrl = document.getElementById('serverUrl').value.trim();
    const volume = parseInt(document.getElementById('initialVolume').value);

    if (!name) {
        showAlert('Please enter a player name', 'warning');
        return;
    }

    try {
        const response = await fetch('./api/players', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                name,
                device: device || null,
                serverUrl: serverUrl || null,
                volume,
                persist: true
            })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to create player');
        }

        // Close modal and refresh
        bootstrap.Modal.getInstance(document.getElementById('addPlayerModal')).hide();
        document.getElementById('addPlayerForm').reset();
        document.getElementById('initialVolumeValue').textContent = '75%';
        await refreshStatus();
        showAlert(`Player "${name}" created successfully`, 'success');
    } catch (error) {
        console.error('Error adding player:', error);
        showAlert(error.message, 'danger');
    }
}

async function deletePlayer(name) {
    if (!confirm(`Delete player "${name}"? This will also remove its configuration.`)) {
        return;
    }

    try {
        const response = await fetch(`./api/players/${encodeURIComponent(name)}`, {
            method: 'DELETE'
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to delete player');
        }

        await refreshStatus();
        showAlert(`Player "${name}" deleted`, 'success');
    } catch (error) {
        console.error('Error deleting player:', error);
        showAlert(error.message, 'danger');
    }
}

async function stopPlayer(name) {
    try {
        const response = await fetch(`./api/players/${encodeURIComponent(name)}/stop`, {
            method: 'POST'
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to stop player');
        }

        await refreshStatus();
        showAlert(`Player "${name}" stopped`, 'info');
    } catch (error) {
        console.error('Error stopping player:', error);
        showAlert(error.message, 'danger');
    }
}

async function restartPlayer(name) {
    try {
        const response = await fetch(`./api/players/${encodeURIComponent(name)}/restart`, {
            method: 'POST'
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to restart player');
        }

        await refreshStatus();
        showAlert(`Player "${name}" restarted`, 'success');
    } catch (error) {
        console.error('Error restarting player:', error);
        showAlert(error.message, 'danger');
    }
}

async function setVolume(name, volume) {
    try {
        const response = await fetch(`./api/players/${encodeURIComponent(name)}/volume`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ volume: parseInt(volume) })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to set volume');
        }

        // Update local state
        if (players[name]) {
            players[name].volume = volume;
        }
    } catch (error) {
        console.error('Error setting volume:', error);
        showAlert(error.message, 'danger');
    }
}

async function setDelay(name, delayMs) {
    // Clamp value to valid range
    delayMs = Math.max(-5000, Math.min(5000, parseInt(delayMs) || 0));

    // Update input field to show clamped value
    const input = document.getElementById('delayInput');
    if (input) input.value = delayMs;

    try {
        const response = await fetch(`./api/players/${encodeURIComponent(name)}/offset`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ delayMs: delayMs })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to set delay');
        }

        // Update local state
        if (players[name]) {
            players[name].delayMs = delayMs;
        }
    } catch (error) {
        console.error('Error setting delay:', error);
        showAlert(error.message, 'danger');
    }
}

function adjustDelay(name, delta) {
    const input = document.getElementById('delayInput');
    if (!input) return;

    const currentValue = parseInt(input.value) || 0;
    const newValue = Math.max(-5000, Math.min(5000, currentValue + delta));
    input.value = newValue;
    setDelay(name, newValue);
}

async function showPlayerStats(name) {
    const player = players[name];
    if (!player) return;

    const modal = document.getElementById('playerStatsModal');
    const body = document.getElementById('playerStatsBody');

    body.innerHTML = `
        <div class="row">
            <div class="col-md-6">
                <h6 class="text-muted text-uppercase small">Configuration</h6>
                <table class="table table-sm">
                    <tr><td><strong>Name</strong></td><td>${escapeHtml(player.name)}</td></tr>
                    <tr><td><strong>Device</strong></td><td>${escapeHtml(player.device || 'default')}</td></tr>
                    <tr><td><strong>Client ID</strong></td><td><code>${escapeHtml(player.clientId)}</code></td></tr>
                    <tr><td><strong>Server</strong></td><td>${escapeHtml(player.serverUrl || 'Auto-discovered')}</td></tr>
                    <tr><td><strong>Volume</strong></td><td>${player.volume}%</td></tr>
                </table>
            </div>
            <div class="col-md-6">
                <h6 class="text-muted text-uppercase small">Status</h6>
                <table class="table table-sm">
                    <tr><td><strong>State</strong></td><td><span class="badge bg-${getStateBadgeClass(player.state)}">${player.state}</span></td></tr>
                    <tr><td><strong>Clock Synced</strong></td><td>${player.isClockSynced ? '<i class="fas fa-check text-success"></i> Yes' : '<i class="fas fa-times text-danger"></i> No'}</td></tr>
                    <tr><td><strong>Muted</strong></td><td>${player.isMuted ? 'Yes' : 'No'}</td></tr>
                    <tr><td><strong>Output Latency</strong></td><td>${player.outputLatencyMs}ms</td></tr>
                    <tr><td><strong>Created</strong></td><td>${new Date(player.createdAt).toLocaleString()}</td></tr>
                    ${player.connectedAt ? `<tr><td><strong>Connected</strong></td><td>${new Date(player.connectedAt).toLocaleString()}</td></tr>` : ''}
                    ${player.errorMessage ? `<tr><td><strong>Error</strong></td><td class="text-danger">${escapeHtml(player.errorMessage)}</td></tr>` : ''}
                </table>
            </div>
        </div>
        <h6 class="text-muted text-uppercase small mt-3">Delay Offset</h6>
        <div class="delay-control d-flex align-items-center gap-2">
            <button class="btn btn-outline-secondary btn-sm" onclick="adjustDelay('${escapeHtml(name)}', -10)" title="Decrease by 10ms">
                <i class="fas fa-minus"></i>
            </button>
            <div class="input-group input-group-sm" style="max-width: 140px;">
                <input type="number" class="form-control text-center" id="delayInput"
                    value="${player.delayMs}" min="-5000" max="5000" step="10"
                    onchange="setDelay('${escapeHtml(name)}', this.value)"
                    onkeydown="if(event.key==='Enter'){setDelay('${escapeHtml(name)}', this.value); event.preventDefault();}">
                <span class="input-group-text">ms</span>
            </div>
            <button class="btn btn-outline-secondary btn-sm" onclick="adjustDelay('${escapeHtml(name)}', 10)" title="Increase by 10ms">
                <i class="fas fa-plus"></i>
            </button>
            <small class="text-muted ms-2">Range: -5000 to +5000ms</small>
        </div>
        ${player.metrics ? `
        <h6 class="text-muted text-uppercase small mt-3">Metrics</h6>
        <div class="d-flex flex-wrap">
            <span class="metric-badge bg-light border"><i class="fas fa-database"></i> Buffer: ${player.metrics.bufferLevel}/${player.metrics.bufferCapacity}ms</span>
            <span class="metric-badge bg-light border"><i class="fas fa-music"></i> Samples: ${player.metrics.samplesPlayed.toLocaleString()}</span>
            <span class="metric-badge ${player.metrics.underruns > 0 ? 'bg-warning' : 'bg-light border'}"><i class="fas fa-exclamation-triangle"></i> Underruns: ${player.metrics.underruns}</span>
            <span class="metric-badge ${player.metrics.overruns > 0 ? 'bg-warning' : 'bg-light border'}"><i class="fas fa-level-up-alt"></i> Overruns: ${player.metrics.overruns}</span>
        </div>
        ` : ''}
    `;

    bootstrap.Modal.getOrCreateInstance(modal).show();
}

// Render players
function renderPlayers() {
    const container = document.getElementById('players-container');
    const playerNames = Object.keys(players);

    if (playerNames.length === 0) {
        container.innerHTML = `
            <div class="col-12">
                <div class="empty-state">
                    <i class="fas fa-volume-mute"></i>
                    <h4>No Players Configured</h4>
                    <p>Click "Add Player" to create your first audio player.</p>
                </div>
            </div>
        `;
        return;
    }

    container.innerHTML = playerNames.map(name => {
        const player = players[name];
        const stateClass = getStateClass(player.state);
        const stateBadgeClass = getStateBadgeClass(player.state);

        return `
            <div class="col-md-6 col-lg-4 mb-3">
                <div class="card player-card h-100" data-player="${escapeHtml(name)}">
                    <div class="card-body">
                        <div class="d-flex justify-content-between align-items-start mb-2">
                            <h5 class="card-title mb-0">${escapeHtml(player.name)}</h5>
                            <div class="d-flex">
                                <button class="btn btn-sm btn-outline-info me-1" onclick="showPlayerStats('${escapeHtml(name)}')" title="Player Stats">
                                    <i class="fas fa-chart-bar"></i>
                                </button>
                                <div class="dropdown">
                                    <button class="btn btn-sm btn-outline-secondary" type="button" data-bs-toggle="dropdown">
                                        <i class="fas fa-ellipsis-v"></i>
                                    </button>
                                    <ul class="dropdown-menu">
                                        <li><a class="dropdown-item" href="#" onclick="restartPlayer('${escapeHtml(name)}'); return false;"><i class="fas fa-sync me-2"></i>Restart</a></li>
                                        <li><a class="dropdown-item" href="#" onclick="stopPlayer('${escapeHtml(name)}'); return false;"><i class="fas fa-stop me-2"></i>Stop</a></li>
                                        <li><hr class="dropdown-divider"></li>
                                        <li><a class="dropdown-item text-danger" href="#" onclick="deletePlayer('${escapeHtml(name)}'); return false;"><i class="fas fa-trash me-2"></i>Delete</a></li>
                                    </ul>
                                </div>
                            </div>
                        </div>

                        <div class="status-container mb-3">
                            <span class="status-indicator ${stateClass}"></span>
                            <span class="badge bg-${stateBadgeClass}">${player.state}</span>
                            <small class="text-muted ms-2">${escapeHtml(player.device || 'default')}</small>
                        </div>

                        <div class="volume-control">
                            <div class="d-flex align-items-center">
                                <i class="fas fa-volume-down me-2"></i>
                                <input type="range" class="form-range flex-grow-1" min="0" max="100" value="${player.volume}"
                                    onchange="setVolume('${escapeHtml(name)}', this.value)"
                                    oninput="this.nextElementSibling.textContent = this.value + '%'">
                                <span class="volume-display ms-2">${player.volume}%</span>
                            </div>
                        </div>

                        ${player.isClockSynced ? `
                            <small class="text-success"><i class="fas fa-clock me-1"></i>Clock synced</small>
                        ` : ''}
                        ${player.errorMessage ? `
                            <small class="text-danger d-block"><i class="fas fa-exclamation-circle me-1"></i>${escapeHtml(player.errorMessage)}</small>
                        ` : ''}
                    </div>
                </div>
            </div>
        `;
    }).join('');
}

function getStateClass(state) {
    const stateMap = {
        'Playing': 'status-playing',
        'Connected': 'status-connected',
        'Buffering': 'status-buffering',
        'Starting': 'status-starting',
        'Connecting': 'status-connecting',
        'Stopped': 'status-stopped',
        'Error': 'status-error'
    };
    return stateMap[state] || 'status-stopped';
}

function getStateBadgeClass(state) {
    const stateMap = {
        'Playing': 'success',
        'Connected': 'success',
        'Buffering': 'warning',
        'Starting': 'info',
        'Connecting': 'info',
        'Created': 'secondary',
        'Stopped': 'secondary',
        'Error': 'danger'
    };
    return stateMap[state] || 'secondary';
}

// Alert helpers
function showAlert(message, type = 'info') {
    const container = document.getElementById('alert-container');
    const alert = document.createElement('div');
    alert.className = `alert alert-${type} alert-dismissible fade show`;
    alert.innerHTML = `
        ${escapeHtml(message)}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    container.appendChild(alert);

    // Auto-dismiss after 5 seconds
    setTimeout(() => {
        alert.classList.remove('show');
        setTimeout(() => alert.remove(), 150);
    }, 5000);
}
