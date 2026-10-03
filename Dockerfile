# syntax=docker/dockerfile:1.7
# =============================================================================
# PageTurn Campus Bookshop - Azure Functions (.NET 10 isolated worker)
# -----------------------------------------------------------------------------
# Multi-stage build, so the final image holds only the published app on the
# Azure Functions runtime and none of the SDK, source code or NuGet cache.
#
#   Stage 1 (restore) : restores NuGet packages, cached unless the .csproj changes
#   Stage 2 (build)   : compiles and publishes the Functions app
#   Stage 3 (runtime) : official Azure Functions host with the published output
#
# Build:  docker build --build-arg APP_VERSION=2.0.0 -t <user>/pageturn-functions:v2.0 .
# Run  :  docker run -p 7071:80 -e AzureWebJobsStorage="<azurite conn string>" <image>
# =============================================================================

# -----------------------------------------------------------------------------
# Global build arguments (declared before the first FROM so every stage can use them)
# Change DOTNET_VERSION here and both the SDK and runtime image tags follow.
# -----------------------------------------------------------------------------
ARG DOTNET_VERSION=10.0
ARG BUILD_CONFIGURATION=Release

# =============================================================================
# STAGE 1: restore
# Only the .csproj is copied first. Docker caches this layer, so rebuilds after
# a source-code change skip the slow NuGet restore.
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
WORKDIR /src

COPY ["PageTurn.Functions.csproj", "./"]
RUN dotnet restore "PageTurn.Functions.csproj"

# =============================================================================
# STAGE 2: build + publish
# Copies the full source and publishes a Release build to /home/site/wwwroot,
# the folder the Azure Functions host loads functions from.
# =============================================================================
FROM restore AS build
ARG BUILD_CONFIGURATION

COPY . .

# --no-restore    : packages came from the cached restore layer
# UseAppHost=false: no native executable needed, the Functions host loads the DLL
RUN dotnet publish "PageTurn.Functions.csproj" \
        --configuration ${BUILD_CONFIGURATION} \
        --no-restore \
        --output /home/site/wwwroot \
        /p:UseAppHost=false

# =============================================================================
# STAGE 3: runtime
# Official Azure Functions isolated-worker base image (host + .NET runtime).
# =============================================================================
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated${DOTNET_VERSION} AS runtime

# Build metadata, supplied at build time (see the build command below)
ARG APP_VERSION=0.0.0
ARG BUILD_DATE=unknown
ARG VCS_REF=unknown

# -----------------------------------------------------------------------------
# OCI image labels: visible with `docker inspect` and on Docker Hub, and they
# link the image back to the exact commit it was built from.
# -----------------------------------------------------------------------------
LABEL org.opencontainers.image.title="pageturn-functions" \
      org.opencontainers.image.description="PageTurn campus bookshop: HTTP CRUD, Blob document storage and queue-triggered order processing on Azure Functions" \
      org.opencontainers.image.version="${APP_VERSION}" \
      org.opencontainers.image.created="${BUILD_DATE}" \
      org.opencontainers.image.revision="${VCS_REF}" \
      org.opencontainers.image.vendor="CLDV6212 Workshop" \
      org.opencontainers.image.base.name="mcr.microsoft.com/azure-functions/dotnet-isolated"

# -----------------------------------------------------------------------------
# Runtime environment
#
# AzureWebJobsScriptRoot  : where the Functions host finds the published app
# FUNCTIONS_WORKER_RUNTIME: tells the host this is a .NET isolated worker
# ...Console__IsEnabled   : sends function logs to stdout, so `docker logs` works
# StatusTransitionDelaySeconds: delay between order status changes. A default is
#                               set here and can be overridden with -e or compose.
#
# AzureWebJobsStorage is deliberately NOT baked into the image. It is passed at
# run time (docker run -e / docker-compose environment:), so one image works
# against Azurite, a different host, or real Azure Storage.
# -----------------------------------------------------------------------------
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    StatusTransitionDelaySeconds=3

# Copy ONLY the published output from the build stage (no SDK, no source code)
COPY --from=build /home/site/wwwroot /home/site/wwwroot

# -----------------------------------------------------------------------------
# Explicit port binding: the Functions host listens on port 80 inside the
# container. EXPOSE documents it, and `-p 7071:80` publishes it on the host.
# -----------------------------------------------------------------------------
EXPOSE 80

# -----------------------------------------------------------------------------
# Health check: marks the container "healthy" once the host accepts TCP
# connections on port 80. It uses bash's /dev/tcp, so no extra tools are needed.
# `docker ps` shows (healthy) or (unhealthy).
# -----------------------------------------------------------------------------
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
    CMD bash -c '</dev/tcp/127.0.0.1/80' || exit 1

# No ENTRYPOINT/CMD needed: the base image already starts the Functions host.