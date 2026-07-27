# syntax=docker/dockerfile:1

ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0-preview
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime:10.0-preview-noble-chiseled

FROM ${SDK_IMAGE} AS build
WORKDIR /src

COPY global.json ./
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY src/ ./src/
COPY tests/ ./tests/

RUN dotnet restore src/Canon.Cli/Canon.Cli.fsproj

RUN dotnet publish src/Canon.Cli/Canon.Cli.fsproj \
    -c Release \
    --no-restore \
    -o /out \
    /p:ContinuousIntegrationBuild=true \
    /p:DebugType=None

FROM ${RUNTIME_IMAGE} AS runtime
WORKDIR /app

COPY --from=build /out/ ./
COPY profiles/ /profiles/

ENV DOTNET_EnableDiagnostics=0
ENV COMPlus_EnableDiagnostics=0

USER 1000

ENTRYPOINT ["dotnet", "Canon.Cli.dll"]

