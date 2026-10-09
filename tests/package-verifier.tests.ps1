. (Join-Path $PSScriptRoot 'installer-test-common.ps1')
$ErrorActionPreference = "Stop"

$RootDir = (Get-Item (Join-Path $PSScriptRoot "..")).FullName
$SourcePackage = Join-Path $RootDir "release"
$TestPackage = Join-Path $RootDir ".test-package"

function Reset-TestPackage {
    if (Test-Path -LiteralPath $TestPackage) {
        Assert-TestWorkspacePath $TestPackage
        Remove-Item -LiteralPath $TestPackage -Recurse -Force
    }
    Copy-Item -LiteralPath $SourcePackage -Destination $TestPackage -Recurse
}

function Write-PackageManifest($Manifest) {
    $manifestPath = Join-Path $TestPackage "PACKAGE_MANIFEST.json"
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($manifestPath, ($Manifest | ConvertTo-Json -Depth 6), $utf8NoBom)
}

function Assert-PackageRejected([string]$Reason) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RootDir "scripts\verify-package.ps1") -PackageDir $TestPackage
    if ($LASTEXITCODE -eq 0) {
        throw "Package verifier accepted $Reason"
    }
}

try {
    Reset-TestPackage
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $RootDir "scripts\verify-package.ps1") -PackageDir $TestPackage
    if ($LASTEXITCODE -ne 0) {
        throw "Baseline release package is invalid"
    }

    Reset-TestPackage
    $manifestPath = Join-Path $TestPackage "PACKAGE_MANIFEST.json"
    $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json
    $manifest.files = @($manifest.files | Select-Object -Skip 1)
    Write-PackageManifest $manifest
    Assert-PackageRejected "a manifest with a missing hash entry"

    Reset-TestPackage
    $manifestPath = Join-Path $TestPackage "PACKAGE_MANIFEST.json"
    $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json
    $manifest.target.runtimeReferences = @($manifest.target.runtimeReferences | Select-Object -First 1)
    Write-PackageManifest $manifest
    Assert-PackageRejected "a manifest with a missing runtime COM reference"

    Reset-TestPackage
    $manifestPath = Join-Path $TestPackage "PACKAGE_MANIFEST.json"
    $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json
    $manifest.PSObject.Properties.Remove("sourceProvenance")
    Write-PackageManifest $manifest
    Assert-PackageRejected "a manifest without source provenance"

    Reset-TestPackage
    $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $TestPackage 'PACKAGE_MANIFEST.json') | ConvertFrom-Json
    $manifest.sourceProvenance.buildMode = 'committed-release'
    $manifest.sourceProvenance.sourceGitDirty = $true
    Write-PackageManifest $manifest
    Assert-PackageRejected 'dirty sources disguised as a committed release'

    Reset-TestPackage
    $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $TestPackage 'PACKAGE_MANIFEST.json') | ConvertFrom-Json
    $manifest.sourceProvenance.buildMode = 'working-tree-snapshot'
    $manifest.sourceProvenance.inputFiles = @()
    Write-PackageManifest $manifest
    Assert-PackageRejected 'a snapshot without input hashes'
    Reset-TestPackage
    $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $TestPackage 'PACKAGE_MANIFEST.json') | ConvertFrom-Json
    if ($manifest.features) {
        $manifest.features.batchTrayMeasurement = if ($manifest.features.batchTrayMeasurement -eq 'disabled') { 'enabled' } else { 'disabled' }
        Write-PackageManifest $manifest
        Assert-PackageRejected 'a feature declaration different from the compiled DLL'
        Reset-TestPackage
        $manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $TestPackage 'PACKAGE_MANIFEST.json') | ConvertFrom-Json
        $manifest.PSObject.Properties.Remove('features')
        Write-PackageManifest $manifest
        Assert-PackageRejected 'missing feature declarations on a profiled DLL'
    }
    Write-Host "PACKAGE VERIFIER REGRESSION: PASS" -ForegroundColor Green
    exit 0
} catch {
    Write-Host "PACKAGE VERIFIER REGRESSION: FAIL - $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    if (Test-Path -LiteralPath $TestPackage) {
        Assert-TestWorkspacePath $TestPackage
        Remove-Item -LiteralPath $TestPackage -Recurse -Force -ErrorAction SilentlyContinue
    }
}
