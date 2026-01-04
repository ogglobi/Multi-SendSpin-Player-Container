# Third-Party Licenses

This project includes the following third-party components:

## Audio Protocol

### Sendspin

- **License**: Apache License 2.0
- **Source**: https://github.com/music-assistant/server (Music Assistant)
- **Specification**: https://github.com/Sendspin/spec
- **Website**: https://www.sendspin-audio.com/
- **Description**: Open source multimedia streaming and synchronizing protocol

Sendspin is licensed under the Apache License, Version 2.0. You may obtain
a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

---

## .NET Dependencies

See `src/MultiRoomAudio/MultiRoomAudio.csproj` for the full list. Key dependencies include:

| Package | License | Purpose |
|---------|---------|---------|
| SendSpin.SDK | Apache-2.0 | Sendspin protocol implementation |
| PortAudioSharp2 | MIT | Cross-platform audio I/O |
| YamlDotNet | MIT | YAML configuration parsing |
| Swashbuckle.AspNetCore | MIT | OpenAPI/Swagger documentation |
| Microsoft.AspNetCore.SignalR | Apache-2.0 | Real-time WebSocket communication |

---

## Runtime

### .NET 8.0

- **License**: MIT
- **Source**: https://github.com/dotnet/runtime
- **Description**: Cross-platform runtime for building applications

---

## Audio Libraries

### PortAudio

- **License**: MIT
- **Source**: http://www.portaudio.com/
- **Description**: Cross-platform audio I/O library

PortAudio is licensed under the MIT license, allowing use in both open-source
and proprietary software.

---

## License Compatibility

This Docker image bundles Apache-2.0 and MIT licensed components.
These licenses are fully compatible for distribution.

For source code availability:
- Sendspin: https://github.com/music-assistant/server
- PortAudio: http://www.portaudio.com/
- .NET Runtime: https://github.com/dotnet/runtime

---

## Disclaimer

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED. See individual license texts for complete terms and conditions.
