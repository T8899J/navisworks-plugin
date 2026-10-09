$ErrorActionPreference = 'Stop'
$TestWorkspace = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
. (Join-Path $TestWorkspace 'scripts\host-paths.ps1')
$TestHostSource = Resolve-Navisworks2023Path $env:NAVISWORKS_2023_PATH
function Assert-TestWorkspacePath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($TestWorkspace + '\.test-', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe test cleanup path: $full"
    }
}
