# ==============================================================================
# CBS Bangladesh Core Banking System - Hardened Production Multi-Stage Dockerfile
# Conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024)
# ==============================================================================

# Stage 1: Minimal Secure Runtime Base
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app
EXPOSE 5000

# Install ICU libraries for Bangladesh locale/currency globalization formatting and curl for health probes
RUN apk add --no-cache icu-libs curl tzdata && \
    cp /usr/share/zoneinfo/Asia/Dhaka /etc/localtime && \
    echo "Asia/Dhaka" > /etc/timezone

# Security Hardening: Dedicated non-privileged system user & group
RUN addgroup -g 10001 -S appgroup && \
    adduser -u 10001 -S appuser -G appgroup

# Set Environment Variables
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8

# Container Healthcheck Probe
HEALTHCHECK --interval=15s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Stage 2: SDK Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy project files first for optimal Docker layer caching
COPY ["src/CBS.Domain/CBS.Domain.csproj", "src/CBS.Domain/"]
COPY ["src/CBS.Application/CBS.Application.csproj", "src/CBS.Application/"]
COPY ["src/CBS.Infrastructure/CBS.Infrastructure.csproj", "src/CBS.Infrastructure/"]
COPY ["src/CBS.Api/CBS.Api.csproj", "src/CBS.Api/"]

# Restore NuGet dependencies
RUN dotnet restore "src/CBS.Api/CBS.Api.csproj"

# Copy entire source tree
COPY src/ src/

# Build and Publish in Release mode
WORKDIR "/src/src/CBS.Api"
RUN dotnet publish "CBS.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Stage 3: Final Production Appliance
FROM base AS final
WORKDIR /app

# Copy compiled artifacts with ownership set to non-root appuser
COPY --from=build --chown=appuser:appgroup /app/publish .

# Drop privileges to non-root user
USER appuser

ENTRYPOINT ["dotnet", "CBS.Api.dll"]
