# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY PsXAiTool.sln .
COPY PsXAiTool.Core/PsXAiTool.Core.csproj            PsXAiTool.Core/
COPY PsXAiTool.Infrastructure/PsXAiTool.Infrastructure.csproj PsXAiTool.Infrastructure/
COPY PsXAiTool.Web/PsXAiTool.Web.csproj              PsXAiTool.Web/
COPY PsXAiTool.Tests/PsXAiTool.Tests.csproj          PsXAiTool.Tests/

RUN dotnet restore

# Copy everything else and publish
COPY . .
RUN dotnet publish PsXAiTool.Web/PsXAiTool.Web.csproj \
    -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Copy the PSX companies CSV so CompanySeed can find it at /app/data/psx_companies.csv
COPY data/psx_companies.csv /app/data/psx_companies.csv

ENV ASPNETCORE_ENVIRONMENT=Production
# Railway injects PORT; bind to it
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}

EXPOSE 8080

ENTRYPOINT ["dotnet", "PsXAiTool.Web.dll"]
