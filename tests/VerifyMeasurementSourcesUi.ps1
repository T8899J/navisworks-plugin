# Synthetic WinForms checks only; does not open Navisworks or measure a real model.
param([string]$NavisworksInstallDir = 'F:\Navisworks\Navisworks Manage 2023', [string]$PluginPath)
$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Run with Windows PowerShell 5.1 -STA.' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$env:PATH = $NavisworksInstallDir + ';' + $env:PATH
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Core
[Reflection.Assembly]::LoadFrom((Join-Path $NavisworksInstallDir 'Autodesk.Navisworks.Api.dll')) | Out-Null
if (-not $PluginPath) { $PluginPath = Join-Path $repoRoot 'bin\Release\傑出品NavisworksPlugin.dll' }
$plugin = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($PluginPath))
Add-Type -TypeDefinition @'
using System;
using System.Linq.Expressions;
public static class MeasurementUiProbe {
    public static object BatchSource, ManualSource;
    public static object SingleReading;
    public static int SingleCalls;
    public static bool SingleFails;
    public static int SelectionCount = 2, Captures;
    public static bool Current = true;
    public static object Capture(object kind) {
        Captures++;
        return kind.ToString() == "ManualSelection" ? ManualSource : BatchSource;
    }
    public static Delegate CaptureDelegate(Type delegateType, Type kindType, Type sourceType) {
        var kind = Expression.Parameter(kindType, "kind");
        var call = Expression.Call(typeof(MeasurementUiProbe).GetMethod("Capture"), Expression.Convert(kind, typeof(object)));
        return Expression.Lambda(delegateType, Expression.Convert(call, sourceType), kind).Compile();
    }
    public static Delegate CurrentDelegate(Type delegateType, Type stampType) {
        return Expression.Lambda(delegateType, Expression.Field(null, typeof(MeasurementUiProbe).GetField("Current")),
            Expression.Parameter(stampType, "stamp")).Compile();
    }
    public static int GetCount() { return SelectionCount; }
    public static void OpenSearch() { }
    public static object ReadSingle() {
        SingleCalls++;
        if (SingleFails) throw new InvalidOperationException("Synthetic geometry failure");
        return SingleReading;
    }
    public static Delegate SingleDelegate(Type delegateType, Type readingType) {
        var call = Expression.Call(typeof(MeasurementUiProbe).GetMethod("ReadSingle"));
        return Expression.Lambda(delegateType, Expression.Convert(call, readingType)).Compile();
    }
}
'@ -ReferencedAssemblies System.Core
$prefix = 'JiePinPai.Navisworks.TrayMeasurement.'
$formType = $plugin.GetType($prefix + 'BatchMeasurementForm', $true)
$sourceType = $plugin.GetType($prefix + 'MeasurementSource', $true)
$stampType = $plugin.GetType($prefix + 'MeasurementSourceStamp', $true)
$targetType = $plugin.GetType($prefix + 'MeasurementTarget', $true)
$kindType = $plugin.GetType($prefix + 'MeasurementSourceKind', $true)
$manual = [Enum]::Parse($kindType, 'ManualSelection')
$batch = [Enum]::Parse($kindType, 'SearchResults')
function Make-Source($kind, [int]$count) {
    $source = [Activator]::CreateInstance($sourceType, $true)
    $stamp = [Activator]::CreateInstance($stampType, @($kind, [long]0, [long]0))
    $sourceType.GetField('Stamp').SetValue($source, $stamp)
    $targets = $sourceType.GetField('Targets').GetValue($source)
    for ($i = 1; $i -le $count; $i++) {
        $target = [Activator]::CreateInstance($targetType, $true)
        $targetType.GetField('Number').SetValue($target, $i)
        $targetType.GetField('Name').SetValue($target, "Tray-$i")
        $targets.Add($target)
    }
    return $source
}
[MeasurementUiProbe]::BatchSource = Make-Source $batch 3
[MeasurementUiProbe]::ManualSource = Make-Source $manual 2
$ctor = $formType.GetConstructors()[0]
$parameters = $ctor.GetParameters()
$capture = [MeasurementUiProbe]::CaptureDelegate($parameters[1].ParameterType, $kindType, $sourceType)
$current = [MeasurementUiProbe]::CurrentDelegate($parameters[2].ParameterType, $stampType)
$countDelegate = [Delegate]::CreateDelegate([Func[int]], [MeasurementUiProbe].GetMethod('GetCount'))
$openDelegate = [Delegate]::CreateDelegate([Action], [MeasurementUiProbe].GetMethod('OpenSearch'))
$form = $ctor.Invoke(@($null, $capture, $current, $countDelegate, $openDelegate))
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$checks = 0
function Field([string]$name) { return ,$formType.GetField($name, $flags).GetValue($form) }
function Invoke-Form([string]$name, [object[]]$arguments = @()) { return $formType.GetMethod($name, $flags).Invoke($form, $arguments) }
function Assert-Ui([bool]$condition, [string]$message) { if (-not $condition) { throw $message }; $script:checks++ }
try {
    Assert-Ui (-not (Field '_measure').Enabled) 'A measurement source must be chosen first.'
    Assert-Ui ([MeasurementUiProbe]::Captures -eq 0) 'Opening must not silently choose a source.'
    Invoke-Form 'ChooseMode' @($manual)
    Assert-Ui ((Field '_manualMode').Active -and -not (Field '_batchMode').Active) 'Manual mode must be visibly selected.'
    Assert-Ui ((Field '_measure').Enabled -and (Field '_measure').Text -eq '测量当前选择') 'Manual mode must offer one-step measurement.'
    Assert-Ui ([MeasurementUiProbe]::Captures -eq 0) 'Selection must be captured only on measure.'
    Assert-Ui (-not (Field '_grid').Columns[1].Visible) 'Manual mode must hide the irrelevant search-number column.'
    Invoke-Form 'StartBatch'
    Assert-Ui ([MeasurementUiProbe]::Captures -eq 1) 'Measure must capture current selection.'
    Assert-Ui ([object]::ReferenceEquals((Field '_source'), [MeasurementUiProbe]::ManualSource)) 'Manual results must use manual targets.'
    Assert-Ui (-not (Field '_batchMode').Enabled -and (Field '_measure').Text -eq '停止测量') 'Running must allow stop and prevent source mixing.'
    Invoke-Form 'StopBatch'
    [MeasurementUiProbe]::SelectionCount = 0
    Invoke-Form 'RefreshManualSelection'
    Invoke-Form 'InvalidateSearchSource'
    Assert-Ui ([object]::ReferenceEquals((Field '_source'), [MeasurementUiProbe]::ManualSource)) 'New selection and search edits must preserve a manual snapshot.'
    Assert-Ui (-not (Field '_measure').Enabled -and (Field '_export').Enabled) 'An empty new selection must not discard exportable results.'
    [MeasurementUiProbe]::SelectionCount = 1
    [MeasurementUiProbe]::ManualSource = Make-Source $manual 1
    Invoke-Form 'RefreshManualSelection'
    Invoke-Form 'StartBatch'
    Assert-Ui ((Field '_source').Targets.Count -eq 1) 'Next measurement must recapture the new selection.'
    Invoke-Form 'StopBatch'
    Invoke-Form 'ChooseMode' @($batch)
    Assert-Ui ([object]::ReferenceEquals((Field '_source'), [MeasurementUiProbe]::BatchSource)) 'Switching to batch must replace the manual list.'
    Assert-Ui ((Field '_grid').Columns[1].Visible -and (Field '_batchMode').Active) 'Batch mode must restore its search column and active state.'
    Invoke-Form 'InvalidateSearchSource'
    Assert-Ui ($null -eq (Field '_source') -and -not (Field '_measure').Enabled) 'Search edits must expire batch results.'
    Invoke-Form 'ChooseMode' @($manual)
    Invoke-Form 'StartBatch'
    Invoke-Form 'StopBatch'
    [MeasurementUiProbe]::Current = $false
    Invoke-Form 'EnsureCurrent' | Out-Null
    Assert-Ui ($null -eq (Field '_source') -and -not (Field '_export').Enabled) 'A changed model must invalidate either source.'
    Write-Output "MEASUREMENT SOURCE UI: PASS ($checks checks; synthetic controls, no Navisworks GUI/model acceptance)"
} finally { $form.Dispose() }

# The compact window is an independent single-item tool, with no automatic measurement or batch UI.
$simpleType = $plugin.GetType($prefix + 'SingleMeasurementForm', $true)
$readingType = $plugin.GetType($prefix + 'SingleMeasurementReading', $true)
$simpleCtor = $simpleType.GetConstructors([Reflection.BindingFlags]'Instance,NonPublic')[0]
$singleDelegate = [MeasurementUiProbe]::SingleDelegate($simpleCtor.GetParameters()[0].ParameterType, $readingType)
$simple = $simpleCtor.Invoke(@($singleDelegate))
$singleHint = $simpleType.GetMethod('SelectionHint', [Reflection.BindingFlags]'Static,NonPublic')
$checks = 0
function Simple-Field([string]$name) { return ,$simpleType.GetField($name, $flags).GetValue($simple) }
function Measure-Single { $simpleType.GetMethod('RunMeasurement', $flags).Invoke($simple, @()) | Out-Null }
try {
    Assert-Ui ($singleHint.Invoke($null, @([int]0)) -eq '请先选中一段直桥架') 'Empty selection must prompt without measuring.'
    Assert-Ui ($null -eq $singleHint.Invoke($null, @([int]1))) 'Exactly one selected item must be accepted.'
    Assert-Ui ($singleHint.Invoke($null, @([int]2)) -eq '一次只能测量一个构件') 'Multiple selected items must be rejected.'
    Assert-Ui ([MeasurementUiProbe]::SingleCalls -eq 0) 'Opening the small window must not auto-measure.'
    $buttons = @($simple.Controls[0].Controls | Where-Object { $_ -is [Windows.Forms.Button] })
    Assert-Ui ($buttons.Count -eq 1 -and $buttons[0].Text -eq '测量选中构件') 'The small window must keep one measurement button.'
    Assert-Ui ((Simple-Field '_length').Font.Size -ge 30) 'The length must retain the large original display.'
    $reading = [Activator]::CreateInstance($readingType, $true)
    $readingType.GetField('ItemName').SetValue($reading, 'Tray 1')
    $readingType.GetField('Metres').SetValue($reading, [double]1.237)
    [MeasurementUiProbe]::SingleReading = $reading
    Measure-Single
    Assert-Ui ((Simple-Field '_length').Text -eq (([double]1.237).ToString('0.###') + ' m')) 'Display must use the measured length in metres.'
    Assert-Ui ((Simple-Field '_millimetres').Text -eq '1237 mm') 'The original millimetre conversion must remain visible.'
    Assert-Ui ((Simple-Field '_itemName').Text -eq 'Tray 1') 'The displayed name must identify the measured item.'
    $invalid = [Activator]::CreateInstance($readingType, $true)
    $readingType.GetField('Hint').SetValue($invalid, '一次只能测量一个构件')
    [MeasurementUiProbe]::SingleReading = $invalid
    Measure-Single
    Assert-Ui ((Simple-Field '_length').Text -eq '—' -and (Simple-Field '_millimetres').Text -eq '') 'Rejected selections must clear the old length.'
    Assert-Ui ((Simple-Field '_hint').Text -eq '一次只能测量一个构件') 'The input hint must be shown directly in the small window.'
    [MeasurementUiProbe]::SingleFails = $true
    Measure-Single
    Assert-Ui ((Simple-Field '_measure').Enabled -and (Simple-Field '_hint').Text -eq '未能测量，请重新选择一段直桥架') 'Geometry failure must leave the button usable.'
    [MeasurementUiProbe]::SingleFails = $false
    [MeasurementUiProbe]::SingleReading = $reading
    Measure-Single
    Assert-Ui ((Simple-Field '_length').Text -eq (([double]1.237).ToString('0.###') + ' m')) 'The next explicit click must recover after a failure.'
    $entryType = $plugin.GetType($prefix + 'SingleMeasurementPlugin', $true)
    $entry = @($entryType.GetCustomAttributesData() | Where-Object { $_.AttributeType.FullName -eq 'Autodesk.Navisworks.Api.Plugins.PluginAttribute' })[0]
    Assert-Ui ($entry.ConstructorArguments[0].Value -eq 'JiePinPai_QuickTrayMeasurement') 'A unique Add-Ins entry must be registered for convenient access.'
    Write-Output "SINGLE MEASUREMENT UI: PASS ($checks checks; synthetic controls, no Navisworks GUI/model acceptance)"
} finally { $simple.Dispose() }
