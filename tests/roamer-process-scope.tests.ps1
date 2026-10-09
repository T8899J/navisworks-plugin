. (Join-Path $PSScriptRoot 'installer-test-common.ps1')
$ErrorActionPreference = "Stop"

$RootDir = (Get-Item (Join-Path $PSScriptRoot "..")).FullName
$TargetHostDir = Join-Path $RootDir ".test-roamer-target-host"
$OtherHostDir = Join-Path $RootDir ".test-roamer-other-host"
$startedProcesses = New-Object System.Collections.Generic.List[System.Diagnostics.Process]

function New-FakeHost([string]$Path, [switch]$WithApi) {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSHOME "powershell.exe") -Destination (Join-Path $Path "Roamer.exe")
    if ($WithApi) {
        foreach ($dependency in @(
            "Autodesk.Navisworks.Api.dll",
            "Autodesk.Navisworks.ComApi.dll",
            "Autodesk.Navisworks.Interop.ComApi.dll"
        )) {
            Copy-Item -LiteralPath (Join-Path $TestHostSource "$dependency") -Destination $Path
        }
    }
}

function Start-FakeRoamer([string]$HostPath) {
    $process = Start-Process `
        -FilePath (Join-Path $HostPath "Roamer.exe") `
        -ArgumentList "-NoProfile", "-Command", "Start-Sleep -Seconds 60" `
        -WindowStyle Hidden `
        -PassThru
    Start-Sleep -Milliseconds 500
    if ($process.HasExited) {
        throw "Fake Roamer process exited before the test"
    }
    $startedProcesses.Add($process)
    return $process
}

function Invoke-Installer {
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RootDir "scripts\install.ps1") `
        -Version 2023 `
        -NavisworksPath $TargetHostDir `
        -NonInteractive
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }
    return $exitCode
}

function Invoke-Uninstaller {
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RootDir "scripts\uninstall.ps1") `
        -NavisworksPath $TargetHostDir `
        -NonInteractive
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }
    return $exitCode
}

try {
    foreach ($path in @($TargetHostDir, $OtherHostDir)) {
        if (Test-Path -LiteralPath $path) {
            Assert-TestWorkspacePath $path
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
    New-FakeHost -Path $TargetHostDir -WithApi
    New-FakeHost -Path $OtherHostDir

    $otherProcess = Start-FakeRoamer -HostPath $OtherHostDir
    if ((Invoke-Installer) -ne 0) {
        throw "A Roamer process from another installation blocked the target 2023 installation"
    }
    if ((Invoke-Uninstaller) -ne 0) {
        throw "A Roamer process from another installation blocked the target 2023 uninstall"
    }
    Stop-Process -Id $otherProcess.Id -Force -ErrorAction Stop
    $otherProcess.WaitForExit()

    $targetProcess = Start-FakeRoamer -HostPath $TargetHostDir
    if ((Invoke-Installer) -eq 0) {
        throw "Installer did not block the running target Navisworks host"
    }
    if (Test-Path -LiteralPath (Join-Path $TargetHostDir "Plugins\傑出品NavisworksPlugin\傑出品NavisworksPlugin.dll")) {
        throw "Installer wrote plugin files while the target host was running"
    }

    Write-Host "ROAMER PROCESS SCOPE: PASS" -ForegroundColor Green
    exit 0
} catch {
    Write-Host "ROAMER PROCESS SCOPE: FAIL - $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    foreach ($process in $startedProcesses) {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            $process.WaitForExit()
        }
        $process.Dispose()
    }
    Start-Sleep -Milliseconds 200
    foreach ($path in @($TargetHostDir, $OtherHostDir)) {
        for ($attempt = 1; $attempt -le 3 -and (Test-Path -LiteralPath $path); $attempt++) {
            Assert-TestWorkspacePath $path
            Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction SilentlyContinue
            if (Test-Path -LiteralPath $path) {
                Start-Sleep -Milliseconds 200
            }
        }
    }
}
