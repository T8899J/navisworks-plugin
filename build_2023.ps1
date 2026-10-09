param([string]$NavisworksPath, [switch]$DisableBatchMeasurement)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts\host-paths.ps1')
if (-not $NavisworksPath) { $NavisworksPath = $env:NavisworksInstallDir }
if (-not $NavisworksPath) { $NavisworksPath = $env:NAVISWORKS_2023_PATH }
$NavisworksPath = Resolve-Navisworks2023Path $NavisworksPath
$guiAssembly = Join-Path $NavisworksPath 'navisworks.gui.roamer.dll'
if (-not (Test-Path -LiteralPath $guiAssembly -PathType Leaf)) { throw "缺少构建依赖：$guiAssembly" }
if ([Reflection.AssemblyName]::GetAssemblyName($guiAssembly).Version.Major -ne 20) { throw 'GUI 程序集不是 Navisworks 2023 API 20.x。' }
$dotnet = Get-Command dotnet -ErrorAction Stop
Write-Host "BUILD HOST: $NavisworksPath"
$profileArguments = @()
if ($DisableBatchMeasurement) {
    $profileArguments = @('-p:DisableBatchMeasurement=true', '-p:OutputPath=bin\Release-NoBatch\', '-p:IntermediateOutputPath=obj\Release-NoBatch\')
}
& $dotnet.Source build (Join-Path $PSScriptRoot 'NavisworksPlugin.csproj') -c Release -t:Rebuild --ignore-failed-sources "-p:NavisworksInstallDir=$NavisworksPath" -p:NuGetAudit=false -v:minimal @profileArguments
if ($LASTEXITCODE -ne 0) { throw "2023 构建失败：$LASTEXITCODE" }
exit 0
