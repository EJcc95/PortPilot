$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Compilar.ps1')
$artifactRoot = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$zipPath = Join-Path $artifactRoot 'PortPilot-Windows.zip'
$payload = @(
    (Join-Path $PSScriptRoot 'dist\PortPilot.exe'),
    (Join-Path $PSScriptRoot 'dist\PortPilot.exe.config'),
    (Join-Path $PSScriptRoot 'README.md'),
    (Join-Path $PSScriptRoot 'CHANGELOG.md'),
    (Join-Path $PSScriptRoot 'docs')
)
Compress-Archive -LiteralPath $payload -DestinationPath $zipPath -Force
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$hashPath = Join-Path $artifactRoot 'SHA256SUMS.txt'
[System.IO.File]::WriteAllText($hashPath, "$hash  PortPilot-Windows.zip`n", [System.Text.Encoding]::ASCII)
Write-Host "Paquete generado: $zipPath"
Write-Host "SHA256: $hash"
