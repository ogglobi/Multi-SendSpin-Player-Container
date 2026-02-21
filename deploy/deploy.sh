#!/bin/bash
# Volume Scale Feature - Deployment Script
# ==========================================
# Dieses Script kopiert die geänderten Dateien an die richtigen Orte
# und baut die Anwendung neu.

set -e  # Stop bei Fehlern

echo "=========================================="
echo "Volume Scale Feature Deployment"
echo "=========================================="

# Pfade
SRC_DIR="/tmp/deploy"
APP_DIR="/opt/multiroom"
TARGET_DIR="$APP_DIR/src/MultiRoomAudio"

# Prüfen ob wir im richtigen Verzeichnis sind
if [ ! -d "$SRC_DIR" ]; then
    echo "FEHLER: $SRC_DIR nicht gefunden!"
    echo "Bitte zuerst den deploy-Ordner auf die VM kopieren:"
    echo "  scp -r deploy root@192.168.10.182:/tmp/"
    exit 1
fi

echo ""
echo "1. Dateien kopieren..."
echo "----------------------"

# Controllers
echo "  - Controllers/SettingsEndpoint.cs"
cp "$SRC_DIR/Controllers/SettingsEndpoint.cs" "$TARGET_DIR/Controllers/"

# Services
echo "  - Services/ConfigurationService.cs"
cp "$SRC_DIR/Services/ConfigurationService.cs" "$TARGET_DIR/Services/"

echo "  - Services/PlayerManagerService.cs"
cp "$SRC_DIR/Services/PlayerManagerService.cs" "$TARGET_DIR/Services/"

# Program.cs
echo "  - Program.cs"
cp "$SRC_DIR/Program.cs" "$TARGET_DIR/"

echo ""
echo "2. Anwendung bauen..."
echo "----------------------"
cd "$TARGET_DIR"

# .NET prüfen
DOTNET_CMD="/root/.dotnet/dotnet"
if [ ! -x "$DOTNET_CMD" ]; then
    echo "FEHLER: dotnet nicht gefunden unter $DOTNET_CMD"
    echo "Suche nach dotnet..."
    DOTNET_CMD=$(which dotnet 2>/dev/null || echo "")
    if [ -z "$DOTNET_CMD" ]; then
        echo "FEHLER: Kein dotnet gefunden!"
        exit 1
    fi
fi

echo "Verwende: $DOTNET_CMD"

# Build
echo "Baue Release für linux-x64..."
$DOTNET_CMD publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o /opt/multiroom-audio/app

echo ""
echo "3. Service neu starten..."
echo "-------------------------"
systemctl restart multiroom-audio

echo ""
echo "4. Warte 3 Sekunden und prüfe Status..."
echo "----------------------------------------"
sleep 3
systemctl status multiroom-audio --no-pager

echo ""
echo "=========================================="
echo "Deployment abgeschlossen!"
echo "=========================================="
echo ""
echo "Testen mit:"
echo "  curl http://localhost:8096/api/settings"
echo "  curl http://localhost:8096/api/settings/volume-scale"
