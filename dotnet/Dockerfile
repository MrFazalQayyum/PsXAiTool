# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Build context is repo root, so all paths need the dotnet/ prefix
COPY dotnet/PsXAiTool.sln .
COPY dotnet/PsXAiTool.Core/PsXAiTool.Core.csproj                         PsXAiTool.Core/
COPY dotnet/PsXAiTool.Infrastructure/PsXAiTool.Infrastructure.csproj      PsXAiTool.Infrastructure/
COPY dotnet/PsXAiTool.Web/PsXAiTool.Web.csproj                            PsXAiTool.Web/
COPY dotnet/PsXAiTool.Tests/PsXAiTool.Tests.csproj                        PsXAiTool.Tests/

RUN dotnet restore

COPY dotnet/ .
RUN dotnet publish PsXAiTool.Web/PsXAiTool.Web.csproj \
    -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# CSV lives at repo root data/ — reachable because build context is repo root
COPY data/psx_companies.csv /app/data/psx_companies.csv

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}

EXPOSE 8080

ENTRYPOINT ["dotnet", "PsXAiTool.Web.dll"]
