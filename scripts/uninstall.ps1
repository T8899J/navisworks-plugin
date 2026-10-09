param(
    [string]$NavisworksPath,
    [switch]$Standalone,
    [switch]$NonInteractive
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "host-paths.ps1")

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-HostPath([string]$Path) {
    if (-not $Path) { return $false }
    return (Test-Path -LiteralPath (Join-Path $Path "Roamer.exe") -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $Path "Autodesk.Navisworks.Api.dll") -PathType Leaf)
}

function Test-IsUnderProgramFiles([string]$Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    $roots = @($env:ProgramFiles, ${env:ProgramW6432}, ${env:ProgramFiles(x86)}) |
        Where-Object { $_ } |
        Sort-Object -Unique
    foreach ($root in $roots) {
        $fullRoot = [System.IO.Path]::GetFullPath($root).TrimEnd('\')
        if ($fullPath.Equals($fullRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
            $fullPath.StartsWith("$fullRoot\", [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return $false
}

function Test-IsTargetRoamerRunning([string]$Path) {
    $targetRoamerPath = [System.IO.Path]::GetFullPath((Join-Path $Path "Roamer.exe")).TrimEnd('\')
    foreach ($process in @(Get-Process -Name "Roamer" -ErrorAction SilentlyContinue)) {
        try {
            $processPath = [System.IO.Path]::GetFullPath($process.Path).TrimEnd('\')
        } catch {
            return $true
        }
        if (-not $processPath) {
            return $true
        }
        if ($processPath.Equals($targetRoamerPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return $false
}



try { $NavisworksPath = Resolve-Navisworks2023Path $NavisworksPath } catch { Write-Error $_; exit 1 }
if (-not (Test-HostPath $NavisworksPath)) {
    Write-Host "[ERROR] 未找到 Navisworks Manage 2023 主程序目录。" -ForegroundColor Red
    exit 1
}
if ((Test-IsUnderProgramFiles $NavisworksPath) -and -not (Test-IsAdministrator)) {
    Write-Host "[ERROR] 卸载需要管理员权限。" -ForegroundColor Red
    exit 1
}
if (Test-IsTargetRoamerRunning -Path $NavisworksPath) {
    Write-Host "[ERROR] 请先关闭目标目录中的 Navisworks Manage 2023。" -ForegroundColor Red
    exit 1
}

$pluginsDir = Join-Path $NavisworksPath "Plugins"
$pluginDir = Join-Path $pluginsDir "傑出品NavisworksPlugin"
$ownedFiles = @(
    (Join-Path $pluginsDir "傑出品NavisworksPlugin.plugin"),
    (Join-Path $pluginDir "傑出品NavisworksPlugin.dll"),
    (Join-Path $pluginsDir "傑出品NavisworksPlugin.dll"),
    (Join-Path $pluginDir "傑出品NavisworksPlugin.plugin")
)

try {
    foreach ($file in $ownedFiles) {
        Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path -LiteralPath $pluginDir -PathType Container) {
        $remaining = Get-ChildItem -LiteralPath $pluginDir -Force -ErrorAction SilentlyContinue
        if (-not $remaining) {
            Remove-Item -LiteralPath $pluginDir -Force -ErrorAction Stop
        }
    }

    $leftovers = @($ownedFiles | Where-Object { Test-Path -LiteralPath $_ })
    if ($leftovers.Count -gt 0) {
        throw "仍有插件文件未删除: $($leftovers -join ', ')"
    }

    Write-Host "UNINSTALL PASS: 已删除傑出品插件文件，未修改 Navisworks 配置。" -ForegroundColor Green
    exit 0
} catch {
    Write-Host "UNINSTALL FAIL: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    if ($Standalone -and -not $NonInteractive) {
        Read-Host "按 Enter 退出"
    }
}
