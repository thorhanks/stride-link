$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
    dotnet restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE" }

    dotnet publish -c Release -r win-x64 --self-contained true
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

    $publishDir = Join-Path $PSScriptRoot 'bin\Release\net10.0\win-x64\publish'
    $zipPath = Join-Path $PSScriptRoot 'StrideLink-v0.1.5-win-x64.zip'
    if (Test-Path $zipPath) { Remove-Item $zipPath }
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath
    Write-Host "Built $zipPath" -ForegroundColor Green
}
finally {
    Pop-Location
}
