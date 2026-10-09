# Verify the compiled dispatch and dialog instructions without opening a modal UI.
param([string]$PluginPath, [string]$NavisworksInstallDir, [switch]$ExpectBatchEnabled)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $root 'scripts\host-paths.ps1')
$NavisworksInstallDir = Resolve-Navisworks2023Path $NavisworksInstallDir
if (-not $PluginPath) { $PluginPath = Join-Path $root 'release\傑出品NavisworksPlugin.dll' }
$env:PATH = $NavisworksInstallDir + ';' + $env:PATH
Add-Type -AssemblyName System.Windows.Forms
[void][Reflection.Assembly]::LoadFrom((Join-Path $NavisworksInstallDir 'Autodesk.Navisworks.Api.dll'))
$plugin = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($PluginPath))
$opcodes = @{}
foreach ($field in [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')) {
    $opcode = $field.GetValue($null)
    $opcodes[([int]$opcode.Value -band 65535)] = $opcode
}
function Read-Instructions($method) {
    $body = $method.GetMethodBody()
    if (-not $body) { return }
    $bytes = $body.GetILAsByteArray()
    $pos = 0
    while ($pos -lt $bytes.Length) {
        $code = [int]$bytes[$pos++]
        if ($code -eq 254) { $code = 65024 + [int]$bytes[$pos++] }
        $opcode = $opcodes[$code]
        if (-not $opcode) { throw "Unknown IL opcode $code at $pos in $($method.Name)" }
        $value = $null
        $length = switch ($opcode.OperandType.ToString()) {
            'InlineNone' { 0 }
            { $_ -in @('ShortInlineBrTarget','ShortInlineI','ShortInlineVar') } { 1 }
            'InlineVar' { 2 }
            { $_ -in @('InlineI8','InlineR') } { 8 }
            'InlineSwitch' { 4 + 4 * [BitConverter]::ToInt32($bytes,$pos) }
            default { 4 }
        }
        if ($opcode.OperandType.ToString() -eq 'InlineString') { $value = $method.Module.ResolveString([BitConverter]::ToInt32($bytes,$pos)) }
        if ($opcode.OperandType.ToString() -eq 'InlineMethod') { $value = $method.Module.ResolveMethod([BitConverter]::ToInt32($bytes,$pos)) }
        [PSCustomObject]@{ Op = $opcode.Name; Value = $value }
        $pos += $length
    }
}
$checks = 0
function Assert([bool]$ok,[string]$message) { if (-not $ok) { throw $message }; $script:checks++ }
$metadata = @{}
foreach ($attribute in $plugin.GetCustomAttributesData()) {
    if ($attribute.AttributeType.Name -eq 'AssemblyMetadataAttribute') { $metadata[$attribute.ConstructorArguments[0].Value] = $attribute.ConstructorArguments[1].Value }
}
$expected = if ($ExpectBatchEnabled) { 'enabled' } else { 'disabled' }
Assert ($metadata['BatchTrayMeasurement'] -eq $expected) 'DLL profile mismatch'
$dialog = $plugin.GetType('JiePinPai.Navisworks.SearchDialog',$true)
$method = $dialog.GetMethod('OpenTrayMeasurement',[Reflection.BindingFlags]'Instance,NonPublic')
$instructions = @(Read-Instructions $method)
$calls = @($instructions | Where-Object { $_.Value -is [Reflection.MethodBase] })
if ($ExpectBatchEnabled) {
    Assert (@($calls | Where-Object { $_.Value.DeclaringType.Name -eq 'BatchMeasurementForm' -and $_.Op -eq 'newobj' }).Count -eq 1) 'Normal build lost batch entry'
} else {
    Assert (@($instructions | Where-Object { $_.Op -eq 'ldstr' -and $_.Value -eq '当前功能暂不可用' }).Count -eq 1) 'Exact unavailable message missing'
    Assert (@($instructions | Where-Object { $_.Op -eq 'ldstr' -and $_.Value -eq '批量测量' }).Count -eq 1) 'Modal title missing'
    Assert ($calls.Count -eq 1 -and $calls[0].Value.DeclaringType.FullName -eq 'System.Windows.Forms.MessageBox' -and $calls[0].Value.Name -eq 'Show') 'Disabled handler calls more than the message box'
    Assert (@($instructions | Where-Object { $_.Op -eq 'newobj' }).Count -eq 0) 'Disabled handler creates a batch window'
    Assert ($instructions[-1].Op -eq 'ret') 'Disabled handler does not return'
}
Assert ($metadata['QuickTrayMeasurement'] -eq 'enabled') 'Quick measurement disabled'
$entry = $plugin.GetType('JiePinPai.Navisworks.TrayMeasurement.SingleMeasurementPlugin',$true)
$attributes = @($entry.GetCustomAttributesData())
$registration = @($attributes | Where-Object { $_.AttributeType.Name -eq 'PluginAttribute' })[0]
Assert ($registration.ConstructorArguments[0].Value -eq 'JiePinPai_QuickTrayMeasurement') 'Quick entry registration missing'
Assert (@($attributes | Where-Object { $_.AttributeType.Name -eq 'AddInPluginAttribute' }).Count -eq 1) 'Add-Ins registration missing'
$entryCalls = @(Read-Instructions ($entry.GetMethod('Execute')))
Assert (@($entryCalls | Where-Object { $_.Value -is [Reflection.MethodBase] -and $_.Value.DeclaringType.Name -eq 'SingleMeasurementWindow' -and $_.Value.Name -eq 'Open' }).Count -eq 1) 'Quick entry does not open the independent window'
$menuCalls = @($dialog.GetNestedTypes([Reflection.BindingFlags]'Public,NonPublic') | ForEach-Object {
    $_.GetMethods([Reflection.BindingFlags]'DeclaredOnly,Instance,Static,Public,NonPublic') | Where-Object { $_.Name -like '*ToggleMeasurementPicker*' } | ForEach-Object { Read-Instructions $_ }
})
Assert (@($menuCalls | Where-Object { $_.Value -is [Reflection.MethodBase] -and $_.Value.Name -eq 'OpenTrayMeasurement' }).Count -eq 1) 'Batch menu does not dispatch to the guarded handler'
Assert (@($menuCalls | Where-Object { $_.Value -is [Reflection.MethodBase] -and $_.Value.DeclaringType.Name -eq 'SingleMeasurementWindow' -and $_.Value.Name -eq 'Open' }).Count -eq 1) 'Quick menu lost its independent dispatch'
Write-Output "PACKAGED MEASUREMENT DISPATCH: PASS ($checks checks; compiled IL and registration; batch $expected)"