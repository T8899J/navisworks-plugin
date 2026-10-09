param(
    [ValidateSet("2023")]
    [string]$Version = "2023",
    [switch]$Standalone,
    [string]$NavisworksPath,
    [switch]$NonInteractive,
    [switch]$AllowUnsigned,
    [Parameter(DontShow = $true)]
    [switch]$TestFailPostCommitValidation
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "host-paths.ps1")
$DllName = "傑出品NavisworksPlugin.dll"
$ManifestName = "傑出品NavisworksPlugin.plugin"
$RequiredHostAssemblies = @(
    @{ Name = "Autodesk.Navisworks.Api"; File = "Autodesk.Navisworks.Api.dll" },
    @{ Name = "Autodesk.Navisworks.ComApi"; File = "Autodesk.Navisworks.ComApi.dll" },
    @{ Name = "Autodesk.Navisworks.Interop.ComApi"; File = "Autodesk.Navisworks.Interop.ComApi.dll" }
)

if ($Standalone) {
    $DllSourceDir = $ScriptDir
    $ManifestSourceDir = $ScriptDir
} else {
    $RootDir = (Get-Item (Join-Path $ScriptDir "..")).FullName
    $DllSourceDir = Join-Path $RootDir "bin\Release"
    $ManifestSourceDir = Join-Path $RootDir "manifests"
}

function Normalize-Path([string]$Path) {
    if (-not $Path) { return $null }
    try {
        return [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    } catch {
        return $Path.TrimEnd('\')
    }
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Assert-StandalonePackageIntegrity {
    if (-not $Standalone) { return }

    $verifierPath = Join-Path $ScriptDir "verify-package.ps1"
    if (-not (Test-Path -LiteralPath $verifierPath -PathType Leaf)) {
        throw "发布包不完整，缺少 verify-package.ps1。"
    }

    & powershell -NoProfile -ExecutionPolicy Bypass -File $verifierPath -PackageDir $ScriptDir
    if ($LASTEXITCODE -ne 0) {
        throw "发布包完整性验证失败，安装已在写入前停止。"
    }

    $packageManifestPath = Join-Path $ScriptDir "PACKAGE_MANIFEST.json"
    $packageManifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $packageManifestPath | ConvertFrom-Json
    if ($packageManifest.signing.mode -ne "unsigned") { return }
    if ($AllowUnsigned) { return }

    if ($NonInteractive) {
        throw "发布包未签名。AI 或非交互安装必须在用户明确授权后添加 -AllowUnsigned。"
    }

    $confirmation = Read-Host "发布包未签名。确认信任该来源并继续安装？输入 YES"
    if ($confirmation -cne "YES") {
        throw "用户未授权安装未签名发布包。"
    }
}

function Test-IsTargetRoamerRunning([string]$Path) {
    $targetRoamerPath = Normalize-Path (Join-Path $Path "Roamer.exe")
    foreach ($process in @(Get-Process -Name "Roamer" -ErrorAction SilentlyContinue)) {
        try {
            $processPath = Normalize-Path $process.Path
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

function Test-IsUnderProgramFiles([string]$Path) {
    $normalizedPath = Normalize-Path $Path
    foreach ($root in $script:ProgramFilesRoots) {
        $normalizedRoot = Normalize-Path $root
        if (-not $normalizedRoot) { continue }
        if ($normalizedPath.Equals($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
            $normalizedPath.StartsWith("$normalizedRoot\", [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return $false
}

function Get-NavisworksHostValidationError([string]$Path) { return (Get-Navisworks2023HostError $Path) }

function Test-IsNavisworksHostPath([string]$Path) {
    return [string]::IsNullOrEmpty((Get-NavisworksHostValidationError -Path $Path))
}



function Ensure-ManageInstallTarget([string]$Path) {
    $validationError = Get-NavisworksHostValidationError -Path $Path
    if ($validationError) { throw $validationError }

    if ((Test-IsUnderProgramFiles -Path $Path) -and -not $script:IsAdmin) {
        throw "目标目录位于 Program Files，需要以管理员身份运行安装程序。"
    }
    if (Test-IsTargetRoamerRunning -Path $Path) {
        throw "目标目录中的 Navisworks Manage 2023 正在运行，或无法确认 Roamer.exe 路径。请关闭目标程序后重试。"
    }
}

function Test-PluginPayload(
    [string]$DllPath,
    [System.IO.FileInfo]$ManifestPath,
    [switch]$SkipReferenceValidation
) {
    if (-not (Test-Path -LiteralPath $DllPath -PathType Leaf)) {
        throw "找不到插件 DLL: $DllPath"
    }

    try {
        $manifestXml = New-Object System.Xml.XmlDocument
        $manifestXml.Load($ManifestPath.FullName)
    } catch {
        throw "插件清单不是合法 XML: $($_.Exception.Message)"
    }

    $plugin = $manifestXml.PluginContainer.Plugin
    if (-not $plugin -or $plugin.id -ne "JiePinPai_SearchPlugin") {
        throw "插件清单中的 pluginId 不正确。"
    }
    if ($plugin.Tab.Panel.Button.pluginId -ne $plugin.id) {
        throw "Ribbon 按钮的 pluginId 与插件 ID 不一致。"
    }
    if ($plugin.SelectSingleNode("Assembly")) {
        throw "插件清单不能包含 Assembly 节点；DLL 由 Navisworks 同名目录规则加载。"
    }

    try {
        $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($DllPath)
        if ($assemblyName.Name -ne "傑出品NavisworksPlugin") {
            throw "程序集名称不正确: $($assemblyName.Name)"
        }
        if ($assemblyName.ProcessorArchitecture -ne [System.Reflection.ProcessorArchitecture]::Amd64) {
            throw "插件 DLL 不是 x64 程序集。"
        }

        if (-not $SkipReferenceValidation) {
            $assembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($DllPath)
            $references = @($assembly.GetReferencedAssemblies())
            foreach ($dependency in $RequiredHostAssemblies) {
                $reference = $references | Where-Object { $_.Name -eq $dependency.Name } | Select-Object -First 1
                if (-not $reference -or $reference.Version.Major -ne 20) {
                    throw "插件 DLL 未正确引用 Navisworks 2023 依赖 $($dependency.Name) 20.x。"
                }
            }
        }
    } catch {
        throw "插件 DLL 校验失败: $($_.Exception.Message)"
    }
}

function Deploy-PluginFiles([string]$NavisPath, [string]$DllPath, [System.IO.FileInfo]$ManifestPath) {
    $pluginsDir = Join-Path $NavisPath "Plugins"
    $dllSubDir = Join-Path $pluginsDir "傑出品NavisworksPlugin"
    $manifestTarget = Join-Path $pluginsDir "傑出品NavisworksPlugin.plugin"
    $dllTarget = Join-Path $dllSubDir $DllName
    $manifestStage = "$manifestTarget.installing"
    $dllStage = "$dllTarget.installing"
    $manifestBackup = "$manifestTarget.backup"
    $dllBackup = "$dllTarget.backup"

    Test-PluginPayload -DllPath $DllPath -ManifestPath $ManifestPath
    New-Item -ItemType Directory -Force -Path $pluginsDir -ErrorAction Stop | Out-Null
    New-Item -ItemType Directory -Force -Path $dllSubDir -ErrorAction Stop | Out-Null

    if ((Test-Path -LiteralPath $manifestBackup) -or (Test-Path -LiteralPath $dllBackup)) {
        throw '发现上次安装留下的备份，请先保留并检查 .backup 文件后重试。'
    }
    $preserveBackups = $false
    Remove-Item -LiteralPath $manifestStage, $dllStage -Force -ErrorAction SilentlyContinue
    $hadManifest = Test-Path -LiteralPath $manifestTarget -PathType Leaf
    $hadDll = Test-Path -LiteralPath $dllTarget -PathType Leaf
    $manifestCommitted = $false
    $dllCommitted = $false

    try {
        Copy-Item -LiteralPath $ManifestPath.FullName -Destination $manifestStage -ErrorAction Stop
        Copy-Item -LiteralPath $DllPath -Destination $dllStage -ErrorAction Stop
        Unblock-File -LiteralPath $manifestStage -ErrorAction SilentlyContinue
        Unblock-File -LiteralPath $dllStage -ErrorAction SilentlyContinue

        if ((Get-FileHash -Algorithm SHA256 -LiteralPath $ManifestPath.FullName).Hash -ne
            (Get-FileHash -Algorithm SHA256 -LiteralPath $manifestStage).Hash) {
            throw "清单复制后哈希不一致。"
        }
        if ((Get-FileHash -Algorithm SHA256 -LiteralPath $DllPath).Hash -ne
            (Get-FileHash -Algorithm SHA256 -LiteralPath $dllStage).Hash) {
            throw "DLL 复制后哈希不一致。"
        }

        if ($hadManifest) { Copy-Item -LiteralPath $manifestTarget -Destination $manifestBackup -ErrorAction Stop }
        if ($hadDll) { Copy-Item -LiteralPath $dllTarget -Destination $dllBackup -ErrorAction Stop }

        Move-Item -LiteralPath $dllStage -Destination $dllTarget -Force -ErrorAction Stop
        $dllCommitted = $true
        Move-Item -LiteralPath $manifestStage -Destination $manifestTarget -Force -ErrorAction Stop
        $manifestCommitted = $true

        if ($TestFailPostCommitValidation) {
            throw "Injected post-commit validation failure"
        }

        Unblock-File -LiteralPath $manifestTarget -ErrorAction SilentlyContinue
        Unblock-File -LiteralPath $dllTarget -ErrorAction SilentlyContinue

        if ((Get-FileHash -Algorithm SHA256 -LiteralPath $ManifestPath.FullName).Hash -ne
            (Get-FileHash -Algorithm SHA256 -LiteralPath $manifestTarget).Hash) {
            throw "安装后的清单与发布包不一致。"
        }
        if ((Get-FileHash -Algorithm SHA256 -LiteralPath $DllPath).Hash -ne
            (Get-FileHash -Algorithm SHA256 -LiteralPath $dllTarget).Hash) {
            throw "安装后的 DLL 与发布包不一致。"
        }
        Test-PluginPayload `
            -DllPath $dllTarget `
            -ManifestPath (Get-Item -LiteralPath $manifestTarget) `
            -SkipReferenceValidation
    } catch {
        $installError = $_.Exception.Message
        $rollbackErrors = New-Object System.Collections.Generic.List[string]

        try {
            if ($hadDll -and (Test-Path -LiteralPath $dllBackup -PathType Leaf)) {
                Copy-Item -LiteralPath $dllBackup -Destination $dllTarget -Force -ErrorAction Stop
            } elseif ($dllCommitted) {
                Remove-Item -LiteralPath $dllTarget -Force -ErrorAction Stop
            }
        } catch {
            $rollbackErrors.Add("DLL 回滚失败: $($_.Exception.Message)")
        }

        try {
            if ($hadManifest -and (Test-Path -LiteralPath $manifestBackup -PathType Leaf)) {
                Copy-Item -LiteralPath $manifestBackup -Destination $manifestTarget -Force -ErrorAction Stop
            } elseif ($manifestCommitted) {
                Remove-Item -LiteralPath $manifestTarget -Force -ErrorAction Stop
            }
        } catch {
            $rollbackErrors.Add("清单回滚失败: $($_.Exception.Message)")
        }

        if ($rollbackErrors.Count -gt 0) {
            $preserveBackups = $true
            throw "安装提交失败: $installError；$($rollbackErrors -join '；')"
        }
        throw "安装提交失败并已恢复旧版本: $installError"
    } finally {
        Remove-Item -LiteralPath $manifestStage, $dllStage -Force -ErrorAction SilentlyContinue
        if (-not $preserveBackups) { Remove-Item -LiteralPath $manifestBackup, $dllBackup -Force -ErrorAction SilentlyContinue }
    }
    $legacyRootDll = Join-Path $pluginsDir $DllName
    $legacySubdirManifest = Join-Path $dllSubDir "傑出品NavisworksPlugin.plugin"
    Remove-Item -LiteralPath $legacyRootDll, $legacySubdirManifest -Force -ErrorAction SilentlyContinue

    return [PSCustomObject]@{
        ManifestTargetPath = $manifestTarget
        DllTargetPath = $dllTarget
    }
}

function Wait-ForUser {
    if ($Standalone -and -not $NonInteractive) {
        Read-Host "按 Enter 退出"
    }
}

$script:IsAdmin = Test-IsAdministrator
$script:ProgramFilesRoots = @(
    $env:ProgramFiles,
    ${env:ProgramW6432},
    ${env:ProgramFiles(x86)}
) | Where-Object { $_ } | Sort-Object -Unique

Write-Host "============================================"
Write-Host "  傑出品 Navisworks Manage 2023 插件安装器"
Write-Host "============================================"
Write-Host ""

if ($Version -ne "2023") {
    Write-Host "[ERROR] 此项目仅支持 Navisworks Manage 2023。" -ForegroundColor Red
    Wait-ForUser
    exit 1
}

try {
    Assert-StandalonePackageIntegrity
} catch {
    Write-Host "[ERROR] 安装失败: $($_.Exception.Message)" -ForegroundColor Red
    Wait-ForUser
    exit 1
}

try { $targetPath = Resolve-Navisworks2023Path $NavisworksPath } catch { Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red; Wait-ForUser; exit 1 }
if (-not $targetPath -and $Standalone -and -not $NonInteractive) {
    Write-Host "未自动找到 Navisworks Manage 2023，请输入主程序目录。" -ForegroundColor Yellow
    $targetPath = Read-Host "安装路径"
}
if (-not $targetPath) {
    Write-Host "[ERROR] 未找到 Navisworks Manage 2023 主程序目录。" -ForegroundColor Red
    Wait-ForUser
    exit 1
}
$targetPath = Normalize-Path $targetPath

try {
    Ensure-ManageInstallTarget -Path $targetPath
    $dllPath = Join-Path $DllSourceDir $DllName
    $manifestPath = Join-Path $ManifestSourceDir $ManifestName
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "找不到插件清单: $manifestPath"
    }

    $result = Deploy-PluginFiles `
        -NavisPath $targetPath `
        -DllPath $dllPath `
        -ManifestPath (Get-Item -LiteralPath $manifestPath)

    Write-Host "[PASS] Navisworks Manage 2023 插件安装完成。" -ForegroundColor Green
    Write-Host "  清单: $($result.ManifestTargetPath)"
    Write-Host "  DLL:  $($result.DllTargetPath)"
    Write-Host "  请重启 Navisworks，在 Add-Ins 中完成界面和功能验证。"
    $exitCode = 0
} catch {
    Write-Host "[ERROR] 安装失败: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $script:IsAdmin -and (Test-IsUnderProgramFiles -Path $targetPath)) {
        Write-Host "请右键安装.bat，以管理员身份运行。" -ForegroundColor Yellow
    }
    $exitCode = 1
}

Wait-ForUser
exit $exitCode
