. (Join-Path $PSScriptRoot 'installer-test-common.ps1')
$ErrorActionPreference = "Stop"

$RootDir = (Get-Item (Join-Path $PSScriptRoot "..")).FullName
$HostDir = Join-Path $RootDir ".test-navisworks-rollback-host"
$PluginDir = Join-Path $HostDir "Plugins\傑出品NavisworksPlugin"
$ManifestPath = Join-Path $HostDir "Plugins\傑出品NavisworksPlugin.plugin"
$DllPath = Join-Path $PluginDir "傑出品NavisworksPlugin.dll"

function Invoke-Installer {
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RootDir "scripts\install.ps1") `
        -Version 2023 `
        -NavisworksPath $HostDir `
        -NonInteractive
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }
    return $exitCode
}

function Invoke-InstallerWithPostCommitHashFailure {
    $installerPath = Join-Path $RootDir "scripts\install.ps1"
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $installerPath `
        -Version 2023 `
        -NavisworksPath $HostDir `
        -NonInteractive `
        -TestFailPostCommitValidation
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
    foreach ($dependency in @(
        "Autodesk.Navisworks.Api.dll",
        "Autodesk.Navisworks.ComApi.dll",
        "Autodesk.Navisworks.Interop.ComApi.dll"
    )) {
        Copy-Item -LiteralPath (Join-Path $TestHostSource "$dependency") -Destination $HostDir
    }

    if ((Invoke-Installer) -ne 0) { throw "Initial installation failed" }

    $manifestText = [System.IO.File]::ReadAllText($ManifestPath)
    $legacyManifestText = $manifestText.Replace("傑出品查找", "旧版傑出品查找")
    [System.IO.File]::WriteAllText($ManifestPath, $legacyManifestText, (New-Object System.Text.UTF8Encoding($true)))
    $manifestHashBefore = (Get-FileHash -Algorithm SHA256 -LiteralPath $ManifestPath).Hash
    $dllHashBefore = (Get-FileHash -Algorithm SHA256 -LiteralPath $DllPath).Hash

    $lock = [System.IO.File]::Open($DllPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::None)
    try {
        $failedUpgradeExit = Invoke-Installer
    } finally {
        $lock.Dispose()
    }

    if ($failedUpgradeExit -eq 0) { throw "Installer reported success while the installed DLL was locked" }
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $ManifestPath).Hash -ne $manifestHashBefore) {
        throw "Failed upgrade did not preserve the previous manifest"
    }
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $DllPath).Hash -ne $dllHashBefore) {
        throw "Failed upgrade did not preserve the previous DLL"
    }

    $postCommitFailureExit = Invoke-InstallerWithPostCommitHashFailure
    if ($postCommitFailureExit -eq 0) {
        throw "Installer reported success after an injected post-commit validation failure"
    }
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $ManifestPath).Hash -ne $manifestHashBefore) {
        throw "Post-commit validation failure did not restore the previous manifest"
    }
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $DllPath).Hash -ne $dllHashBefore) {
        throw "Post-commit validation failure did not restore the previous DLL"
    }

    Write-Host "INSTALL ROLLBACK: PASS" -ForegroundColor Green
    exit 0
} catch {
    Write-Host "INSTALL ROLLBACK: FAIL - $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    if (Test-Path -LiteralPath $HostDir) {
        Assert-TestWorkspacePath $HostDir
        Remove-Item -LiteralPath $HostDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
