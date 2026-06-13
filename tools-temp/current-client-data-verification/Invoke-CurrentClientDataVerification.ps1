param(
    [string]$RepoRoot = "",
    [string]$AoClientRoot = "C:\Funcom\Anarchy Online",
    [string]$TargetDatabase = "cellao_codex_clean",
    [string]$Database = "",
    [string]$MysqlExe = "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe",
    [string]$MysqlUser = "cellaodbuser",
    [string]$MysqlPassword = "1Guiness4828!",
    [string]$ReportPath = "",
    [string]$OutDir = "",
    [switch]$DryRun,
    [switch]$AllowWrite
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-RepoRoot {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Add-AssemblyIfNeeded {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Missing assembly: $Path"
    }

    Add-Type -Path $Path
}

function Read-DataHeader {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return [pscustomobject]@{
            Path = $Path
            Exists = $false
            Length = 0
            Version = ""
            PackCount = 0
            Capacity = 0
            Slices = 0
            LastWriteTime = $null
        }
    }

    $item = Get-Item -LiteralPath $Path
    $reader = [System.IO.BinaryReader]::new([System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite))
    try {
        $versionLength = [int]$reader.ReadByte()
        $version = -join $reader.ReadChars($versionLength)
        $packCount = $reader.ReadInt32()
        $capacity = $reader.ReadInt32()
        $slices = $reader.ReadInt32()
        return [pscustomobject]@{
            Path = $Path
            Exists = $true
            Length = $item.Length
            Version = $version
            PackCount = $packCount
            Capacity = $capacity
            Slices = $slices
            LastWriteTime = $item.LastWriteTime
        }
    } finally {
        $reader.Dispose()
    }
}

function Invoke-MySqlRows {
    param([string]$Sql)

    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $rows = & $MysqlExe "-u$MysqlUser" "-p$MysqlPassword" $Database --batch --raw --skip-column-names -e $Sql 2>$null
        if ($LASTEXITCODE -ne 0) {
            throw "mysql exited with code $LASTEXITCODE"
        }
        return @($rows)
    } finally {
        $ErrorActionPreference = $oldPreference
    }
}

function Convert-ToUInt32Value {
    param($Value)

    $asInt64 = [int64]$Value
    if ($asInt64 -lt 0) {
        $asInt64 += 4294967296
    }

    return [uint32]$asInt64
}

function Format-Coord {
    param($X, $Y, $Z)
    return "{0:0.###},{1:0.###},{2:0.###}" -f [double]$X, [double]$Y, [double]$Z
}

function Get-ObjectPropertyValue {
    param(
        $Object,
        [string]$PropertyName
    )

    if ($null -eq $Object -or [string]::IsNullOrWhiteSpace($PropertyName)) {
        return ""
    }

    $property = $Object.PSObject.Properties[$PropertyName]
    if ($null -eq $property -or $null -eq $property.Value) {
        return ""
    }

    return [string]$property.Value
}

function Join-DistinctValues {
    param([object[]]$Values)

    $seen = [ordered]@{}
    foreach ($value in $Values) {
        if ($null -eq $value) {
            continue
        }

        $text = [string]$value
        if ([string]::IsNullOrWhiteSpace($text)) {
            continue
        }

        if (-not $seen.Contains($text)) {
            $seen[$text] = $true
        }
    }

    return (@($seen.Keys) -join " | ")
}

function Format-MarkdownCell {
    param($Value)

    if ($null -eq $Value) {
        return ""
    }

    return ([string]$Value).Replace("|", "<br>").Replace("`r", " ").Replace("`n", " ")
}

function Format-SourceDoorSummary {
    param($DoorRow)

    if ($null -eq $DoorRow) {
        return ""
    }

    $sourceName = Get-ObjectPropertyValue $DoorRow "SourcePlayfieldName"
    $sourceDoor = Get-ObjectPropertyValue $DoorRow "SourceInstanceHex"
    $sourceCoords = Get-ObjectPropertyValue $DoorRow "SourceCoords"
    if ([string]::IsNullOrWhiteSpace($sourceName) -and [string]::IsNullOrWhiteSpace($sourceDoor) -and [string]::IsNullOrWhiteSpace($sourceCoords)) {
        return ""
    }

    $doorName = ("$sourceName $sourceDoor").Trim()
    if ([string]::IsNullOrWhiteSpace($sourceCoords)) {
        return $doorName
    }

    if ([string]::IsNullOrWhiteSpace($doorName)) {
        return $sourceCoords
    }

    return "$doorName @ $sourceCoords"
}

function Get-SourceDoorKey {
    param($DoorRow)

    if ($null -eq $DoorRow) {
        return ""
    }

    return ((Get-ObjectPropertyValue $DoorRow "SourcePlayfieldName"),
        (Get-ObjectPropertyValue $DoorRow "SourceInstanceHex"),
        (Get-ObjectPropertyValue $DoorRow "SourceCoords")) -join "|"
}

function Format-DestinationDoorSummary {
    param($DoorRow)

    if ($null -eq $DoorRow) {
        return ""
    }

    $destinationDoor = Get-ObjectPropertyValue $DoorRow "DestinationInstanceHex"
    $destinationCoords = Get-ObjectPropertyValue $DoorRow "DestinationCoords"
    if ([string]::IsNullOrWhiteSpace($destinationDoor) -and [string]::IsNullOrWhiteSpace($destinationCoords)) {
        return ""
    }

    if ([string]::IsNullOrWhiteSpace($destinationCoords)) {
        return $destinationDoor
    }

    if ([string]::IsNullOrWhiteSpace($destinationDoor)) {
        return $destinationCoords
    }

    return "$destinationDoor @ $destinationCoords"
}

function Get-StatValue {
    param($Template, [int]$StatId)

    if ($null -eq $Template -or $null -eq $Template.Stats) {
        return $null
    }

    if ($Template.Stats.ContainsKey($StatId)) {
        return [int]$Template.Stats[$StatId]
    }

    return $null
}

function Get-LiveVendorMeshOverrides {
    param([string]$VendorSourcePath)

    $result = @{}
    if (-not (Test-Path -LiteralPath $VendorSourcePath)) {
        return $result
    }

    foreach ($line in Get-Content -LiteralPath $VendorSourcePath) {
        if ($line -match "\{\s*(\d+)\s*,\s*(\d+)\s*\}") {
            $result[[int]$matches[1]] = [int]$matches[2]
        }
    }

    return $result
}

$coverageExcludedStatelTemplates = @{
    155225 = "NonShopStatelTemplate"
}

function Test-CoverageExcludedStatelTemplate {
    param($TemplateId)

    if ($null -eq $TemplateId -or [string]::IsNullOrWhiteSpace([string]$TemplateId)) {
        return $false
    }

    try {
        return $coverageExcludedStatelTemplates.ContainsKey([int]$TemplateId)
    } catch {
        return $false
    }
}

if (-not [string]::IsNullOrWhiteSpace($Database)) {
    $TargetDatabase = $Database
}
$Database = $TargetDatabase

$RepoRoot = Resolve-RepoRoot $RepoRoot
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $RepoRoot "docs\CurrentClientDataVerification.md"
}
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $RepoRoot "tools-temp\current-client-data-verification"
}

$built = Join-Path $RepoRoot "CellAO\Built\Debug"
$sourceData = Join-Path $RepoRoot "CellAO\Datafiles"
$aoVersionPath = Join-Path $AoClientRoot "version.id"

Write-Host "Resolved current-client verification paths:"
Write-Host "  RepoRoot=$RepoRoot"
Write-Host "  Built=$built"
Write-Host "  SourceData=$sourceData"
Write-Host "  AoClientRoot=$AoClientRoot"
Write-Host "  AoVersionPath=$aoVersionPath"
Write-Host "  TargetDatabase=$Database"
Write-Host "  ReportPath=$ReportPath"
Write-Host "  OutDir=$OutDir"
Write-Host "  IntendedAction=query current database and write verification reports"

if ($DryRun -or -not $AllowWrite) {
    Write-Host "No database query performed and no report files written. Pass -AllowWrite to run verification output generation."
    return
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $ReportPath) -Force | Out-Null

$aoVersion = if (Test-Path -LiteralPath $aoVersionPath) { (Get-Content -LiteralPath $aoVersionPath -Raw).Trim() } else { "" }

$previousCurrentDirectory = [Environment]::CurrentDirectory
[Environment]::CurrentDirectory = $built
Push-Location $built
try {
    Add-AssemblyIfNeeded (Join-Path $built "MsgPack.dll")
    Add-AssemblyIfNeeded (Join-Path $built "SmokeLounge.AOtomation.Messaging.dll")
    Add-AssemblyIfNeeded (Join-Path $built "CellAO.Enums.dll")
    Add-AssemblyIfNeeded (Join-Path $built "CellAO.Interfaces.dll")
    Add-AssemblyIfNeeded (Join-Path $built "CellAO.Core.dll")
    Add-AssemblyIfNeeded (Join-Path $built "PlayfieldLoader.dll")

    [CellAO.Core.Items.ItemLoader]::ItemList.Clear()
    [void][CellAO.Core.Items.ItemLoader]::CacheAllItems((Join-Path $built "items.dat"))
    [CellAO.Core.Nanos.NanoLoader]::NanoList.Clear()
    [void][CellAO.Core.Nanos.NanoLoader]::CacheAllNanos((Join-Path $built "nanos.dat"))
    [ZoneEngine.Core.Playfields.PlayfieldLoader]::PFData.Clear()
    [void][ZoneEngine.Core.Playfields.PlayfieldLoader]::CacheAllPlayfieldData((Join-Path $built "playfields.dat"))
} finally {
    Pop-Location
    [Environment]::CurrentDirectory = $previousCurrentDirectory
}

$dataFiles = foreach ($name in "items.dat", "nanos.dat", "playfields.dat") {
    $sourceHeader = Read-DataHeader (Join-Path $sourceData $name)
    $builtHeader = Read-DataHeader (Join-Path $built $name)
    [pscustomobject]@{
        Name = $name
        SourceVersion = $sourceHeader.Version
        SourceCapacity = $sourceHeader.Capacity
        SourceLength = $sourceHeader.Length
        SourceLastWriteTime = $sourceHeader.LastWriteTime
        BuiltVersion = $builtHeader.Version
        BuiltCapacity = $builtHeader.Capacity
        BuiltLength = $builtHeader.Length
        BuiltLastWriteTime = $builtHeader.LastWriteTime
        Matches = ($sourceHeader.Version -eq $builtHeader.Version -and $sourceHeader.Capacity -eq $builtHeader.Capacity -and $sourceHeader.Length -eq $builtHeader.Length)
    }
}
$dataFilesCsv = Join-Path $OutDir "data-file-version-audit.csv"
$dataFiles | Export-Csv -LiteralPath $dataFilesCsv -NoTypeInformation

$vendorSourcePath = Join-Path $RepoRoot "CellAO\Libraries\Source\CellAO.Core\Entities\Vendor.cs"
$meshOverrides = Get-LiveVendorMeshOverrides $vendorSourcePath
$liveVendorEvidence = @(
    [pscustomobject]@{
        Label = "Basic Bookstore"
        TemplateId = 155599
        LiveMesh = 85976
        Capture = "tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260607-005753"
        Note = "Rome Blue live VendingMachineFullUpdate"
    },
    [pscustomobject]@{
        Label = "Omni Basic Devices"
        TemplateId = 155603
        LiveMesh = 85976
        Capture = "tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260607-005753"
        Note = "Rome Blue live VendingMachineFullUpdate"
    }
)

$vendorMeshAudit = foreach ($evidence in $liveVendorEvidence) {
    $template = $null
    if ([CellAO.Core.Items.ItemLoader]::ItemList.ContainsKey($evidence.TemplateId)) {
        $template = [CellAO.Core.Items.ItemLoader]::ItemList[$evidence.TemplateId]
    }

    $itemMesh = Get-StatValue $template 12
    $overrideMesh = if ($meshOverrides.ContainsKey($evidence.TemplateId)) { $meshOverrides[$evidence.TemplateId] } else { $null }
    $status = "Needs repair"
    if ($itemMesh -eq $evidence.LiveMesh) {
        $status = "Item cache matches live"
    } elseif ($overrideMesh -eq $evidence.LiveMesh) {
        $status = "Runtime override matches live"
    }

    [pscustomobject]@{
        Label = $evidence.Label
        TemplateId = $evidence.TemplateId
        ItemDatMesh = $itemMesh
        LiveMesh = $evidence.LiveMesh
        RuntimeOverrideMesh = $overrideMesh
        Status = $status
        Capture = $evidence.Capture
        Note = $evidence.Note
    }
}
$vendorMeshCsv = Join-Path $OutDir "live-vendor-mesh-audit.csv"
$vendorMeshAudit | Export-Csv -LiteralPath $vendorMeshCsv -NoTypeInformation

$vendorRows = Invoke-MySqlRows @"
SELECT v.Id, v.Playfield, v.TemplateId, v.Hash, COALESCE(vt.ItemTemplate, -1), COALESCE(vt.ShopInvHash, ''), COALESCE(vt.Name, ''), COUNT(sit.Id)
FROM vendors v
LEFT JOIN vendortemplate vt ON vt.Hash = v.Hash
LEFT JOIN shopinventorytemplates sit ON sit.Hash = vt.ShopInvHash
    AND COALESCE(sit.Active, 1) <> 0
    AND COALESCE(sit.MinQl, 1) <= COALESCE(vt.MaxQl, 1)
    AND COALESCE(sit.MaxQl, 1) >= COALESCE(vt.MinQl, 1)
GROUP BY v.Id, v.Playfield, v.TemplateId, v.Hash, vt.ItemTemplate, vt.ShopInvHash, vt.Name
ORDER BY v.Playfield, v.Id;
"@

$dbVendors = foreach ($line in $vendorRows) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $p = $line -split "`t"
    [pscustomobject]@{
        Id = [int64]$p[0]
        Playfield = [int]$p[1]
        DbTemplateId = [int]$p[2]
        Hash = $p[3]
        VendorTemplateItem = [int]$p[4]
        ShopInvHash = $p[5]
        VendorTemplateName = $p[6]
        ActiveShopItems = [int]$p[7]
    }
}

$vendorDbAudit = foreach ($vendor in $dbVendors) {
    $issues = New-Object System.Collections.Generic.List[string]
    if ($vendor.VendorTemplateItem -lt 0) {
        $issues.Add("missing vendortemplate hash")
    } elseif (-not [CellAO.Core.Items.ItemLoader]::ItemList.ContainsKey($vendor.VendorTemplateItem)) {
        $issues.Add("vendortemplate item missing from items.dat")
    }

    if ($vendor.VendorTemplateItem -ge 0 -and $vendor.DbTemplateId -ne $vendor.VendorTemplateItem) {
        $issues.Add("vendors.TemplateId differs from vendortemplate.ItemTemplate")
    }

    if ($vendor.VendorTemplateItem -ge 0 -and $vendor.ActiveShopItems -eq 0) {
        $issues.Add("shop inventory empty or inactive")
    }

    [pscustomobject]@{
        Id = $vendor.Id
        Playfield = $vendor.Playfield
        Hash = $vendor.Hash
        DbTemplateId = $vendor.DbTemplateId
        VendorTemplateItem = $vendor.VendorTemplateItem
        ShopInvHash = $vendor.ShopInvHash
        ActiveShopItems = $vendor.ActiveShopItems
        VendorTemplateName = $vendor.VendorTemplateName
        Issues = ($issues -join "; ")
    }
}
$vendorDbCsv = Join-Path $OutDir "vendor-db-audit.csv"
$vendorDbAudit | Export-Csv -LiteralPath $vendorDbCsv -NoTypeInformation

$shopRows = Invoke-MySqlRows @"
SELECT Id, Hash, COALESCE(LowId, -1), COALESCE(HighId, -1), COALESCE(MinQl, -1), COALESCE(MaxQl, -1), COALESCE(Active, 1), AdminDescription
FROM shopinventorytemplates
ORDER BY Hash, Id;
"@

$shopAudit = foreach ($line in $shopRows) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $p = $line -split "`t", 8
    $low = [int]$p[2]
    $high = [int]$p[3]
    $issues = New-Object System.Collections.Generic.List[string]
    if ($low -gt 0 -and -not [CellAO.Core.Items.ItemLoader]::ItemList.ContainsKey($low)) {
        $issues.Add("LowId missing from items.dat")
    }
    if ($high -gt 0 -and -not [CellAO.Core.Items.ItemLoader]::ItemList.ContainsKey($high)) {
        $issues.Add("HighId missing from items.dat")
    }
    if ($low -gt 0 -and $high -gt 0) {
        $lowTemplate = $null
        $highTemplate = $null
        if ([CellAO.Core.Items.ItemLoader]::ItemList.ContainsKey($low)) { $lowTemplate = [CellAO.Core.Items.ItemLoader]::ItemList[$low] }
        if ([CellAO.Core.Items.ItemLoader]::ItemList.ContainsKey($high)) { $highTemplate = [CellAO.Core.Items.ItemLoader]::ItemList[$high] }
        if ($null -ne $lowTemplate -and $null -ne $highTemplate -and $lowTemplate.Quality -gt $highTemplate.Quality) {
            $issues.Add("LowId quality is greater than HighId quality")
        }
    }

    [pscustomobject]@{
        Id = [int]$p[0]
        Hash = $p[1]
        LowId = $low
        HighId = $high
        MinQl = [int]$p[4]
        MaxQl = [int]$p[5]
        Active = [int]$p[6]
        AdminDescription = $p[7]
        Issues = ($issues -join "; ")
    }
}
$shopCsv = Join-Path $OutDir "shop-inventory-template-audit.csv"
$shopAudit | Export-Csv -LiteralPath $shopCsv -NoTypeInformation

$dbVendorByComputedId = @{}
foreach ($vendor in $dbVendors) {
    $dbVendorByComputedId["$($vendor.Playfield):$($vendor.Id)"] = $vendor
}

$vendingMachineType = 51035
$statelVendorRows = New-Object System.Collections.Generic.List[object]
foreach ($pfEntry in [ZoneEngine.Core.Playfields.PlayfieldLoader]::PFData.GetEnumerator()) {
    $pfId = [int]$pfEntry.Key
    $pf = $pfEntry.Value
    foreach ($sd in $pf.Statels) {
        if ([int]$sd.Identity.Type -ne $vendingMachineType) {
            continue
        }

        $instance = Convert-ToUInt32Value $sd.Identity.Instance
        $computedId = [int64]((($instance -shr 16) -band 0xff) -bor ($pfId -shl 16))
        $key = "$($pfId):$($computedId)"
        $matched = $dbVendorByComputedId.ContainsKey($key)
        $dbVendor = if ($matched) { $dbVendorByComputedId[$key] } else { $null }
        $issues = New-Object System.Collections.Generic.List[string]
        if (-not $matched) {
            $issues.Add("no vendors row; runtime spawns empty statel vendor")
        } elseif ($dbVendor.ActiveShopItems -eq 0) {
            $issues.Add("matched vendor has no active shop inventory")
        }

        $coverageExcluded = Test-CoverageExcludedStatelTemplate $sd.TemplateId
        $exclusionReason = if ($coverageExcluded) { $coverageExcludedStatelTemplates[[int]$sd.TemplateId] } else { "" }

        $statelVendorRows.Add([pscustomobject]@{
            Playfield = $pfId
            PlayfieldName = $pf.Name
            ComputedVendorId = $computedId
            StatelInstanceHex = ("0x{0:X8}" -f $instance)
            StatelTemplateId = $sd.TemplateId
            Coords = Format-Coord $sd.X $sd.Y $sd.Z
            HasVendorRow = $matched
            VendorHash = if ($matched) { $dbVendor.Hash } else { "" }
            VendorTemplateItem = if ($matched) { $dbVendor.VendorTemplateItem } else { -1 }
            ActiveShopItems = if ($matched) { $dbVendor.ActiveShopItems } else { 0 }
            CoverageExcluded = $coverageExcluded
            ExclusionReason = $exclusionReason
            Issues = ($issues -join "; ")
        })
    }
}
$statelVendorCsv = Join-Path $OutDir "statel-vendor-coverage.csv"
$statelVendorRows | Sort-Object Playfield, ComputedVendorId | Export-Csv -LiteralPath $statelVendorCsv -NoTypeInformation

$problemDataFiles = @($dataFiles | Where-Object { -not $_.Matches -or $_.BuiltVersion -ne $aoVersion })
$problemVendorDb = @($vendorDbAudit | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Issues) })
$problemShopItems = @($shopAudit | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Issues) })
$excludedStatelVendors = @($statelVendorRows | Where-Object { $_.CoverageExcluded })
$problemStatelVendors = @($statelVendorRows | Where-Object { -not $_.CoverageExcluded -and -not [string]::IsNullOrWhiteSpace($_.Issues) })
$problemVendorMesh = @($vendorMeshAudit | Where-Object { $_.Status -ne "Item cache matches live" })

$teleportAuditCsv = Join-Path $RepoRoot "tools-temp\playfield-teleport-audit.csv"
$remapCandidatesCsv = Join-Path $RepoRoot "tools-temp\playfield-remap-ranked-candidates.csv"
$namedLocationsCsv = Join-Path $OutDir "vendor-scan-route-named-locations.csv"

$teleportAuditRows = @()
if (Test-Path -LiteralPath $teleportAuditCsv) {
    $teleportAuditRows = @(Import-Csv -LiteralPath $teleportAuditCsv)
}

$remapCandidateRows = @()
if (Test-Path -LiteralPath $remapCandidatesCsv) {
    $remapCandidateRows = @(Import-Csv -LiteralPath $remapCandidatesCsv)
}

$namedLocationRows = @()
if (Test-Path -LiteralPath $namedLocationsCsv) {
    $namedLocationRows = @(Import-Csv -LiteralPath $namedLocationsCsv)
}

$remapByDestinationPlayfield = @{}
foreach ($candidate in $remapCandidateRows) {
    if ([string]::IsNullOrWhiteSpace($candidate.DestinationPlayfield)) {
        continue
    }

    $remapByDestinationPlayfield[[int]$candidate.DestinationPlayfield] = $candidate
}

$namedLocationByInternalPlayfield = @{}
foreach ($namedLocation in $namedLocationRows) {
    if ([string]::IsNullOrWhiteSpace($namedLocation.InternalPlayfield)) {
        continue
    }

    $internalPlayfield = [int]$namedLocation.InternalPlayfield
    if (-not $namedLocationByInternalPlayfield.ContainsKey($internalPlayfield)) {
        $namedLocationByInternalPlayfield[$internalPlayfield] = $namedLocation
    }
}

$vendorScanTargetsCsv = Join-Path $OutDir "vendor-scan-targets.csv"
$existingVendorScanTargetsByKey = @{}
if (Test-Path -LiteralPath $vendorScanTargetsCsv) {
    $vendorScanTargetRows = @(Import-Csv -LiteralPath $vendorScanTargetsCsv)
    foreach ($targetRow in $vendorScanTargetRows) {
        $targetKey = "$($targetRow.Playfield):$($targetRow.VendorId):$($targetRow.StatelInstanceHex)"
        $existingVendorScanTargetsByKey[$targetKey] = $targetRow
    }
}
$baseVendorScanTargets = foreach ($row in $problemStatelVendors | Sort-Object Playfield, ComputedVendorId) {
    $targetKey = "$($row.Playfield):$($row.ComputedVendorId):$($row.StatelInstanceHex)"
    $existingTarget = if ($existingVendorScanTargetsByKey.ContainsKey($targetKey)) { $existingVendorScanTargetsByKey[$targetKey] } else { $null }

    [pscustomobject]@{
        Priority = if ($null -ne $existingTarget) { $existingTarget.Priority } else { 5 }
        Playfield = $row.Playfield
        PlayfieldName = $row.PlayfieldName
        Coordinates = $row.Coords
        VendorId = $row.ComputedVendorId
        StatelInstanceHex = $row.StatelInstanceHex
        TemplateId = $row.StatelTemplateId
        TemplateName = if ($null -ne $existingTarget) { $existingTarget.TemplateName } else { "" }
        Family = if ($null -ne $existingTarget) { $existingTarget.Family } else { "Unclassified" }
        CaptureInstruction = if ($null -ne $existingTarget) { $existingTarget.CaptureInstruction } else { "Review current statel vendor coverage before capture." }
        AccessStatus = if ($null -ne $existingTarget) { $existingTarget.AccessStatus } else { "Unclassified" }
    }
}

$vendorScanDoorLocationsCsv = Join-Path $OutDir "vendor-scan-door-locations.csv"
$doorEvidenceByPlayfield = @{}
$doorLocationRows = foreach ($group in @($baseVendorScanTargets | Group-Object Playfield | Sort-Object {[int]$_.Name})) {
    $playfield = [int]$group.Name
    $firstTarget = $group.Group | Select-Object -First 1
    $inboundDoors = @($teleportAuditRows |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.DestinationPlayfield) -and [int]$_.DestinationPlayfield -eq $playfield } |
        Sort-Object SourcePlayfieldName, SourceCoords, SourceInstanceHex)
    $namedLocation = if ($namedLocationByInternalPlayfield.ContainsKey($playfield)) { $namedLocationByInternalPlayfield[$playfield] } else { $null }
    $routeDoorCoords = Get-ObjectPropertyValue $namedLocation "DoorCoordinates"
    $routeNamedLocation = Get-ObjectPropertyValue $namedLocation "NamedLocation"
    $primaryDoor = $null
    if (-not [string]::IsNullOrWhiteSpace($routeDoorCoords)) {
        $matchingDoors = @($inboundDoors | Where-Object { (Get-ObjectPropertyValue $_ "SourceCoords") -eq $routeDoorCoords } | Select-Object -First 1)
        if ($matchingDoors.Count -gt 0) {
            $primaryDoor = $matchingDoors[0]
        }
    }
    if ($null -eq $primaryDoor -and -not [string]::IsNullOrWhiteSpace($routeNamedLocation)) {
        $matchingDoors = @($inboundDoors | Where-Object { (Get-ObjectPropertyValue $_ "SourcePlayfieldName") -eq $routeNamedLocation } | Select-Object -First 1)
        if ($matchingDoors.Count -gt 0) {
            $primaryDoor = $matchingDoors[0]
        }
    }
    if ($null -eq $primaryDoor -and $inboundDoors.Count -gt 0) {
        $primaryDoor = $inboundDoors[0]
    }

    $primaryDoorKey = Get-SourceDoorKey $primaryDoor
    $otherDoors = @($inboundDoors | Where-Object {
        $doorKey = Get-SourceDoorKey $_
        $doorKey -ne $primaryDoorKey
    })
    $remapCandidate = if ($remapByDestinationPlayfield.ContainsKey($playfield)) { $remapByDestinationPlayfield[$playfield] } else { $null }
    $sourceDoorSummary = Join-DistinctValues @($inboundDoors | ForEach-Object { Format-SourceDoorSummary $_ })
    $otherDoorSummary = Join-DistinctValues @($otherDoors | ForEach-Object { Format-SourceDoorSummary $_ })
    $destinationDoorSummary = Join-DistinctValues @($inboundDoors | ForEach-Object { Format-DestinationDoorSummary $_ })
    $warnings = Join-DistinctValues @($inboundDoors | ForEach-Object { Get-ObjectPropertyValue $_ "Warnings" })

    $doorRow = [pscustomobject]@{
        Playfield = $playfield
        PlayfieldName = $firstTarget.PlayfieldName
        AccessStatus = $firstTarget.AccessStatus
        UncoveredTerminals = $group.Count
        RouteNamedLocation = $routeNamedLocation
        RouteShopName = Get-ObjectPropertyValue $namedLocation "ShopName"
        RouteDoorCoords = $routeDoorCoords
        SourceDoorCount = $inboundDoors.Count
        PrimarySourceDoorSummary = Format-SourceDoorSummary $primaryDoor
        PrimarySourcePlayfield = Get-ObjectPropertyValue $primaryDoor "SourcePlayfieldName"
        PrimarySourceDoor = Get-ObjectPropertyValue $primaryDoor "SourceInstanceHex"
        PrimarySourceCoords = Get-ObjectPropertyValue $primaryDoor "SourceCoords"
        OtherSourceDoors = $otherDoorSummary
        AllSourceDoors = $sourceDoorSummary
        DestinationDoor = $destinationDoorSummary
        DestinationDoorCoords = Join-DistinctValues @($inboundDoors | ForEach-Object { Get-ObjectPropertyValue $_ "DestinationCoords" })
        TeleportWarnings = $warnings
        PairedDoorCurrent = Get-ObjectPropertyValue $remapCandidate "CurrentDoor"
        PairedDoorCurrentCoords = Get-ObjectPropertyValue $remapCandidate "CurrentCoords"
        PairedDoorCandidate = Get-ObjectPropertyValue $remapCandidate "CandidateDoor"
        PairedDoorCandidateCoords = Get-ObjectPropertyValue $remapCandidate "CandidateCoords"
        PairedDoorDistance = Get-ObjectPropertyValue $remapCandidate "Distance"
        PairedDoorConfidence = Get-ObjectPropertyValue $remapCandidate "Confidence"
        PairedDoorReason = Get-ObjectPropertyValue $remapCandidate "Reason"
        PairedDoorNextAction = Get-ObjectPropertyValue $remapCandidate "NextAction"
    }
    $doorEvidenceByPlayfield[$playfield] = $doorRow
    $doorRow
}
$doorLocationRows | Export-Csv -LiteralPath $vendorScanDoorLocationsCsv -NoTypeInformation

$currentVendorScanTargets = foreach ($target in $baseVendorScanTargets) {
    $doorEvidence = if ($doorEvidenceByPlayfield.ContainsKey([int]$target.Playfield)) { $doorEvidenceByPlayfield[[int]$target.Playfield] } else { $null }

    [pscustomobject]@{
        Priority = $target.Priority
        Playfield = $target.Playfield
        PlayfieldName = $target.PlayfieldName
        Coordinates = $target.Coordinates
        VendorId = $target.VendorId
        StatelInstanceHex = $target.StatelInstanceHex
        TemplateId = $target.TemplateId
        TemplateName = $target.TemplateName
        Family = $target.Family
        CaptureInstruction = $target.CaptureInstruction
        AccessStatus = $target.AccessStatus
        PrimarySourcePlayfield = Get-ObjectPropertyValue $doorEvidence "PrimarySourcePlayfield"
        PrimarySourceDoor = Get-ObjectPropertyValue $doorEvidence "PrimarySourceDoor"
        PrimarySourceCoords = Get-ObjectPropertyValue $doorEvidence "PrimarySourceCoords"
        DestinationDoor = Get-ObjectPropertyValue $doorEvidence "DestinationDoor"
        DestinationDoorCoords = Get-ObjectPropertyValue $doorEvidence "DestinationDoorCoords"
        PairedDoorCandidate = Get-ObjectPropertyValue $doorEvidence "PairedDoorCandidate"
        PairedDoorCandidateCoords = Get-ObjectPropertyValue $doorEvidence "PairedDoorCandidateCoords"
        DoorEvidenceNote = Get-ObjectPropertyValue $doorEvidence "PairedDoorNextAction"
    }
}
$currentVendorScanTargets | Export-Csv -LiteralPath $vendorScanTargetsCsv -NoTypeInformation

$vendorScanTargetsByLocationMd = Join-Path $OutDir "vendor-scan-targets-by-location.md"
$targetMarkdown = New-Object System.Collections.Generic.List[string]
$targetMarkdown.Add("# Remaining Vendor Scan Targets")
$targetMarkdown.Add("")
$targetMarkdown.Add("Generated from current vendor coverage after excluding non-shop statel templates. Actionable uncovered statel vendors: $($currentVendorScanTargets.Count).")
$targetMarkdown.Add("")
$targetMarkdown.Add("## Practical Location Summary")
$targetMarkdown.Add("")
$targetMarkdown.Add("| Playfield | Location | Access | Uncovered terminals | Main families |")
$targetMarkdown.Add("| ---: | --- | --- | ---: | --- |")
$targetGroups = @($currentVendorScanTargets | Group-Object Playfield | Sort-Object -Property @{ Expression = "Count"; Descending = $true }, @{ Expression = "Name"; Descending = $false })
foreach ($group in $targetGroups) {
    $firstTarget = $group.Group | Select-Object -First 1
    $familySummary = (($group.Group | Group-Object Family | Sort-Object -Property @{ Expression = "Count"; Descending = $true }, @{ Expression = "Name"; Descending = $false } | Select-Object -First 4 | ForEach-Object { "$($_.Name) ($($_.Count))" }) -join "; ")
    $targetMarkdown.Add("| $($firstTarget.Playfield) | $($firstTarget.PlayfieldName) | $($firstTarget.AccessStatus) | $($group.Count) | $familySummary |")
}
$targetMarkdown.Add("")
$targetMarkdown.Add("## Door Evidence By Target Playfield")
$targetMarkdown.Add("")
$targetMarkdown.Add("Generated from `playfield-teleport-audit.csv` and `playfield-remap-ranked-candidates.csv`. Source doors are live navigation evidence; destination and paired-door fields are zoning/remap evidence when internal zoning lands on the wrong side of a door pair.")
$targetMarkdown.Add("")
$targetMarkdown.Add("| Playfield | Location | Primary source door | Other source doors | Destination door(s) | Paired-door candidate | Note |")
$targetMarkdown.Add("| ---: | --- | --- | --- | --- | --- | --- |")
foreach ($doorRow in $doorLocationRows | Sort-Object -Property @{ Expression = "UncoveredTerminals"; Descending = $true }, @{ Expression = "Playfield"; Descending = $false }) {
    $primarySource = $doorRow.PrimarySourceDoorSummary
    $pairedDoor = if ([string]::IsNullOrWhiteSpace($doorRow.PairedDoorCandidate)) { "" } else { "$($doorRow.PairedDoorCurrent) @ $($doorRow.PairedDoorCurrentCoords) -> $($doorRow.PairedDoorCandidate) @ $($doorRow.PairedDoorCandidateCoords)" }
    $noteParts = @()
    if (-not [string]::IsNullOrWhiteSpace($doorRow.PairedDoorConfidence)) {
        $noteParts += "$($doorRow.PairedDoorConfidence): $($doorRow.PairedDoorReason)"
    }
    if (-not [string]::IsNullOrWhiteSpace($doorRow.PairedDoorNextAction)) {
        $noteParts += $doorRow.PairedDoorNextAction
    }
    if (-not [string]::IsNullOrWhiteSpace($doorRow.TeleportWarnings)) {
        $noteParts += "Warnings: $($doorRow.TeleportWarnings)"
    }
    $targetMarkdown.Add("| $($doorRow.Playfield) | $(Format-MarkdownCell $doorRow.PlayfieldName) | $(Format-MarkdownCell $primarySource) | $(Format-MarkdownCell $doorRow.OtherSourceDoors) | $(Format-MarkdownCell $doorRow.DestinationDoor) | $(Format-MarkdownCell $pairedDoor) | $(Format-MarkdownCell ($noteParts -join " ")) |")
}
$targetMarkdown.Add("")
$targetMarkdown.Add("## Targets By Location")
foreach ($group in $targetGroups) {
    $firstTarget = $group.Group | Select-Object -First 1
    $doorEvidence = if ($doorEvidenceByPlayfield.ContainsKey([int]$firstTarget.Playfield)) { $doorEvidenceByPlayfield[[int]$firstTarget.Playfield] } else { $null }
    $targetMarkdown.Add("")
    $targetMarkdown.Add("### $($firstTarget.Playfield) - $($firstTarget.PlayfieldName) ($($group.Count))")
    $targetMarkdown.Add("")
    if ($null -ne $doorEvidence) {
        if (-not [string]::IsNullOrWhiteSpace($doorEvidence.PrimarySourceDoorSummary)) {
            $targetMarkdown.Add("- Primary source door: $($doorEvidence.PrimarySourceDoorSummary)")
        }
        if (-not [string]::IsNullOrWhiteSpace($doorEvidence.OtherSourceDoors)) {
            $targetMarkdown.Add("- Other source doors: $($doorEvidence.OtherSourceDoors)")
        }
        if (-not [string]::IsNullOrWhiteSpace($doorEvidence.DestinationDoor)) {
            $targetMarkdown.Add("- Destination door evidence: $($doorEvidence.DestinationDoor)")
        }
        if (-not [string]::IsNullOrWhiteSpace($doorEvidence.PairedDoorCandidate)) {
            $targetMarkdown.Add("- Paired-door candidate: $($doorEvidence.PairedDoorCurrent) @ $($doorEvidence.PairedDoorCurrentCoords) -> $($doorEvidence.PairedDoorCandidate) @ $($doorEvidence.PairedDoorCandidateCoords) ($($doorEvidence.PairedDoorConfidence); $($doorEvidence.PairedDoorReason))")
        }
        $targetMarkdown.Add("")
    }
    $targetMarkdown.Add("| Priority | Vendor ID | Template | Name | Coords | Family |")
    $targetMarkdown.Add("| ---: | ---: | ---: | --- | --- | --- |")
    foreach ($target in $group.Group | Sort-Object Priority, VendorId) {
        $targetMarkdown.Add("| $($target.Priority) | $($target.VendorId) | $($target.TemplateId) | $($target.TemplateName) | $($target.Coordinates) | $($target.Family) |")
    }
}
$targetMarkdown | Set-Content -LiteralPath $vendorScanTargetsByLocationMd -Encoding UTF8

$markdown = New-Object System.Collections.Generic.List[string]
$markdown.Add("# Current Client Data Verification")
$markdown.Add("")
$markdown.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$markdown.Add("")
$markdown.Add("## Scope")
$markdown.Add("")
$markdown.Add("- AO client root: $AoClientRoot")
$markdown.Add("- AO client version.id: $aoVersion")
$markdown.Add("- CellAO repo: $RepoRoot")
$markdown.Add("- Database read: $Database only")
$markdown.Add("")
$markdown.Add("## Summary")
$markdown.Add("")
$markdown.Add("| Area | Result |")
$markdown.Add("| --- | ---: |")
$markdown.Add("| Built item templates | $([CellAO.Core.Items.ItemLoader]::ItemList.Count) |")
$markdown.Add("| Built nano formulas | $([CellAO.Core.Nanos.NanoLoader]::NanoList.Count) |")
$markdown.Add("| Built playfields | $([ZoneEngine.Core.Playfields.PlayfieldLoader]::PFData.Count) |")
$markdown.Add("| Data file source/runtime/version issues | $($problemDataFiles.Count) |")
$markdown.Add("| Live vendor mesh evidence rows not satisfied by item cache | $($problemVendorMesh.Count) |")
$markdown.Add("| Vendor DB rows with issues | $($problemVendorDb.Count) |")
$markdown.Add("| Shop inventory rows with item-cache issues | $($problemShopItems.Count) |")
$markdown.Add("| Vending statels without complete DB shop coverage | $($problemStatelVendors.Count) |")
$markdown.Add("| Vending statels excluded from coverage | $($excludedStatelVendors.Count) |")
$markdown.Add("")
$markdown.Add("## Latest Vendor Import Milestone")
$markdown.Add("")
$markdown.Add("- Arete ICC implant/cluster import promoted from AOSharp capture `20260613-172753`.")
$markdown.Add("- Validated coverage added: 5 vendor rows in `6553 Arete Landing`, 5 vendor templates, and 5 new shop inventory groups with 573 inventory rows.")
$markdown.Add("- The imported rows cover ICC Basic Implants, ICC Faded Clusters, ICC Bright Clusters, ICC Shiny Clusters, and ICC Pharmacy; incidental nearby capture evidence was intentionally excluded.")
$markdown.Add(("- Current-client verification after import showed actionable uncovered statel vendors dropped from `147` to `{0}`. Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> {0}`." -f $problemStatelVendors.Count))
$markdown.Add("")
$markdown.Add("## Coverage Exclusions")
$markdown.Add("")
$markdown.Add("| Template | Name | Reason | Excluded statels | Evidence |")
$markdown.Add("| ---: | --- | --- | ---: | --- |")
$markdown.Add("| 155225 | Refreshing Drink | NonShopStatelTemplate | $($excludedStatelVendors.Count) | AOSharp captures 20260612-012644 and 20260612-044234 emitted VendorFullUpdate evidence but no ShopUpdate inventory rows; live operator confirmed the Superior instances were not reachable/openable. |")
$markdown.Add("")
$markdown.Add("Excluded statels remain in the raw statel vendor coverage CSV with CoverageExcluded and ExclusionReason fields, but they are excluded from coverage metrics, missing-vendor reports, capture targeting, and import planning.")
$markdown.Add("")
$markdown.Add("## Data Files")
$markdown.Add("")
$markdown.Add("| File | Source version | Source rows | Built version | Built rows | Matches |")
$markdown.Add("| --- | --- | ---: | --- | ---: | --- |")
foreach ($row in $dataFiles) {
    $markdown.Add("| $($row.Name) | $($row.SourceVersion) | $($row.SourceCapacity) | $($row.BuiltVersion) | $($row.BuiltCapacity) | $($row.Matches) |")
}
$markdown.Add("")
$markdown.Add("Direct implication: source and built data must match before committing data alignment work. A fresh dev pull uses source data, while Mike's current running folder can contain extractor-generated files.")
$markdown.Add("")
$markdown.Add("## Live Vendor Mesh Evidence")
$markdown.Add("")
$markdown.Add("| Label | Template | items.dat mesh | Live mesh | Runtime override | Status |")
$markdown.Add("| --- | ---: | ---: | ---: | ---: | --- |")
foreach ($row in $vendorMeshAudit) {
    $markdown.Add("| $($row.Label) | $($row.TemplateId) | $($row.ItemDatMesh) | $($row.LiveMesh) | $($row.RuntimeOverrideMesh) | $($row.Status) |")
}
$markdown.Add("")
$markdown.Add("This separates template-cache truth from live runtime instance truth. If items.dat still differs from live but the runtime override matches, that is not an item import failure.")
$markdown.Add("")
$markdown.Add("## Highest Value Repair Targets")
$markdown.Add("")
if ($problemDataFiles.Count -gt 0) {
    $markdown.Add("1. Normalize source/runtime data files so clean rebuilds do not diverge.")
} else {
    $markdown.Add("1. Source/runtime data files are aligned.")
}
if ($problemVendorDb.Count -gt 0) {
    $markdown.Add("2. Fix vendor DB rows with missing template/shop coverage before chasing individual shop terminal behavior.")
} else {
    $markdown.Add("2. Vendor DB rows have template/shop coverage.")
}
if ($problemStatelVendors.Count -gt 0) {
    $markdown.Add("3. Review statel vendors with no DB row; these spawn as empty statel vendors and can produce missing shop entry behavior.")
} else {
    $markdown.Add("3. Current vending statels have DB coverage.")
}
if ($problemShopItems.Count -gt 0) {
    $markdown.Add("4. Repair shop inventory rows whose item IDs do not exist in the current item cache.")
} else {
    $markdown.Add("4. Shop inventory item IDs resolve in current item cache.")
}
$markdown.Add("")
$markdown.Add("## Report Files")
$markdown.Add("")
$markdown.Add("- Data file audit: $dataFilesCsv")
$markdown.Add("- Live vendor mesh audit: $vendorMeshCsv")
$markdown.Add("- Vendor DB audit: $vendorDbCsv")
$markdown.Add("- Shop inventory audit: $shopCsv")
$markdown.Add("- Statel vendor coverage: $statelVendorCsv")
$markdown.Add("- Vendor scan door locations: $vendorScanDoorLocationsCsv")
$markdown.Add("")
$markdown.Add("## Top Vendor DB Issues")
$markdown.Add("")
if ($problemVendorDb.Count -eq 0) {
    $markdown.Add("No vendor DB issues found.")
} else {
    $markdown.Add("| Id | Playfield | Hash | DB template | Template item | Shop hash | Active items | Issues |")
    $markdown.Add("| ---: | ---: | --- | ---: | ---: | --- | ---: | --- |")
    foreach ($row in $problemVendorDb | Select-Object -First 25) {
        $markdown.Add("| $($row.Id) | $($row.Playfield) | $($row.Hash) | $($row.DbTemplateId) | $($row.VendorTemplateItem) | $($row.ShopInvHash) | $($row.ActiveShopItems) | $($row.Issues) |")
    }
}
$markdown.Add("")
$markdown.Add("## Top Statel Vendor Coverage Issues")
$markdown.Add("")
if ($problemStatelVendors.Count -eq 0) {
    $markdown.Add("No statel vendor coverage issues found.")
} else {
    $markdown.Add("| Playfield | Name | Vendor id | Statel | Template | Coords | Issues |")
    $markdown.Add("| ---: | --- | ---: | --- | ---: | --- | --- |")
    foreach ($row in $problemStatelVendors | Sort-Object Playfield, ComputedVendorId | Select-Object -First 40) {
        $markdown.Add("| $($row.Playfield) | $($row.PlayfieldName) | $($row.ComputedVendorId) | $($row.StatelInstanceHex) | $($row.StatelTemplateId) | $($row.Coords) | $($row.Issues) |")
    }
}

$markdown | Set-Content -LiteralPath $ReportPath -Encoding UTF8

[pscustomobject]@{
    ReportPath = $ReportPath
    DataFilesCsv = $dataFilesCsv
    VendorMeshCsv = $vendorMeshCsv
    VendorDbCsv = $vendorDbCsv
    ShopInventoryCsv = $shopCsv
    StatelVendorCsv = $statelVendorCsv
    VendorScanDoorLocationsCsv = $vendorScanDoorLocationsCsv
    DataFileIssues = $problemDataFiles.Count
    VendorDbIssues = $problemVendorDb.Count
    ShopInventoryIssues = $problemShopItems.Count
    StatelVendorIssues = $problemStatelVendors.Count
    StatelVendorExclusions = $excludedStatelVendors.Count
}
