# --- Build stage: restore + publish the API ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (cached layer) — copy only the csproj so a code change doesn't bust the restore cache.
COPY Depicta.Api/Depicta.Api.csproj Depicta.Api/
RUN dotnet restore Depicta.Api/Depicta.Api.csproj

# Now the rest of the source, and publish.
COPY Depicta.Api/ Depicta.Api/
RUN dotnet publish Depicta.Api/Depicta.Api.csproj -c Release -o /app /p:UseAppHost=false

# --- Runtime stage: small ASP.NET image ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Magick.NET (image resizing) needs libgomp on Debian-based images.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgomp1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

# Listen on 8080 (non-privileged) inside the container; Production by default.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "Depicta.Api.dll"]
