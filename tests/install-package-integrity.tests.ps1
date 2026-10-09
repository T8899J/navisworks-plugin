. (Join-Path $PSScriptRoot 'installer-test-common.ps1')
$ErrorActionPreference = "Stop"

$RootDir = (Get-Item (Join-Path $PSScriptRoot "..")).FullName
$HostDir = Join-Path $RootDir ".test-navisworks-package-integrity-host"
$PackageDir = Join-Path $RootDir ".test-install-package"
$ManifestTarget = Join-Path $HostDir "Plugins\傑出品NavisworksPlugin.plugin"
$DllTarget = Join-Path $HostDir "Plugins\傑出品NavisworksPlugin\傑出品NavisworksPlugin.dll"

try {
    foreach ($path in @($HostDir, $PackageDir)) {
        if (Test-Path -LiteralPath $path) {
            Assert-TestWorkspacePath $path
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }

    Copy-Item -LiteralPath (Join-Path $RootDir "release") -Destination $PackageDir -Recurse
    Add-Content -LiteralPath (Join-Path $PackageDir "使用说明.txt") -Value "`nTAMPERED"

    New-Item -ItemType Directory -Path $HostDir | Out-Null
    [System.IO.File]::WriteAllBytes((Join-Path $HostDir "Roamer.exe"), [byte[]](0x4D, 0x5A))
    foreach ($dependency in @(
        "Autodesk.Navisworks.Api.dll",
        "Autodesk.Navisworks.ComApi.dll",
        "Autodesk.Navisworks.Interop.ComApi.dll"
    )) {
        Copy-Item -LiteralPath (Join-Path $TestHostSource "$dependency") -Destination $HostDir
    }

    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PackageDir "install.ps1") `
        -Standalone `
        -NavisworksPath $HostDir `
        -NonInteractive
    $exitCode = $LASTEXITCODE

    if ($exitCode -eq 0) {
        throw "Standalone installer accepted a tampered package"
    }
    if ((Test-Path -LiteralPath $ManifestTarget) -or (Test-Path -LiteralPath $DllTarget)) {
        throw "Standalone installer wrote plugin files before package verification"
    }

    Write-Host "INSTALL PACKAGE INTEGRITY: PASS" -ForegroundColor Green
    exit 0
} catch {
    Write-Host "INSTALL PACKAGE INTEGRITY: FAIL - $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    foreach ($path in @($HostDir, $PackageDir)) {
        if (Test-Path -LiteralPath $path) {
            Assert-TestWorkspacePath $path
            Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
