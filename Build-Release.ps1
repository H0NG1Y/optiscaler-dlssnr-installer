<#
.SYNOPSIS
  产出可公开发行的 Release 产物：便携 EXE、公开版 MSI，以及 SHA-256 校验文件。

.DESCRIPTION
  与 build-msi.ps1 的区别：那个是本机整包（可含 158 MB 的 DLSS-NR 模型与第三方 DLL），
  这个是**对外发布**用的，按项目规则排除一切第三方二进制：
    - 不含 DLSS-NR 模型 nvngx_dlssnr.dll（属本机素材，不作为公开发行内容）
    - 不含上游桥接文件 nvngx.dll_dlssnr.dll
    - 不含 FSR 两个可选 DLL
  这两样由使用者自备或由程序界面上的在线下载/刷新功能从上游获取。

  流程：
    1. 校验 src\AssemblyInfo.cs 里的版本号与 -Version 一致
    2. 调 build.ps1 -Test（先跑测试再构建）
    3. README.md → dist\使用说明.md，并刷新 dist\SHA256SUMS.txt
    4. build-msi.ps1 -NoModel -NoFsr -NoBridge → 规范名 MSI
    5. 生成 .sha256 与 checksums.txt

  只做本地构建，不联网、不安装、不触碰任何游戏目录。

.PARAMETER Version
  版本号（三段式），默认 1.7.0。

.PARAMETER SkipMsi
  只出便携 EXE。

.PARAMETER SkipPortable
  只出 MSI。

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Build-Release.ps1
#>
[CmdletBinding()]
param(
  [string]$Version = '1.7.0',
  [switch]$SkipMsi,
  [switch]$SkipPortable
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$powershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'

# ---------------------------------------------------------------- 1. 版本校验
$assemblyInfo = Get-Content -LiteralPath (Join-Path $root 'src\AssemblyInfo.cs') -Raw
$expectedVersionLine = 'AssemblyFileVersion("' + $Version + '.0")'
if (-not $assemblyInfo.Contains($expectedVersionLine)) {
  throw "src\AssemblyInfo.cs 里的版本与目标版本不一致，找不到：$expectedVersionLine"
}
Write-Host "版本 $Version 与 src\AssemblyInfo.cs 一致。" -ForegroundColor Green

# ---------------------------------------------------------------- 2. 编译（先跑测试）
& $powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'build.ps1') -Test
if ($LASTEXITCODE -ne 0) { throw "build.ps1 -Test 失败（exit $LASTEXITCODE）" }
$appExe = Join-Path $dist 'OptiScaler-DLSSNR-Installer.exe'
if (-not (Test-Path -LiteralPath $appExe)) { throw "未生成主程序：$appExe" }

# ---------------------------------------------------------------- 3. 说明文档与便携包清单
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $dist '使用说明.md') -Force
Write-Host '已把 README.md 同步为 dist\使用说明.md（安装包载荷之一）。'

$sumsPath = Join-Path $dist 'SHA256SUMS.txt'
if (Test-Path -LiteralPath $sumsPath) {
  $entries = foreach ($line in (Get-Content -LiteralPath $sumsPath -Encoding UTF8)) {
    if ($line -match '^([0-9a-fA-F]{64})\s+(.+)$') { $Matches[2].Trim() }
  }
  $lines = foreach ($rel in $entries) {
    $abs = Join-Path $dist ($rel -replace '/', '\')
    if (-not (Test-Path -LiteralPath $abs)) { throw "清单里的文件不存在：$rel（无法刷新 SHA256SUMS.txt）" }
    '{0}  {1}' -f (Get-FileHash -LiteralPath $abs -Algorithm SHA256).Hash.ToLower(), $rel
  }
  [IO.File]::WriteAllLines($sumsPath, $lines, (New-Object Text.UTF8Encoding($false)))
  Write-Host "已刷新 dist\SHA256SUMS.txt（$($lines.Count) 项）。"
}

# ---------------------------------------------------------------- 4. 产物
$artifacts = [System.Collections.Generic.List[string]]::new()

if (-not $SkipPortable) {
  $portablePath = Join-Path $dist "OptiScaler-DLSSNR-Installer-v$Version.exe"
  Copy-Item -LiteralPath $appExe -Destination $portablePath -Force
  $artifacts.Add($portablePath)
  Write-Host ("便携版 {0}（{1:N0} 字节）" -f [IO.Path]::GetFileName($portablePath), (Get-Item $portablePath).Length)
}

if (-not $SkipMsi) {
  Write-Host '构建公开版 MSI（不含模型 / FSR / 桥接 DLL）…' -ForegroundColor Yellow
  & $powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'build-msi.ps1') `
    -Version $Version -NoModel -NoFsr -NoBridge
  if ($LASTEXITCODE -ne 0) { throw "build-msi.ps1 失败（exit $LASTEXITCODE）" }
  $builtMsi = Join-Path $dist "OptiScaler-DLSSNR-Installer-$Version-win-x64-nomodel-nofsr-nobridge.msi"
  if (-not (Test-Path -LiteralPath $builtMsi)) { throw "未生成 MSI：$builtMsi" }
  $setupPath = Join-Path $dist "OptiScaler-DLSSNR-Installer-Setup-v$Version.msi"
  Copy-Item -LiteralPath $builtMsi -Destination $setupPath -Force
  $artifacts.Add($setupPath)
  Write-Host ("安装包 {0}（{1:N0} 字节）" -f [IO.Path]::GetFileName($setupPath), (Get-Item $setupPath).Length)
}

# ---------------------------------------------------------------- 5. 校验文件
foreach ($artifact in $artifacts) {
  $hash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash
  [IO.File]::WriteAllText("$artifact.sha256", "$hash  $([IO.Path]::GetFileName($artifact))`r`n", [Text.Encoding]::ASCII)
}
$checksumLines = foreach ($artifact in $artifacts) {
  '{0}  {1}' -f (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash, [IO.Path]::GetFileName($artifact)
}
[IO.File]::WriteAllText((Join-Path $dist 'checksums.txt'), (($checksumLines -join "`r`n") + "`r`n"), [Text.Encoding]::ASCII)

Write-Host "`nRelease 产物：" -ForegroundColor Green
Get-ChildItem -LiteralPath $dist -File |
  Where-Object { $_.Name -like "*v$Version*" -or $_.Name -eq 'checksums.txt' } |
  Sort-Object Name | ForEach-Object { "{0,14:N0}  {1}" -f $_.Length, $_.Name }
