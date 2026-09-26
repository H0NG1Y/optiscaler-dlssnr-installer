<#
.SYNOPSIS
  构建 OptiScaler DLSSNR 安装助手的 MSI 安装包。

.DESCRIPTION
  流程：
    1. 检查 WiX v3 工具链（build\tools\wix314\tools\candle.exe / light.exe）
    2. 按白名单整理载荷到 build\msi\payload，并逐个校验 SHA-256
    3. 生成 SHA256SUMS.txt 与许可协议 RTF
    4. candle → light 产出 dist\OptiScaler-DLSSNR-Installer-<版本>-win-x64.msi
    5. 校验：管理安装（msiexec /a）解包比对哈希 + dark 反编译核对表内容

  只做本地构建，不联网、不安装、不触碰任何游戏目录。

.PARAMETER Version
  版本号（三段式）。默认从 src\AssemblyInfo.cs 读取。

.PARAMETER NoModel
  不把 nvngx_dlssnr.dll（约 158 MB 本机模型）打进 MSI。

.PARAMETER NoFsr
  不把 FSR 两个可选 DLL 打进 MSI。

.PARAMETER NoBridge
  不把上游桥接文件 nvngx.dll_dlssnr.dll 打进 MSI（公开发行版用：不再分发第三方二进制）。

.PARAMETER RebuildExe
  先用 build.ps1 重新编译助手 EXE（默认直接使用 dist 中已有的 EXE，以保持与便携包一致）。

.PARAMETER StageOnly
  只整理载荷并校验哈希，不调用 WiX。

.PARAMETER NoVerify
  跳过管理安装解包校验。

.EXAMPLE
  pwsh -File build-msi.ps1

.EXAMPLE
  pwsh -File build-msi.ps1 -NoModel -RebuildExe
#>
[CmdletBinding()]
param(
  [string]$Version,
  [switch]$NoModel,
  [switch]$NoFsr,
  [switch]$NoBridge,
  [switch]$RebuildExe,
  [switch]$StageOnly,
  [switch]$NoVerify
)

$ErrorActionPreference = 'Stop'

$root      = $PSScriptRoot
$srcDir    = Join-Path $root 'src'
$distDir   = Join-Path $root 'dist'
$installer = Join-Path $root 'installer'
$stageDir  = Join-Path $root 'build\msi'
$payload   = Join-Path $stageDir 'payload'
$toolsDir  = Join-Path $root 'build\tools\wix314\tools'
$candleExe = Join-Path $toolsDir 'candle.exe'
$lightExe  = Join-Path $toolsDir 'light.exe'
$darkExe   = Join-Path $toolsDir 'dark.exe'
$uiExt     = Join-Path $toolsDir 'WixUIExtension.dll'
$utilExt   = Join-Path $toolsDir 'WixUtilExtension.dll'
$iconPath  = Join-Path $root 'assets\installer.ico'
$licenseSrc= Join-Path $installer 'License-zh-CN.txt'
$licenseRtf= Join-Path $stageDir 'License.rtf'
$wxl       = Join-Path $installer 'WixUI_zh-CN.wxl'
$wxs       = Join-Path $installer 'Installer.wxs'
$upgradeCode = 'B7E1D2A4-5C39-4F62-9E10-6D8A3C4B5F27'

# 上游官方包（桥接 DLL 的权威来源）与已部署副本（兜底）
$officialZip = 'E:\BaiduNetdiskDownload\DLSS 5\OptiScaler-DLSSNR\OptiScaler-DLSSNR-v0.2.0.zip'
$deployedDir = 'D:\Program Files\OptiScaler DLSSNR Installer'

# 已知哈希（来自已核对的发布/部署记录，不匹配即拒绝打包）
$expectedBridge = '17CF51D25D142D0BE15FCAC945CC928C37849A1CB61011CB9C742D72DF86647C'
$expectedModel  = 'E67DEE209320CDAFE0E93E45675D7AA34323A53ACC57A72B2E40A181581C989A'

function Write-Step([string]$text) { Write-Host "`n== $text" -ForegroundColor Cyan }
function Write-Ok([string]$text)   { Write-Host "   OK   $text" -ForegroundColor Green }
function Write-Info([string]$text) { Write-Host "   $text" }

function Get-FileSha256([string]$path) {
  return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}

function Assert-Hash([string]$path, [string]$expected, [string]$label) {
  $actual = Get-FileSha256 $path
  if ($actual -ne $expected.ToUpper()) {
    throw "$label 哈希不符：`n  期望 $($expected.ToUpper())`n  实际 $actual`n  文件 $path"
  }
  return $actual
}

function ConvertTo-RtfEscaped([string]$text) {
  $sb = New-Object System.Text.StringBuilder
  foreach ($ch in $text.ToCharArray()) {
    $code = [int][char]$ch
    if ($ch -eq '\') { [void]$sb.Append('\\') }
    elseif ($ch -eq '{') { [void]$sb.Append('\{') }
    elseif ($ch -eq '}') { [void]$sb.Append('\}') }
    elseif ($ch -eq "`t") { [void]$sb.Append(' ') }
    elseif ($code -lt 128) { [void]$sb.Append($ch) }
    else {
      if ($code -gt 32767) { $code -= 65536 }
      [void]$sb.Append("\u$code?")
    }
  }
  return $sb.ToString()
}

function New-LicenseRtf([string]$sourceTxt, [string]$targetRtf, [string]$version) {
  $raw = Get-Content -LiteralPath $sourceTxt -Raw -Encoding UTF8
  $raw = $raw.Replace('{{VERSION}}', $version).Replace("`r`n", "`n")
  $sb = New-Object System.Text.StringBuilder
  [void]$sb.Append("{\rtf1\ansi\ansicpg936\deff0{\fonttbl{\f0\fnil\fcharset134 SimSun;}}`n")
  [void]$sb.Append("\viewkind4\uc1\pard\f0\fs18`n")
  foreach ($line in ($raw -split "`n")) {
    [void]$sb.Append((ConvertTo-RtfEscaped $line))
    [void]$sb.Append("\par`n")
  }
  [void]$sb.Append("}`n")
  [IO.File]::WriteAllText($targetRtf, $sb.ToString(), [Text.Encoding]::ASCII)
}

# ---------------------------------------------------------------- 版本号
Write-Step '读取版本号'
if (-not $Version) {
  $asmInfo = Get-Content -LiteralPath (Join-Path $srcDir 'AssemblyInfo.cs') -Raw -Encoding UTF8
  if ($asmInfo -match 'AssemblyFileVersion\("(\d+\.\d+\.\d+)') { $Version = $Matches[1] }
  else { throw '无法从 src\AssemblyInfo.cs 解析版本号，请用 -Version 指定。' }
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "版本号必须是三段式 x.y.z，当前：$Version" }
Write-Ok "版本 $Version"

# ---------------------------------------------------------------- 工具链
Write-Step '检查 WiX 工具链'
foreach ($tool in @($candleExe, $lightExe, $uiExt, $utilExt)) {
  if (-not (Test-Path $tool)) {
    throw "缺少 WiX 工具：$tool`n请下载 https://api.nuget.org/v3-flatcontainer/wix/3.14.1/wix.3.14.1.nupkg 并解压到 build\tools\wix314。"
  }
}
Write-Ok 'candle.exe / light.exe / WixUIExtension.dll / WixUtilExtension.dll 就位'
if (-not (Test-Path $wxl)) { throw "缺少本地化文件：$wxl" }
if (-not (Test-Path $iconPath)) { throw "缺少图标：$iconPath" }

# ---------------------------------------------------------------- 助手 EXE
Write-Step '准备助手主程序'
$exePath = Join-Path $distDir 'OptiScaler-DLSSNR-Installer.exe'
if ($RebuildExe -or -not (Test-Path $exePath)) {
  Write-Info '调用 build.ps1 重新编译…'
  & "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'build.ps1')
  if ($LASTEXITCODE -ne 0) { throw "build.ps1 失败（exit $LASTEXITCODE）" }
}
if (-not (Test-Path $exePath)) { throw "找不到助手主程序：$exePath" }
Write-Ok ("助手 EXE {0:N0} 字节" -f (Get-Item $exePath).Length)

# dist\SHA256SUMS.txt 是 1.7.0 便携包的官方清单，可用作 EXE/说明/FSR 的期望哈希
$distSums = @{}
$distSumsPath = Join-Path $distDir 'SHA256SUMS.txt'
if (Test-Path $distSumsPath) {
  foreach ($line in (Get-Content -LiteralPath $distSumsPath -Encoding UTF8)) {
    if ($line -match '^([0-9a-fA-F]{64})\s+(.+)$') { $distSums[$Matches[2].Trim()] = $Matches[1].ToUpper() }
  }
}

# ---------------------------------------------------------------- 载荷白名单
Write-Step '整理载荷到 build\msi\payload'
if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }
New-Item -ItemType Directory -Path $payload -Force | Out-Null

$manifest = New-Object System.Collections.ArrayList

function Add-Payload([string]$sourcePath, [string]$relative, [string]$expectedHash, [string]$origin) {
  $target = Join-Path $payload $relative
  $parent = Split-Path $target -Parent
  if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
  Copy-Item -LiteralPath $sourcePath -Destination $target -Force
  $actual = Get-FileSha256 $target
  if ($expectedHash) {
    if ($actual -ne $expectedHash.ToUpper()) {
      throw "$relative 哈希不符：`n  期望 $($expectedHash.ToUpper())`n  实际 $actual`n  来源 $sourcePath"
    }
  }
  [void]$manifest.Add([pscustomobject]@{
    path   = $relative
    size   = (Get-Item $target).Length
    sha256 = $actual
    origin = $origin
  })
  Write-Ok ("{0,-40} {1,12:N0} 字节" -f $relative, (Get-Item $target).Length)
}

Add-Payload $exePath 'OptiScaler-DLSSNR-Installer.exe' $distSums['OptiScaler-DLSSNR-Installer.exe'] 'dist\OptiScaler-DLSSNR-Installer.exe'
Add-Payload (Join-Path $distDir '使用说明.md') '使用说明.md' $distSums['使用说明.md'] 'dist\使用说明.md'
Add-Payload (Join-Path $installer 'MSI-安装说明.md') 'MSI-安装说明.md' $null 'installer\MSI-安装说明.md'

# 桥接 DLL：优先从上游官方包中提取（可追溯），否则用已部署副本（哈希必须一致）
$bridgeTemp = Join-Path $stageDir 'nvngx.dll_dlssnr.dll'
if ($NoBridge) {
  Write-Info '已按 -NoBridge 跳过上游桥接文件 nvngx.dll_dlssnr.dll'
} elseif (Test-Path $officialZip) {
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $zip = [IO.Compression.ZipFile]::OpenRead($officialZip)
  try {
    $entry = $zip.Entries | Where-Object { $_.Name -eq 'nvngx.dll_dlssnr.dll' } | Select-Object -First 1
    if (-not $entry) { throw "上游官方包中没有 nvngx.dll_dlssnr.dll：$officialZip" }
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $bridgeTemp, $true)
  } finally { $zip.Dispose() }
  Add-Payload $bridgeTemp 'nvngx.dll_dlssnr.dll' $expectedBridge "上游官方包 OptiScaler-DLSSNR-v0.2.0.zip"
} else {
  $fallback = Join-Path $deployedDir 'nvngx.dll_dlssnr.dll'
  if (-not (Test-Path $fallback)) { throw "找不到桥接 DLL 来源：$officialZip / $fallback" }
  Add-Payload $fallback 'nvngx.dll_dlssnr.dll' $expectedBridge "已部署副本 $deployedDir"
}

if (-not $NoModel) {
  $model = Join-Path $root 'local-assets\nvngx_dlssnr.dll'
  if (-not (Test-Path $model)) { throw "找不到模型文件：$model（如需跳过请加 -NoModel）" }
  Add-Payload $model 'nvngx_dlssnr.dll' $expectedModel 'local-assets\nvngx_dlssnr.dll'
} else {
  Write-Info '已按 -NoModel 跳过 DLSS 神经渲染模型'
}

$fsrFiles = @('dlss-enabler-headless.dll', 'dlssg_to_fsr3_amd_is_better.dll')
if (-not $NoFsr) {
  foreach ($name in $fsrFiles) {
    $src = Join-Path $distDir "FSR\$name"
    if (-not (Test-Path $src)) { throw "找不到 FSR 组件：$src（如需跳过请加 -NoFsr）" }
    Add-Payload $src "FSR\$name" $distSums["FSR/$name"] 'dist\FSR'
  }
} else {
  Write-Info '已按 -NoFsr 跳过 FSR 可选组件'
}

# ---------------------------------------------------------------- SHA256SUMS.txt
Write-Step '生成 SHA256SUMS.txt'
$sumsLines = @()
foreach ($item in $manifest) {
  $sumsLines += ('{0}  {1}' -f $item.sha256.ToLower(), ($item.path -replace '\\', '/'))
}
$sumsPath = Join-Path $payload 'SHA256SUMS.txt'
[IO.File]::WriteAllLines($sumsPath, $sumsLines, (New-Object Text.UTF8Encoding($false)))
foreach ($l in $sumsLines) { Write-Info $l }

# ---------------------------------------------------------------- 许可协议 RTF
Write-Step '生成许可协议 RTF'
New-LicenseRtf $licenseSrc $licenseRtf $Version
Write-Ok ("License.rtf {0:N0} 字节（由 License-zh-CN.txt 转义生成）" -f (Get-Item $licenseRtf).Length)

$payloadBytes = ($manifest | Measure-Object -Property size -Sum).Sum
Write-Host ("`n载荷合计 {0} 个文件，{1:N0} 字节（{2:N1} MB）" -f $manifest.Count, $payloadBytes, ($payloadBytes / 1MB)) -ForegroundColor Yellow

if ($StageOnly) {
  Write-Host '已按 -StageOnly 结束。' -ForegroundColor Yellow
  return
}

# ---------------------------------------------------------------- candle + light
$wixobj = Join-Path $stageDir 'Installer.wixobj'
# 默认产出规范名；用 -NoModel / -NoFsr / -NoBridge 做裁剪版时加后缀，避免覆盖正式包
$variant = ''
if ($NoModel -or $NoFsr -or $NoBridge) {
  $variantParts = @()
  if ($NoModel)  { $variantParts += 'nomodel' }
  if ($NoFsr)    { $variantParts += 'nofsr' }
  if ($NoBridge) { $variantParts += 'nobridge' }
  $variant = '-' + ($variantParts -join '-')
}
$msiName = "OptiScaler-DLSSNR-Installer-$Version-win-x64$variant.msi"
$msiPath = Join-Path $distDir $msiName

Write-Step 'candle：编译 Installer.wxs'
$candleArgs = @(
  '-nologo', '-arch', 'x64',
  '-ext', $utilExt,
  "-dProductVersion=$Version",
  "-dPayloadDir=$payload",
  "-dIconPath=$iconPath",
  "-dLicenseRtf=$licenseRtf",
  "-dUpgradeCode=$upgradeCode",
  "-dIncludeModel=$(if ($NoModel) { '0' } else { '1' })",
  "-dIncludeFsr=$(if ($NoFsr) { '0' } else { '1' })",
  "-dIncludeBridge=$(if ($NoBridge) { '0' } else { '1' })",
  '-out', $wixobj,
  $wxs
)
Write-Info ($candleArgs -join ' ')
& $candleExe @candleArgs
if ($LASTEXITCODE -ne 0) { throw "candle 失败（exit $LASTEXITCODE）" }
Write-Ok ("Installer.wixobj {0:N0} 字节" -f (Get-Item $wixobj).Length)

Write-Step 'light：链接生成 MSI'
if (Test-Path $msiPath) { Remove-Item $msiPath -Force }
$lightArgs = @(
  '-nologo',
  '-ext', $uiExt,
  '-ext', $utilExt,
  # 只抑制这两条 ICE（其余全部保留）：
  #   ICE43/ICE57 —— 本包是 ALLUSERS=1 的 per-machine 安装，快捷方式写在“所有用户”的
  #   开始菜单(CommonProgramsFolder)与桌面(DesktopFolder)。ICE 仍把 DesktopFolder 当作每用户
  #   目录，于是报 “non-advertised shortcuts should use a registry key under HKCU as its KeyPath”
  #   与 “per-user and per-machine data with a per-machine KeyPath”，对 per-machine 安装属误报：
  #   MSI 自己在 costing 阶段就把 DesktopFolder 解析成 C:\Users\Public\Desktop（安装日志可见），
  #   不是当前用户桌面。这里用不了 HKCU KeyPath 的“正规修法”（那会把 per-machine 包写成每用户组件）。
  #   真实行为由 installer\test-msi-install.ps1 的实际安装/卸载回归验证（快捷方式落在 Public 桌面）。
  '-sice:ICE43',
  '-sice:ICE57',
  '-cultures:zh-CN',
  '-loc', $wxl,
  '-out', $msiPath,
  $wixobj
)
Write-Info ($lightArgs -join ' ')
& $lightExe @lightArgs
if ($LASTEXITCODE -ne 0) { throw "light 失败（exit $LASTEXITCODE）" }
$msiItem = Get-Item $msiPath
$msiHash = Get-FileSha256 $msiPath
Write-Ok ("$msiName  {0:N0} 字节（{1:N1} MB）" -f $msiItem.Length, ($msiItem.Length / 1MB))
Write-Ok "SHA-256 $msiHash"

# ---------------------------------------------------------------- 校验
$verify = [ordered]@{ performed = $false }

if (-not $NoVerify) {
  Write-Step '校验：管理安装解包比对'
  $adminDir = Join-Path $stageDir 'admin-extract'
  if (Test-Path $adminDir) { Remove-Item $adminDir -Recurse -Force }
  New-Item -ItemType Directory -Path $adminDir -Force | Out-Null
  $logPath = Join-Path $stageDir 'admin-install.log'
  $argLine = '/a "{0}" /qn /norestart TARGETDIR="{1}" /l*v "{2}"' -f $msiPath, $adminDir, $logPath
  $proc = Start-Process -FilePath 'msiexec.exe' -ArgumentList $argLine -Wait -PassThru
  Write-Info "msiexec /a 退出码 $($proc.ExitCode)"
  if ($proc.ExitCode -ne 0) { throw "管理安装失败（exit $($proc.ExitCode)），日志：$logPath" }

  $extracted = Get-ChildItem $adminDir -Recurse -File
  Write-Info ("解包 {0} 个文件：" -f $extracted.Count)
  foreach ($f in $extracted) {
    Write-Info ("  {0,-46} {1,12:N0}" -f $f.FullName.Substring($adminDir.Length).TrimStart('\'), $f.Length)
  }

  # 用哈希多重集比对（管理安装可能改写文件名的 8.3 形式，因此不按名字比）
  # 期望集合 = 载荷清单 + 生成的 SHA256SUMS.txt（该文件记录的是别人的哈希，天然不含自身）
  $expect = @{}
  foreach ($item in $manifest) {
    $k = $item.sha256
    if ($expect.ContainsKey($k)) { $expect[$k]++ } else { $expect[$k] = 1 }
  }
  $sumsFile = Join-Path $payload 'SHA256SUMS.txt'
  if (-not (Test-Path $sumsFile)) { throw "载荷目录缺少 SHA256SUMS.txt" }
  $sumsKey = Get-FileSha256 $sumsFile
  if ($expect.ContainsKey($sumsKey)) { $expect[$sumsKey]++ } else { $expect[$sumsKey] = 1 }

  # msiexec /a 会在解包目录额外放一份精简过的 MSI 副本，按文件名排除，不参与比对
  $imageMsi = @($extracted | Where-Object { $_.Name -eq $msiName })
  foreach ($m in $imageMsi) { Write-Info ("（镜像 MSI 副本，管理安装自带，不参与比对：{0:N0} 字节）" -f $m.Length) }
  $payloadFiles = @($extracted | Where-Object { $_.Name -ne $msiName })

  $actual = @{}
  foreach ($f in $payloadFiles) {
    $k = Get-FileSha256 $f.FullName
    if ($actual.ContainsKey($k)) { $actual[$k]++ } else { $actual[$k] = 1 }
  }
  $missing = @(); $extra = @()
  foreach ($k in $expect.Keys) { if (-not $actual.ContainsKey($k) -or $actual[$k] -lt $expect[$k]) { $missing += $k } }
  foreach ($k in $actual.Keys) { if (-not $expect.ContainsKey($k) -or $actual[$k] -gt $expect[$k]) { $extra += $k } }
  if ($missing.Count -or $extra.Count) {
    foreach ($k in $missing) {
      $n = ($manifest | Where-Object { $_.sha256 -eq $k } | Select-Object -First 1)
      Write-Bad ("缺少 {0}（{1}）" -f $k, $(if ($n) { $n.path } else { 'SHA256SUMS.txt 或未知' }))
    }
    foreach ($k in $extra) {
      $e = ($payloadFiles | Where-Object { (Get-FileSha256 $_.FullName) -eq $k } | Select-Object -First 1)
      Write-Bad ("多出 {0}（{1}）" -f $k, $(if ($e) { $e.FullName.Substring($adminDir.Length + 1) } else { '？' }))
    }
    throw "管理安装内容与载荷不一致：缺少 $($missing.Count) 项，多出 $($extra.Count) 项"
  }
  Write-Ok "解包内容与载荷逐字节一致（$($payloadFiles.Count) 个文件）"
  $verify.performed = $true
  $verify.adminExtract = $adminDir
  $verify.fileCount = $payloadFiles.Count
  $verify.log = $logPath

  Write-Step '校验：dark 反编译核对表内容'
  $decompiled = Join-Path $stageDir 'decompiled.wxs'
  $darkExtract = Join-Path $stageDir 'dark-extract'
  if (Test-Path $decompiled) { Remove-Item $decompiled -Force }
  if (Test-Path $darkExtract) { Remove-Item $darkExtract -Recurse -Force }
  & $darkExe -nologo -x $darkExtract -o $decompiled $msiPath
  if ($LASTEXITCODE -ne 0) { throw "dark 反编译失败（exit $LASTEXITCODE）" }
  $productId = ''
  if ((Get-Content -LiteralPath $decompiled -Raw) -match 'Product Id="([^"]+)"') { $productId = $Matches[1] }
  $rows = Select-String -LiteralPath $decompiled -Pattern '<Shortcut |<RegistryValue |<Feature |<CustomAction |<Condition |<Publish ' |
    ForEach-Object { $_.Line.Trim() }
  foreach ($r in $rows) { Write-Info $r }
  Write-Ok "ProductCode $productId"
  $verify.productCode = $productId
  $verify.decompiled = $decompiled
}

# ---------------------------------------------------------------- 结果 JSON
$result = [ordered]@{
  builtAt       = (Get-Date).ToString('s')
  version       = $Version
  msi           = $msiPath
  msiSize       = $msiItem.Length
  msiSha256     = $msiHash
  includeModel  = (-not $NoModel)
  includeFsr    = (-not $NoFsr)
  payloadBytes  = $payloadBytes
  payload       = $manifest
  features      = @('MainFeature') + $(if ($NoModel) { @() } else { @('NeuralRenderingModelFeature') })
  verify        = $verify
}
$resultPath = Join-Path $stageDir 'msi-build-result.json'
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resultPath -Encoding UTF8
Write-Ok "构建记录 $resultPath"

Write-Host "`n完成：$msiPath" -ForegroundColor Green
