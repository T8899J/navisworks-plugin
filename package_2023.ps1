param([switch]$Snapshot, [string]$NavisworksPath, [switch]$DisableBatchMeasurement)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot 'scripts\host-paths.ps1')
if (-not $NavisworksPath) { $NavisworksPath = $env:NavisworksInstallDir }
if (-not $NavisworksPath) { $NavisworksPath = $env:NAVISWORKS_2023_PATH }
$NavisworksPath = Resolve-Navisworks2023Path $NavisworksPath

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = (Get-Item $ScriptDir).FullName
$ReleaseDir = Join-Path $RootDir "release"
$DistDir = Join-Path $RootDir "dist"
$StageDir = Join-Path $RootDir ".release-staging"
$BackupDir = Join-Path $RootDir ".release-backup"
$ArchiveStagePath = Join-Path $RootDir ".release-archive-staging.zip"
$ArchiveVerifyDir = Join-Path $RootDir ".archive-verification"
$Year = 2023
$DllName = "傑出品NavisworksPlugin.dll"
$ManifestName = "傑出品NavisworksPlugin.plugin"
$BuildOutput = if ($DisableBatchMeasurement) { 'bin\Release-NoBatch' } else { 'bin\Release' }
$BatchFeature = if ($DisableBatchMeasurement) { 'disabled' } else { 'enabled' }

function Assert-SafeWorkspacePath([string]$Path, [string]$ExpectedLeaf) {
    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    $rootPath = [System.IO.Path]::GetFullPath($RootDir).TrimEnd('\')
    if (-not $fullPath.StartsWith("$rootPath\", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "拒绝操作工作区之外的路径: $fullPath"
    }
    if ((Split-Path -Leaf $fullPath) -ne $ExpectedLeaf) {
        throw "拒绝操作非预期目录: $fullPath"
    }
}

function Remove-SafeDirectory([string]$Path, [string]$ExpectedLeaf) {
    Assert-SafeWorkspacePath -Path $Path -ExpectedLeaf $ExpectedLeaf
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
    }
}

function Remove-SafeFile([string]$Path, [string]$ExpectedLeaf) {
    Assert-SafeWorkspacePath -Path $Path -ExpectedLeaf $ExpectedLeaf
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
    }
}

function Invoke-GitText([string]$RepositoryPath, [object[]]$Arguments) {
    $output = & git -c core.excludesFile=.gitignore -C $RepositoryPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Git 命令失败 ($RepositoryPath): $($output -join ' ')"
    }
    return ($output -join "`n").Trim()
}

function Get-TextSha256([string]$Text) {
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return ([BitConverter]::ToString($sha256.ComputeHash($bytes))).Replace("-", "")
    } finally {
        $sha256.Dispose()
    }
}

function Get-SourceProvenance {
    $projectPath = Join-Path $RootDir 'NavisworksPlugin.csproj'
    $projectXml = [xml][IO.File]::ReadAllText($projectPath)
    $sourcePaths = @($projectXml.Project.ItemGroup.Compile | Where-Object { $_ } | ForEach-Object { $_.Include })
    $deliveryPaths = @('NavisworksPlugin.csproj', 'build_2023.ps1', 'package_2023.ps1', 'package_2023.bat', 'scripts', 'manifests', '使用说明.txt')
    $hashLines = New-Object 'System.Collections.Generic.List[string]'
    foreach ($path in $sourcePaths) {
        $fullPath = Join-Path $RootDir $path
        $hashLines.Add("source/$($path.Replace('\','/'))|$((Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash)")
    }
    $deliveryLines = New-Object 'System.Collections.Generic.List[string]'
    foreach ($path in $deliveryPaths) {
        foreach ($file in @(Get-ChildItem -LiteralPath (Join-Path $RootDir $path) -File -Recurse)) {
            $relative = $file.FullName.Substring($RootDir.Length + 1).Replace('\','/')
            $deliveryLines.Add("package/$relative|$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)")
        }
    }
    foreach ($name in @('Autodesk.Navisworks.Api.dll', 'Autodesk.Navisworks.ComApi.dll', 'Autodesk.Navisworks.Interop.ComApi.dll', 'navisworks.gui.roamer.dll')) {
        $deliveryLines.Add("reference/$name|$((Get-FileHash -LiteralPath (Join-Path $NavisworksPath $name) -Algorithm SHA256).Hash)")
    }
    $sourceStatus = Invoke-GitText $RootDir (@('status','--porcelain','--untracked-files=all','--') + $sourcePaths + @('NavisworksPlugin.csproj'))
    $packageStatus = Invoke-GitText $RootDir (@('status','--porcelain','--untracked-files=all','--') + $deliveryPaths)
    if (($sourceStatus -or $packageStatus) -and -not $Snapshot) { throw '交付输入存在未提交更新。请提交后打包，或使用 -Snapshot 保留当前工作区版本及输入哈希。' }
    $commit = Invoke-GitText $RootDir @('rev-parse','HEAD')
    $branch = Invoke-GitText $RootDir @('rev-parse','--abbrev-ref','HEAD')
    return [ordered]@{
        buildMode = $(if ($Snapshot) { 'working-tree-snapshot' } else { 'committed-release' })
        buildProperties = [ordered]@{ DisableBatchMeasurement = [bool]$DisableBatchMeasurement }
        sourceGitCommit = $commit
        sourceGitBranch = $branch
        sourceGitDirty = [bool]$sourceStatus
        wrapperGitCommit = $commit
        wrapperGitBranch = $branch
        wrapperGitDirty = [bool]$packageStatus
        compiledSourceSha256 = Get-TextSha256 (($hashLines | Sort-Object) -join "`n")
        compiledSourceFileCount = $hashLines.Count
        deliveryInputSha256 = Get-TextSha256 (($deliveryLines | Sort-Object) -join "`n")
        inputFiles = @(@($hashLines) + @($deliveryLines) | Sort-Object -Unique)
    }
}
function Invoke-CodeSigning([string]$PackageStageDir) {
    $thumbprint = [Environment]::GetEnvironmentVariable("JIEPINPAI_CODE_SIGNING_THUMBPRINT")
    $requireSigned = [Environment]::GetEnvironmentVariable("JIEPINPAI_REQUIRE_SIGNED") -eq "1"
    $timestampServer = [Environment]::GetEnvironmentVariable("JIEPINPAI_TIMESTAMP_SERVER")
    if (-not $thumbprint) {
        if ($requireSigned) {
            throw "要求签名发布，但未配置 JIEPINPAI_CODE_SIGNING_THUMBPRINT。"
        }
        return [ordered]@{
            mode = "unsigned"
            reason = "No trusted code-signing certificate was configured."
        }
    }

    $thumbprint = $thumbprint.Replace(" ", "")
    $certificate = @(
        Get-ChildItem -Path "Cert:\CurrentUser\My\$thumbprint", "Cert:\LocalMachine\My\$thumbprint" -ErrorAction SilentlyContinue
    ) | Where-Object { $_.HasPrivateKey } | Select-Object -First 1
    if (-not $certificate) {
        throw "找不到带私钥的代码签名证书: $thumbprint"
    }

    $filesToSign = @(
        (Join-Path $PackageStageDir $DllName)
    ) + @(Get-ChildItem -LiteralPath $PackageStageDir -File -Filter "*.ps1" | Select-Object -ExpandProperty FullName)
    foreach ($file in $filesToSign) {
        $signArguments = @{
            FilePath = $file
            Certificate = $certificate
            HashAlgorithm = "SHA256"
        }
        if ($timestampServer) {
            $signArguments.TimestampServer = $timestampServer
        }
        $signature = Set-AuthenticodeSignature @signArguments
        if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
            throw "代码签名失败: $file ($($signature.Status))"
        }
    }

    return [ordered]@{
        mode = "authenticode"
        certificateThumbprint = $certificate.Thumbprint
        certificateSubject = $certificate.Subject
        certificateNotAfterUtc = $certificate.NotAfter.ToUniversalTime().ToString("o")
        timestampServer = $timestampServer
    }
}

Write-Host "[1/7] 验证发布契约 ..."
$contractTest = Join-Path $RootDir "tests\release-contract.tests.ps1"
& powershell -NoProfile -ExecutionPolicy Bypass -File $contractTest
if ($LASTEXITCODE -ne 0) {
    throw "发布契约测试失败。"
}

Write-Host "[2/7] 验证源码与交付输入可追溯 ..."
$sourceProvenance = Get-SourceProvenance

Write-Host "[3/7] 构建 Navisworks 2023 插件 ..."
$buildScript = Join-Path $RootDir "build_2023.ps1"
$buildArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $buildScript, '-NavisworksPath', $NavisworksPath)
if ($DisableBatchMeasurement) { $buildArguments += '-DisableBatchMeasurement' }
& powershell @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "插件构建失败。"
}

Write-Host "[4/7] 在临时目录组装发布包 ..."
Remove-SafeDirectory -Path $StageDir -ExpectedLeaf ".release-staging"
New-Item -ItemType Directory -Path $StageDir -ErrorAction Stop | Out-Null

$copyMap = [ordered]@{
    (Join-Path $RootDir "$BuildOutput\$DllName") = $DllName
    (Join-Path $RootDir "manifests\$ManifestName") = $ManifestName
    (Join-Path $RootDir "scripts\install-standalone.bat") = "安装.bat"
    (Join-Path $RootDir "scripts\verify-standalone.bat") = "验证.bat"
    (Join-Path $RootDir "scripts\uninstall-standalone.bat") = "卸载.bat"
    (Join-Path $RootDir "scripts\install.ps1") = "install.ps1"
    (Join-Path $RootDir "scripts\verify.ps1") = "verify.ps1"
    (Join-Path $RootDir "scripts\uninstall.ps1") = "uninstall.ps1"
    (Join-Path $RootDir "scripts\diagnose.ps1") = "diagnose.ps1"
    (Join-Path $RootDir "scripts\host-paths.ps1") = "host-paths.ps1"
    (Join-Path $RootDir "scripts\setup.ps1") = "setup.ps1"
    (Join-Path $RootDir "使用说明.txt") = "使用说明.txt"
    (Join-Path $RootDir "scripts\verify-package.ps1") = "verify-package.ps1"
}

foreach ($entry in $copyMap.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $entry.Key -PathType Leaf)) {
        throw "缺少打包输入: $($entry.Key)"
    }
    Copy-Item -LiteralPath $entry.Key -Destination (Join-Path $StageDir $entry.Value) -Force -ErrorAction Stop
}

if ($DisableBatchMeasurement) {
    $instructionsPath = Join-Path $StageDir '使用说明.txt'
    $instructions = [IO.File]::ReadAllText($instructionsPath)
    $section = '(?s)桥架测量：.*?(?=只支持 Manage 2023)'
    if ([regex]::Matches($instructions, $section).Count -ne 1) { throw '未找到唯一的桥架测量说明，停止生成不匹配的安装包。' }
    $replacement = @'
桥架测量（本安装包）：
1. 点击 Curi 页头“桥架测量 ▾”→“便捷测量”，打开简易测量窗口。
   选中一个桥架构件，点击“测量选中构件”，查看长度和毫米换算。
2. 已包含 Add-Ins“桥架便捷测量”快捷入口，可直接打开便捷测量，
   无需先打开查找窗口。一次测量一个构件，便捷测量正常可用。
3. “批量测量”暂不可用。点击后仅弹窗提示：当前功能暂不可用。
   关闭提示后可继续使用便捷测量和原有查找功能。

'@
    [IO.File]::WriteAllText($instructionsPath, [regex]::Replace($instructions, $section, ($replacement + "`r`n")), [Text.UTF8Encoding]::new($true))
}

$version = [regex]::Match([IO.File]::ReadAllText((Join-Path $RootDir 'Properties\AssemblyInfo.cs')), 'AssemblyFileVersion\("([^"]+)"\)').Groups[1].Value
$dllPath = Join-Path $StageDir $DllName
$dllFileVersion = (Get-Item -LiteralPath $dllPath).VersionInfo.FileVersion
if ($dllFileVersion -ne $version) {
    throw "DLL 文件版本 $dllFileVersion 与 AssemblyInfo.cs $version 不一致。"
}
$signing = Invoke-CodeSigning -PackageStageDir $StageDir
$dllAssembly = [System.Reflection.Assembly]::ReflectionOnlyLoad(
    [System.IO.File]::ReadAllBytes($dllPath)
)
$apiReference = $dllAssembly.GetReferencedAssemblies() |
    Where-Object { $_.Name -eq "Autodesk.Navisworks.Api" } |
    Select-Object -First 1
$runtimeReferences = @($dllAssembly.GetReferencedAssemblies() |
    Where-Object { $_.Name -in @(
        "Autodesk.Navisworks.Api",
        "Autodesk.Navisworks.ComApi",
        "Autodesk.Navisworks.Interop.ComApi"
    ) } |
    Sort-Object Name |
    ForEach-Object { "$($_.Name), Version=$($_.Version)" })
$fileEntries = @(Get-ChildItem -LiteralPath $StageDir -File | Sort-Object Name | ForEach-Object {
    [ordered]@{
        name = $_.Name
        length = $_.Length
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash
    }
})
$packageManifest = [ordered]@{
    schemaVersion = "jiepinpai.navisworks.package.v1"
    packageVersion = $version
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    sourceProvenance = $sourceProvenance
    signing = $signing
    features = [ordered]@{
        batchTrayMeasurement = $BatchFeature
        quickTrayMeasurement = 'enabled'
        quickTrayMeasurementEntry = 'JiePinPai_QuickTrayMeasurement'
    }
    target = [ordered]@{
        product = "Navisworks Manage"
        year = $Year
        architecture = "x64"
        dotNetFramework = "4.8"
        apiReference = $apiReference.Version.ToString()
        runtimeReferences = $runtimeReferences
    }
    loadingContract = [ordered]@{
        manifest = "Plugins\傑出品NavisworksPlugin.plugin"
        assembly = "Plugins\傑出品NavisworksPlugin\傑出品NavisworksPlugin.dll"
        modifiesRoamerConfig = $false
    }
    files = $fileEntries
}
$manifestJsonPath = Join-Path $StageDir "PACKAGE_MANIFEST.json"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText(
    $manifestJsonPath,
    ($packageManifest | ConvertTo-Json -Depth 6),
    $utf8NoBom
)

Write-Host "[5/7] 验证临时发布包 ..."
$packageVerifier = Join-Path $StageDir "verify-package.ps1"
& powershell -NoProfile -ExecutionPolicy Bypass -File $packageVerifier -PackageDir $StageDir
if ($LASTEXITCODE -ne 0) {
    throw "发布包自检失败，未替换现有 release。"
}
$afterBuild = Get-SourceProvenance
if ($sourceProvenance.compiledSourceSha256 -ne $afterBuild.compiledSourceSha256 -or
    $sourceProvenance.deliveryInputSha256 -ne $afterBuild.deliveryInputSha256 -or
    $sourceProvenance.sourceGitCommit -ne $afterBuild.sourceGitCommit -or
    $sourceProvenance.wrapperGitCommit -ne $afterBuild.wrapperGitCommit) {
    throw '构建期间输入发生变化，拒绝交付。请在源码稳定后重新打包。'
}

Write-Host "[6/7] 发布验证通过的包 ..."
Remove-SafeDirectory -Path $BackupDir -ExpectedLeaf ".release-backup"
try {
    if (Test-Path -LiteralPath $ReleaseDir) {
        Assert-SafeWorkspacePath -Path $ReleaseDir -ExpectedLeaf "release"
        Move-Item -LiteralPath $ReleaseDir -Destination $BackupDir -ErrorAction Stop
    }
    Move-Item -LiteralPath $StageDir -Destination $ReleaseDir -ErrorAction Stop
    Remove-SafeDirectory -Path $BackupDir -ExpectedLeaf ".release-backup"
} catch {
    if (-not (Test-Path -LiteralPath $ReleaseDir) -and (Test-Path -LiteralPath $BackupDir)) {
        Move-Item -LiteralPath $BackupDir -Destination $ReleaseDir -ErrorAction SilentlyContinue
    }
    throw
}

Write-Host "[7/7] 创建并复验版本化 ZIP ..."
Assert-SafeWorkspacePath -Path $DistDir -ExpectedLeaf "dist"
New-Item -ItemType Directory -Path $DistDir -Force -ErrorAction Stop | Out-Null
Remove-SafeFile -Path $ArchiveStagePath -ExpectedLeaf ".release-archive-staging.zip"
Remove-SafeDirectory -Path $ArchiveVerifyDir -ExpectedLeaf ".archive-verification"

$suffix = if ($Snapshot) { '-snapshot-' + (Get-Date -Format 'yyyyMMdd-HHmmss') } else { '' }
if ($DisableBatchMeasurement) { $suffix = '-no-batch' + $suffix }
$archiveName = "JiePinPai-Navisworks-2023-v$version$suffix.zip"
$archivePath = Join-Path $DistDir $archiveName
$checksumPath = "$archivePath.sha256"
Remove-SafeFile -Path $archivePath -ExpectedLeaf $archiveName
Remove-SafeFile -Path $checksumPath -ExpectedLeaf "$archiveName.sha256"

try {
    Compress-Archive -Path (Join-Path $ReleaseDir "*") -DestinationPath $ArchiveStagePath -CompressionLevel Optimal -ErrorAction Stop
    Expand-Archive -LiteralPath $ArchiveStagePath -DestinationPath $ArchiveVerifyDir -Force -ErrorAction Stop
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $ArchiveVerifyDir "verify-package.ps1") -PackageDir $ArchiveVerifyDir
    if ($LASTEXITCODE -ne 0) {
        throw "ZIP 解压后的发布包验证失败。"
    }

    Move-Item -LiteralPath $ArchiveStagePath -Destination $archivePath -Force -ErrorAction Stop
    $archiveHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash
    [System.IO.File]::WriteAllText(
        $checksumPath,
        "$archiveHash *$archiveName`r`n",
        [System.Text.Encoding]::ASCII
    )
} finally {
    if (Test-Path -LiteralPath $ArchiveStagePath) {
        Remove-SafeFile -Path $ArchiveStagePath -ExpectedLeaf ".release-archive-staging.zip"
    }
    Remove-SafeDirectory -Path $ArchiveVerifyDir -ExpectedLeaf ".archive-verification"
}

Write-Host ""
Write-Host "PACKAGE SUCCESS: $ReleaseDir" -ForegroundColor Green
Write-Host "DELIVERY ZIP: $archivePath" -ForegroundColor Green
Write-Host "ZIP SHA256: $archiveHash"
Write-Host "解压后双击 安装.bat；阅读 使用说明.txt 即可安装。"
exit 0
