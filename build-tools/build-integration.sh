#!/bin/bash
set -e

DIR="$( cd "$( dirname "$0" )" && pwd )"
ROOT_DIR="$DIR/../.."

echo "=== 1. Building and Packing CanonFlow.Assurance ==="
cd "$ROOT_DIR/CanonFlow"
dotnet restore --locked-mode
dotnet pack src/CanonFlow.Assurance/CanonFlow.Assurance.fsproj -c Release -o "$ROOT_DIR/ONDCFlow/local-feed"

echo "=== 2. Building ONDCFlow ==="
cd "$ROOT_DIR/ONDCFlow"
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test -c Release --no-build

echo "Integration verified!"
