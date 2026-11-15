param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'src/FilerApp/FilerApp.csproj')
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$') { throw 'Invalid project version' }
$destination = Join-Path $root "artifacts/NestFiler-win-x64-v$version"
if ((Test-Path -LiteralPath $destination) -or (Test-Path -LiteralPath "$destination.zip")) { throw "Output already exists: $destination" }
& dotnet publish (Join-Path $root 'src/FilerApp/FilerApp.csproj') -c $Configuration -p:Platform=x64 -r win-x64 --self-contained true -o $destination
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Copy-Item -LiteralPath (Join-Path $root 'DISTRIBUTION.md') -Destination $destination
Copy-Item -LiteralPath (Join-Path $root 'RELEASE-NOTES.md') -Destination $destination
Compress-Archive -Path (Join-Path $destination '*') -DestinationPath "$destination.zip"
$hash = Get-FileHash -LiteralPath "$destination.zip" -Algorithm SHA256
"$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName("$destination.zip"))" | Set-Content -LiteralPath "$destination.zip.sha256" -Encoding ascii
$hash | Format-List
Write-Output "Portable output: $destination"
