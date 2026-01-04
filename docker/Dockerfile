# Multi-Room Audio Controller - Unified Docker Image
# Works for both HAOS Add-on and standalone Docker deployments

# Build arguments for multi-arch support
ARG TARGETARCH

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
ARG TARGETARCH
WORKDIR /src
COPY src/MultiRoomAudio/MultiRoomAudio.csproj MultiRoomAudio/
RUN dotnet restore "MultiRoomAudio/MultiRoomAudio.csproj"
COPY src/MultiRoomAudio/ MultiRoomAudio/
WORKDIR /src/MultiRoomAudio
# Note: Trimming disabled because SendSpin.SDK uses reflection
# Select RID based on target architecture for multi-arch builds
RUN case ${TARGETARCH} in \
        "amd64") RID="linux-musl-x64" ;; \
        "arm64") RID="linux-musl-arm64" ;; \
        *) echo "Unsupported arch: ${TARGETARCH}" && exit 1 ;; \
    esac && \
    dotnet publish "MultiRoomAudio.csproj" \
        -c Release \
        -o /app/publish \
        -p:PublishSingleFile=true \
        --self-contained true \
        -r ${RID}

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
    CMD curl -f http://localhost:8096/api/health || exit 1

# Single binary handles both HAOS and Docker modes
# Environment detection happens at runtime in C#
ENTRYPOINT ["./MultiRoomAudio"]
