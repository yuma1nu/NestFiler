param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'src/FilerApp/FilerApp.csproj')
$tag = 'v' + [string]$project.Project.PropertyGroup.Version
$repository = 'yuma1nu/NestFiler'
$api = "https://api.github.com/repos/$repository"
$env:GIT_TERMINAL_PROMPT = '0'
$env:GCM_INTERACTIVE = 'never'
# Use the configured Git credential helper; never write or print credentials.
$credential = "protocol=https`nhost=github.com`n`n" | git credential fill
if ($LASTEXITCODE -ne 0) { throw 'GitHub credentials are unavailable in the configured Git credential helper' }
$password = $credential | Where-Object { $_.StartsWith('password=') } | Select-Object -First 1
if (!$password) { throw 'GitHub credential helper did not return a token' }
$headers = @{ Authorization = 'Bearer ' + $password.Substring(9); Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28'; 'User-Agent' = 'NestFiler-release' }
$credential = $null
$password = $null
try {
    $metadata = Invoke-RestMethod -Uri $api -Headers $headers
    if (!$metadata.permissions.push) { throw 'GitHub credentials do not have repository write permission' }
    $releases = Invoke-RestMethod -Uri "$api/releases" -Headers $headers
    if ($CheckOnly) { "GitHub release access ready: $repository; existing releases=$($releases.Count)"; return }
    $sha = (git -C $root rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve release commit' }
    $remoteRef = Invoke-RestMethod -Uri "$api/git/ref/tags/$tag" -Headers $headers
    if ($remoteRef.object.type -ne 'commit' -or $remoteRef.object.sha -ne $sha) { throw 'Remote release tag must point directly to the local release commit' }
    $release = $releases | Where-Object tag_name -eq $tag | Select-Object -First 1
    if ($release -and !$release.draft) { throw "Release already published: $($release.html_url)" }
    if (!$release) {
        $body = @{ tag_name = $tag; target_commitish = $sha; name = "Nest Filer $tag — 試用版"; body = Get-Content -LiteralPath (Join-Path $root 'RELEASE-NOTES.md') -Raw; draft = $true; prerelease = $true } | ConvertTo-Json
        $release = Invoke-RestMethod -Method Post -Uri "$api/releases" -Headers $headers -ContentType 'application/json; charset=utf-8' -Body $body
    }
    $uploadBase = $release.upload_url -replace '\{.*$', ''
    foreach ($name in @("NestFiler-win-x64-$tag.zip", "NestFiler-win-x64-$tag.zip.sha256")) {
        $path = Join-Path $root "artifacts/$name"
        $file = Get-Item -LiteralPath $path
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        $asset = $release.assets | Where-Object name -eq $name | Select-Object -First 1
        if (!$asset) {
            $contentType = if ($name.EndsWith('.zip')) { 'application/zip' } else { 'text/plain' }
            $asset = Invoke-RestMethod -Method Post -Uri ($uploadBase + '?name=' + [uri]::EscapeDataString($name)) -Headers $headers -ContentType $contentType -InFile $path
        }
        if ($asset.state -ne 'uploaded' -or $asset.size -ne $file.Length -or $asset.digest -ne "sha256:$hash") { throw "Release asset verification failed: $name" }
        "Verified uploaded asset: $name ($($file.Length) bytes), SHA256 $hash"
    }
    $published = Invoke-RestMethod -Method Patch -Uri "$api/releases/$($release.id)" -Headers $headers -ContentType 'application/json' -Body (@{ draft = $false; prerelease = $true; make_latest = 'false' } | ConvertTo-Json)
    if ($published.draft -or !$published.prerelease) { throw 'Release was not published as a pre-release' }
    "Published pre-release: $($published.html_url)"
} finally {
    $headers.Clear()
}
