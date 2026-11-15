$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$profile = Join-Path $root ('artifacts/instance-check-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $profile | Out-Null
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = Join-Path $root 'src/FilerApp/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/NestFiler.exe'
$start.UseShellExecute = $false
$start.Environment['FILERAPP_PROFILE_DIR'] = $profile
$primary = [Diagnostics.Process]::Start($start)
try {
    for ($iteration = 0; $iteration -lt 60; $iteration++) {
        Start-Sleep -Milliseconds 100
        $primary.Refresh()
        if ($primary.HasExited) { throw 'Primary exited before initialization' }
        if ($primary.MainWindowHandle -ne 0) { break }
    }
    $second = [Diagnostics.Process]::Start($start)
    if (!$second.WaitForExit(8000)) { $second.Kill(); throw 'Second instance did not exit' }
    $primary.Refresh()
    if ($primary.HasExited -or $primary.MainWindowHandle -eq 0) { throw 'Primary window was not retained' }
    'PASS second launch exits and retains the original process/window'
    if (!$primary.CloseMainWindow() -or !$primary.WaitForExit(8000)) { throw 'Primary did not close normally' }
    if (!(Test-Path -LiteralPath (Join-Path $profile 'state.db'))) { throw 'No settings saved by primary' }
    'PASS primary saves settings on normal close'
    $reopened = [Diagnostics.Process]::Start($start)
    Start-Sleep -Seconds 2
    $reopened.Refresh()
    if ($reopened.HasExited -or $reopened.MainWindowHandle -eq 0) { throw 'Profile could not be opened again' }
    if (!$reopened.CloseMainWindow() -or !$reopened.WaitForExit(8000)) { throw 'Reopened instance did not close normally' }
    'PASS instance lock is released and the saved profile reopens'
    "Profile: $profile"
} finally {
    if (!$primary.HasExited) { $null = $primary.CloseMainWindow() }
}
