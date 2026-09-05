FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY src/AudioProvider.csproj .

RUN dotnet restore "AudioProvider.csproj"

COPY . .

RUN dotnet publish "AudioProvider.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0

ARG TARGETARCH
RUN echo "TARGETARCH: $TARGETARCH"

RUN case "$TARGETARCH" in \
        amd64) YTDLP_BINARY="yt-dlp_linux" ;; \
        arm64) YTDLP_BINARY="yt-dlp_linux_aarch64" ;; \
        *) echo "Unsupported architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        ffmpeg \
        curl \
    && curl -L \
        https://github.com/yt-dlp/yt-dlp/releases/download/2026.08.19/${YTDLP_BINARY} \
        -o /usr/local/bin/yt-dlp \
    && chmod a+rx /usr/local/bin/yt-dlp \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "AudioProvider.dll"]
