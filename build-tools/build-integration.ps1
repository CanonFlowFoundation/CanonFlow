Write-Host "=== 1. Building and Packing CanonFlow.Assurance ==="
$rootDir = (Get-Item $PSScriptRoot).Parent.Parent.FullName
Set-Location "$rootDir\CanonFlow"
dotnet restore --locked-mode
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet pack src/CanonFlow.Assurance/CanonFlow.Assurance.fsproj -c Release -o "$rootDir\ONDCFlow\local-feed"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "=== 2. Building ONDCFlow ==="
Set-Location "$rootDir\ONDCFlow"
dotnet restore --locked-mode
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build -c Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Integration verified!"
