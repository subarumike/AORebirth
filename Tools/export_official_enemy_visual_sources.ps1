param(
    [string]$SourceClientPath = "C:\Users\Mike\Documents\AO stripdown\Anarchy Online",
    [string]$AodbPluginPath = "C:\Users\Mike\Documents\AO Decompiler\AO-Model-Viewer\Assets\Plugins",
    [string]$MonsterDataCorpusPath = "C:\Users\Mike\Documents\AO stripdown\Docs\generated\monster_data\monster_data_corpus_inventory.json",
    [string]$CatMeshMapPath = "C:\Users\Mike\Documents\AO Decompiler\AO-Model-Viewer\Assets\Resources\CatMeshToMonsterData.txt",
    [string]$OutputPath = "build-verify\enemy-archetype-census\official-enemy-visual-sources.json"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ExpectedDatabaseHashes = [ordered]@{
    "ResourceDatabase.dat" = "3cabdede7b9b2468ed22f10f536fb2f7083ea05ed9483e2d96b22cf080d736a6"
    "ResourceDatabase.dat.001" = "f8884a2c382ce7c95f20b4423567f176ed40675ba9ce8362527288712871ba73"
    "ResourceDatabase.dat.002" = "2024021f966c3c8a8c083e01cbad2335ba33c19a1661a148060391755a608cc1"
    "ResourceDatabase.idx" = "ba152f59096d5358f4d1b6511d3a3d264999e0a59f1ab7bf3a7cc18a4888c273"
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

function Get-LowerSha256File([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-JsonSha256($Value) {
    $json = ConvertTo-Json -InputObject $Value -Depth 30 -Compress
    return Get-LowerSha256Bytes ([System.Text.Encoding]::UTF8.GetBytes($json))
}

function Write-NullableString([System.IO.BinaryWriter]$Writer, $Value) {
    if ($null -eq $Value) {
        $Writer.Write([byte]0)
        return
    }
    $Writer.Write([byte]1)
    $Writer.Write([string]$Value)
}

function Write-Vector2([System.IO.BinaryWriter]$Writer, $Value) {
    $Writer.Write([single]$Value.X)
    $Writer.Write([single]$Value.Y)
}

function Write-Vector3([System.IO.BinaryWriter]$Writer, $Value) {
    $Writer.Write([single]$Value.X)
    $Writer.Write([single]$Value.Y)
    $Writer.Write([single]$Value.Z)
}

function Write-Quaternion([System.IO.BinaryWriter]$Writer, $Value) {
    $Writer.Write([single]$Value.X)
    $Writer.Write([single]$Value.Y)
    $Writer.Write([single]$Value.Z)
    $Writer.Write([single]$Value.W)
}

function Write-Color([System.IO.BinaryWriter]$Writer, $Value) {
    $Writer.Write([single]$Value.R)
    $Writer.Write([single]$Value.G)
    $Writer.Write([single]$Value.B)
    $Writer.Write([single]$Value.A)
}

function Complete-WriterHash([System.IO.MemoryStream]$Stream, [System.IO.BinaryWriter]$Writer) {
    $Writer.Flush()
    return Get-LowerSha256Bytes $Stream.ToArray()
}

function Get-JointSignature($CatMesh) {
    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream, [System.Text.Encoding]::UTF8, $true)
    try {
        $joints = @($CatMesh.Joints)
        $writer.Write([int]$joints.Count)
        foreach ($joint in $joints) {
            Write-NullableString $writer $joint.Name
            $writer.Write([single]$joint.Scale)
            $children = @($joint.ChildJoints)
            $writer.Write([int]$children.Count)
            foreach ($child in $children) {
                $writer.Write([int]$child)
            }
        }
        return Complete-WriterHash $stream $writer
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function Get-GeometrySignature($CatMesh) {
    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream, [System.Text.Encoding]::UTF8, $true)
    try {
        $joints = @($CatMesh.Joints)
        $writer.Write([int]$joints.Count)
        foreach ($joint in $joints) {
            Write-NullableString $writer $joint.Name
            $writer.Write([single]$joint.Scale)
            $children = @($joint.ChildJoints)
            $writer.Write([int]$children.Count)
            foreach ($child in $children) {
                $writer.Write([int]$child)
            }
        }

        $groups = @($CatMesh.MeshGroups)
        $writer.Write([int]$groups.Count)
        foreach ($group in $groups) {
            Write-NullableString $writer $group.Name
            $writer.Write([int]$group.Unk)
            $meshes = @($group.Meshes)
            $writer.Write([int]$meshes.Count)
            foreach ($mesh in $meshes) {
                $writer.Write([int]$mesh.MaterialId)
                $vertices = @($mesh.Vertices)
                $writer.Write([int]$vertices.Count)
                $triangles = @($mesh.Triangles)
                $writer.Write([int]$triangles.Count)
            }
        }

        $attractors = @($CatMesh.Attractors)
        $writer.Write([int]$attractors.Count)
        foreach ($attractor in $attractors) {
            Write-NullableString $writer $attractor.Name
            Write-Vector3 $writer $attractor.Position
            Write-Quaternion $writer $attractor.Rotation
            $writer.Write([single]$attractor.Scale)
            $writer.Write([int]$attractor.Unknown)
        }
        return Complete-WriterHash $stream $writer
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function Get-TextureSignature($CatMesh) {
    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream, [System.Text.Encoding]::UTF8, $true)
    try {
        $textures = @($CatMesh.Textures)
        $writer.Write([int]$textures.Count)
        foreach ($texture in $textures) {
            Write-NullableString $writer $texture.Name
            $writer.Write([int]$texture.Texture1)
            $writer.Write([int]$texture.Texture2)
            $writer.Write([int]$texture.Texture3)
        }

        $materials = @($CatMesh.Materials)
        $writer.Write([int]$materials.Count)
        foreach ($material in $materials) {
            Write-NullableString $writer $material.Name
            $writer.Write([int]$material.Unknown2)
            Write-NullableString $writer $material.TextureName
            Write-NullableString $writer $material.EnvTextureName
            Write-Color $writer $material.Diffuse
            Write-Color $writer $material.Specular
            Write-Color $writer $material.Ambient
            Write-Color $writer $material.Emission
            $writer.Write([single]$material.Sheen)
            $writer.Write([single]$material.Unknown4)
            $writer.Write([single]$material.SheenOpacity)
        }
        return Complete-WriterHash $stream $writer
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function Get-StatState([hashtable]$Stats, [string]$Key) {
    if (-not $Stats.ContainsKey($Key)) {
        return [ordered]@{ state = "absent"; value = $null }
    }
    $value = [int64]$Stats[$Key]
    if ($value -eq 1234567890) {
        return [ordered]@{ state = "sentinel/default"; value = $null }
    }
    return [ordered]@{ state = "value"; value = $value }
}

$commonDll = Join-Path $AodbPluginPath "AODB.Common.dll"
$aodbDll = Join-Path $AodbPluginPath "AODB.dll"
$requiredFiles = @($commonDll, $aodbDll, $MonsterDataCorpusPath, $CatMeshMapPath)
foreach ($path in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing required official-source input: $path"
    }
}

$databaseDirectory = Join-Path $SourceClientPath "cd_image\data\db"
$databaseHashes = [ordered]@{}
foreach ($entry in $ExpectedDatabaseHashes.GetEnumerator()) {
    $path = Join-Path $databaseDirectory $entry.Key
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing official ResourceDatabase input: $path"
    }
    $actual = Get-LowerSha256File $path
    if ($actual -ne $entry.Value) {
        throw "Official ResourceDatabase hash mismatch for $($entry.Key): $actual"
    }
    $databaseHashes[$entry.Key] = $actual
}

$monsterCorpusSha = Get-LowerSha256File $MonsterDataCorpusPath
$monsterCorpus = Get-Content -LiteralPath $MonsterDataCorpusPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$monsterCorpus.MonsterDataRecordCount -ne 1470 -or [int]$monsterCorpus.UniqueMonsterDataInstanceCount -ne 1470) {
    throw "Unexpected official MonsterData corpus size."
}
foreach ($entry in $ExpectedDatabaseHashes.GetEnumerator()) {
    if ([string]$monsterCorpus.OfficialInputs.($entry.Key) -ne $entry.Value) {
        throw "MonsterData corpus provenance mismatch for $($entry.Key)."
    }
}

Add-Type -Path $commonDll
Add-Type -Path $aodbDll

$controller = [AODB.RdbController]::new($SourceClientPath)
$catMeshDecodeFailures = [System.Collections.Generic.List[object]]::new()
try {
    $catMeshIds = @($controller.RecordTypeToId[1010002].Keys | Sort-Object)
    $catMeshes = [System.Collections.Generic.List[object]]::new()
    foreach ($id in $catMeshIds) {
        try {
            $catMesh = $controller.Get(1010002, [int]$id)
            $raw = $controller.GetRaw(1010002, [int]$id)
        }
        catch {
            $catMeshDecodeFailures.Add([ordered]@{
                recordId = [int]$id
                error = $_.Exception.Message
            })
            continue
        }
        if ($null -eq $catMesh) {
            $catMeshDecodeFailures.Add([ordered]@{
                recordId = [int]$id
                error = "AODB returned null."
            })
            continue
        }
        $textureRecords = @($catMesh.Textures | ForEach-Object {
            [ordered]@{
                name = $_.Name
                texture1 = [int]$_.Texture1
                texture2 = [int]$_.Texture2
                texture3 = [int]$_.Texture3
            }
        })
        $materialRecords = @($catMesh.Materials | ForEach-Object {
            [ordered]@{
                name = $_.Name
                textureName = $_.TextureName
                environmentTextureName = $_.EnvTextureName
            }
        })
        $meshGroups = @($catMesh.MeshGroups | ForEach-Object {
            [ordered]@{
                name = $_.Name
                meshCount = @($_.Meshes).Count
                vertexCount = (@($_.Meshes | ForEach-Object { @($_.Vertices).Count }) | Measure-Object -Sum).Sum
                triangleIndexCount = (@($_.Meshes | ForEach-Object { @($_.Triangles).Count }) | Measure-Object -Sum).Sum
            }
        })
        $catMeshes.Add([ordered]@{
            recordId = [int]$id
            recordVersion = [int]$catMesh.RecordVersion
            identifier = [int]$catMesh.Identifier
            rawSha256 = Get-LowerSha256Bytes $raw
            meshStructureSha256 = Get-GeometrySignature $catMesh
            jointSha256 = Get-JointSignature $catMesh
            textureSha256 = Get-TextureSignature $catMesh
            textureRecords = $textureRecords
            materialRecords = $materialRecords
            jointNames = @($catMesh.Joints | ForEach-Object { $_.Name })
            meshGroups = $meshGroups
            attractorCount = @($catMesh.Attractors).Count
        })
    }
}
finally {
    $controller.Dispose()
}

$catMeshById = @{}
foreach ($record in $catMeshes) {
    $catMeshById[[string]$record.recordId] = $record
}

$monsterRecords = [System.Collections.Generic.List[object]]::new()
foreach ($record in @($monsterCorpus.Records | Sort-Object ResourceInstance)) {
    $stats = @{}
    foreach ($pair in @($record.OrderedStatPairs)) {
        $stats[[string][int]$pair[0]] = [int64]$pair[1]
    }
    $mesh = Get-StatState $stats "12"
    $headMesh = Get-StatState $stats "64"
    $features = Get-StatState $stats "224"
    $fabricType = Get-StatState $stats "41"
    $charRadius = Get-StatState $stats "421"
    $groupMap1 = @($record.GroupMap1 | ForEach-Object {
        [ordered]@{
            group = [int]$_[0]
            values = @($_[1] | ForEach-Object { [int]$_ })
        }
    })
    $group120 = @($groupMap1 | Where-Object { $_.group -eq 120 } | ForEach-Object { $_.values })
    $group6000 = @($groupMap1 | Where-Object { $_.group -eq 6000 } | ForEach-Object { $_.values })
    $meshId = if ($mesh.state -eq "value") { [string]$mesh.value } else { $null }
    $headMeshId = if ($headMesh.state -eq "value") { [string]$headMesh.value } else { $null }
    $monsterRecords.Add([ordered]@{
        monsterData = [int]$record.ResourceInstance
        officialName = [string]$record.OfficialName
        recordSha256 = [string]$record.RecordSha256
        mesh = $mesh
        headMesh = $headMesh
        features = $features
        fabricType = $fabricType
        charRadius = $charRadius
        animationGroupCount = $groupMap1.Count
        animationGroupMapSha256 = Get-JsonSha256 $groupMap1
        animationGroup120 = $group120
        animationGroup6000 = $group6000
        catMeshRecordPresent = ($null -ne $meshId -and $catMeshById.ContainsKey($meshId))
        headMeshRecordPresent = ($null -ne $headMeshId -and $catMeshById.ContainsKey($headMeshId))
    })
}

$catMeshMapSha = Get-LowerSha256File $CatMeshMapPath
$catMeshMap = Get-Content -LiteralPath $CatMeshMapPath -Raw -Encoding UTF8 | ConvertFrom-Json
$mapMembership = @{}
foreach ($property in $catMeshMap.PSObject.Properties) {
    foreach ($monsterData in @($property.Value)) {
        $key = [string][int]$monsterData
        if (-not $mapMembership.ContainsKey($key)) {
            $mapMembership[$key] = [System.Collections.Generic.HashSet[int]]::new()
        }
        [void]$mapMembership[$key].Add([int]$property.Name)
    }
}
$corroborated = 0
$contradicted = 0
$unmapped = 0
foreach ($record in $monsterRecords) {
    if ($record.mesh.state -ne "value") {
        $unmapped++
        continue
    }
    $key = [string]$record.monsterData
    if (-not $mapMembership.ContainsKey($key)) {
        $unmapped++
        continue
    }
    if ($mapMembership[$key].Contains([int]$record.mesh.value)) {
        $corroborated++
    }
    else {
        $contradicted++
    }
}
if ($contradicted -ne 0) {
    throw "CATMesh-to-MonsterData corroboration contains $contradicted contradictions."
}

$output = [ordered]@{
    schemaVersion = 1
    sourceClientBuild = "18.8.62_EP1"
    sourceClientVariant = "EP1_OLD_GRAPHICS_CLIENT"
    resourceTypes = [ordered]@{
        catMesh = 1010002
        monsterData = 1040023
    }
    sourceHashes = [ordered]@{
        resourceDatabase = $databaseHashes
        monsterDataCorpus = $monsterCorpusSha
        catMeshToMonsterDataMap = $catMeshMapSha
        aodbCommon = Get-LowerSha256File $commonDll
        aodb = Get-LowerSha256File $aodbDll
    }
    semantics = [ordered]@{
        monsterDataSelector = "SimpleChar stat 359 -> resource 1040023 instance"
        catMeshSelector = "MonsterData stat 12 Mesh -> n3VisualDynel_t.SetCatMesh"
        headMeshSelector = "MonsterData stat 64 HeadMesh -> head/race setup and VisualCATMesh_t.SetSkinData"
        animationSelector = "MonsterData group map 1 -> CAT-mesh animation/effect selection"
        catMeshPlacementIdentity = $false
        monsterDataPlacementIdentity = $false
    }
    counts = [ordered]@{
        officialMonsterDataRecords = $monsterRecords.Count
        officialCatMeshIndexRecords = $catMeshIds.Count
        decodedCatMeshRecords = $catMeshes.Count
        catMeshDecodeFailures = $catMeshDecodeFailures.Count
        catMeshMapCorroboratedMonsterData = $corroborated
        catMeshMapUnmappedMonsterData = $unmapped
        catMeshMapContradictions = $contradicted
    }
    monsterDataRecords = @($monsterRecords)
    catMeshRecords = @($catMeshes)
    catMeshDecodeFailures = @($catMeshDecodeFailures)
}

$resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
}
else {
    Join-Path (Get-Location) $OutputPath
}
$outputDirectory = Split-Path -Parent $resolvedOutput
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$json = $output | ConvertTo-Json -Depth 30
[System.IO.File]::WriteAllText($resolvedOutput, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))

Write-Output "OFFICIAL_ENEMY_VISUAL_SOURCE_EXPORT=PASS"
Write-Output "OFFICIAL_MONSTER_DATA_RECORDS=$($monsterRecords.Count)"
Write-Output "OFFICIAL_CAT_MESH_INDEX_RECORDS=$($catMeshIds.Count)"
Write-Output "DECODED_CAT_MESH_RECORDS=$($catMeshes.Count)"
Write-Output "CAT_MESH_DECODE_FAILURES=$($catMeshDecodeFailures.Count)"
Write-Output "CAT_MESH_MAP_CORROBORATED=$corroborated"
Write-Output "CAT_MESH_MAP_UNMAPPED=$unmapped"
Write-Output "OUTPUT=$resolvedOutput"
