param([switch]$Pruebas)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw 'No se encuentra csc.exe. Comprueba que .NET Framework 4.8 esta disponible.' }
$sourceRoot = Join-Path $PSScriptRoot 'src'
$outputRoot = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$sources = @((Join-Path $sourceRoot 'PortPilot.cs'), (Join-Path $sourceRoot 'Interface.cs'))
$destination = Join-Path $outputRoot 'PortPilot.exe'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /utf8output "/out:$destination" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Error de compilacion. Si la app esta abierta, cierrala e intenta de nuevo.' }
Copy-Item -LiteralPath (Join-Path $sourceRoot 'PortPilot.exe.config') -Destination (Join-Path $outputRoot 'PortPilot.exe.config') -Force
Write-Host "Aplicacion generada: $destination"
if ($Pruebas) {
    $testsRoot = Join-Path $PSScriptRoot 'artifacts\tests'
    New-Item -ItemType Directory -Path $testsRoot -Force | Out-Null
    $testPath = Join-Path $testsRoot 'PortPilot.Tests.exe'
    & $compiler /nologo /target:exe /platform:anycpu /optimize+ /utf8output "/out:$testPath" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll $sources
    if ($LASTEXITCODE -ne 0) { throw 'Error de compilacion de pruebas.' }
    & $testPath --self-test
    if ($LASTEXITCODE -ne 0) { throw "Pruebas incompletas o fallidas (codigo $LASTEXITCODE)." }
}
