param ()

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

Push-Location $scriptRoot
try {
    $docfx = Get-Command docfx -ErrorAction SilentlyContinue
    if ($null -eq $docfx) {
        Write-Host "DocFX not found in PATH. Installing DocFX as global tool..."
        dotnet tool install --global docfx

        $docfx = Get-Command docfx -ErrorAction SilentlyContinue
        if ($null -eq $docfx) {
            Write-Error "Failed to install DocFX. Please install it manually."
            exit 1
        }
    }

    Write-Host "Serving DrawnUi documentation at http://localhost:8080"
    Write-Host "Press Ctrl+C to stop the server"
    docfx docfx.json --serve

    if ($LASTEXITCODE -ne 0) {
        Write-Error "DocFX serve failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}