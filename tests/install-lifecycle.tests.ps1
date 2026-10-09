. (Join-Path $PSScriptRoot 'installer-test-common.ps1')
$ErrorActionPreference = "Stop"

$RootDir = (Get-Item (Join-Path $PSScriptRoot "..")).FullName
$HostDir = Join-Path $RootDir ".test-navisworks-host"
$PluginDir = Join-Path $HostDir "Plugins\傑出品NavisworksPlugin"
$ManifestPath = Join-Path $HostDir "Plugins\傑出品NavisworksPlugin.plugin"
$DllPath = Join-Path $PluginDir "傑出品NavisworksPlugin.dll"
$ConfigPath = Join-Path $HostDir "Roamer.exe.config"

function Invoke-Script([string]$ScriptPath, [string[]]$Arguments) {
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $ScriptPath @Arguments
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }
    return $exitCode
}

try {
    if (Test-Path -LiteralPath $HostDir) {
        Assert-TestWorkspacePath $HostDir
        Remove-Item -LiteralPath $HostDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $HostDir | Out-Null
    [System.IO.File]::WriteAllBytes((Join-Path $HostDir "Roamer.exe"), [byte[]](0x4D, 0x5A))
    Copy-Item -LiteralPath (Join-Path $TestHostSource "Autodesk.Navisworks.Api.dll") -Destination $HostDir

    $missingDependencyExit = Invoke-Script (Join-Path $RootDir "scripts\install.ps1") @(
        "-Version", "2023",
        "-NavisworksPath", $HostDir,
        "-NonInteractive"
    )
    if ($missingDependencyExit -eq 0) { throw "Installer accepted a host without required COM dependencies" }
    if (Test-Path -LiteralPath $ManifestPath) { throw "Failed dependency validation installed the manifest" }
    if (Test-Path -LiteralPath $DllPath) { throw "Failed dependency validation installed the DLL" }

    Copy-Item -LiteralPath (Join-Path $TestHostSource "Autodesk.Navisworks.ComApi.dll") -Destination $HostDir
    Copy-Item -LiteralPath (Join-Path $TestHostSource "Autodesk.Navisworks.Interop.ComApi.dll") -Destination $HostDir

    $installExit = Invoke-Script (Join-Path $RootDir "scripts\install.ps1") @(
        "-Version", "2023",
        "-NavisworksPath", $HostDir,
        "-NonInteractive"
    )
    if ($installExit -ne 0) { throw "Installer returned $installExit" }
    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) { throw "Manifest was not installed" }
    if (-not (Test-Path -LiteralPath $DllPath -PathType Leaf)) { throw "DLL was not installed" }
    if (Test-Path -LiteralPath $ConfigPath) { throw "Installer modified Roamer.exe.config" }

    $verifyExit = Invoke-Script (Join-Path $RootDir "scripts\verify.ps1") @(
        "-NavisworksPath", $HostDir
    )
    if ($verifyExit -ne 0) { throw "Verifier returned $verifyExit after installation" }

    Copy-Item -LiteralPath $DllPath -Destination (Join-Path $HostDir "Plugins\傑出品NavisworksPlugin.dll")
    $legacyExit = Invoke-Script (Join-Path $RootDir "scripts\verify.ps1") @(
        "-NavisworksPath", $HostDir,
        "-Json"
    )
    if ($legacyExit -eq 0) { throw "Verifier accepted a conflicting legacy layout" }
    Remove-Item -LiteralPath (Join-Path $HostDir "Plugins\傑出品NavisworksPlugin.dll") -Force

    $uninstallExit = Invoke-Script (Join-Path $RootDir "scripts\uninstall.ps1") @(
        "-NavisworksPath", $HostDir,
        "-NonInteractive"
    )
    if ($uninstallExit -ne 0) { throw "Uninstaller returned $uninstallExit" }
    if (Test-Path -LiteralPath $ManifestPath) { throw "Manifest remains after uninstall" }
    if (Test-Path -LiteralPath $DllPath) { throw "DLL remains after uninstall" }
    if (-not (Test-Path -LiteralPath (Join-Path $HostDir "Roamer.exe"))) { throw "Uninstaller removed host files" }
    if (Test-Path -LiteralPath $ConfigPath) { throw "Lifecycle modified Roamer.exe.config" }

    Write-Host "INSTALL LIFECYCLE: PASS" -ForegroundColor Green
    exit 0
} catch {
    Write-Host "INSTALL LIFECYCLE: FAIL - $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    if (Test-Path -LiteralPath $HostDir) {
        Assert-TestWorkspacePath $HostDir
        Remove-Item -LiteralPath $HostDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
