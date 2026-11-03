param(
    [string]$ArchivePath,
    [switch]$Download
)
$ErrorActionPreference = 'Stop'
$ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$SourceUrl = 'https://kenney.nl/media/pages/assets/furniture-kit/440e0608a4-1677580847/kenney_furniture-kit.zip'
$ExpectedHash = 'E67652D0932CEE41683F74711C03D3E192A2AF9979EF8E6B237711F5482D46B0'
if ($Download) {
    if (-not $ArchivePath) { throw 'Specify an explicit output ArchivePath for the download.' }
    if (Test-Path -LiteralPath $ArchivePath) { throw 'Download target already exists; use it without -Download.' }
    Invoke-WebRequest -Uri $SourceUrl -OutFile $ArchivePath
}
if (-not $ArchivePath -or -not (Test-Path -LiteralPath $ArchivePath -PathType Leaf)) { throw 'Specify the downloaded Furniture Kit ZIP.' }
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $ArchivePath).Hash -ne $ExpectedHash) { throw 'Archive hash differs. Review source/license before importing a new release.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($ArchivePath))
try {
    $Names = @('desk', 'chairDesk', 'computerScreen', 'computerKeyboard', 'computerMouse', 'kitchenCoffeeMachine', 'pottedPlant', 'bookcaseClosed', 'books', 'trashcan')
    $ResourceRoot = Join-Path $ProjectRoot 'Assets/Broker/Resources/BrokerModels/KenneyFurniture'
    $LicenseRoot = Join-Path $ProjectRoot 'ThirdParty/KenneyFurniture'
    $Writes = @()
    foreach ($Name in $Names) {
        foreach ($Extension in @('obj', 'mtl')) {
            $Source = "Models/OBJ format/$Name.$Extension"
            $Entry = $Zip.GetEntry($Source)
            if (-not $Entry) { throw "Missing source entry: $Source" }
            $Writes += @{ Entry = $Entry; Path = Join-Path $ResourceRoot "$Name.$Extension"; Source = $Source }
        }
    }
    $Writes += @{ Entry = $Zip.GetEntry('License.txt'); Path = Join-Path $LicenseRoot 'License.txt'; Source = 'License.txt' }
    if (-not $Writes[-1].Entry) { throw 'Missing license.' }
    # Prepare and check all bytes before writing; never overwrite modified project assets.
    foreach ($Write in $Writes) {
        $Stream = $Write.Entry.Open(); $Memory = [IO.MemoryStream]::new()
        try { $Stream.CopyTo($Memory); $Write.Bytes = $Memory.ToArray() }
        finally { $Stream.Dispose(); $Memory.Dispose() }
        if (Test-Path -LiteralPath $Write.Path) {
            $Current = [Convert]::ToBase64String([IO.File]::ReadAllBytes($Write.Path))
            if ($Current -ne [Convert]::ToBase64String($Write.Bytes)) { throw "Modified target preserved: $($Write.Path)" }
        }
    }
    $LicenseText = [Text.Encoding]::UTF8.GetString($Writes[-1].Bytes)
    if ($LicenseText -notmatch 'CC0' -or $LicenseText -notmatch 'commercial') { throw 'Review license: expected CC0 commercial-use declaration.' }
    $Files = @()
    foreach ($Write in $Writes) {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Write.Path)) | Out-Null
        if (-not (Test-Path -LiteralPath $Write.Path)) { [IO.File]::WriteAllBytes($Write.Path, $Write.Bytes) }
        $Relative = $Write.Path.Substring($ProjectRoot.Length + 1).Replace('\', '/')
        $Files += [ordered]@{ path = $Relative; archiveEntry = $Write.Source; bytes = $Write.Bytes.Length; sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $Write.Path).Hash.ToLowerInvariant() }
    }
    $Manifest = [ordered]@{
        author = 'Kenney'; pack = 'Furniture Kit'; license = 'CC0-1.0'; checkedOn = '2026-10-21'
        assetPage = 'https://kenney.nl/assets/furniture-kit'; termsPage = 'https://kenney.nl/support'
        licenseUrl = 'https://creativecommons.org/publicdomain/zero/1.0/'; archiveUrl = $SourceUrl
        archiveSha256 = $ExpectedHash.ToLowerInvariant(); files = $Files
    }
    $ManifestPath = Join-Path $LicenseRoot 'SOURCES.json'
    $Json = ($Manifest | ConvertTo-Json -Depth 6) + "`n"
    if (Test-Path -LiteralPath $ManifestPath) {
        if ([IO.File]::ReadAllText($ManifestPath) -ne $Json) { throw 'Existing source manifest differs; review it manually.' }
    } else { [IO.File]::WriteAllText($ManifestPath, $Json, [Text.UTF8Encoding]::new($false)) }
    Write-Output "Imported/checked $($Names.Count) OBJ models, MTL files and original license; source hashes saved. Unity import is not verified here."
} finally { $Zip.Dispose() }
