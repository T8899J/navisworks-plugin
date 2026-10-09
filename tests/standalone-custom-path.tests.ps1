. (Join-Path $PSScriptRoot 'installer-test-common.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$fixture = [IO.Path]::GetFullPath((Join-Path $root '.test-custom-delivery'))
if ($fixture -ne (Join-Path ([IO.Path]::GetFullPath($root)) '.test-custom-delivery')) { throw 'Unsafe fixture path' }
$package = Join-Path $fixture "U盘 解压 [包] & O'Brien `$value"
$target = Join-Path $fixture "设计 软件 [2023] & O'Brien `$value\Manage"
$previousEnv = $env:NAVISWORKS_2023_PATH
function Invoke-Checked([string]$Name, [string[]]$ExtraArguments) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $package $Name) -Standalone @ExtraArguments
    if ($LASTEXITCODE -ne 0) { throw "$Name failed: $LASTEXITCODE" }
}
try {
    [void][IO.Directory]::CreateDirectory($fixture)
    [void][IO.Directory]::CreateDirectory($target)
    Copy-Item -LiteralPath (Join-Path $root 'release') -Destination $package -Recurse
    [IO.File]::WriteAllBytes((Join-Path $target 'Roamer.exe'), [byte[]](0x4d,0x5a))
    Copy-Item -Path (Join-Path $TestHostSource 'Autodesk*.dll') -Destination $target
    Invoke-Checked 'install.ps1' @('-NonInteractive','-AllowUnsigned','-NavisworksPath',(Join-Path $target 'Roamer.exe'))
    $env:NAVISWORKS_2023_PATH = $target
    Invoke-Checked 'verify.ps1' @('-Json','-NavisworksPath',$target)
    $dll = Join-Path $target 'Plugins\傑出品NavisworksPlugin\傑出品NavisworksPlugin.dll'
    if ((Get-FileHash -LiteralPath $dll).Hash -ne (Get-FileHash -LiteralPath (Join-Path $package '傑出品NavisworksPlugin.dll')).Hash) { throw 'Installed DLL mismatch' }
    Invoke-Checked 'uninstall.ps1' @('-NonInteractive','-NavisworksPath',$target)
    if (Test-Path -LiteralPath $dll) { throw 'Plugin not removed' }
    if (-not (Test-Path -LiteralPath (Join-Path $target 'Roamer.exe'))) { throw 'Host was modified' }
    Write-Host 'STANDALONE CUSTOM PATH: PASS (install, discover, verify, uninstall)'
} finally {
    $env:NAVISWORKS_2023_PATH = $previousEnv
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
