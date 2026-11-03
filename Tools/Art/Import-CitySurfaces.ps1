param([Parameter(Mandatory = $true)][string]$DownloadDirectory)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($DownloadDirectory)) | Out-Null
$Sources = @(); $Writes = @()
foreach ($Item in @(@{ key = 'brick'; id = 'brick_wall_001' }, @{ key = 'grass'; id = 'aerial_grass_rock' })) {
    # Use the public asset API; never scrape the site's preview images.
    $Api = "https://api.polyhaven.com/files/$($Item.id)"
    $Data = Invoke-RestMethod -Uri $Api
    foreach ($Map in @(@{ name = 'albedo'; source = 'Diffuse'; linear = $false }, @{ name = 'normal'; source = 'nor_gl'; linear = $true })) {
        $Info = $Data.($Map.source).'1k'.jpg
        if (-not $Info -or -not $Info.url.StartsWith('https://dl.polyhaven.org/file/ph-assets/')) { throw 'Unexpected source URL.' }
        $Local = Join-Path $DownloadDirectory "$($Item.key)_$($Map.name).jpg"
        if (-not (Test-Path -LiteralPath $Local)) { Invoke-WebRequest -Uri $Info.url -OutFile $Local }
        if ((Get-FileHash -Algorithm MD5 -LiteralPath $Local).Hash.ToLowerInvariant() -ne $Info.md5) { throw "Source checksum mismatch: $Local" }
        $Bytes = [IO.File]::ReadAllBytes($Local); $Packed = [Convert]::ToBase64String($Bytes) + "`n"
        if ($Packed.Length -gt 3000000) { throw 'Packed texture exceeds runtime limit.' }
        $Relative = "Assets/Broker/Resources/BrokerMaterials/$($Item.key)_$($Map.name).b64.txt"
        $Path = Join-Path $Root $Relative
        if ((Test-Path -LiteralPath $Path) -and [IO.File]::ReadAllText($Path) -ne $Packed) { throw "Modified surface preserved: $Path" }
        $Writes += @{ Path = $Path; Text = $Packed }
        $Sha = [Security.Cryptography.SHA256]::Create()
        try { $SourceHash = ([BitConverter]::ToString($Sha.ComputeHash($Bytes))).Replace('-','').ToLowerInvariant() }
        finally { $Sha.Dispose() }
        $Sources += [ordered]@{ key = $Item.key; assetId = $Item.id; assetPage = "https://polyhaven.com/a/$($Item.id)"; api = $Api; map = $Map.name; normalConvention = $(if ($Map.linear) { 'OpenGL tangent-space +Y' } else { $null }); width = 1024; height = 1024; path = $Relative; sourceUrl = $Info.url; sourceMd5 = $Info.md5; sourceSha256 = $SourceHash; sourceBytes = $Bytes.Length; transformation = 'Original 1k JPEG encoded as base64, no resizing or recoloring' }
    }
}
$ManifestPath = Join-Path $Root 'ThirdParty/CitySurfaces/SOURCES.json'
$Manifest = [ordered]@{ author = 'Poly Haven'; license = 'CC0-1.0'; licenseUrl = 'https://polyhaven.com/license'; checkedOn = '2026-10-21'; files = $Sources }
$Json = ($Manifest | ConvertTo-Json -Depth 6) + "`n"
if ((Test-Path -LiteralPath $ManifestPath) -and [IO.File]::ReadAllText($ManifestPath) -ne $Json) { throw 'Existing manifest differs; review manually.' }
foreach ($Write in $Writes) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Write.Path)) | Out-Null
    if (-not (Test-Path -LiteralPath $Write.Path)) { [IO.File]::WriteAllText($Write.Path, $Write.Text, [Text.UTF8Encoding]::new($false)) }
}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($ManifestPath)) | Out-Null
if (-not (Test-Path -LiteralPath $ManifestPath)) { [IO.File]::WriteAllText($ManifestPath, $Json, [Text.UTF8Encoding]::new($false)) }
Write-Output 'Four original 1k JPEG maps imported as local base64 resources with source MD5/SHA-256. Not a Unity texture test.'
