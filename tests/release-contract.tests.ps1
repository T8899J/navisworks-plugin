$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$scripts = @(Get-ChildItem -LiteralPath (Join-Path $root 'scripts') -Filter '*.ps1' -File | Where-Object { $_.Name -ne 'install_2023.ps1' }) + @(
    Get-Item -LiteralPath (Join-Path $root 'build_2023.ps1'), (Join-Path $root 'package_2023.ps1')
)
foreach ($file in $scripts) {
    $tokens = $null; $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw "$($file.Name): $errors" }
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    if ($bytes.Length -lt 3 -or $bytes[0] -ne 239 -or $bytes[1] -ne 187 -or $bytes[2] -ne 191) { throw "UTF-8 BOM required: $($file.Name)" }
}
foreach ($file in @(Get-ChildItem -LiteralPath (Join-Path $root 'scripts') -Filter '*.bat' -File) + @(Get-Item -LiteralPath (Join-Path $root 'package_2023.bat'))) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    if (@($bytes | Where-Object { $_ -gt 127 }).Count -or [Text.Encoding]::ASCII.GetString($bytes) -match "(?<!`r)`n") { throw "ASCII/CRLF required: $($file.Name)" }
}
$info = [IO.File]::ReadAllText((Join-Path $root 'Properties\AssemblyInfo.cs'))
$assembly = [regex]::Match($info, 'AssemblyVersion\("([^"]+)"\)').Groups[1].Value
$fileVersion = [regex]::Match($info, 'AssemblyFileVersion\("([^"]+)"\)').Groups[1].Value
if (-not $assembly -or $assembly -ne $fileVersion) { throw 'Assembly versions differ' }
Write-Host "RELEASE CONTRACT: PASS (2023, $assembly, PowerShell 5.1 syntax and encoding)"
