param(
    [string]$NavisworksPath,
    [switch]$Standalone,
    [switch]$Json
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$verifyScript = Join-Path $ScriptDir "verify.ps1"
if (-not (Test-Path -LiteralPath $verifyScript -PathType Leaf)) {
    Write-Host "DIAGNOSE FAIL: 找不到 verify.ps1。" -ForegroundColor Red
    exit 1
}

$arguments = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $verifyScript
)
if ($NavisworksPath) {
    $arguments += @("-NavisworksPath", $NavisworksPath)
}
if ($Standalone) {
    $arguments += "-Standalone"
}
if ($Json) {
    $arguments += "-Json"
}

& powershell @arguments
exit $LASTEXITCODE
