# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build-Stage
# ---------------------------------------------------------------------------
# Das SDK-Image enthält das .NET CLI. Bootstrap wird NICHT über ein globales
# LibMan-Tool geholt: Das NuGet-Paket `Microsoft.Web.LibraryManager.Build`
# (siehe src/BlogCms.Web/BlogCms.Web.csproj) führt den LibMan-Restore
# automatisch während `dotnet build`/`dotnet publish` aus – inklusive
# Source-Maps. Es ist also kein zusätzlicher Installationsschritt im Image
# nötig (Voraussetzung: Netzwerkzugriff auf cdn.jsdelivr.net beim Build).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1) Nur Projektdateien + LibMan-Konfiguration kopieren -> stabiles Layer-Caching
COPY src/BlogCms.Domain/BlogCms.Domain.csproj                 src/BlogCms.Domain/
COPY src/BlogCms.Infrastructure/BlogCms.Infrastructure.csproj src/BlogCms.Infrastructure/
COPY src/BlogCms.Web/BlogCms.Web.csproj                       src/BlogCms.Web/
COPY src/BlogCms.Web/libman.json                              src/BlogCms.Web/
RUN dotnet restore src/BlogCms.Web/BlogCms.Web.csproj

# 2) Restlichen Quellcode kopieren und veröffentlichen.
#    `dotnet publish` stößt den LibMan-Restore an, daher liegt
#    wwwroot/lib/bootstrap/dist/ anschließend im Publish-Output.
COPY . .
RUN dotnet publish src/BlogCms.Web/BlogCms.Web.csproj \
        -c Release \
        -o /app/publish \
        /p:UseAppHost=false

# ---------------------------------------------------------------------------
# Runtime-Stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Standard-Port im Container; Compose mappt ihn auf den Host.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

# Upload-Verzeichnis für lokale Medien (Media:Provider=Local) anlegen und dem
# Nicht-Root-Benutzer übereignen, damit Uploads persistieren können.
RUN mkdir -p /app/wwwroot/uploads && chown -R $APP_UID:$APP_UID /app/wwwroot/uploads

# Als Nicht-Root-Benutzer ausführen (APP_UID ist im offiziellen Image definiert).
USER $APP_UID

ENTRYPOINT ["dotnet", "BlogCms.Web.dll"]
