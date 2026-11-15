param([Parameter(Mandatory)][string]$ZipPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'src/FilerApp/FilerApp.csproj')
$expectedVersion = [string]$project.Project.PropertyGroup.FileVersion
$checkRoot = Join-Path $root ('artifacts/portable-check-' + [guid]::NewGuid().ToString('N'))
$application = Join-Path $checkRoot 'app'
$profile = Join-Path $checkRoot 'profile'
New-Item -ItemType Directory -Path $profile | Out-Null
Expand-Archive -LiteralPath $ZipPath -DestinationPath $application
foreach ($file in @('NestFiler.exe', 'NestFiler.dll', 'NestFiler.pri', 'App.xbf', 'MainWindow.xbf', 'DISTRIBUTION.md', 'RELEASE-NOTES.md')) {
    if (!(Test-Path -LiteralPath (Join-Path $application $file))) { throw "Missing portable file: $file" }
}
$version = (Get-Item -LiteralPath (Join-Path $application 'NestFiler.exe')).VersionInfo
if ($version.FileVersion -ne $expectedVersion -or $version.ProductName -ne 'Nest Filer') { throw 'Unexpected executable version or product name' }
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = Join-Path $application 'NestFiler.exe'
$start.UseShellExecute = $false
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$start.Environment['FILERAPP_PROFILE_DIR'] = $profile
$primary = [Diagnostics.Process]::Start($start)
$second = $null
$reopened = $null
try {
    Start-Sleep -Seconds 5
    $primary.Refresh()
    if ($primary.HasExited -or !$primary.Responding -or $primary.MainWindowHandle -eq 0) { throw 'Portable application failed to start' }
    'PASS portable ZIP contains executable, runtime resources and documentation; application starts and responds'
    $second = [Diagnostics.Process]::Start($start)
    if (!$second.WaitForExit(8000)) { throw 'Second portable instance did not exit' }
    $primary.Refresh()
    if ($primary.HasExited -or $primary.MainWindowHandle -eq 0) { throw 'Primary portable window was not retained' }
    'PASS portable second launch retains original process and window'
    if (!$primary.CloseMainWindow() -or !$primary.WaitForExit(8000)) { throw 'Portable application did not close normally' }
    if (!(Test-Path -LiteralPath (Join-Path $profile 'state.db'))) { throw 'Portable settings were not saved' }
    'PASS portable application closes normally and saves settings in isolated profile'
    $reopened = [Diagnostics.Process]::Start($start)
    Start-Sleep -Seconds 3
    $reopened.Refresh()
    if ($reopened.HasExited -or !$reopened.Responding -or $reopened.MainWindowHandle -eq 0) { throw 'Portable application failed to reopen saved profile' }
    if (!$reopened.CloseMainWindow() -or !$reopened.WaitForExit(8000)) { throw 'Reopened portable application did not close normally' }
    if (Test-Path -LiteralPath (Join-Path $profile 'logs/app.log')) { throw "Portable application recorded errors: $profile" }
    'PASS portable saved profile reopens and closes without application error log'
    "Portable check: $checkRoot"
} finally {
    foreach ($owned in @($primary, $second, $reopened)) {
        if ($null -ne $owned -and !$owned.HasExited) { $owned.Kill(); $owned.WaitForExit() }
    }
}
