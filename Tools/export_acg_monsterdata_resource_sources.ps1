param(
    [string]$Ep1ClientPath = "C:\Users\Mike\Documents\AO stripdown\Anarchy Online",
    [string]$Ep2ClientPath = "C:\Funcom\Anarchy Online",
    [string]$AodbPluginPath = "C:\Users\Mike\Documents\AO Decompiler\AO-Model-Viewer\Assets\Plugins",
    [string]$MonsterDataCorpusPath = "C:\Users\Mike\Documents\AO stripdown\Docs\generated\monster_data\monster_data_corpus_inventory.json",
    [string]$AcgHashInventoryPath = "C:\Users\Mike\Documents\AO stripdown\Docs\generated\playfield_district_info\acghash_global_inventory.json",
    [string]$RawScanPath = "build-verify\acg-monsterdata-resource-audit\effective-resource-raw-scan.bin",
    [string]$OutputPath = "build-verify\acg-monsterdata-resource-audit\official-resource-sources.json"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$RelevantTypes = [ordered]@{
    PlayfieldDistrictInfo = 1000014
    InfoObject = 1000010
    PlayfieldDynels = 1000026
    CatMesh = 1010002
    MonsterData = 1040023
}

$ExpectedEp1Hashes = [ordered]@{
    "ResourceDatabase.dat" = "3cabdede7b9b2468ed22f10f536fb2f7083ea05ed9483e2d96b22cf080d736a6"
    "ResourceDatabase.dat.001" = "f8884a2c382ce7c95f20b4423567f176ed40675ba9ce8362527288712871ba73"
    "ResourceDatabase.dat.002" = "2024021f966c3c8a8c083e01cbad2335ba33c19a1661a148060391755a608cc1"
    "ResourceDatabase.idx" = "ba152f59096d5358f4d1b6511d3a3d264999e0a59f1ab7bf3a7cc18a4888c273"
}

function Get-LowerSha256File([string]$Path) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        return ([System.BitConverter]::ToString($sha.ComputeHash($stream))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $stream.Dispose()
        $sha.Dispose()
    }
}

function Get-LowerSha256Bytes([byte[]]$Bytes) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Swap-UInt32([uint32]$Value) {
    return [uint32]((($Value -band 0x000000FF) -shl 24) -bor
        (($Value -band 0x0000FF00) -shl 8) -bor
        (($Value -band 0x00FF0000) -shr 8) -bor
        (($Value -band 0xFF000000) -shr 24))
}

function Get-DatabaseFiles([string]$ClientPath) {
    $databaseDirectory = Join-Path $ClientPath "cd_image\data\db"
    $indexPath = Join-Path $databaseDirectory "ResourceDatabase.idx"
    $basePath = Join-Path $databaseDirectory "ResourceDatabase.dat"
    if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $basePath -PathType Leaf)) {
        throw "ResourceDatabase inputs are incomplete under $databaseDirectory"
    }
    $segments = @(Get-ChildItem -LiteralPath $databaseDirectory -File |
        Where-Object { $_.Name -eq "ResourceDatabase.dat" -or $_.Name -match '^ResourceDatabase\.dat\.\d{3}$' } |
        Sort-Object @{ Expression = {
            if ($_.Name -eq "ResourceDatabase.dat") { -1 }
            else { [int]$_.Extension.TrimStart('.') }
        } })
    for ($index = 0; $index -lt $segments.Count; $index++) {
        $expectedName = if ($index -eq 0) { "ResourceDatabase.dat" } else { "ResourceDatabase.dat.$($index.ToString('000'))" }
        if ($segments[$index].Name -ne $expectedName) {
            throw "ResourceDatabase segment gap: expected $expectedName, found $($segments[$index].Name)"
        }
    }
    return [ordered]@{
        databaseDirectory = $databaseDirectory
        indexPath = $indexPath
        segments = $segments
    }
}

function Get-ClientInventory([string]$ClientPath, [string]$ExpectedVersion) {
    $files = Get-DatabaseFiles $ClientPath
    $versionPath = Join-Path $ClientPath "version.id"
    $version = if (Test-Path -LiteralPath $versionPath -PathType Leaf) {
        (Get-Content -LiteralPath $versionPath -Raw).Trim()
    }
    else {
        $ExpectedVersion
    }
    if ($version -ne $ExpectedVersion) {
        throw "Unexpected client version under ${ClientPath}: $version"
    }
    $globalStart = [int64]0
    $segments = [System.Collections.Generic.List[object]]::new()
    foreach ($segment in $files.segments) {
        $segments.Add([ordered]@{
            name = $segment.Name
            length = [int64]$segment.Length
            globalStart = $globalStart
            globalEnd = $globalStart + [int64]$segment.Length
            sha256 = Get-LowerSha256File $segment.FullName
        })
        $globalStart += [int64]$segment.Length
    }
    return [ordered]@{
        clientPath = $ClientPath
        version = $version
        index = [ordered]@{
            name = "ResourceDatabase.idx"
            length = [int64](Get-Item -LiteralPath $files.indexPath).Length
            sha256 = Get-LowerSha256File $files.indexPath
        }
        segments = @($segments)
        segmentCount = $segments.Count
        totalSegmentBytes = $globalStart
    }
}

$commonDll = Join-Path $AodbPluginPath "AODB.Common.dll"
$aodbDll = Join-Path $AodbPluginPath "AODB.dll"
foreach ($path in @($commonDll, $aodbDll, $MonsterDataCorpusPath, $AcgHashInventoryPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing AODB input: $path"
    }
}

$ep1Inventory = Get-ClientInventory $Ep1ClientPath "18.8.62_EP1"
$ep2Inventory = Get-ClientInventory $Ep2ClientPath "18.8.62_EP2"
foreach ($entry in $ExpectedEp1Hashes.GetEnumerator()) {
    $actual = if ($entry.Key -eq "ResourceDatabase.idx") {
        $ep1Inventory.index.sha256
    }
    else {
        @($ep1Inventory.segments | Where-Object { $_.name -eq $entry.Key })[0].sha256
    }
    if ($actual -ne $entry.Value) {
        throw "Accepted EP1 ResourceDatabase hash mismatch for $($entry.Key): $actual"
    }
}

Add-Type -Path $commonDll
Add-Type -Path $aodbDll
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;

public sealed class OfficialRawReferenceHit
{
    public int Offset { get; set; }
    public uint Value { get; set; }
    public uint PreviousValue { get; set; }
    public uint NextValue { get; set; }
}

public static class OfficialRawReferenceScanner
{
    public static OfficialRawReferenceHit[] Scan(byte[] data, HashSet<uint> targets)
    {
        var hits = new List<OfficialRawReferenceHit>();
        for (int offset = 0; offset + 4 <= data.Length; offset++)
        {
            uint value = (uint)(data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16)
                | (data[offset + 3] << 24));
            if (targets.Contains(value))
            {
                uint previous = offset >= 4
                    ? (uint)(data[offset - 4] | (data[offset - 3] << 8) | (data[offset - 2] << 16) | (data[offset - 1] << 24))
                    : UInt32.MaxValue;
                uint next = offset + 8 <= data.Length
                    ? (uint)(data[offset + 4] | (data[offset + 5] << 8) | (data[offset + 6] << 16) | (data[offset + 7] << 24))
                    : UInt32.MaxValue;
                hits.Add(new OfficialRawReferenceHit { Offset = offset, Value = value, PreviousValue = previous, NextValue = next });
            }
        }
        return hits.ToArray();
    }
}
"@

$monsterCorpus = Get-Content -LiteralPath $MonsterDataCorpusPath -Raw -Encoding UTF8 | ConvertFrom-Json
$acgInventory = Get-Content -LiteralPath $AcgHashInventoryPath -Raw -Encoding UTF8 | ConvertFrom-Json
$scanTargets = [System.Collections.Generic.HashSet[uint32]]::new()
foreach ($record in @($monsterCorpus.Records)) {
    $value = [uint32]$record.ResourceInstance
    [void]$scanTargets.Add($value)
    [void]$scanTargets.Add((Swap-UInt32 $value))
}
foreach ($tag in @($acgInventory.Tags)) {
    foreach ($value in @($tag.OfficialNativeUInt32Values)) {
        [void]$scanTargets.Add([uint32]$value)
    }
}

$resolvedRawScan = if ([System.IO.Path]::IsPathRooted($RawScanPath)) { $RawScanPath } else { Join-Path (Get-Location) $RawScanPath }
[System.IO.Directory]::CreateDirectory((Split-Path -Parent $resolvedRawScan)) | Out-Null
$rawScanStream = [System.IO.File]::Open($resolvedRawScan, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
$rawScanWriter = [System.IO.BinaryWriter]::new($rawScanStream, [System.Text.Encoding]::UTF8, $false)
$ep1Controller = [AODB.RdbController]::new($Ep1ClientPath)
$ep2Controller = [AODB.RdbController]::new($Ep2ClientPath)
$sha = [System.Security.Cryptography.SHA256]::Create()
try {
    $typeInventory = [System.Collections.Generic.List[object]]::new()
    foreach ($type in @($ep1Controller.RecordTypeToId.Keys | Sort-Object)) {
        $typeInventory.Add([ordered]@{
            resourceType = [int]$type
            recordCount = @($ep1Controller.RecordTypeToId[$type].Keys).Count
        })
    }

    $effectiveResourceCount = 0
    foreach ($item in $typeInventory) {
        $effectiveResourceCount += [int]$item["recordCount"]
    }
    $rawScanWriter.Write([System.Text.Encoding]::ASCII.GetBytes("AOMDREF2"))
    $rawScanWriter.Write([int]2)
    $rawScanWriter.Write([int]$effectiveResourceCount)
    $rawScanFailures = [System.Collections.Generic.List[object]]::new()
    foreach ($type in @($ep1Controller.RecordTypeToId.Keys | Sort-Object)) {
        foreach ($id in @($ep1Controller.RecordTypeToId[$type].Keys | Sort-Object)) {
            $rawScanWriter.Write([int]$type)
            $rawScanWriter.Write([int]$id)
            try {
                $raw = $ep1Controller.GetRaw([int]$type, [int]$id)
                $hash = $sha.ComputeHash($raw)
                $hits = [OfficialRawReferenceScanner]::Scan($raw, $scanTargets)
                $rawScanWriter.Write([int]$raw.Length)
                $rawScanWriter.Write($hash)
                $rawScanWriter.Write([int]$hits.Length)
                foreach ($hit in $hits) {
                    $rawScanWriter.Write([int]$hit.Offset)
                    $rawScanWriter.Write([uint32]$hit.Value)
                    $rawScanWriter.Write([uint32]$hit.PreviousValue)
                    $rawScanWriter.Write([uint32]$hit.NextValue)
                }
            }
            catch {
                $rawScanWriter.Write([int]-1)
                $rawScanWriter.Write((New-Object byte[] 32))
                $rawScanWriter.Write([int]0)
                $rawScanFailures.Add([ordered]@{
                    resourceType = [int]$type
                    resourceInstance = [int]$id
                    error = $_.Exception.Message
                })
            }
        }
    }
    $rawScanWriter.Flush()

    $parity = [System.Collections.Generic.List[object]]::new()
    foreach ($entry in $RelevantTypes.GetEnumerator()) {
        $type = [int]$entry.Value
        $ep1Ids = @($ep1Controller.RecordTypeToId[$type].Keys | Sort-Object)
        $ep2Ids = @($ep2Controller.RecordTypeToId[$type].Keys | Sort-Object)
        $ep2Set = [System.Collections.Generic.HashSet[int]]::new()
        foreach ($id in $ep2Ids) { [void]$ep2Set.Add([int]$id) }
        $mismatches = [System.Collections.Generic.List[int]]::new()
        foreach ($id in $ep1Ids) {
            if (-not $ep2Set.Contains([int]$id)) { continue }
            $ep1Hash = [System.BitConverter]::ToString($sha.ComputeHash($ep1Controller.GetRaw($type, [int]$id)))
            $ep2Hash = [System.BitConverter]::ToString($sha.ComputeHash($ep2Controller.GetRaw($type, [int]$id)))
            if ($ep1Hash -ne $ep2Hash) { $mismatches.Add([int]$id) }
        }
        $ep1Set = [System.Collections.Generic.HashSet[int]]::new()
        foreach ($id in $ep1Ids) { [void]$ep1Set.Add([int]$id) }
        $parity.Add([ordered]@{
            name = [string]$entry.Key
            resourceType = $type
            ep1Records = $ep1Ids.Count
            ep2Records = $ep2Ids.Count
            onlyEp1 = @($ep1Ids | Where-Object { -not $ep2Set.Contains([int]$_) })
            onlyEp2 = @($ep2Ids | Where-Object { -not $ep1Set.Contains([int]$_) })
            rawMismatchCount = $mismatches.Count
            rawMismatchIds = @($mismatches)
        })
    }

    $monsterIds = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($id in $ep1Controller.RecordTypeToId[$RelevantTypes.MonsterData].Keys) {
        [void]$monsterIds.Add([int]$id)
    }
    $dynelCount = 0
    $playfieldDynelResourceRecords = @($ep1Controller.RecordTypeToId[$RelevantTypes.PlayfieldDynels].Keys).Count
    $nonzeroTemplateCount = 0
    $templateMonsterMatches = 0
    $identityMonsterMatches = 0
    $templateIds = [System.Collections.Generic.HashSet[int]]::new()
    $identityTypeCounts = @{}
    foreach ($id in @($ep1Controller.RecordTypeToId[$RelevantTypes.PlayfieldDynels].Keys | Sort-Object)) {
        $playfieldDynels = $ep1Controller.Get($RelevantTypes.PlayfieldDynels, [int]$id)
        foreach ($dynel in @($playfieldDynels.Dynels)) {
            $dynelCount++
            $template = [int]$dynel.TemplateId
            if ($template -ne 0) {
                $nonzeroTemplateCount++
                [void]$templateIds.Add($template)
            }
            if ($monsterIds.Contains($template)) { $templateMonsterMatches++ }
            if ($monsterIds.Contains([int]$dynel.IdentityInstance)) { $identityMonsterMatches++ }
            $identityType = [string][int]$dynel.IdentityType
            if (-not $identityTypeCounts.ContainsKey($identityType)) { $identityTypeCounts[$identityType] = 0 }
            $identityTypeCounts[$identityType]++
        }
    }
    $identityTypes = @($identityTypeCounts.GetEnumerator() | Sort-Object Name | ForEach-Object {
        [ordered]@{ identityType = [int]$_.Name; count = [int]$_.Value }
    })

    $infoObject = $ep1Controller.Get($RelevantTypes.InfoObject, 1)
    $infoObjectTypes = @($infoObject.Types.GetEnumerator() | Sort-Object { [int]$_.Key } | ForEach-Object {
        [ordered]@{ resourceType = [int]$_.Key; namedRecords = [int]$_.Value.Count }
    })
}
finally {
    $rawScanWriter.Dispose()
    $sha.Dispose()
    $ep1Controller.Dispose()
    $ep2Controller.Dispose()
}

$output = [ordered]@{
    schemaVersion = 1
    resourceTypes = $RelevantTypes
    ep1 = $ep1Inventory
    ep2 = $ep2Inventory
    ep1TypeInventory = @($typeInventory)
    relevantTypeParity = @($parity)
    playfieldDynels = [ordered]@{
        resourceRecords = $playfieldDynelResourceRecords
        dynels = $dynelCount
        nonzeroTemplateRows = $nonzeroTemplateCount
        uniqueTemplateIds = $templateIds.Count
        templateIdsMatchingMonsterData = $templateMonsterMatches
        identityInstancesMatchingMonsterData = $identityMonsterMatches
        identityTypes = $identityTypes
    }
    infoObject = [ordered]@{
        resourceRecords = 1
        namedResourceTypes = $infoObjectTypes
        containsMonsterDataRegistry = $false
    }
    semantics = [ordered]@{
        dataSegmentsArePhysicalConcatenation = $true
        dataSegmentsAreExpansionOverlays = $false
        activeResourceSelectionOwner = "ResourceDatabase.idx active B-tree leaf chain"
        duplicateActiveKeyPrecedence = "not observed; duplicate active keys fail closed"
        physicalUnindexedRecords = "not enumerable from the active logical index; reported as not observed rather than zero"
    }
    rawReferenceScan = [ordered]@{
        path = $resolvedRawScan
        sha256 = Get-LowerSha256File $resolvedRawScan
        format = "AOMDREF2 little-endian binary framing with adjacent uint32 context"
        effectiveResourceRecords = [int]$effectiveResourceCount
        targetValues = $scanTargets.Count
        failures = @($rawScanFailures)
    }
    sourceHashes = [ordered]@{
        aodbCommon = Get-LowerSha256File $commonDll
        aodb = Get-LowerSha256File $aodbDll
        monsterDataCorpus = Get-LowerSha256File $MonsterDataCorpusPath
        acgHashInventory = Get-LowerSha256File $AcgHashInventoryPath
    }
}

$resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path (Get-Location) $OutputPath }
[System.IO.Directory]::CreateDirectory((Split-Path -Parent $resolvedOutput)) | Out-Null
$json = $output | ConvertTo-Json -Depth 30
[System.IO.File]::WriteAllText($resolvedOutput, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))

Write-Output "ACG_MONSTERDATA_SOURCE_EXPORT=PASS"
Write-Output "EP1_SEGMENTS=$($ep1Inventory.segmentCount)"
Write-Output "EP2_SEGMENTS=$($ep2Inventory.segmentCount)"
Write-Output "RESOURCE_TYPES=$($typeInventory.Count)"
Write-Output "EFFECTIVE_RESOURCE_RECORDS=$effectiveResourceCount"
Write-Output "RAW_SCAN_FAILURES=$($rawScanFailures.Count)"
Write-Output "PLAYFIELD_DYNELS=$dynelCount"
Write-Output "PLAYFIELD_DYNEL_MONSTERDATA_TEMPLATE_MATCHES=$templateMonsterMatches"
Write-Output "OUTPUT=$resolvedOutput"
