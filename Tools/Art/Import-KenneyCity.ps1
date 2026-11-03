param(
    [Parameter(Mandatory = $true)][ValidateSet('Commercial', 'Roads', 'Nature', 'Cars')][string]$Pack,
    [Parameter(Mandatory = $true)][string]$ArchivePath
)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ($Pack -eq 'Commercial') {
    $Hash = 'F8B09B081C2BB88BCC126E2DEC1CB40FD0DAD7E7E591B6C26AAEFE96FB35276B'
    $Url = 'https://kenney.nl/media/pages/assets/city-kit-commercial/a742d900eb-1753115042/kenney_city-kit-commercial_2.1.zip'
    $Page = 'https://kenney.nl/assets/city-kit-commercial'
    $Names = @('building-a', 'building-b', 'building-c', 'building-d', 'building-skyscraper-a', 'building-skyscraper-b', 'building-skyscraper-c', 'low-detail-building-a', 'low-detail-building-b', 'low-detail-building-c', 'detail-awning', 'detail-parasol-a')
} elseif ($Pack -eq 'Roads') {
    $Hash = '374A4552115CBD11B4EC5C930EE31A533562DC78A09AF249A655D03F7EA3B959'
    $Url = 'https://kenney.nl/media/pages/assets/city-kit-roads/74288c9459-1791628607/kenney_city-kit-roads.zip'
    $Page = 'https://kenney.nl/assets/city-kit-roads'
    $Names = @('light-curved', 'dumpster', 'construction-barrier', 'construction-cone', 'road-sign-empty', 'traffic-light')
} elseif ($Pack -eq 'Nature') {
    $Hash = 'FA7974A0D342BFE63C38664BA9F8EC1A4AAB8EA25F099BDC56870E33588C4D9D'
    $Url = 'https://kenney.nl/media/pages/assets/nature-kit/37ac38a37b-1677698939/kenney_nature-kit.zip'
    $Page = 'https://kenney.nl/assets/nature-kit'
    $Names = @('tree_detailed', 'tree_oak')
} else {
    $Hash = 'FAC7DACAC5C7874348CF19729AF3EF205F3D366493EDAF0A827D93F4FDF3D0C4'
    $Url = 'https://kenney.nl/media/pages/assets/car-kit/1a312ec241-1775131960/kenney_car-kit.zip'
    $Page = 'https://kenney.nl/assets/car-kit'
    $Names = @('sedan', 'suv', 'wheel-default')
}
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $ArchivePath).Hash -ne $Hash) { throw 'Unknown archive hash; review its source and license first.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($ArchivePath))
try {
    $Folder = "KenneyCity$Pack"
    $ResourceRoot = Join-Path $Root "Assets/Broker/Resources/BrokerModels/$Folder"
    $LicenseRoot = Join-Path $Root "ThirdParty/$Folder"
    $Entries = @()
    foreach ($Name in $Names) {
        foreach ($Ext in @('obj', 'mtl')) { $Entries += @{ Source = "Models/OBJ format/$Name.$Ext"; Path = Join-Path $ResourceRoot "$Name.$Ext" } }
    }
    if ($Pack -ne 'Nature') { $Entries += @{ Source = 'Models/OBJ format/Textures/colormap.png'; Path = Join-Path $ResourceRoot 'Textures/colormap.png' } }
    $Entries += @{ Source = 'License.txt'; Path = Join-Path $LicenseRoot 'License.txt' }
    foreach ($Item in $Entries) {
        $Entry = $Zip.GetEntry($Item.Source); if (-not $Entry) { throw "Missing archive entry $($Item.Source)" }
        $Stream = $Entry.Open(); $Memory = [IO.MemoryStream]::new()
        try { $Stream.CopyTo($Memory); $Item.Bytes = $Memory.ToArray() } finally { $Stream.Dispose(); $Memory.Dispose() }
        if ((Test-Path -LiteralPath $Item.Path) -and [Convert]::ToBase64String([IO.File]::ReadAllBytes($Item.Path)) -ne [Convert]::ToBase64String($Item.Bytes)) { throw "Modified target preserved: $($Item.Path)" }
    }
    $License = [Text.Encoding]::UTF8.GetString($Entries[-1].Bytes)
    if ($License -notmatch 'CC0' -or $License -notmatch 'commercial') { throw 'Expected CC0 commercial-use declaration absent.' }
    $Sha = [Security.Cryptography.SHA256]::Create()
    try {
        $Files = @($Entries | ForEach-Object {
            [ordered]@{ path = $_.Path.Substring($Root.Length + 1).Replace('\','/'); archiveEntry = $_.Source; bytes = $_.Bytes.Length; sha256 = ([BitConverter]::ToString($Sha.ComputeHash($_.Bytes))).Replace('-','').ToLowerInvariant() }
        })
    } finally { $Sha.Dispose() }
    $Manifest = [ordered]@{ author = 'Kenney'; pack = "City Kit $Pack"; license = 'CC0-1.0'; checkedOn = '2026-10-21'; assetPage = $Page; termsPage = 'https://kenney.nl/support'; licenseUrl = 'https://creativecommons.org/publicdomain/zero/1.0/'; archiveUrl = $Url; archiveSha256 = $Hash.ToLowerInvariant(); files = $Files }
    $ManifestPath = Join-Path $LicenseRoot 'SOURCES.json'; $Json = ($Manifest | ConvertTo-Json -Depth 6) + "`n"
    if ((Test-Path -LiteralPath $ManifestPath) -and [IO.File]::ReadAllText($ManifestPath) -ne $Json) { throw 'Existing manifest differs; review manually.' }
    foreach ($Item in $Entries) {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Item.Path)) | Out-Null
        if (-not (Test-Path -LiteralPath $Item.Path)) { [IO.File]::WriteAllBytes($Item.Path, $Item.Bytes) }
    }
    if (-not (Test-Path -LiteralPath $ManifestPath)) { [IO.File]::WriteAllText($ManifestPath, $Json, [Text.UTF8Encoding]::new($false)) }
    Write-Output "Imported/checked $($Names.Count) models, matching MTL, required textures and CC0 license ($Pack). Not a Unity import test."
} finally { $Zip.Dispose() }
