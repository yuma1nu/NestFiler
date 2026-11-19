param([string]$ExecutablePath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$profile = Join-Path $root ('artifacts/copy-drop-check-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $profile | Out-Null
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = if ($ExecutablePath) { (Resolve-Path -LiteralPath $ExecutablePath).Path } else { Join-Path $root 'src/FilerApp/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/NestFiler.exe' }
$start.UseShellExecute = $false
$start.Environment['FILERAPP_PROFILE_DIR'] = $profile
$start.Environment['FILERAPP_COPY_DROP_CHECK'] = '1'
$process = [Diagnostics.Process]::Start($start)
if (!$process.WaitForExit(60000)) { $process.Kill(); throw "Copy/drop check timed out: $profile" }
$report = Join-Path $profile 'copy-drop-check.txt'
if (!(Test-Path -LiteralPath $report)) { throw "No copy/drop report: $profile" }
$lines = @(Get-Content -LiteralPath $report)
$lines | Write-Output
if (($lines | Where-Object { $_ -like 'FAIL*' }) -or ($lines | Where-Object { $_ -like 'PASS*' }).Count -ne 19) { throw "Copy/drop checks failed: $report" }
Write-Output "Report: $report"
