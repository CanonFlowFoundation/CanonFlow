# Build the CanonFlow Evaluator Docker image
$imageName = "ghcr.io/canonflowfoundation/canonflow-evaluator:0.1.0-alpha"

Write-Host "Building Docker Image: $imageName"
docker build -t $imageName .

Write-Host "Image built successfully!"

