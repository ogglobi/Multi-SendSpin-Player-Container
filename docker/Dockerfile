# Multi-Room Audio Controller - Unified Docker Image
# Works for both HAOS Add-on and standalone Docker deployments

# Base: Official Microsoft .NET 8 Alpine image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 8096

# Audio dependencies for PortAudioSharp2 native bindings
RUN apk add --no-cache \
    alsa-utils \
    alsa-lib \
    alsa-plugins-pulse \
    pulseaudio-utils \
    portaudio \
    curl

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY src/MultiRoomAudio/MultiRoomAudio.csproj MultiRoomAudio/
RUN dotnet restore "MultiRoomAudio/MultiRoomAudio.csproj"
COPY src/MultiRoomAudio/ MultiRoomAudio/
WORKDIR /src/MultiRoomAudio
# Note: Trimming disabled because SendSpin.SDK uses reflection
RUN dotnet publish "MultiRoomAudio.csproj" \
    -c Release \
    -o /app/publish \
    -p:PublishSingleFile=true \
    --self-contained true \
    -r linux-musl-x64

# Final image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Create directories for both deployment modes
# /app/config, /app/logs - Docker mode
# /data, /share - HAOS mode
RUN mkdir -p /app/config /app/logs /data /share

# ALSA→PulseAudio bridge (auto-routes ALSA to PulseAudio when available)
# This allows the same binary to work with both ALSA (Docker) and PulseAudio (HAOS)
RUN echo 'pcm.!default { type pulse fallback "sysdefault" }' > /etc/asound.conf && \
    echo 'ctl.!default { type pulse fallback "sysdefault" }' >> /etc/asound.conf

# Health check - verifies the API is responding
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8096/api/players || exit 1

# Single binary handles both HAOS and Docker modes
# Environment detection happens at runtime in C#
ENTRYPOINT ["./MultiRoomAudio"]
