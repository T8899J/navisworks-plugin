param(
    [string]$PackageDir = "."
)

$ErrorActionPreference = "Stop"
$PackageDir = (Get-Item -LiteralPath $PackageDir).FullName
$failures = New-Object System.Collections.Generic.List[string]
$requiredPluginReferences = @(
    "Autodesk.Navisworks.Api",
    "Autodesk.Navisworks.ComApi",
    "Autodesk.Navisworks.Interop.ComApi"
)

function Add-Failure([string]$Message) {
    $failures.Add($Message)
}

$expectedFiles = @(
    "PACKAGE_MANIFEST.json",
    "diagnose.ps1",
    "host-paths.ps1",
    "setup.ps1",
    "使用说明.txt",
    "install.ps1",
    "uninstall.ps1",
    "verify-package.ps1",
    "verify.ps1",
    "傑出品NavisworksPlugin.dll",
    "傑出品NavisworksPlugin.plugin",
    "安装.bat",
    "卸载.bat",
    "验证.bat"
)

$actualFiles = @(Get-ChildItem -LiteralPath $PackageDir -File | Select-Object -ExpandProperty Name | Sort-Object)
$missing = @($expectedFiles | Where-Object { $_ -notin $actualFiles })
$unexpected = @($actualFiles | Where-Object { $_ -notin $expectedFiles })
foreach ($file in $missing) { Add-Failure "缺少文件: $file" }
foreach ($file in $unexpected) { Add-Failure "存在未声明文件: $file" }

$manifestPath = Join-Path $PackageDir "PACKAGE_MANIFEST.json"
if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
    try {
        $packageManifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json
        if ($packageManifest.schemaVersion -ne "jiepinpai.navisworks.package.v1") {
            Add-Failure "PACKAGE_MANIFEST.json schemaVersion 不正确。"
        }
        if ($packageManifest.target.product -ne "Navisworks Manage" -or $packageManifest.target.year -ne 2023) {
            Add-Failure "PACKAGE_MANIFEST.json 目标产品不正确。"
        }
        if (-not $packageManifest.sourceProvenance -or
            -not $packageManifest.sourceProvenance.sourceGitCommit -or
            -not $packageManifest.sourceProvenance.wrapperGitCommit -or
            -not $packageManifest.sourceProvenance.compiledSourceSha256) {
            Add-Failure "PACKAGE_MANIFEST.json 缺少完整的源码追溯信息。"
        }
        if (($packageManifest.sourceProvenance.sourceGitDirty -ne $false -or
            $packageManifest.sourceProvenance.wrapperGitDirty -ne $false) -and
            $packageManifest.sourceProvenance.buildMode -ne 'working-tree-snapshot') {
            Add-Failure "发布包不能来自未提交的交付输入。"
        }
        if ($packageManifest.sourceProvenance.buildMode -eq 'working-tree-snapshot' -and
            (-not $packageManifest.sourceProvenance.inputFiles -or
             -not $packageManifest.sourceProvenance.deliveryInputSha256)) {
            Add-Failure "工作区快照缺少逐文件输入哈希。"
        }
        if (-not $packageManifest.signing -or
            $packageManifest.signing.mode -notin @("unsigned", "authenticode")) {
            Add-Failure "PACKAGE_MANIFEST.json 缺少明确的签名状态。"
        }
        $declaredRuntimeReferences = @($packageManifest.target.runtimeReferences | ForEach-Object {
            ($_ -split ',')[0].Trim()
        })
        foreach ($requiredReference in $requiredPluginReferences) {
            if ($requiredReference -notin $declaredRuntimeReferences) {
                Add-Failure "PACKAGE_MANIFEST.json 缺少运行时引用: $requiredReference"
            }
        }
        foreach ($declaredReference in $declaredRuntimeReferences) {
            if ($declaredReference -notin $requiredPluginReferences) {
                Add-Failure "PACKAGE_MANIFEST.json 包含未知运行时引用: $declaredReference"
            }
        }
        $expectedHashEntries = @($expectedFiles | Where-Object { $_ -ne "PACKAGE_MANIFEST.json" } | Sort-Object)
        $declaredHashEntries = @($packageManifest.files | Select-Object -ExpandProperty name | Sort-Object)
        foreach ($file in $expectedHashEntries) {
            if ($file -notin $declaredHashEntries) {
                Add-Failure "PACKAGE_MANIFEST.json 缺少哈希条目: $file"
            }
        }
        foreach ($file in $declaredHashEntries) {
            if ($file -notin $expectedHashEntries) {
                Add-Failure "PACKAGE_MANIFEST.json 包含未知哈希条目: $file"
            }
        }
        foreach ($entry in $packageManifest.files) {
            $path = Join-Path $PackageDir $entry.name
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
            $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
            if ($actualHash -ne $entry.sha256) {
                Add-Failure "哈希不一致: $($entry.name)"
            }
        }
    } catch {
        Add-Failure "PACKAGE_MANIFEST.json 无法解析: $($_.Exception.Message)"
    }
}

$pluginManifestPath = Join-Path $PackageDir "傑出品NavisworksPlugin.plugin"
if (Test-Path -LiteralPath $pluginManifestPath -PathType Leaf) {
    try {
        $xml = New-Object System.Xml.XmlDocument
        $xml.Load($pluginManifestPath)
        $plugin = $xml.PluginContainer.Plugin
        if ($plugin.id -ne "JiePinPai_SearchPlugin" -or $plugin.Tab.Panel.Button.pluginId -ne $plugin.id) {
            Add-Failure "插件清单 pluginId 不一致。"
        }
        if ($plugin.SelectSingleNode("Assembly")) {
            Add-Failure "插件清单不能包含 Assembly 节点。"
        }
    } catch {
        Add-Failure "插件清单 XML 无效: $($_.Exception.Message)"
    }
}

$dllPath = Join-Path $PackageDir "傑出品NavisworksPlugin.dll"
if (Test-Path -LiteralPath $dllPath -PathType Leaf) {
    try {
        $name = [System.Reflection.AssemblyName]::GetAssemblyName($dllPath)
        if ($name.Name -ne "傑出品NavisworksPlugin" -or
            $name.ProcessorArchitecture -ne [System.Reflection.ProcessorArchitecture]::Amd64) {
            Add-Failure "插件 DLL 名称或架构不正确。"
        }
        $assembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($dllPath)
        $references = @($assembly.GetReferencedAssemblies())
        $metadata = @{}
        foreach ($attribute in $assembly.GetCustomAttributesData()) {
            if ($attribute.AttributeType.FullName -eq 'System.Reflection.AssemblyMetadataAttribute') {
                $metadata[$attribute.ConstructorArguments[0].Value] = $attribute.ConstructorArguments[1].Value
            }
        }
        # The profile must match the actual DLL, not just a filename or mutable setting.
        if ($packageManifest.features -or $metadata.ContainsKey('BatchTrayMeasurement')) {
            if ($packageManifest.features.batchTrayMeasurement -notin @('enabled', 'disabled') -or
                $packageManifest.features.batchTrayMeasurement -ne $metadata['BatchTrayMeasurement']) {
                Add-Failure '批量测量功能声明与 DLL 构建不一致。'
            }
            if ($packageManifest.features.quickTrayMeasurement -ne 'enabled' -or $metadata['QuickTrayMeasurement'] -ne 'enabled' -or
                $packageManifest.features.quickTrayMeasurementEntry -ne 'JiePinPai_QuickTrayMeasurement' -or
                $metadata['QuickTrayMeasurementEntry'] -ne 'JiePinPai_QuickTrayMeasurement') {
                Add-Failure '缺少便捷测量或其快捷入口的功能声明。'
            }
            $disabled = $packageManifest.features.batchTrayMeasurement -eq 'disabled'
            if ($packageManifest.sourceProvenance.buildProperties.DisableBatchMeasurement -ne $disabled) {
                Add-Failure '构建参数与批量测量功能声明不一致。'
            }
        }
        foreach ($requiredReference in $requiredPluginReferences) {
            $reference = $references | Where-Object { $_.Name -eq $requiredReference } | Select-Object -First 1
            if (-not $reference -or $reference.Version.Major -ne 20) {
                Add-Failure "插件 DLL 未引用 $requiredReference 20.x。"
            }
        }
        if ($packageManifest -and
            (Get-Item -LiteralPath $dllPath).VersionInfo.FileVersion -ne $packageManifest.packageVersion) {
            Add-Failure "PACKAGE_MANIFEST.json 的 packageVersion 与 DLL 文件版本不一致。"
        }
    } catch {
        Add-Failure "插件 DLL 无法读取: $($_.Exception.Message)"
    }
}

if ($packageManifest -and $packageManifest.signing.mode -eq "authenticode") {
    foreach ($signedFileName in @(
        "傑出品NavisworksPlugin.dll",
        "install.ps1",
        "verify.ps1",
        "uninstall.ps1",
        "diagnose.ps1",
        "host-paths.ps1",
        "setup.ps1",
        "verify-package.ps1"
    )) {
        $signature = Get-AuthenticodeSignature -LiteralPath (Join-Path $PackageDir $signedFileName)
        if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
            Add-Failure "Authenticode 签名无效: $signedFileName ($($signature.Status))"
        }
    }
}

foreach ($scriptName in @("install.ps1", "verify.ps1", "uninstall.ps1", "diagnose.ps1", "verify-package.ps1", "host-paths.ps1", "setup.ps1")) {
    $path = Join-Path $PackageDir $scriptName
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    if (-not $hasBom) { Add-Failure "$scriptName 不是 UTF-8 with BOM。" }
    try {
        [void][ScriptBlock]::Create((Get-Content -Raw -Encoding UTF8 -LiteralPath $path))
    } catch {
        Add-Failure "$scriptName PowerShell 语法无效: $($_.Exception.Message)"
    }
}

foreach ($batName in @("安装.bat", "验证.bat", "卸载.bat")) {
    $path = Join-Path $PackageDir $batName
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes | Where-Object { $_ -gt 0x7F } | Select-Object -First 1) {
        Add-Failure "$batName 包含非 ASCII 字节。"
    }
    $text = [System.Text.Encoding]::ASCII.GetString($bytes)
    if ($text -match "(?<!`r)`n") {
        Add-Failure "$batName 不是 CRLF 换行。"
    }
}

if ($failures.Count -gt 0) {
    Write-Host "PACKAGE VERIFY: FAIL" -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "PACKAGE VERIFY: PASS ($($expectedFiles.Count) files)" -ForegroundColor Green
exit 0
