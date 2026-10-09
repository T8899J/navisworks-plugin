. (Join-Path $PSScriptRoot 'installer-test-common.ps1')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $root 'scripts\host-paths.ps1')
$testRoot = Join-Path $root '.test-host-discovery'
if ([IO.Path]::GetFullPath($testRoot) -ne (Join-Path ([IO.Path]::GetFullPath($root)) '.test-host-discovery')) { throw 'Unsafe fixture path' }
$first = Join-Path $testRoot "软件 自定义 & O'Brien `$data\Manage"
$second = Join-Path $testRoot '另一个安装'
$bad = Join-Path $testRoot 'Exporters'
$checks = 0
function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message }; $script:checks++ }
try {
    foreach ($dir in @($first, $second, $bad)) { [void][IO.Directory]::CreateDirectory($dir) }
    foreach ($dir in @($first, $second)) {
        [IO.File]::WriteAllBytes((Join-Path $dir 'Roamer.exe'), [byte[]](0x4d,0x5a))
        Copy-Item -Path (Join-Path $TestHostSource 'Autodesk*.dll') -Destination $dir
    }
    Assert (-not (Get-Navisworks2023HostError $first)) 'Custom Chinese/space path rejected'
    Assert ((ConvertTo-NavisworksDirectory ('"' + (Join-Path $first 'Roamer.exe') + '"')) -eq $first) 'Quoted executable normalization failed'
    Assert ([bool](Get-Navisworks2023HostError $bad)) 'Exporters-only directory accepted'
    # Validate Autodesk's actual generic ProductName without loading native libraries.
    $genericExe = Join-Path $testRoot 'generic.exe'
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $fixtureSource = Join-Path $testRoot 'Host.cs'
    [IO.File]::WriteAllText($fixtureSource, 'using System.Reflection; [assembly: AssemblyProduct("Navisworks")] public class Host { public static void Main() {} }')
    & $csc /nologo /target:exe ("/out:" + $genericExe) $fixtureSource
    if ($LASTEXITCODE -ne 0) { throw 'Cannot compile generic host fixture' }
    Copy-Item -LiteralPath $genericExe -Destination (Join-Path $first 'Roamer.exe') -Force
    Assert (-not (Get-Navisworks2023HostError $first)) 'Generic Autodesk product name was rejected'
    $futureApi = Join-Path $testRoot 'Autodesk.Navisworks.Api.dll'
    [IO.File]::WriteAllText($fixtureSource, 'using System.Reflection; [assembly: AssemblyVersion("18.0.0.0")] public class Host {}')
    & $csc /nologo /target:library ("/out:" + $futureApi) $fixtureSource
    if ($LASTEXITCODE -ne 0) { throw 'Cannot compile mismatched API fixture' }
    Copy-Item -LiteralPath $futureApi -Destination $second -Force
    Assert ([bool](Get-Navisworks2023HostError $second)) 'API 18.x was accepted as 2023'
    Copy-Item -LiteralPath (Join-Path $TestHostSource 'Autodesk.Navisworks.Api.dll') -Destination $second -Force
    $shell = New-Object -ComObject WScript.Shell
    try {
        $linkPath = Join-Path $testRoot 'Navisworks Manage 2023.lnk'
        $link = $shell.CreateShortcut($linkPath); $link.TargetPath = Join-Path $first 'Roamer.exe'; $link.Save()
        Assert ((ConvertTo-NavisworksDirectory $linkPath) -eq $first) 'Shortcut normalization failed'
    } finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
    function Get-NavisworksPathCandidates { $bad; $first; (Join-Path $first 'Roamer.exe'); $linkPath }
    Assert (@(Find-Navisworks2023Paths).Count -eq 1) 'Discovery failed to filter invalid paths or deduplicate'
    Assert ((Resolve-Navisworks2023Path) -eq $first) 'Single valid candidate not selected'
    function Get-NavisworksPathCandidates { $first; $second }
    $rejected = $false
    try { Resolve-Navisworks2023Path | Out-Null } catch { $rejected = $true }
    Assert $rejected 'Multiple valid hosts silently selected'
    Assert ((Resolve-Navisworks2023Path $second) -eq $second) 'Explicit path did not win'
    $tokens = $null; $parseErrors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'scripts\setup.ps1'), [ref]$tokens, [ref]$parseErrors)
    Assert ($parseErrors.Count -eq 0) 'Setup parse errors'
    foreach ($name in @('ConvertTo-PowerShellLiteral','Invoke-SetupAction')) {
        $node = $ast.Find({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name }, $true)
        . ([scriptblock]::Create($node.Extent.Text))
    }
    Add-Type -AssemblyName System.Windows.Forms
    $SetupDir = Join-Path $root 'scripts'; $Action = 'Verify'; $Standalone = $false
    $result = Invoke-SetupAction $first $false
    Assert ($result.ExitCode -ne 0) 'Empty host incorrectly reported installed'
    Assert ($result.Output.Contains($first)) 'Child command lost path characters'
    Assert ($result.Output.Contains('manifest_location')) 'Child verifier did not run'
    Write-Host "HOST DISCOVERY: PASS ($checks checks)"
} finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
