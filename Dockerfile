# AITestHub backend - multi-stage build.
# No secrets (API keys, passwords) are ever baked into this image.

# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore dependencies first for better layer caching.
COPY AITestHub.csproj .
RUN dotnet restore AITestHub.csproj

# Build and publish (the AITestHub.Web and AITestHub.Tests folders are
# excluded via .dockerignore / csproj exclusions).
COPY . .
RUN dotnet publish AITestHub.csproj -c Release -o /app/publish --no-restore

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl is required by the Docker healthcheck.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Run as a non-root user. The .NET base images ship with a built-in
# non-root 'app' user (UID 1654); no need to create one.
RUN chown -R app:app /app
USER app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "AITestHub.dll"]
