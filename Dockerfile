# syntax=docker/dockerfile:1

ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.301@sha256:493fca072aac81307027cbb7b7c9a82b6e87d222af315504d05dc6530e69b519
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime:10.0.9-noble-chiseled@sha256:aaed8768468a774a23de3751856c24adfd9a51de802a89a4ce2199092f221cc3

FROM ${SDK_IMAGE} AS build
WORKDIR /src

COPY global.json ./
COPY Directory.Build.props ./
COPY Directory.Build.targets ./
COPY Directory.Packages.props ./
COPY NuGet.config ./
COPY build-tools/ ./build-tools/
COPY .nuget/offline/ ./.nuget/offline/
COPY src/ ./src/

RUN dotnet tool install FsAssay.Cli \
    --tool-path /tools \
    --add-source /src/.nuget/offline \
    --version 1.0.4

RUN dotnet restore src/Canon.Cli/Canon.Cli.fsproj --locked-mode

RUN dotnet publish src/Canon.Cli/Canon.Cli.fsproj \
    -c Release \
    --no-restore \
    -o /out \
    /p:ContinuousIntegrationBuild=true \
    /p:DebugType=None

FROM ${RUNTIME_IMAGE} AS runtime
WORKDIR /app

COPY --from=build /out/ ./
COPY --from=build /tools/ /tools/
ENV DOTNET_EnableDiagnostics=0
ENV COMPlus_EnableDiagnostics=0
ENV CANONFLOW_FSASSAY_PATH=/tools/fsassay

USER $APP_UID

ENTRYPOINT ["dotnet", "Canon.Cli.dll"]

