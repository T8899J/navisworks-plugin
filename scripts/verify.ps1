param(
    [string]$NavisworksPath,
    [switch]$Standalone,
    [switch]$Json
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "host-paths.ps1")
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = if ($Standalone) { $ScriptDir } else { (Get-Item (Join-Path $ScriptDir "..")).FullName }
$checks = New-Object System.Collections.Generic.List[object]
$RequiredHostAssemblies = @(
    @{ Name = "Autodesk.Navisworks.Api"; File = "Autodesk.Navisworks.Api.dll"; Check = "api" },
    @{ Name = "Autodesk.Navisworks.ComApi"; File = "Autodesk.Navisworks.ComApi.dll"; Check = "com_api" },
    @{ Name = "Autodesk.Navisworks.Interop.ComApi"; File = "Autodesk.Navisworks.Interop.ComApi.dll"; Check = "interop_com_api" }
)

function Add-Check([string]$Name, [string]$Status, [string]$Detail) {
    $checks.Add([PSCustomObject]@{
        name = $Name
        status = $Status
        detail = $Detail
    })
}

function Test-HostPath([string]$Path) {
    if (-not $Path) { return $false }
    if (-not (Test-Path -LiteralPath (Join-Path $Path "Roamer.exe") -PathType Leaf)) { return $false }
    foreach ($dependency in $RequiredHostAssemblies) {
        if (-not (Test-Path -LiteralPath (Join-Path $Path $dependency.File) -PathType Leaf)) { return $false }
    }
    return $true
}



try { $NavisworksPath = Resolve-Navisworks2023Path $NavisworksPath } catch { $pathResolutionError = $_.Exception.Message; $NavisworksPath = $null }

if (-not (Test-HostPath $NavisworksPath)) {
    Add-Check "navisworks_host" "FAIL" $pathResolutionError
} else {
    $NavisworksPath = [System.IO.Path]::GetFullPath($NavisworksPath).TrimEnd('\')
    Add-Check "navisworks_host" "PASS" $NavisworksPath

    $hostAssemblyNames = @{}
    foreach ($dependency in $RequiredHostAssemblies) {
        try {
            $hostAssemblyName = [System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $NavisworksPath $dependency.File))
            $hostAssemblyNames[$dependency.Name] = $hostAssemblyName
            if ($hostAssemblyName.Name -eq $dependency.Name -and $hostAssemblyName.Version.Major -eq 20) {
                Add-Check "host_$($dependency.Check)" "PASS" $hostAssemblyName.FullName
            } else {
                Add-Check "host_$($dependency.Check)" "FAIL" "$($dependency.File) 不是 Navisworks 2023 的 20.x 程序集。"
            }
        } catch {
            Add-Check "host_$($dependency.Check)" "FAIL" $_.Exception.Message
        }
    }
    $hostApiName = $hostAssemblyNames["Autodesk.Navisworks.Api"]

    $pluginsDir = Join-Path $NavisworksPath "Plugins"
    $manifestPath = Join-Path $pluginsDir "傑出品NavisworksPlugin.plugin"
    $dllDir = Join-Path $pluginsDir "傑出品NavisworksPlugin"
    $dllPath = Join-Path $dllDir "傑出品NavisworksPlugin.dll"
    $legacyRootDll = Join-Path $pluginsDir "傑出品NavisworksPlugin.dll"
    $legacySubdirManifest = Join-Path $dllDir "傑出品NavisworksPlugin.plugin"

    if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
        Add-Check "manifest_location" "PASS" $manifestPath
        try {
            $manifest = New-Object System.Xml.XmlDocument
            $manifest.Load($manifestPath)
            $plugin = $manifest.PluginContainer.Plugin
            if ($plugin.id -ne "JiePinPai_SearchPlugin") {
                Add-Check "manifest_plugin_id" "FAIL" "实际值: $($plugin.id)"
            } elseif ($plugin.Tab.Panel.Button.pluginId -ne $plugin.id) {
                Add-Check "manifest_plugin_id" "FAIL" "Button pluginId 与 Plugin id 不一致。"
            } elseif ($plugin.SelectSingleNode("Assembly")) {
                Add-Check "manifest_plugin_id" "FAIL" "清单不应包含 Assembly 节点。"
            } else {
                Add-Check "manifest_plugin_id" "PASS" $plugin.id
            }
        } catch {
            Add-Check "manifest_xml" "FAIL" $_.Exception.Message
        }
    } else {
        Add-Check "manifest_location" "FAIL" "缺少: $manifestPath"
    }

    $pluginApiReference = $null
    $pluginReferencesByName = @{}
    if (Test-Path -LiteralPath $dllPath -PathType Leaf) {
        Add-Check "dll_location" "PASS" $dllPath
        try {
            $dllName = [System.Reflection.AssemblyName]::GetAssemblyName($dllPath)
            if ($dllName.Name -eq "傑出品NavisworksPlugin" -and
                $dllName.ProcessorArchitecture -eq [System.Reflection.ProcessorArchitecture]::Amd64) {
                Add-Check "dll_identity" "PASS" "$($dllName.FullName); x64"
            } else {
                Add-Check "dll_identity" "FAIL" "$($dllName.FullName); architecture=$($dllName.ProcessorArchitecture)"
            }

            $assembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($dllPath)
            $pluginReferences = @($assembly.GetReferencedAssemblies())
            foreach ($dependency in $RequiredHostAssemblies) {
                $reference = $pluginReferences | Where-Object { $_.Name -eq $dependency.Name } | Select-Object -First 1
                if ($reference) { $pluginReferencesByName[$dependency.Name] = $reference }
                if ($reference -and $reference.Version.Major -eq 20) {
                    Add-Check "plugin_$($dependency.Check)" "PASS" $reference.FullName
                } else {
                    Add-Check "plugin_$($dependency.Check)" "FAIL" "插件未引用 $($dependency.Name) 20.x。"
                }
            }
            $pluginApiReference = $pluginReferencesByName["Autodesk.Navisworks.Api"]
        } catch {
            Add-Check "dll_identity" "FAIL" $_.Exception.Message
        }

        if (Test-Path -LiteralPath "$dllPath`:Zone.Identifier") {
            Add-Check "dll_unblocked" "FAIL" "DLL 仍带有 Zone.Identifier。"
        } else {
            Add-Check "dll_unblocked" "PASS" "DLL 未被 Windows 标记为远程文件。"
        }
    } else {
        Add-Check "dll_location" "FAIL" "缺少: $dllPath"
    }

    if ($pluginApiReference -and $hostApiName -and $pluginApiReference.Version -ne $hostApiName.Version) {
        Add-Check "api_patch_match" "WARN" "插件编译 API=$($pluginApiReference.Version)，目标机 API=$($hostApiName.Version)。建议将 Navisworks 2023 更新到相同补丁。"
    } elseif ($pluginApiReference -and $hostApiName) {
        Add-Check "api_patch_match" "PASS" $hostApiName.Version.ToString()
    }

    foreach ($dependency in $RequiredHostAssemblies | Where-Object { $_.Name -ne "Autodesk.Navisworks.Api" }) {
        $pluginReference = $pluginReferencesByName[$dependency.Name]
        $hostReference = $hostAssemblyNames[$dependency.Name]
        if ($pluginReference -and $hostReference -and $pluginReference.Version -ne $hostReference.Version) {
            Add-Check "$($dependency.Check)_patch_match" "WARN" "插件编译版本=$($pluginReference.Version)，目标机版本=$($hostReference.Version)。"
        } elseif ($pluginReference -and $hostReference) {
            Add-Check "$($dependency.Check)_patch_match" "PASS" $hostReference.Version.ToString()
        }
    }

    if ((Test-Path -LiteralPath $legacyRootDll) -or (Test-Path -LiteralPath $legacySubdirManifest)) {
        Add-Check "legacy_layout" "FAIL" "发现旧布局残留，可能导致重复或错误加载。"
    } else {
        Add-Check "legacy_layout" "PASS" "未发现旧布局残留。"
    }

    $sourceDll = if ($Standalone) {
        Join-Path $ScriptDir "傑出品NavisworksPlugin.dll"
    } else {
        Join-Path $RootDir "bin\Release\傑出品NavisworksPlugin.dll"
    }
    if ((Test-Path -LiteralPath $sourceDll) -and (Test-Path -LiteralPath $dllPath)) {
        $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourceDll).Hash
        $installedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $dllPath).Hash
        if ($sourceHash -eq $installedHash) {
            Add-Check "dll_hash" "PASS" $installedHash
        } else {
            Add-Check "dll_hash" "FAIL" "安装后的 DLL 与发布包不一致。"
        }
    }
}

$failed = @($checks | Where-Object { $_.status -eq "FAIL" }).Count
$warned = @($checks | Where-Object { $_.status -eq "WARN" }).Count
$report = [PSCustomObject]@{
    schemaVersion = "jiepinpai.navisworks.verify.v1"
    target = "Navisworks Manage 2023"
    navisworksPath = $NavisworksPath
    success = ($failed -eq 0)
    failedChecks = $failed
    warnings = $warned
    checks = $checks
}

if ($Json) {
    $report | ConvertTo-Json -Depth 5
} else {
    Write-Host "Navisworks 2023 插件验证"
    foreach ($check in $checks) {
        $color = if ($check.status -eq "PASS") { "Green" } elseif ($check.status -eq "WARN") { "Yellow" } else { "Red" }
        Write-Host ("[{0}] {1}: {2}" -f $check.status, $check.name, $check.detail) -ForegroundColor $color
    }
    Write-Host ""
    if ($report.success) {
        Write-Host "VERIFY PASS: 文件级安装验证通过。请启动 Navisworks 完成界面验证。" -ForegroundColor Green
    } else {
        Write-Host "VERIFY FAIL: $failed 项失败。" -ForegroundColor Red
    }
}

if ($report.success) { exit 0 }
exit 1
