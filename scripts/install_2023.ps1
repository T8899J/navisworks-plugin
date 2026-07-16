$ErrorActionPreference = "Stop"

Write-Host "============================================"
Write-Host "  JiePinPai Navisworks Plugin Installer"
Write-Host "  Target: Navisworks Manage 2023"
Write-Host "============================================"
Write-Host ""

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Resolve-Path (Join-Path $scriptDir "..")
$releaseDir = Join-Path $rootDir "bin\Release"
$manifestDir = Join-Path $rootDir "manifests"

$dll = Get-ChildItem -LiteralPath $releaseDir -Filter "*.dll" |
    Where-Object { $_.Name -like "*NavisworksPlugin.dll" } |
    Select-Object -First 1

if (-not $dll) {
    Write-Host "[ERROR] Build output not found under: $releaseDir"
    Write-Host "        Run build_2023.bat from the repository root first."
    exit 1
}
Write-Host "[OK] Found plugin DLL: $($dll.FullName)"

$manifest = Get-ChildItem -LiteralPath $manifestDir -Filter "*.plugin" |
    Select-Object -First 1

if (-not $manifest) {
    Write-Host "[ERROR] Plugin manifest not found under: $manifestDir"
    exit 1
}
Write-Host "[OK] Found plugin manifest: $($manifest.FullName)"

$installRoot = $env:NAVISWORKS_2023_PATH
if ([string]::IsNullOrWhiteSpace($installRoot)) {
    $installRoot = $env:ProgramW6432
    if ([string]::IsNullOrWhiteSpace($installRoot)) {
        $installRoot = $env:ProgramFiles
    }
    $installRoot = Join-Path $installRoot "Autodesk\Navisworks Manage 2023"
}

$baseDir = Join-Path $installRoot "Plugins"
$pluginName = [System.IO.Path]::GetFileNameWithoutExtension($dll.Name)
$targetDir = Join-Path $baseDir $pluginName

if (-not (Test-Path -LiteralPath $baseDir)) {
    Write-Host "[WARN] Navisworks Plugins directory not found: $baseDir"
    $targetDir = Join-Path $env:APPDATA "Autodesk\Navisworks Manage 2023\Plugins\$pluginName"
}

Write-Host "[INSTALL] Copying to $targetDir ..."
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $targetDir $dll.Name) -Force
Write-Host "[OK] Installed DLL"

Copy-Item -LiteralPath $manifest.FullName -Destination (Join-Path $targetDir $manifest.Name) -Force
Write-Host "[OK] Installed manifest"

Write-Host ""
Write-Host "============================================"
Write-Host "  INSTALL SUCCESSFUL"
Write-Host "============================================"
Write-Host "  Location: $targetDir"
Write-Host ""
Write-Host "  Restart Navisworks Manage 2023 before testing the updated plugin."
Write-Host "============================================"
