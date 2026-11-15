param([switch]$UseSavedState, [switch]$WithoutSelection)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$profile = Join-Path $root ('artifacts/output-check-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $profile | Out-Null
if ($UseSavedState) {
    # Copy only after closing the normal profile. Never write its database.
    $exe = [IO.Path]::GetFullPath((Join-Path $root 'src/FilerApp/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/NestFiler.exe'))
    $legacyExe = Join-Path (Split-Path $exe -Parent) 'FilerApp.exe'
    if (Get-Process FilerApp,NestFiler -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe -or $_.Path -eq $legacyExe }) { throw 'Close Nest Filer before copying saved state.' }
    $saved = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'FilerApp'
    if (!(Test-Path -LiteralPath (Join-Path $saved 'state.db'))) { throw 'Saved state.db was not found.' }
    foreach ($name in @('state.db', 'state.db-wal')) {
        $source = Join-Path $saved $name
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination (Join-Path $profile $name) }
    }
    "Copied saved state into isolated profile: $profile"
}
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = Join-Path $root 'src/FilerApp/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/NestFiler.exe'
$start.UseShellExecute = $false
$start.Environment['FILERAPP_PROFILE_DIR'] = $profile
$start.Environment['FILERAPP_OUTPUT_CHECK'] = '1'
if ($WithoutSelection) { $start.Environment['FILERAPP_OUTPUT_NO_SELECTION'] = '1' }
$process = [Diagnostics.Process]::Start($start)
if (!$process.WaitForExit(60000)) { $process.Kill(); throw "Output folder check timed out: $profile" }
$report = Join-Path $profile 'output-check.txt'
if (!(Test-Path -LiteralPath $report)) { throw "No output check report: $profile" }
$lines = @(Get-Content -LiteralPath $report)
$lines | Write-Output
if (($lines | Where-Object { $_ -like 'FAIL*' }) -or ($lines | Where-Object { $_ -like 'PASS*' }).Count -ne 6) { throw "Output folder check failed: $report" }
"Report: $report"
