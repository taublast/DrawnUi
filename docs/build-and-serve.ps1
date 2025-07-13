# Build script for DrawnUi documentation
param (
    [switch]$Serve = $false
)

Write-Host "Building DrawnUi documentation..."

# Check if DocFX is installed
$docfx = Get-Command docfx -ErrorAction SilentlyContinue
if ($null -eq $docfx) {
    Write-Host "DocFX not found in PATH. Installing DocFX as local tool..."
    dotnet tool install --global docfx
    
    # Check if installation was successful
    $docfx = Get-Command docfx -ErrorAction SilentlyContinue
    if ($null -eq $docfx) {
        Write-Error "Failed to install DocFX. Please install it manually."
        exit 1
    }
}

# Clean previous build
if (Test-Path "_site") {
    Write-Host "Cleaning previous build..."
    Remove-Item -Recurse -Force "_site" -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force "obj" -ErrorAction SilentlyContinue
}

# Build the documentation
Write-Host "Building documentation with metadata extraction..."
docfx

# Check if build was successful
if ($LASTEXITCODE -ne 0) {
    Write-Error "DocFX build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Documentation built successfully!"
Write-Host "Output directory: _site"

    Write-Host "Starting documentation server at http://localhost:8080"
    Write-Host "Press Ctrl+C to stop the server"
    docfx serve _site
