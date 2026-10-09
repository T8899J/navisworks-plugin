# Run with Windows PowerShell 5.1 -STA after a Release build.
# Exercises the production WinForms dialog with synthetic results and no document.
# Does not validate Navisworks model selection, selection sets, or hiding.
param(
    [Parameter(Mandatory = $true)][string]$NavisworksInstallDir,
    [string]$ArtifactsDirectory
)

$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    throw 'Run this script with powershell.exe -STA.'
}
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ArtifactsDirectory) {
    $ArtifactsDirectory = Join-Path $repoRoot '.superpowers\duplicate-ui-smoke'
}
New-Item -ItemType Directory -Path $ArtifactsDirectory -Force | Out-Null
$env:PATH = $NavisworksInstallDir + ';' + $env:PATH
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
[Reflection.Assembly]::LoadFrom((Join-Path $NavisworksInstallDir 'Autodesk.Navisworks.Api.dll')) | Out-Null
$plugin = [Reflection.Assembly]::LoadFrom((Join-Path $repoRoot 'bin\Release\傑出品NavisworksPlugin.dll'))
$dialogType = $plugin.GetType('JiePinPai.Navisworks.SearchDialog', $true)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$script:checks = 0

function Field([string]$name) {
    return ,$dialogType.GetField($name, $flags).GetValue($dialog)
}
function Invoke-Dialog([string]$name, [object[]]$arguments = @()) {
    return ,$dialogType.GetMethod($name, $flags).Invoke($dialog, $arguments)
}
function Assert-Ui([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
    $script:checks++
}
function Click-Header([int]$column, [Windows.Forms.MouseButtons]$button = 'Left') {
    $mouse = [Windows.Forms.MouseEventArgs]::new($button, 1, 0, 0, 0)
    $event = [Windows.Forms.DataGridViewCellMouseEventArgs]::new($column, -1, 0, 0, $mouse)
    Invoke-Dialog 'ResultsGrid_ColumnHeaderMouseClick' @($grid, $event) | Out-Null
}
function Set-ResultFilter([JiePinPai.Navisworks.SearchResultFilter]$value) {
    Invoke-Dialog 'SetActiveResultFilter' @($value) | Out-Null
    [Windows.Forms.Application]::DoEvents()
}
function Header-State {
    $ids = Invoke-Dialog 'GetCurrentFilteredResults'
    $getIds = $dialogType.GetMethod('GetDuplicateResultIds', [Reflection.BindingFlags]'Static,NonPublic')
    $eligible = $getIds.Invoke($null, [object[]](, $ids))
    $getState = $dialogType.GetMethod('GetHeaderCheckBoxState', [Reflection.BindingFlags]'Static,NonPublic')
    return $getState.Invoke($null, [object[]]@($eligible, (Field '_includedDuplicateResultIndices'), (-not (Field '_lastHideExecuted'))))
}
function Capture([string]$name, [Drawing.Size]$size) {
    $dialog.Size = $size
    [Windows.Forms.Application]::DoEvents()
    $panel = $toggle.Parent
    foreach ($control in $panel.Controls) {
        Assert-Ui ($panel.ClientRectangle.Contains($control.Bounds)) ('Action clipped: ' + $control.Text)
    }
    Assert-Ui ($panel.Bottom -le $grid.Top) 'Action row overlaps grid.'
    $bitmap = [Drawing.Bitmap]::new($dialog.Width, $dialog.Height)
    try {
        $dialog.DrawToBitmap($bitmap, [Drawing.Rectangle]::new([Drawing.Point]::Empty, $dialog.Size))
        $bitmap.Save((Join-Path $ArtifactsDirectory $name), [Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }
}

$dialog = [Activator]::CreateInstance($dialogType, [object[]]@($null, $null, [IntPtr]::Zero))
try {
    $dialog.ShowInTaskbar = $false
    $dialog.Opacity = 0
    $dialog.Show()
    (Field '_tabControl').SelectedTab = Field '_tabResults'
    $grid = Field '_resultsGrid'
    $toggle = Field '_btnToggleDuplicateInclusion'
    Assert-Ui (-not $toggle.Enabled) 'Empty results must disable bulk inclusion.'
    Assert-Ui ((Header-State) -eq 'UncheckedDisabled') 'Empty header must be disabled.'

    $results = [Collections.Generic.List[JiePinPai.Navisworks.SearchResult]]::new()
    for ($i = 0; $i -lt 60; $i++) {
        $condition = [JiePinPai.Navisworks.SearchCondition]::new()
        $condition.CategoryDisplay = 'Element'
        $condition.PropertyDisplay = 'Tag'
        $condition.Test = 'equals'
        $condition.Value = 'M12-' + ($i + 1).ToString('000')
        $result = [JiePinPai.Navisworks.SearchResult]::new()
        $result.Condition = [JiePinPai.Navisworks.SearchConditionSnapshot]::From($i, $condition)
        $result.Status = 'Duplicate'
        $result.MatchCount = 2
        $result.StatusMessage = '匹配到多个对象，请确认是否计入。'
        if ($i -eq 0) { $result.Status = 'Found'; $result.MatchCount = 1; $result.StatusMessage = '唯一匹配' }
        if ($i -eq 1) { $result.Status = 'NotFound'; $result.MatchCount = 0; $result.StatusMessage = '未找到' }
        if ($i -eq 2) { $result.Status = 'ConditionInvalid'; $result.MatchCount = 0; $result.StatusMessage = '属性不存在' }
        $results.Add($result)
    }
    Invoke-Dialog 'FinalizeSearchResults' @($results, 0, $false, 'TS-M12') | Out-Null
    $dialogType.GetField('_currentModelPrefix', $flags).SetValue($dialog, 'TS-M12')
    Set-ResultFilter 'All'
    Assert-Ui ($toggle.Enabled) 'Duplicate results must enable bulk inclusion.'
    Assert-Ui ((Header-State) -eq 'UncheckedNormal') 'Found rows must not make duplicate header mixed.'
    $grid.Rows[0].Cells[0].Value = $true
    $grid.Rows[4].Cells[0].Value = $true
    $exportIds = @((Field '_checkedExportResultIndices') | ForEach-Object { $_ })
    $grid.FirstDisplayedScrollingRowIndex = 30
    $scrollIndex = $grid.FirstDisplayedScrollingRowIndex
    $toggle.PerformClick()
    Assert-Ui ((Field '_includedDuplicateResultIndices').Count -eq 57) 'Bulk select missed duplicates.'
    Assert-Ui ((Header-State) -eq 'CheckedNormal') 'All selected header is wrong.'
    Assert-Ui ($toggle.Text -eq '取消全选重复项') 'Bulk button did not offer clear all.'
    Assert-Ui ($grid.FirstDisplayedScrollingRowIndex -eq $scrollIndex) 'Bulk selection lost scroll position.'
    Assert-Ui ((Compare-Object $exportIds @((Field '_checkedExportResultIndices') | ForEach-Object { $_ })) -eq $null) 'Inclusion changed export checks.'
    Assert-Ui ($grid.Rows[0].Cells[1].ReadOnly -and $grid.Rows[0].Cells[1].Value) 'Found row must stay included and read-only.'
    Assert-Ui (-not $grid.Rows[1].Cells[1].Value -and -not $grid.Rows[2].Cells[1].Value) 'Invalid or missing row was included.'
    Assert-Ui (@($grid.Rows | Where-Object { $_.Tag.Status -eq 'Duplicate' -and -not $_.Cells[1].Value }).Count -eq 0) 'Bulk selection did not update every visible duplicate.'

    $grid.Rows[4].Cells[1].Value = $false
    Assert-Ui ((Field '_includedDuplicateResultIndices').Count -eq 56) 'Single deselect after bulk failed.'
    Assert-Ui ((Header-State) -eq 'MixedNormal') 'Partial selection must show mixed header.'
    Assert-Ui ($toggle.Text -eq '全选重复项') 'Partial selection must offer select all.'
    Click-Header 1 'Right'
    Assert-Ui ((Field '_includedDuplicateResultIndices').Count -eq 56) 'Right click changed inclusion.'
    Click-Header 1
    Assert-Ui ((Field '_includedDuplicateResultIndices').Count -eq 57) 'Mixed header click must select all.'

    Set-ResultFilter 'Duplicate'
    Assert-Ui ($grid.Rows.Count -eq 57 -and (Header-State) -eq 'CheckedNormal') 'Filter lost inclusion.'
    Click-Header 1
    Assert-Ui ((Field '_includedDuplicateResultIndices').Count -eq 0) 'Checked header click must clear duplicates.'
    Set-ResultFilter 'Found'
    Assert-Ui ((Header-State) -eq 'UncheckedDisabled') 'Header without visible duplicates must be disabled.'
    $toggle.PerformClick()
    Assert-Ui ((Field '_includedDuplicateResultIndices').Count -eq 57) 'Global button must include duplicates outside filter.'
    Click-Header 1
    Assert-Ui ((Field '_includedDuplicateResultIndices').Count -eq 57) 'Empty scope must not affect hidden duplicates.'
    Set-ResultFilter 'Problems'
    Click-Header 0
    Assert-Ui ((Field '_includedDuplicateResultIndices').Count -eq 57) 'Export header changed duplicate inclusion.'
    $toggle.PerformClick()
    Assert-Ui ((Field '_includedDuplicateResultIndices').Count -eq 0) 'Clear-all button failed.'
    $toggle.PerformClick()
    Capture 'results-all-minimum.png' $dialog.MinimumSize
    $grid.Rows[2].Cells[1].Value = $false
    Capture 'results-partial-default.png' ([Drawing.Size]::new(960, 680))
    Capture 'results-partial-minimum.png' $dialog.MinimumSize

    $dialogType.GetField('_lastHideExecuted', $flags).SetValue($dialog, $true)
    Invoke-Dialog 'RefreshResultsGrid' (, $results) | Out-Null
    Assert-Ui (-not $toggle.Enabled) 'Hidden results must disable bulk button.'
    Assert-Ui ((Header-State) -eq 'MixedDisabled') 'Locked partial header must remain mixed and disabled.'
    Assert-Ui (@($grid.Rows | Where-Object { -not $_.Cells[1].ReadOnly }).Count -eq 0) 'Hidden results must lock all inclusion cells.'
    $countBefore = (Field '_includedDuplicateResultIndices').Count
    Click-Header 1
    $toggle.PerformClick()
    Assert-Ui ((Field '_includedDuplicateResultIndices').Count -eq $countBefore) 'Locked bulk entry changed inclusion.'
    Capture 'results-locked-minimum.png' $dialog.MinimumSize

    Invoke-Dialog 'InvalidateSearchResults' @('条件已修改，请重新执行搜索。') | Out-Null
    Assert-Ui ((Field '_includedDuplicateResultIndices').Count -eq 0) 'Invalidation retained duplicate inclusion.'
    Assert-Ui ((Field '_checkedExportResultIndices').Count -eq 0) 'Invalidation retained export checks.'
    Assert-Ui ($grid.Rows.Count -eq 0 -and -not $toggle.Enabled) 'Invalidation retained actionable results.'
    Invoke-Dialog 'FinalizeSearchResults' @($results, 0, $false, 'TS-M12') | Out-Null
    Assert-Ui ($toggle.Enabled -and (Header-State) -eq 'UncheckedNormal') 'New search must unlock with duplicates excluded.'
    Write-Output ('UI PASS: ' + $script:checks + ' checks; synthetic data, no Navisworks document operations.')
    Write-Output ('Rendered previews: ' + $ArtifactsDirectory)
} catch {
    Write-Output $_.ScriptStackTrace
    throw
} finally {
    $dialog.Dispose()
}
