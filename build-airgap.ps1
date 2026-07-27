# Package the air-gap distribution
$imageName = "ghcr.io/canonflowfoundation/canonflow-evaluator:0.1.0-alpha"
$version = "0.1.0-alpha"
$bundleDir = "canonflow-evaluator-airgap-$version"

Write-Host "Creating Air-Gap Bundle: $bundleDir"

if (Test-Path $bundleDir) { Remove-Item -Recurse -Force $bundleDir }
New-Item -ItemType Directory -Force -Path $bundleDir | Out-Null
New-Item -ItemType Directory -Force -Path "$bundleDir/images" | Out-Null
New-Item -ItemType Directory -Force -Path "$bundleDir/profiles" | Out-Null
New-Item -ItemType Directory -Force -Path "$bundleDir/public-keys" | Out-Null
New-Item -ItemType Directory -Force -Path "$bundleDir/schemas" | Out-Null
New-Item -ItemType Directory -Force -Path "$bundleDir/examples" | Out-Null
New-Item -ItemType Directory -Force -Path "$bundleDir/sbom" | Out-Null

Write-Host "Saving docker image..."
docker save -o "$bundleDir/images/canonflow-evaluator.tar" $imageName

if (Test-Path "profiles") { Copy-Item -Recurse "profiles/*" "$bundleDir/profiles/" }

Write-Host "Extracting SBOM from Docker image..."
# Depending on syft being installed, this is a placeholder check
if (Get-Command syft -ErrorAction SilentlyContinue) {
    syft packages $imageName -o spdx-json > "$bundleDir/sbom/bom.json"
} else {
    Write-Host "syft not found, skipping SBOM generation."
}

Write-Host "Generating checksums..."
Set-Location $bundleDir
$checksumFile = "checksums.sha256"
if (Test-Path $checksumFile) { Remove-Item $checksumFile }

Get-ChildItem -Recurse -File | ForEach-Object {
    $hash = Get-FileHash $_.FullName -Algorithm SHA256
    $relativePath = $_.FullName.Substring((Get-Location).Path.Length + 1).Replace("\", "/")
    "$($hash.Hash.ToLower())  $relativePath" | Add-Content $checksumFile
}

Set-Location ..

Write-Host "Air-gap distribution ready in: $bundleDir/"

