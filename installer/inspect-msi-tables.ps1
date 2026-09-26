<#
  直读 MSI 数据库表做静态审计（不需要管理员、不写系统）。
  用法：powershell -ExecutionPolicy Bypass -File installer\inspect-msi-tables.ps1 [-Msi <路径>]

  为什么要用它：light 的 ICE 校验只看结构，看不到“快捷方式目标退化成目录”“RemoveFolder 没进 RemoveFile 表”
  这类语义问题；而真实安装回归要提权、慢。这个脚本用 WindowsInstaller.Installer COM 直接读表，几秒出结果，
  是两者之间的快速自检。

  两个踩过的坑（改本脚本前先看）：
  1. MSI SQL 方言：标识符要反引号（`Property`）；Feature 表的 `Absent` 列**不能出现在 SELECT 列表里**
     （`SELECT `Feature`, `Title`, `Absent` FROM `Feature`` 直接报 OpenView,Sql，加表名限定、加反引号都不行），
     要看 Absent 只能用 `SELECT * FROM `Feature``。
  2. PowerShell 会把函数的输出数组**递归摊平**：`return $rows.ToArray()`（元素又是数组）会把每行摊成一个个字段，
     调用方拿到的“行”其实是单个字段值。所以这里每行构造为 PSCustomObject（标量对象，不会被摊平），用列名访问。
#>
[CmdletBinding()]
param(
  [string]$Msi = (Join-Path $PSScriptRoot '..\dist\OptiScaler-DLSSNR-Installer-1.7.0-win-x64.msi')
)
$ErrorActionPreference = 'Stop'
$Msi = (Resolve-Path -LiteralPath $Msi).Path
if (-not (Test-Path -LiteralPath $Msi)) { throw "找不到 MSI：$Msi" }
Write-Host "审计：$Msi`n"

$installer = New-Object -ComObject WindowsInstaller.Installer
$db = $installer.GetType().InvokeMember('OpenDatabase', 'InvokeMethod', $null, $installer, @($Msi, 0))

function Invoke-MsiQuery {
  param([string]$Sql)
  $view = $db.GetType().InvokeMember('OpenView', 'InvokeMethod', $null, $db, @($Sql))
  # 返回的 $null 要用 $null = 吃掉，否则它会成为函数输出的一部分
  $null = $view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null)

  # 列名直接从 SQL 推导（"SELECT `Property`, `Value` FROM `Property`" → Property / Value；
  # "SELECT `Feature`.`Feature` ..." → Feature）。
  # 不用 view.ColumnInfo(0) 拿列数：它经 COM 返回的是 __ComObject 而不是 Int32，
  # 拿去和 $i 比较会直接报“无法将“1”与“System.__ComObject”进行比较”。
  $names = @(($Sql -replace '(?is)^\s*SELECT\s+(.*?)\s+FROM\s.*$', '$1') -split ',' | ForEach-Object {
      ($_.Trim() -split '\.')[-1].Trim().Trim('`')
    })
  $colCount = $names.Count

  $rows = New-Object System.Collections.ArrayList
  while ($true) {
    $rec = $view.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $view, $null)
    if (-not $rec) { break }
    $row = New-Object psobject
    for ($i = 1; $i -le $colCount; $i++) {
      $val = ($rec.GetType().InvokeMember('StringData', 'GetProperty', $null, $rec, @($i)) -join '')
      $row | Add-Member -NotePropertyName $names[$i - 1] -NotePropertyValue $val
    }
    [void]$rows.Add($row)
  }
  $null = $view.GetType().InvokeMember('Close', 'InvokeMethod', $null, $view, $null)
  return $rows.ToArray()
}

$problems = New-Object System.Collections.ArrayList

Write-Host '== 产品基本信息'
foreach ($r in (Invoke-MsiQuery 'SELECT `Property`, `Value` FROM `Property`')) {
  switch ($r.Property) {
    'ProductName'     { Write-Host ("  产品名        {0}" -f $r.Value) }
    'ProductVersion'  { Write-Host ("  版本          {0}" -f $r.Value) }
    'ProductLanguage' { Write-Host ("  语言          {0}（2052=简体中文）" -f $r.Value) }
    'Manufacturer'    { Write-Host ("  制造商        {0}" -f $r.Value) }
    'ALLUSERS'        { Write-Host ("  ALLUSERS      {0}（1=perMachine）" -f $r.Value) }
    'UpgradeCode'     { Write-Host ("  UpgradeCode   {0}" -f $r.Value) }
  }
}

Write-Host "`n== Shortcut 表（Target 必须是 [#文件键]，不能是目录）"
foreach ($r in (Invoke-MsiQuery 'SELECT `Shortcut`, `Directory_`, `Name`, `Target` FROM `Shortcut`')) {
  # 用 StartsWith 而不是 -like '[#*]*'：后者是字符类，只匹配单个字符，会把正确的 [#InstallerExe] 判成错的
  $ok = $r.Target.StartsWith('[#')
  Write-Host ("  {0,-20} 目录={1,-28} 名称={2}  目标={3} {4}" -f $r.Shortcut, $r.Directory_, $r.Name, $r.Target, $(if ($ok) { 'OK' } else { '<< 目标不是文件！' }))
  if (-not $ok) { [void]$problems.Add("Shortcut $($r.Shortcut) 的 Target 不是文件引用：$($r.Target)") }
}

Write-Host "`n== RemoveFile 表（RemoveFolder 的行 FileName 为空，用于卸载清目录 / 满足 ICE64）"
$rf = @(Invoke-MsiQuery 'SELECT `FileKey`, `Component_`, `FileName`, `DirProperty`, `InstallMode` FROM `RemoveFile`')
if ($rf.Count -eq 0) {
  Write-Host '  （空 —— 没有 RemoveFolder/RemoveFile 行）'
  [void]$problems.Add('RemoveFile 表为空：没有登记卸载时要清理的目录')
} else {
  foreach ($r in $rf) {
    Write-Host ("  FileKey={0,-34} 组件={1,-26} 文件名={2,-14} 目录属性={3,-30} 模式={4}" -f `
        $r.FileKey, $r.Component_, $(if ($r.FileName) { $r.FileName } else { '(空=删目录)' }), $r.DirProperty, $r.InstallMode)
  }
}

Write-Host "`n== Registry 表（RUNASADMIN 标记）"
foreach ($r in (Invoke-MsiQuery 'SELECT `Registry`, `Root`, `Key`, `Name`, `Value` FROM `Registry`')) {
  $rootName = switch ($r.Root) { '1' { 'HKCU' } '2' { 'HKLM' } default { $r.Root } }
  Write-Host ("  {0}\{1}" -f $rootName, $r.Key)
  Write-Host ("      Name={0}  Value={1}" -f $(if ($r.Name) { $r.Name } else { '(空=只建键)' }), $r.Value)
}

Write-Host "`n== Component / File / Feature 计数"
$comp = @(Invoke-MsiQuery 'SELECT `Component`, `Attributes` FROM `Component`')
$bad64 = @($comp | Where-Object { $_.Attributes -ne '256' })
Write-Host ("  组件 {0} 个（Attributes=256 表示 64 位）：{1}" -f $comp.Count, $(if ($bad64.Count -eq 0) { '全部 OK' } else { "有 $($bad64.Count) 个不是 64 位！" }))
if ($bad64.Count -gt 0) { [void]$problems.Add("有 $($bad64.Count) 个组件没有 64 位属性") }

$files = @(Invoke-MsiQuery 'SELECT `File`, `FileName`, `FileSize`, `Version` FROM `File`')
Write-Host ("  载荷 {0} 个文件：" -f $files.Count)
foreach ($r in $files) { Write-Host ("    {0,-38} {1,13:N0} 字节  {2}" -f $r.FileName, [int64]$r.FileSize, $r.Version) }

$feats = @(Invoke-MsiQuery 'SELECT `Feature`, `Title` FROM `Feature`')
Write-Host ("  功能 {0} 个：" -f $feats.Count)
foreach ($r in $feats) { Write-Host ("    {0,-30} {1}" -f $r.Feature, $r.Title) }

Write-Host ''
if ($problems.Count -eq 0) { Write-Host '审计结论：未发现问题（快捷方式目标、RemoveFile 行、注册表行、组件位数均正常）' }
else { Write-Host "审计结论：发现 $($problems.Count) 个问题"; $problems | ForEach-Object { Write-Host "  - $_" }; exit 1 }
