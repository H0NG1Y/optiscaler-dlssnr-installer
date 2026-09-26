<#
.SYNOPSIS
  对本项目构建出的 MSI 做一次「真实安装 → 校验 → 卸载 → 再校验」的回归测试。

.DESCRIPTION
  这个脚本会真正改动系统，请先看清楚它做什么：
    1. 备份现有的开始菜单 / 桌面快捷方式（同名 .lnk）与 AppCompatFlags 里对应的值；
    2. msiexec /i 静默安装（默认装到 C:\Program Files\OptiScaler DLSSNR Installer）；
    3. 校验：文件哈希（对照安装目录里的 SHA256SUMS.txt）、开始菜单/桌面快捷方式、RUNASADMIN 标记、ARP 登记；
    4. msiexec /x 静默卸载；
    5. 校验卸载是否清理干净（文件、快捷方式、注册表值都不该剩下）；
    6. 还原第 1 步备份的东西。

  需要管理员权限；脚本会自己弹 UAC 提权（同一进程会重跑一遍）。
  不会碰任何游戏目录。

.PARAMETER Msi
  MSI 路径。默认取 dist 目录中最新的 OptiScaler-DLSSNR-Installer-*-win-x64.msi。

.PARAMETER InstallDir
  自定义安装目录（默认使用 MSI 默认的 Program Files 路径）。

.PARAMETER KeepInstalled
  只安装不卸载（调试用）。

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File installer\test-msi-install.ps1
#>
[CmdletBinding()]
param(
  [string]$Msi,
  [string]$InstallDir,
  [switch]$KeepInstalled
)

$ErrorActionPreference = 'Stop'

$projRoot  = Split-Path $PSScriptRoot -Parent
$distDir   = Join-Path $projRoot 'dist'
$reportDir = Join-Path $projRoot 'build\msi'
$backupDir = Join-Path $reportDir 'shortcut-backup'
$stamp     = Get-Date -Format 'yyyyMMdd-HHmmss'
$regKey    = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers'
$productName = 'OptiScaler DLSSNR 安装助手'

function Test-Admin {
  $id = [Security.Principal.WindowsIdentity]::GetCurrent()
  return (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Write-Step([string]$t) { Write-Host "`n== $t" -ForegroundColor Cyan }
function Write-Ok([string]$t)   { Write-Host "   OK   $t" -ForegroundColor Green }
function Write-Bad([string]$t)  { Write-Host "   失败 $t" -ForegroundColor Red }
function Write-Info([string]$t) { Write-Host "   $t" }

if (-not (Test-Admin)) {
  Write-Host '需要管理员权限，正在请求提权（请在 UAC 对话框中点“是”）…' -ForegroundColor Yellow
  $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $PSCommandPath))
  if ($Msi)        { $argList += @('-Msi', ('"{0}"' -f $Msi)) }
  if ($InstallDir) { $argList += @('-InstallDir', ('"{0}"' -f $InstallDir)) }
  if ($KeepInstalled) { $argList += '-KeepInstalled' }
  try {
    $proc = Start-Process -FilePath "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" `
                          -ArgumentList $argList -Verb RunAs -Wait -PassThru
    Write-Host "提权进程退出码：$($proc.ExitCode)"
    exit $proc.ExitCode
  } catch {
    Write-Bad "提权被取消或失败：$($_.Exception.Message)"
    exit 1
  }
}

# ---------------------------------------------------------------- 找到 MSI
if (-not $Msi) {
  $cand = Get-ChildItem $distDir -Filter 'OptiScaler-DLSSNR-Installer-*-win-x64.msi' -ErrorAction SilentlyContinue |
          Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if (-not $cand) { throw "dist 目录里没有找到 MSI，请先运行 build-msi.ps1" }
  $Msi = $cand.FullName
}
if (-not (Test-Path $Msi)) { throw "找不到 MSI：$Msi" }
$msiItem = Get-Item $Msi
$msiHash = (Get-FileHash -LiteralPath $Msi -Algorithm SHA256).Hash

Write-Step '测试对象'
Write-Info $msiItem.FullName
Write-Info ("{0:N0} 字节，SHA-256 {1}" -f $msiItem.Length, $msiHash)

# 目标安装目录：默认取 MSI 默认值（Program Files64）
if (-not $InstallDir) { $InstallDir = Join-Path $env:ProgramFiles 'OptiScaler DLSSNR Installer' }
Write-Info "安装目录 $InstallDir"

$launchExe = Join-Path $InstallDir 'OptiScaler-DLSSNR-Installer.exe'
$shortcutTargets = @(
  (Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\OptiScaler DLSSNR Installer\OptiScaler DLSSNR 安装助手.lnk'),
  (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\OptiScaler DLSSNR Installer\OptiScaler DLSSNR 安装助手.lnk'),
  (Join-Path $env:PUBLIC 'Desktop\OptiScaler DLSSNR 安装助手.lnk'),
  (Join-Path ([Environment]::GetFolderPath('Desktop')) 'OptiScaler DLSSNR 安装助手.lnk')
)
# 本 MSI 是 perMachine 安装，只写“所有用户”的开始菜单与桌面（[0] / [2]）。
# [1] / [3] 是每用户路径，旧的手动部署（D:\Program Files 那份）可能留下同名快捷方式：
# 这里只备份/还原，不做“应当存在/应当消失”的断言，否则会误报。
$msiShortcuts = @($shortcutTargets[0], $shortcutTargets[2])

# ---------------------------------------------------------------- 备份
Write-Step '备份现有快捷方式与注册表值'
if (Test-Path $backupDir) { Remove-Item $backupDir -Recurse -Force }
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
$backup = [ordered]@{ shortcuts = @(); registry = @() }
foreach ($p in $shortcutTargets) {
  if (Test-Path $p) {
    $name = '{0}-{1}' -f $stamp, ($p -replace '[\\/:*?"<>|]', '_')
    $dest = Join-Path $backupDir $name
    Copy-Item -LiteralPath $p -Destination $dest -Force
    $backup.shortcuts += [pscustomobject]@{ original = $p; backup = $dest }
    Write-Info "已备份 $p"
  }
}
$existingValues = @{}
if (Test-Path $regKey) {
  $item = Get-ItemProperty -Path $regKey
  foreach ($n in $item.PSObject.Properties.Name) {
    if ($n -notlike 'PS*') { $existingValues[$n] = $item.$n }
  }
}
$backup.registry = $existingValues
$backupPath = Join-Path $backupDir 'backup.json'
$backup | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $backupPath -Encoding UTF8
Write-Ok "备份记录 $backupPath（原有 AppCompatFlags 值 $($existingValues.Count) 项）"

$report = [ordered]@{
  startedAt = (Get-Date).ToString('s')
  msi = $msiItem.FullName
  msiSha256 = $msiHash
  installDir = $InstallDir
  steps = [ordered]@{}
}

function Invoke-Msiexec([string]$argLine, [string]$logName) {
  $log = Join-Path $reportDir "$stamp-$logName.log"
  $proc = Start-Process -FilePath 'msiexec.exe' -ArgumentList ($argLine + " /l*v `"$log`"") -Wait -PassThru
  return [pscustomobject]@{ exitCode = $proc.ExitCode; log = $log }
}

# ---------------------------------------------------------------- 安装
Write-Step '安装（msiexec /i /qn）'
$installArgs = '/i "{0}" /qn /norestart' -f $Msi
if ($InstallDir -ne (Join-Path $env:ProgramFiles 'OptiScaler DLSSNR Installer')) {
  $installArgs += ' INSTALLFOLDER="{0}"' -f $InstallDir
}
$r = Invoke-Msiexec $installArgs 'install'
Write-Info "退出码 $($r.exitCode)，日志 $($r.log)"
if ($r.exitCode -ne 0) { throw "安装失败（exit $($r.exitCode)），请看日志 $($r.log)" }
$report.steps.install = @{ exitCode = $r.exitCode; log = $r.log }

# ---------------------------------------------------------------- 校验安装结果
Write-Step '校验安装结果'
$problems = New-Object System.Collections.ArrayList

$sumsPath = Join-Path $InstallDir 'SHA256SUMS.txt'
if (-not (Test-Path $sumsPath)) { [void]$problems.Add('安装目录缺少 SHA256SUMS.txt') }
else {
  $bad = 0
  foreach ($line in (Get-Content -LiteralPath $sumsPath -Encoding UTF8)) {
    if ($line -match '^([0-9a-fA-F]{64})\s+(.+)$') {
      $want = $Matches[1].ToUpper()
      $rel  = $Matches[2].Trim() -replace '/', '\'
      $file = Join-Path $InstallDir $rel
      if (-not (Test-Path $file)) { [void]$problems.Add("缺少文件 $rel"); $bad++; continue }
      $got = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
      if ($got -ne $want) { [void]$problems.Add("哈希不符 $rel`n    期望 $want`n    实际 $got"); $bad++ }
      else { Write-Ok "哈希一致 $rel" }
    }
  }
  if ($bad -eq 0) { Write-Ok "安装目录中所有随包文件哈希一致" }
}

$wsh = New-Object -ComObject WScript.Shell
foreach ($p in $msiShortcuts) {
  if (Test-Path $p) {
    $t = $wsh.CreateShortcut($p).TargetPath
    Write-Ok "快捷方式存在 $p`n        -> $t"
    if ($t -ne $launchExe) { [void]$problems.Add("快捷方式目标不对：$p`n    期望 $launchExe`n    实际 $t（可能是旧部署留下的同名快捷方式，未被本 MSI 改写）") }
  } else { [void]$problems.Add("缺少快捷方式 $p") }
}

if (Test-Path $regKey) {
  $v = (Get-ItemProperty -Path $regKey -Name $launchExe -ErrorAction SilentlyContinue).$launchExe
  if ($v) { Write-Ok "RUNASADMIN 标记 $launchExe = $v" } else { [void]$problems.Add("缺少 RUNASADMIN 标记：$launchExe") }
} else { [void]$problems.Add("注册表键不存在：$regKey") }

$arp = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue |
       Where-Object { $_.DisplayName -eq $productName }
if ($arp) { Write-Ok "ARP 登记：$($arp.DisplayName) $($arp.DisplayVersion)（$($arp.PSChildName)）" }
else { [void]$problems.Add('ARP 中没有登记（“设置 → 应用”里看不到）') }

$report.steps.verifyInstall = @{ problems = @($problems) }
if ($problems.Count) {
  Write-Host "`n安装校验发现 $($problems.Count) 个问题：" -ForegroundColor Red
  $problems | ForEach-Object { Write-Bad $_ }
} else {
  Write-Ok '安装校验全部通过'
}

# ---------------------------------------------------------------- 卸载
if ($KeepInstalled) {
  Write-Host "`n已按 -KeepInstalled 保留安装，未执行卸载。" -ForegroundColor Yellow
} else {
  Write-Step '卸载（msiexec /x /qn）'
  $r2 = Invoke-Msiexec ('/x "{0}" /qn /norestart' -f $Msi) 'uninstall'
  Write-Info "退出码 $($r2.exitCode)，日志 $($r2.log)"
  if ($r2.exitCode -ne 0) { throw "卸载失败（exit $($r2.exitCode)），请看日志 $($r2.log)" }
  $report.steps.uninstall = @{ exitCode = $r2.exitCode; log = $r2.log }

  Write-Step '校验卸载清理'
  $left = New-Object System.Collections.ArrayList
  if (Test-Path $launchExe) { [void]$left.Add("仍存在主程序 $launchExe") }
  foreach ($p in $msiShortcuts) { if (Test-Path $p) { [void]$left.Add("仍存在快捷方式 $p") } }
  if (Test-Path $regKey) {
    $v = (Get-ItemProperty -Path $regKey -Name $launchExe -ErrorAction SilentlyContinue).$launchExe
    if ($v) { [void]$left.Add("仍存在 RUNASADMIN 标记：$launchExe = $v") }
  }
  $arp2 = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue |
          Where-Object { $_.DisplayName -eq $productName }
  if ($arp2) { [void]$left.Add('ARP 登记仍在') }
  if (Test-Path $InstallDir) {
    $rest = Get-ChildItem $InstallDir -Recurse -File -ErrorAction SilentlyContinue
    if ($rest) { [void]$left.Add("安装目录残留 $($rest.Count) 个文件（$($rest[0].FullName) …）") }
    else { Write-Ok '安装目录已空（目录本身保留，符合 MSI 默认行为）' }
  }
  if ($left.Count) {
    Write-Host "`n卸载后仍有残留：" -ForegroundColor Red
    $left | ForEach-Object { Write-Bad $_ }
  } else { Write-Ok '卸载清理干净' }
  $report.steps.verifyUninstall = @{ leftovers = @($left) }
}

# ---------------------------------------------------------------- 还原备份
Write-Step '还原备份的快捷方式与注册表值'
# 注意：卸载时 MSI 会按 RemoveFolder 把 ...\Programs\OptiScaler DLSSNR Installer\ 整个目录删掉，
# 而旧部署（D:\Program Files 那份）可能往同一个目录里放过同名快捷方式。还原前必须先确认父目录存在，
# 否则 Copy-Item 会抛异常、后面的还原与结果文件都写不出来（2026-09-26 那次回归就是这么失败的）。
$restoreFailed = New-Object System.Collections.ArrayList
foreach ($s in $backup.shortcuts) {
  try {
    $parent = Split-Path $s.original -Parent
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    Copy-Item -LiteralPath $s.backup -Destination $s.original -Force
    Write-Ok "已还原 $($s.original)"
  } catch {
    [void]$restoreFailed.Add("$($s.original)：$($_.Exception.Message)")
    Write-Bad "还原失败 $($s.original)：$($_.Exception.Message)"
  }
}
if (Test-Path $regKey) {
  $now = Get-ItemProperty -Path $regKey
  foreach ($n in $backup.registry.Keys) {
    if ($now.PSObject.Properties.Name -notcontains $n) {
      New-ItemProperty -Path $regKey -Name $n -Value $backup.registry[$n] -PropertyType String -Force | Out-Null
      Write-Ok "已还原注册表值 $n"
    }
  }
}

$report.finishedAt = (Get-Date).ToString('s')
$report.restoreFailures = @($restoreFailed)
$reportPath = Join-Path $reportDir 'msi-install-test-result.json'
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Ok "测试记录 $reportPath"

# 退出码要有意义：校验发现的问题、卸载残留、还原失败，任何一个都算测试失败。
$failCount = 0
if ($report.steps.verifyInstall) { $failCount += @($report.steps.verifyInstall.problems).Count }
if ($report.steps.verifyUninstall) { $failCount += @($report.steps.verifyUninstall.leftovers).Count }
$failCount += $restoreFailed.Count
if ($failCount -gt 0) {
  Write-Host "`n测试结束：共 $failCount 个问题（见上面的“失败”行与 $reportPath）。" -ForegroundColor Red
  exit 1
}
Write-Host "`n测试结束：安装、校验、卸载、还原全部通过。" -ForegroundColor Green
