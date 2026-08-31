# syntax=docker/dockerfile:1

## ---- Build & publish -------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only project files first so `dotnet restore` is cached independently of source changes.
# Test projects are excluded (see .dockerignore) — the runtime image only needs the Api's
# own project graph (Application, Infrastructure, Domain, Common).
COPY src/KotoDibo.Api/KotoDibo.Api.csproj src/KotoDibo.Api/
COPY src/KotoDibo.Application/KotoDibo.Application.csproj src/KotoDibo.Application/
COPY src/KotoDibo.Domain/KotoDibo.Domain.csproj src/KotoDibo.Domain/
COPY src/KotoDibo.Infrastructure/KotoDibo.Infrastructure.csproj src/KotoDibo.Infrastructure/
COPY src/KotoDibo.Common/KotoDibo.Common.csproj src/KotoDibo.Common/

RUN dotnet restore src/KotoDibo.Api/KotoDibo.Api.csproj

COPY src/ src/
RUN dotnet publish src/KotoDibo.Api/KotoDibo.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

## ---- Runtime -----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is needed for the HEALTHCHECK below; the base image doesn't ship it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Base image already ships a non-root "app" user (uid 64198); no root process needed.
USER app

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "KotoDibo.Api.dll"]
