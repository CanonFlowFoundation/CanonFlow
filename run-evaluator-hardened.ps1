# Run the Evaluator in a hardened, offline docker container
$imageName = "ghcr.io/canonflowfoundation/canonflow-evaluator:0.1.0-alpha"

# Note: Some capabilities like --cap-drop ALL might require Linux containers explicitly.
# Ensure you are running Docker in Linux container mode if executing on Windows.

New-Item -ItemType Directory -Force -Path "report" | Out-Null
$pwd = (Get-Location).Path -replace '\\', '/'
# Prefix with forward slash if it's a windows drive like C:/
if ($pwd -match "^[a-zA-Z]:/") {
    $pwd = "/" + $pwd
}

Write-Host "Starting hardened CanonFlow evaluator container..."
docker run --rm `
  --network none `
  --read-only `
  --cap-drop ALL `
  --security-opt no-new-privileges `
  --pids-limit 256 `
  --memory 2g `
  --cpus 2 `
  --tmpfs /tmp:rw,noexec,nosuid,size=128m `
  --mount type=bind,src="$pwd",dst=/input,readonly `
  --mount type=bind,src="$pwd/report",dst=/output `
  $imageName `
  evaluate `
  --manifest /input/canonflow-evaluation.json `
  --output /output

