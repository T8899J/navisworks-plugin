param(
    [string]$Version = "2023",
    [switch]$AllVersions,
    [switch]$ListVersions
)

$ErrorActionPreference = "Stop"

# ── 版本号映射：年份 → Autodesk 内部版本号 ──
$VersionMap = @{
    "2017" = "14.0"; "2018" = "15.0"; "2019" = "16.0"
    "2020" = "17.0"; "2021" = "18.0"; "2022" = "19.0"
    "2023" = "20.0"; "2024" = "21.0"; "2025" = "22.0"; "2026" = "23.0"
}

# ── 路径解析 ──
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Resolve-Path (Join-Path $scriptDir "..")
$binDir = Join-Path $rootDir "bin\Release"
$manifestDir = Join-Path $rootDir "manifests"

# ── 列出已安装版本 ──
function Get-InstalledNavisworksVersions {
    $installed = @()
    foreach ($year in $VersionMap.Keys | Sort-Object) {
        $path = Find-NavisworksPath -Year $year
        if ($path) {
            $installed += [PSCustomObject]@{ Year = $year; InternalVersion = $VersionMap[$year]; Path = $path }
        }
    }
    return $installed
}

if ($ListVersions) {
    Write-Host "Installed Navisworks versions:" -ForegroundColor Cyan
    $installed = Get-InstalledNavisworksVersions
    if ($installed.Count -eq 0) {
        Write-Host "  (none found on this machine)" -ForegroundColor Yellow
    } else {
        $installed | Format-Table -AutoSize
    }
    exit 0
}

# ── 路径发现 ──
function Find-NavisworksPath([string]$Year) {
    # 1) 环境变量 NAVISWORKS_{YEAR}_PATH
    $envVarName = "NAVISWORKS_${Year}_PATH"
    $envPath = [Environment]::GetEnvironmentVariable($envVarName)
    if ($envPath -and (Test-Path (Join-Path $envPath "Autodesk.Navisworks.Api.dll"))) {
        return $envPath
    }

    # 2) 注册表 HKLM\SOFTWARE\Autodesk\Navisworks Manage\{version}\Location\Path
    $internalVer = $VersionMap[$Year]
    if ($internalVer) {
        $regLocationPath = "HKLM:\SOFTWARE\Autodesk\Navisworks Manage\$internalVer\Location"
        if (Test-Path $regLocationPath) {
            $regProps = Get-ItemProperty -Path $regLocationPath -ErrorAction SilentlyContinue
            $regLocation = $regProps.Path
            if ($regLocation -and (Test-Path (Join-Path $regLocation "Autodesk.Navisworks.Api.dll"))) {
                return $regLocation
            }
        }
    }

    # 3) 标准 Program Files
    $stdPath = "${env:ProgramFiles}\Autodesk\Navisworks Manage $Year"
    if (Test-Path (Join-Path $stdPath "Autodesk.Navisworks.Api.dll")) {
        return $stdPath
    }

    # 4) F 盘自定义路径
    $fPath = "F:\Navisworks\Navisworks Manage $Year"
    if (Test-Path (Join-Path $fPath "Autodesk.Navisworks.Api.dll")) {
        return $fPath
    }

    return $null
}

# ── 部署单个版本 ──
function Install-Version([string]$Year) {
    Write-Host "── Navisworks Manage $Year ──" -ForegroundColor Cyan

    $navisPath = Find-NavisworksPath -Year $Year
    if (-not $navisPath) {
        Write-Host "  [SKIP] Navisworks $Year not found on this machine." -ForegroundColor Yellow
        Write-Host "         Set `$env:NAVISWORKS_${Year}_PATH to your install folder." -ForegroundColor Yellow
        return $false
    }
    Write-Host "  [OK] Found: $navisPath"

    # 检查 DLL
    $dllPath = Join-Path $binDir "傑出品NavisworksPlugin.dll"
    if (-not (Test-Path $dllPath)) {
        Write-Host "  [ERROR] Plugin DLL not found: $dllPath" -ForegroundColor Red
        Write-Host "          Run build.bat $Year first." -ForegroundColor Red
        return $false
    }

    # 匹配清单文件
    $manifestPattern = "*_${Year}.plugin"
    $manifestPath = Get-ChildItem -Path $manifestDir -Filter $manifestPattern | Select-Object -First 1
    if (-not $manifestPath) {
        Write-Host "  [ERROR] Manifest not found: $manifestDir\$manifestPattern" -ForegroundColor Red
        return $false
    }

    # 部署目标
    $targetDir = Join-Path $navisPath "Plugins\傑出品NavisworksPlugin"
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

    Copy-Item -LiteralPath $dllPath -Destination (Join-Path $targetDir "傑出品NavisworksPlugin.dll") -Force
    Write-Host "  [OK] DLL installed"

    Copy-Item -LiteralPath $manifestPath.FullName -Destination (Join-Path $targetDir $manifestPath.Name) -Force
    Write-Host "  [OK] Manifest installed ($($manifestPath.Name))"

    Write-Host "  [DONE] Restart Navisworks Manage $Year to load the plugin." -ForegroundColor Green
    return $true
}

# ── 主逻辑 ──
Write-Host "============================================"
Write-Host "  JiePinPai Navisworks Plugin Installer"
Write-Host "============================================"
Write-Host ""

if ($AllVersions) {
    Write-Host "Installing to all detected Navisworks versions..." -ForegroundColor Cyan
    Write-Host ""
    $installed = Get-InstalledNavisworksVersions
    if ($installed.Count -eq 0) {
        Write-Host "No Navisworks installations found on this machine." -ForegroundColor Yellow
        exit 1
    }
    $successCount = 0
    foreach ($v in $installed) {
        if (Install-Version -Year $v.Year) { $successCount++ }
        Write-Host ""
    }
    Write-Host "============================================"
    Write-Host "  Installed to $successCount / $($installed.Count) version(s)"
    Write-Host "============================================"
} else {
    # 单版本或多版本（逗号分隔）
    $versions = $Version -split ',' | ForEach-Object { $_.Trim() }
    $successCount = 0
    foreach ($v in $versions) {
        if (Install-Version -Year $v) { $successCount++ }
        Write-Host ""
    }
    Write-Host "============================================"
    if ($versions.Count -gt 1) {
        Write-Host "  Installed to $successCount / $($versions.Count) version(s)"
    } elseif ($successCount -eq 0) {
        Write-Host "  Installation skipped — Navisworks $Version not found"
    }
    Write-Host "============================================"
}
