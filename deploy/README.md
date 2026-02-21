# Volume Scale Feature - Deployment

## Übersicht

Dieser Ordner enthält alle geänderten Dateien für das Volume Scale Feature.

### Neue Features:
1. **Global Volume Scale** - Eine globale Skalierung für alle Player (0.01-1.0)
2. **Per-Player Volume Scale** - Individuelle Skalierung pro Player (0.01-1.0)

### Formel:
```
EffectiveVolume = Volume × GlobalScale × PlayerScale
```

## Dateien in diesem Ordner

```
deploy/
├── README.md                           # Diese Anleitung
├── deploy.sh                           # Deployment-Script für VM
├── Program.cs                          # GEÄNDERT - SettingsEndpoint registriert
├── Controllers/
│   └── SettingsEndpoint.cs             # NEU - API-Endpunkte für Settings
└── Services/
    ├── ConfigurationService.cs         # GEÄNDERT - GlobalConfiguration und VolumeScale
    └── PlayerManagerService.cs         # GEÄNDERT - CalculateEffectiveVolume()
```

## Deployment-Anleitung

### Schritt 1: Deploy-Ordner auf VM kopieren

Von Windows (im Projekt-Verzeichnis):
```bash
scp -r deploy root@192.168.10.182:/tmp/
```

### Schritt 2: Auf VM einloggen und Deployment ausführen

```bash
ssh root@192.168.10.182
cd /tmp/deploy
chmod +x deploy.sh
./deploy.sh
```

Das Script macht folgendes:
1. Kopiert alle Dateien an die richtigen Orte
2. Baut die Anwendung mit dotnet publish
3. Startet den multiroom-audio Service neu
4. Zeigt den Service-Status

### Alternative: Manuelle Schritte

Falls das Script nicht funktioniert, können die Schritte manuell ausgeführt werden:

```bash
# 1. Dateien kopieren
cp /tmp/deploy/Controllers/SettingsEndpoint.cs /opt/multiroom/src/MultiRoomAudio/Controllers/
cp /tmp/deploy/Services/ConfigurationService.cs /opt/multiroom/src/MultiRoomAudio/Services/
cp /tmp/deploy/Services/PlayerManagerService.cs /opt/multiroom/src/MultiRoomAudio/Services/
cp /tmp/deploy/Program.cs /opt/multiroom/src/MultiRoomAudio/

# 2. Build
cd /opt/multiroom/src/MultiRoomAudio
/root/.dotnet/dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o /opt/multiroom-audio/app

# 3. Service neu starten
systemctl restart multiroom-audio

# 4. Status prüfen
systemctl status multiroom-audio
```

## Testen

Nach dem Deployment können die neuen API-Endpunkte getestet werden:

```bash
# Alle Settings abrufen
curl http://localhost:8096/api/settings

# Global Volume Scale abrufen
curl http://localhost:8096/api/settings/volume-scale

# Global Volume Scale setzen (z.B. 50%)
curl -X PUT -H "Content-Type: application/json" \
  -d '{"volumeScale":0.5}' \
  http://localhost:8096/api/settings/volume-scale

# Per-Player Volume Scale setzen (z.B. 80% für Player "TestPlayer")
curl -X PUT -H "Content-Type: application/json" \
  -d '{"volumeScale":0.8}' \
  http://localhost:8096/api/players/TestPlayer/volume-scale
```

## API-Endpunkte

| Methode | Endpunkt | Beschreibung |
|---------|----------|--------------|
| GET | `/api/settings` | Alle Settings abrufen |
| GET | `/api/settings/volume-scale` | Global Volume Scale abrufen |
| PUT | `/api/settings/volume-scale` | Global Volume Scale setzen |
| PUT | `/api/players/{name}/volume-scale` | Per-Player Volume Scale setzen |

## Volume Scale Werte

- **0.01** = 1% (Minimum)
- **0.5** = 50%
- **1.0** = 100% (Maximum, Standard)

## Beispiel

Bei Volume 80%, Global Scale 0.5 und Player Scale 0.8:
```
EffectiveVolume = 80% × 0.5 × 0.8 = 32%
```
