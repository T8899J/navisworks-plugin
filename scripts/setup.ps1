param(
    [ValidateSet('Install', 'Verify', 'Uninstall')][string]$Action = 'Install',
    [switch]$Standalone,
    [string]$NavisworksPath,
    [switch]$SmokeTest,
    [string]$PreviewPath
)
$ErrorActionPreference = 'Stop'
$SetupDir = $PSScriptRoot
. (Join-Path $PSScriptRoot 'host-paths.ps1')
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()

function ConvertTo-PowerShellLiteral([string]$Value) { return "'" + $Value.Replace("'", "''") + "'" }

function Invoke-SetupAction([string]$SelectedPath, [bool]$UnsignedConsent) {
    $scriptName = @{ Install = 'install.ps1'; Verify = 'verify.ps1'; Uninstall = 'uninstall.ps1' }[$Action]
    $logDir = Join-Path $env:TEMP ('JiePinPai-Setup-' + [Guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory($logDir)
    $logFile = Join-Path $logDir 'operation.log'
    $command = '& ' + (ConvertTo-PowerShellLiteral (Join-Path $SetupDir $scriptName)) + ' -NavisworksPath ' + (ConvertTo-PowerShellLiteral $SelectedPath)
    if ($Standalone) { $command += ' -Standalone' }
    if ($Action -ne 'Verify') { $command += ' -NonInteractive' }
    if ($Action -eq 'Install' -and $UnsignedConsent) { $command += ' -AllowUnsigned' }
    # Encode a script containing SINGLE-QUOTED literals: Chinese, spaces, $, &, and apostrophes remain data.
    $command = 'try { ' + $command + ' *> ' + (ConvertTo-PowerShellLiteral $logFile) + '; exit $LASTEXITCODE } catch { $_ | Out-File -LiteralPath ' + (ConvertTo-PowerShellLiteral $logFile) + ' -Encoding utf8; exit 1 }'
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    $start = @{
        FilePath = (Join-Path $PSHOME 'powershell.exe')
        ArgumentList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $encoded)
        WindowStyle = 'Hidden'
        PassThru = $true
    }
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if ($Action -ne 'Verify' -and -not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { $start.Verb = 'RunAs' }
    $process = Start-Process @start
    try {
        while (-not $process.WaitForExit(100)) { [Windows.Forms.Application]::DoEvents() }
        $output = if (Test-Path -LiteralPath $logFile) { [IO.File]::ReadAllText($logFile) } else { '操作未完成；请检查是否取消了管理员授权。' }
        return [PSCustomObject]@{ ExitCode = $process.ExitCode; Output = $output; LogFile = $logFile }
    } finally { $process.Dispose() }
}

$labels = @{ Install = '安装 / 更新'; Verify = '验证安装'; Uninstall = '卸载插件' }
$form = [Windows.Forms.Form]::new()
$form.Text = '傑出品 · Navisworks Manage 2023 · ' + $labels[$Action]
$form.ClientSize = [Drawing.Size]::new(720, 450)
$form.MinimumSize = [Drawing.Size]::new(736, 489)
$form.StartPosition = 'CenterScreen'
$form.Font = [Drawing.Font]::new('Microsoft YaHei UI', 10)
$form.AutoScaleMode = 'Dpi'
$intro = [Windows.Forms.Label]::new()
$intro.Text = "选择这台电脑上的 Navisworks Manage 2023。软件可以安装在任意盘符和目录。`r`n找不到时，点击【浏览】，选择 Roamer.exe 或 Navisworks 桌面快捷方式。"
$intro.SetBounds(20, 18, 680, 54)
$intro.Anchor = 'Top,Left,Right'
$paths = [Windows.Forms.ComboBox]::new()
$paths.SetBounds(20, 85, 576, 30)
$paths.Anchor = 'Top,Left,Right'
$browse = [Windows.Forms.Button]::new()
$browse.Text = '浏览…'
$browse.SetBounds(606, 83, 94, 32)
$browse.Anchor = 'Top,Right'
$consent = [Windows.Forms.CheckBox]::new()
$consent.Text = '我信任此插件来源，同意安装未签名的插件包'
$consent.SetBounds(20, 127, 680, 28)
$consent.Visible = $Standalone -and $Action -eq 'Install'
$run = [Windows.Forms.Button]::new()
$run.Text = $labels[$Action]
$run.SetBounds(20, 169, 145, 36)
$status = [Windows.Forms.Label]::new()
$status.Text = '正在查找本机安装目录…'
$status.SetBounds(180, 173, 520, 30)
$status.Anchor = 'Top,Left,Right'
$log = [Windows.Forms.TextBox]::new()
$log.Multiline = $true
$log.ReadOnly = $true
$log.ScrollBars = 'Vertical'
$log.SetBounds(20, 220, 680, 210)
$log.Anchor = 'Top,Bottom,Left,Right'
$form.Controls.AddRange(@($intro, $paths, $browse, $consent, $run, $status, $log))
$script:busy = $false
$script:setupExitCode = 1
$form.Add_FormClosing({ param($sender, $eventArgs) if ($script:busy) { $eventArgs.Cancel = $true } })
$browse.Add_Click({
    $dialog = [Windows.Forms.OpenFileDialog]::new()
    $dialog.Title = '选择 Navisworks Manage 2023 的 Roamer.exe 或快捷方式'
    $dialog.Filter = 'Navisworks 程序或快捷方式|Roamer.exe;*.lnk|所有文件|*.*'
    $dialog.DereferenceLinks = $true
    try { if ($dialog.ShowDialog($form) -eq 'OK') { $paths.Text = ConvertTo-NavisworksDirectory $dialog.FileName } }
    finally { $dialog.Dispose() }
})
$run.Add_Click({
    try {
        $selected = Resolve-Navisworks2023Path $paths.Text
        if ($Standalone -and $Action -eq 'Install') {
            $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'PACKAGE_MANIFEST.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($manifest.signing.mode -eq 'unsigned' -and -not $consent.Checked) { throw '请确认信任插件来源，并勾选上方复选框。' }
        }
        if ($Action -eq 'Uninstall' -and [Windows.Forms.MessageBox]::Show($form, "从以下目录移除傑出品插件？`r`n$selected", '确认卸载', 'YesNo', 'Question') -ne 'Yes') { return }
        $script:busy = $true
        $run.Enabled = $false; $browse.Enabled = $false; $paths.Enabled = $false; $consent.Enabled = $false
        $status.Text = '处理中，请在系统提示时允许管理员授权…'
        $log.Text = $selected
        $result = Invoke-SetupAction $selected $consent.Checked
        $log.Text = $result.Output + "`r`n日志：" + $result.LogFile
        if ($result.ExitCode -ne 0) { throw '操作失败，详情见下方日志；关闭目标 Navisworks 后可重试。' }
        $script:setupExitCode = 0
        $status.Text = if ($Action -eq 'Install') { '文件安装验证通过。请启动 Navisworks 检查功能。' } else { '操作完成。' }
        try { Save-Navisworks2023Path $selected } catch { $log.AppendText("`r`n路径未能记住，下次请重新选择：$_") }
    } catch {
        $script:setupExitCode = 1
        $status.Text = '未完成'
        $log.AppendText("`r`n$($_.Exception.Message)")
    } finally {
        $script:busy = $false
        $run.Enabled = $true; $browse.Enabled = $true; $paths.Enabled = $true; $consent.Enabled = $true
    }
})
$form.Add_Shown({
    try {
        foreach ($path in @(Find-Navisworks2023Paths)) { [void]$paths.Items.Add($path) }
        if ($NavisworksPath) { $paths.Text = ConvertTo-NavisworksDirectory $NavisworksPath }
        elseif ($paths.Items.Count -eq 1) { $paths.SelectedIndex = 0 }
        $status.Text = if ($paths.Items.Count -gt 1) { '检测到多个安装，请选择要操作的目录。' } else { '请确认目录，然后点击左侧按钮。' }
    } catch { $log.Text = $_.Exception.Message }
    if ($SmokeTest) {
        if (-not $form.Visible -or -not $run.Visible -or $paths.Width -lt 200) { throw 'Setup window layout failed' }
        if ($PreviewPath) {
            $bitmap = [Drawing.Bitmap]::new($form.Width, $form.Height)
            try {
                $form.DrawToBitmap($bitmap, [Drawing.Rectangle]::new(0, 0, $form.Width, $form.Height))
                $bitmap.Save([IO.Path]::GetFullPath($PreviewPath), [Drawing.Imaging.ImageFormat]::Png)
            } finally { $bitmap.Dispose() }
        }
        Write-Host 'SETUP WINDOW: PASS (no install performed)'
        $script:setupExitCode = 0
        $form.Close()
    }
})
try { [void]$form.ShowDialog() } finally { $form.Dispose() }
exit $script:setupExitCode
