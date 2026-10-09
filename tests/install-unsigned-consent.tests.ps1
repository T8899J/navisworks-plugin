. (Join-Path $PSScriptRoot 'installer-test-common.ps1')
$ErrorActionPreference = "Stop"

$RootDir = (Get-Item (Join-Path $PSScriptRoot "..")).FullName
$SourcePackage = Join-Path $RootDir "release"
$PackageDir = Join-Path $RootDir ".test-unsigned-package"
$HostDir = Join-Path $RootDir ".test-unsigned-host"
$ManifestTarget = Join-Path $HostDir "Plugins\傑出品NavisworksPlugin.plugin"
$DllTarget = Join-Path $HostDir "Plugins\傑出品NavisworksPlugin\傑出品NavisworksPlugin.dll"

function Invoke-Installer([switch]$AllowUnsigned) {
    $arguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $PackageDir "install.ps1"),
        "-Standalone",
        "-NonInteractive",
        "-NavisworksPath", $HostDir
    )
    if ($AllowUnsigned) {
        $arguments += "-AllowUnsigned"
    }

    $output = & powershell @arguments
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }
    return $exitCode
}

try {
    foreach ($path in @($PackageDir, $HostDir)) {
        if (Test-Path -LiteralPath $path) {
            Assert-TestWorkspacePath $path
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }

    Copy-Item -LiteralPath $SourcePackage -Destination $PackageDir -Recurse
    $manifestPath = Join-Path $PackageDir "PACKAGE_MANIFEST.json"
    $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json
    $manifest.signing.mode = "unsigned"
    $manifest.signing.reason = "Regression test"
    [System.IO.File]::WriteAllText(
        $manifestPath,
        ($manifest | ConvertTo-Json -Depth 8),
        (New-Object System.Text.UTF8Encoding($false))
    )

    New-Item -ItemType Directory -Path $HostDir | Out-Null
    [System.IO.File]::WriteAllBytes((Join-Path $HostDir "Roamer.exe"), [byte[]](0x4D, 0x5A))
    foreach ($dependency in @(
        "Autodesk.Navisworks.Api.dll",
        "Autodesk.Navisworks.ComApi.dll",
        "Autodesk.Navisworks.Interop.ComApi.dll"
    )) {
        Copy-Item -LiteralPath (Join-Path $TestHostSource "$dependency") -Destination $HostDir
    }

    if ((Invoke-Installer) -eq 0) {
        throw "Unsigned non-interactive installation succeeded without explicit authorization"
    }
    if ((Test-Path -LiteralPath $ManifestTarget) -or (Test-Path -LiteralPath $DllTarget)) {
        throw "Unsigned installation wrote plugin files before authorization"
    }

    if ((Invoke-Installer -AllowUnsigned) -ne 0) {
        throw "Explicitly authorized unsigned installation failed"
    }
    if (-not (Test-Path -LiteralPath $ManifestTarget) -or -not (Test-Path -LiteralPath $DllTarget)) {
        throw "Authorized unsigned installation did not deploy the plugin"
    }

    Write-Host "INSTALL UNSIGNED CONSENT: PASS" -ForegroundColor Green
    exit 0
} catch {
    Write-Host "INSTALL UNSIGNED CONSENT: FAIL - $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    foreach ($path in @($PackageDir, $HostDir)) {
        if (Test-Path -LiteralPath $path) {
            Assert-TestWorkspacePath $path
            Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
