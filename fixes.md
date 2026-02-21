# Fixes Documentation / Dokumentation

## Übersicht
Dieses Dokument beschreibt die notwendigen Fixes für das MultiRoomAudio-Projekt.

---

## Problem 1: Volume Scale wird nicht an Music Assistant gesendet

### Symptom
即使在UI中将音量设置为1%，音频仍然很大声。

### Ursache
Das Volume Scale wurde lokal berechnet, aber nicht an den Music Assistant (MA) gesendet. MA erhielt das rohe Volume (0-100) statt des effektiven Volumes mit Scale-Faktor.

### Lösung (Code-Änderungen)
In `src/MultiRoomAudio/Services/PlayerManagerService.cs`:

1. **Zeile ~360** (`PushVolumeToServerAsync`):
   - Geändert: `SetVolumeAsync(context.Config.Volume)` → `SetVolumeAsync((int)(context.Player.Volume * 100))`

2. **Zeile ~1416** (`SetVolumeAsync`):
   - Geändert: `SetVolumeAsync(volume)` → `SetVolumeAsync((int)(effectiveVolume * 100))`
   - Zusätzlich: `SendPlayerStateAsync(volume, ...)` → `SendPlayerStateAsync(volumeForMa, ...)`

3. **Zeile ~1559** (`ApplyHardwareVolumeChangeAsync`):
   - Geändert: `SetVolumeAsync(volume)` → `SetVolumeAsync((int)(effectiveVolume * 100))`

4. **Zeile ~2950** (GracePeriod):
   - Geändert: `SetVolumeAsync(context.Config.Volume)` → `SetVolumeAsync((int)(context.Player.Volume * 100))`

### Warum das funktioniert
- `context.Player.Volume` enthält den skalierten Wert (0.0-1.0)
- Wir konvertieren zu Integer (0-100) für MA
- Formel: `effectiveVolume = Volume × GlobalScale × PlayerScale`

---

## Problem 2: Remix-Sink funktioniert nicht

### Symptom
Kein Sound aus den Lautsprechern, obwohl der Player "playing" zeigt.

### Ursache
Der Remap-Sink wird mit `remix=false` erstellt, was bedeutet dass nur Kanäle geroutet werden ohne Mischung. Dadurch geht Audio verloren.

### Lösung (Code-Änderungen)
In `src/MultiRoomAudio/Models/SinkModels.cs`:

1. **Zeile 127**: `public bool Remix { get; set; } = true;` (war `false`)

In `src/MultiRoomAudio/Utilities/PaModuleRunner.cs`:

2. **Zeile 193**: `bool remix = true` (war nicht gesetzt oder `false`)

In `src/MultiRoomAudio/Utilities/IPaModuleRunner.cs`:

3. **Zeile 21**: `bool remix = true` (war nicht gesetzt oder `false`)

### Manueller Workaround (falls Deployment nicht funktioniert)
```bash
# Remap-Sink manuell mit remix=true laden
pactl load-module module-remap-sink \
  sink_name=xx \
  master=alsa_output.usb-0d8c_USB_Sound_Device-00.analog-surround-71 \
  channels=2 \
  channel_map=fl,fr \
  remix=true
```

---

## Problem 3: PulseAudio/PipeWire Start-Reihenfolge

### Symptom
Nach Neustart des Services kann PulseAudio nicht verbunden werden.

### Ursache
Der multiroom-audio Service startet vor PulseAudio/PipeWire.

### Lösung
Stelle sicher, dass PipeWire vor dem Service startet:
```bash
# Starte PipeWire zuerst
export XDG_RUNTIME_DIR=/run/user/1000
su - multiroom-audio -c "export XDG_RUNTIME_DIR=/run/user/1000; pipewire &"
su - multiroom-audio -c "export XDG_RUNTIME_DIR=/run/user/1000; pulseaudio --start &"

# Dann multiroom-audio
systemctl start multiroom-audio
```

---

## Deployment Anleitung

### Schritt 1: Dateien auf VM kopieren
```bash
# Auf lokalem Computer:
scp src/MultiRoomAudio/Services/PlayerManagerService.cs root@192.168.10.182:/tmp/deploy/
scp src/MultiRoomAudio/Models/SinkModels.cs root@192.168.10.182:/tmp/deploy/
scp src/MultiRoomAudio/Utilities/PaModuleRunner.cs root@192.168.10.182:/tmp/deploy/
scp src/MultiRoomAudio/Utilities/IPaModuleRunner.cs root@192.168.10.182:/tmp/deploy/
scp src/MultiRoomAudio/Models/PlayerStatus.cs root@192.168.10.182:/tmp/deploy/
```

### Schritt 2: Auf VM deployen
```bash
ssh root@192.168.10.182

# Service stoppen
systemctl stop multiroom-audio

# Dateien kopieren
cp /tmp/deploy/PlayerManagerService.cs /opt/multiroom/src/MultiRoomAudio/Services/
cp /tmp/deploy/SinkModels.cs /opt/multiroom/src/MultiRoomAudio/Models/
cp /tmp/deploy/PaModuleRunner.cs /opt/multiroom/src/MultiRoomAudio/Utilities/
cp /tmp/deploy/IPaModuleRunner.cs /opt/multiroom/src/MultiRoomAudio/Utilities/
cp /tmp/deploy/PlayerStatus.cs /opt/multiroom/src/MultiRoomAudio/Models/

# Bauen
cd /opt/multiroom/src/MultiRoomAudio
/root/.dotnet/dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o /opt/multiroom-audio/app

# Service starten
systemctl start multiroom-audio
```

### Schritt 3: PipeWire starten (falls nötig)
```bash
export XDG_RUNTIME_DIR=/run/user/1000
su - multiroom-audio -c "export XDG_RUNTIME_DIR=/run/user/1000; pipewire &"
su - multiroom-audio -c "export XDG_RUNTIME_DIR=/run/user/1000; pulseaudio --start &"
```

---

## Testing

### Volume Scale testen
```bash
# Global Volume Scale setzen
curl -X PUT http://localhost:8096/api/settings/volume-scale -H "Content-Type: application/json" -d '{"volumeScale": 0.1}'

# Per-Player Volume Scale setzen
curl -X PUT http://localhost:8096/api/players/{name}/volume-scale -H "Content-Type: application/json" -d '{"volumeScale": 0.1}'

# Prüfen
curl http://localhost:8096/api/players
```

Erwartetes Ergebnis: `volumeScale` sollte z.B. 0.1 (10%) anzeigen.

### Audio testen
```bash
# Player erstellen
curl -X POST http://localhost:8096/api/players -H "Content-Type: application/json" -d '{"name":"test","device":"2"}'

# Musik abspielen und Volume prüfen
wpctl status
```

---

## Bekannte Probleme

1. **Player Creation schlägt fehl**: "PulseAudio sink 'X' not found"
   - Lösung: Remap-Sink erstellen bevor Player erstellt wird

2. **Kein Sound**: Obwohl Player "playing" zeigt
   - Ursache: Remap-Sink fehlt oder hat remix=false
   - Lösung: Sink mit remix=true erstellen

3. **PipeWire nicht erreichbar**: "Connection refused"
   - Ursache: Service läuft als root, PipeWire als User
   - Lösung: XDG_RUNTIME_DIR setzen und als User starten

---

## VM Zugangsdaten

- IP: 192.168.10.182
- User: root
- API Port: 8096

---

## Letzte Änderungen (2026-02-21)

1. Volume Scale wird jetzt korrekt an MA gesendet (4 Stellen in PlayerManagerService.cs)
2. Remix default auf true geändert in SinkModels.cs, PaModuleRunner.cs, IPaModuleRunner.cs
3. PlayerStatus.cs enthält jetzt VolumeScale in PlayerResponse

