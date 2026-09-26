param([switch]$Test, [string]$OfficialZip)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 compiler not found.' }
$outDir = Join-Path $projectRoot $(if ($Test) { 'build\tests' } else { 'dist' })
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$refs = @('/r:System.dll','/r:System.Core.dll','/r:System.Web.Extensions.dll','/r:System.Net.Http.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll')
if ($Test) {
    $files = @((Join-Path $projectRoot 'src\Contracts.cs'),(Join-Path $projectRoot 'src\InstallerEngine.cs'),(Join-Path $projectRoot 'tests\Tests.cs'))
    & $compiler /nologo /utf8output /warn:4 /target:exe /main:TestRunner "/out:$outDir\InstallerTests.exe" @refs @files
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    if ($OfficialZip) { & (Join-Path $outDir 'InstallerTests.exe') --official-zip $OfficialZip }
    else { & (Join-Path $outDir 'InstallerTests.exe') }
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    $packageFiles = @((Join-Path $projectRoot 'src\Contracts.cs'),(Join-Path $projectRoot 'src\PackageService.cs'),(Join-Path $projectRoot 'tests\PackageTests.cs'))
    if (Test-Path -LiteralPath (Join-Path $projectRoot 'src\CurlTransport.cs')) { $packageFiles += Join-Path $projectRoot 'src\CurlTransport.cs' }
    & $compiler /nologo /utf8output /warn:4 /target:exe /main:PackageTestRunner "/out:$outDir\PackageTests.exe" @refs @packageFiles
    if ($LASTEXITCODE -ne 0) { throw 'Package tests compilation failed.' }
    & (Join-Path $outDir 'PackageTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Package tests failed.' }
    $uiFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
    $uiFiles += Join-Path $projectRoot 'tests\UiOptionsTests.cs'
    & $compiler /nologo /utf8output /warn:4 /platform:x64 /target:exe /main:UiOptionsTestRunner "/out:$outDir\UiOptionsTests.exe" @refs @uiFiles
    if ($LASTEXITCODE -ne 0) { throw 'UI option test compilation failed.' }
    & (Join-Path $outDir 'UiOptionsTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'UI option tests failed.' }
} else {
    $files = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
    & $compiler /nologo /utf8output /warn:4 /optimize+ /platform:x64 /target:winexe /main:OptiScalerInstaller.Program "/win32manifest:$projectRoot\app.manifest" "/win32icon:$projectRoot\assets\installer.ico" "/out:$outDir\OptiScaler-DLSSNR-Installer.exe" @refs @files
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    Get-Item -LiteralPath (Join-Path $outDir 'OptiScaler-DLSSNR-Installer.exe') | Select-Object FullName,Length
}
