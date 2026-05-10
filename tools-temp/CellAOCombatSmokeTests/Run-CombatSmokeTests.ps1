param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-RequiredType {
    param(
        [System.Reflection.Assembly]$Assembly,
        [string]$Name
    )

    $type = $Assembly.GetType($Name, $false)
    Assert-True ($null -ne $type) "Missing type $Name"
    return $type
}

function Get-RequiredMethod {
    param(
        [System.Type]$Type,
        [string]$Name,
        [System.Reflection.BindingFlags]$Flags
    )

    $method = $Type.GetMethod($Name, $Flags)
    Assert-True ($null -ne $method) "Missing method $($Type.FullName).$Name"
    return $method
}

function Get-RequiredProperty {
    param(
        [System.Type]$Type,
        [string]$Name
    )

    $property = $Type.GetProperty($Name, [System.Reflection.BindingFlags]'Public, Instance')
    Assert-True ($null -ne $property) "Missing property $($Type.FullName).$Name"
    return $property
}

function Get-PropertyValue {
    param(
        [object]$Object,
        [string]$Name
    )

    return (Get-RequiredProperty $Object.GetType() $Name).GetValue($Object, $null)
}

function Set-PropertyValue {
    param(
        [object]$Object,
        [string]$Name,
        [object]$Value
    )

    (Get-RequiredProperty $Object.GetType() $Name).SetValue($Object, $Value, $null)
}

function New-PrivateObject {
    param(
        [System.Type]$Type
    )

    return [System.Activator]::CreateInstance($Type, $true)
}

function New-LootItemState {
    param(
        [System.Type]$LootItemType,
        [int]$Slot,
        [bool]$Looted = $false
    )

    $item = New-PrivateObject $LootItemType
    Set-PropertyValue $item 'Slot' $Slot
    Set-PropertyValue $item 'Looted' $Looted
    return $item
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$solution = Join-Path $repoRoot 'CellAO\CellAO.sln'
$builtDir = Join-Path $repoRoot 'CellAO\Built\Debug'
$zoneEngine = Join-Path $builtDir 'ZoneEngine.exe'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'

if (-not $SkipBuild) {
    Assert-True (Test-Path $msbuild) "MSBuild was not found at $msbuild"
    & $msbuild $solution /t:Build /p:Configuration=Debug /m
    if ($LASTEXITCODE -ne 0) {
        throw "CellAO solution build failed with exit code $LASTEXITCODE."
    }
}

Assert-True (Test-Path $zoneEngine) "ZoneEngine.exe was not found at $zoneEngine"

$previousLocation = Get-Location
Set-Location $builtDir

try {
    [System.AppDomain]::CurrentDomain.add_AssemblyResolve({
        param($sender, $eventArgs)

        $assemblyName = New-Object System.Reflection.AssemblyName($eventArgs.Name)
        $probingDirs = @(
            $builtDir,
            (Join-Path $repoRoot 'bin\Debug'),
            (Join-Path $repoRoot 'CellAO\Libraries\Source\CellAO.Core\bin\Debug'),
            (Join-Path $repoRoot 'CellAO\Libraries\Source\CellAO.Enums\bin\Debug'),
            (Join-Path $repoRoot 'CellAO\Libraries\Source\CellAO.Interfaces\bin\Debug'),
            (Join-Path $repoRoot 'CellAO\Libraries\Source\CellAO.Stats\bin\Debug'),
            (Join-Path $repoRoot 'CellAO\Libraries\Source\CellAO.Database\bin\Debug'),
            (Join-Path $repoRoot 'CellAO\Libraries\Source\CellAO.ObjectManager\bin\Debug'),
            (Join-Path $repoRoot 'CellAO\Libraries\Source\CellAO.Communication\bin\Debug'),
            (Join-Path $repoRoot 'CellAO\Libraries\Source\Cell.Core\Bin\Debug'),
            (Join-Path $repoRoot 'CellAO\Libraries\Source\Cell.Util\bin\Debug'),
            (Join-Path $repoRoot 'CellAO\Libraries\Source\Utility\bin\Debug'),
            (Join-Path $repoRoot 'CellAO\Libraries\Source\PlayfieldLoader\bin\Debug'),
            (Join-Path $repoRoot 'CellAO\Libraries\Source\Translations\bin\Debug'),
            (Join-Path $repoRoot 'CellAO\Libraries\Source\msgpack-cli\src\MsgPack.Mono\bin\Debug')
        )

        foreach ($dir in $probingDirs) {
            foreach ($extension in @('.dll', '.exe')) {
                $candidate = Join-Path $dir ($assemblyName.Name + $extension)
                if (Test-Path $candidate) {
                    return [System.Reflection.Assembly]::LoadFrom($candidate)
                }
            }
        }

        return $null
    })

    $zoneAssembly = [System.Reflection.Assembly]::LoadFrom($zoneEngine)
    $archetypeType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatTestMobArchetype'
    $playfieldType = Get-RequiredType $zoneAssembly 'CellAO.Core.Playfields.Playfield'

    $allField = $archetypeType.GetField('All', [System.Reflection.BindingFlags]'Public, Static')
    Assert-True ($null -ne $allField) 'CombatTestMobArchetype.All is missing.'
    $entries = @($allField.GetValue($null))
    Assert-True ($entries.Count -ge 4) "Expected at least 4 combat test mobs, found $($entries.Count)."

    $defaultProperty = $archetypeType.GetProperty('Default', [System.Reflection.BindingFlags]'Public, Static')
    Assert-True ($null -ne $defaultProperty) 'CombatTestMobArchetype.Default is missing.'
    $defaultEntry = $defaultProperty.GetValue($null, $null)
    Assert-True ([object]::ReferenceEquals($defaultEntry, $entries[0])) 'Default combat test mob should be the first catalog entry.'

    $tryGetByAlias = Get-RequiredMethod $archetypeType 'TryGetByAlias' ([System.Reflection.BindingFlags]'Public, Static')
    $tryGetByName = Get-RequiredMethod $archetypeType 'TryGetByName' ([System.Reflection.BindingFlags]'Public, Static')

    $seenKeys = @{}
    $seenAliases = @{}
    $seenMonsterData = @{}
    foreach ($entry in $entries) {
        $key = Get-PropertyValue $entry 'Key'
        $displayName = Get-PropertyValue $entry 'DisplayName'
        $aliases = @(Get-PropertyValue $entry 'Aliases')
        $monsterData = [int](Get-PropertyValue $entry 'MonsterData')
        $corpseCatMesh = [int](Get-PropertyValue $entry 'CorpseCatMesh')

        Assert-True (-not [string]::IsNullOrWhiteSpace($key)) 'Combat test mob key is blank.'
        Assert-True (-not [string]::IsNullOrWhiteSpace($displayName)) "DisplayName is blank for $key."
        Assert-True ($aliases.Count -gt 0) "No aliases configured for $displayName."
        Assert-True ($monsterData -gt 0) "MonsterData must be positive for $displayName."
        Assert-True ($corpseCatMesh -gt 0) "CorpseCatMesh must be positive for $displayName."
        Assert-True (-not $seenKeys.ContainsKey($key.ToLowerInvariant())) "Duplicate combat test mob key $key."
        Assert-True (-not $seenMonsterData.ContainsKey($monsterData)) "Duplicate MonsterData $monsterData."

        $seenKeys[$key.ToLowerInvariant()] = $true
        $seenMonsterData[$monsterData] = $true

        $nameArgs = @($displayName, $null)
        Assert-True ([bool]$tryGetByName.Invoke($null, $nameArgs)) "TryGetByName failed for $displayName."
        Assert-True ([object]::ReferenceEquals($entry, $nameArgs[1])) "TryGetByName returned the wrong entry for $displayName."

        foreach ($alias in $aliases) {
            Assert-True (-not [string]::IsNullOrWhiteSpace($alias)) "Blank alias configured for $displayName."
            $aliasKey = $alias.ToLowerInvariant()
            Assert-True (-not $seenAliases.ContainsKey($aliasKey)) "Duplicate combat test mob alias $alias."
            $seenAliases[$aliasKey] = $true

            $aliasArgs = @($alias, $null)
            Assert-True ([bool]$tryGetByAlias.Invoke($null, $aliasArgs)) "TryGetByAlias failed for $alias."
            Assert-True ([object]::ReferenceEquals($entry, $aliasArgs[1])) "TryGetByAlias returned the wrong entry for $alias."
        }
    }

    $corpseMappingsMethod = Get-RequiredMethod $archetypeType 'CorpseVisualMappings' ([System.Reflection.BindingFlags]'Public, Static')
    $corpseMappings = @($corpseMappingsMethod.Invoke($null, @()))
    Assert-True ($corpseMappings.Count -eq $entries.Count) 'Corpse visual mapping count must match the combat test catalog count.'

    $buildMap = Get-RequiredMethod $playfieldType 'BuildMonsterDataToCorpseCatMeshMap' ([System.Reflection.BindingFlags]'NonPublic, Static')
    $monsterToCorpseMap = $buildMap.Invoke($null, @())
    foreach ($entry in $entries) {
        $monsterData = [int](Get-PropertyValue $entry 'MonsterData')
        $corpseCatMesh = [int](Get-PropertyValue $entry 'CorpseCatMesh')
        Assert-True ([bool]$monsterToCorpseMap.ContainsKey($monsterData)) "MonsterData $monsterData is missing from the Playfield corpse visual map."
        Assert-True ([int]$monsterToCorpseMap[$monsterData] -eq $corpseCatMesh) "MonsterData $monsterData maps to the wrong corpse CATMesh."
    }

    $lifetimeMethod = Get-RequiredMethod $playfieldType 'CorpseLifetimeFor' ([System.Reflection.BindingFlags]'NonPublic, Static')
    $lootClassType = $playfieldType.GetNestedType('CorpseLootClass', [System.Reflection.BindingFlags]'NonPublic')
    Assert-True ($null -ne $lootClassType) 'CorpseLootClass enum is missing.'
    $creditsOnly = [System.Enum]::Parse($lootClassType, 'CreditsOnly')
    $itemLoot = [System.Enum]::Parse($lootClassType, 'ItemLoot')
    $majorBoss = [System.Enum]::Parse($lootClassType, 'MajorBoss')
    Assert-True ($lifetimeMethod.Invoke($null, @($creditsOnly)).TotalSeconds -eq 30) 'Credits-only corpses should live for 30 seconds.'
    Assert-True ($lifetimeMethod.Invoke($null, @($itemLoot)).TotalSeconds -eq 60) 'Item-loot corpses should live for 60 seconds.'
    Assert-True ($lifetimeMethod.Invoke($null, @($majorBoss)).TotalMinutes -eq 30) 'Major boss corpses should live for 30 minutes.'

    $rollLootChance = Get-RequiredMethod $playfieldType 'RollLootChance' ([System.Reflection.BindingFlags]'NonPublic, Static')
    Assert-True (-not [bool]$rollLootChance.Invoke($null, @(0))) '0 percent loot chance should never roll true.'
    Assert-True ([bool]$rollLootChance.Invoke($null, @(100))) '100 percent loot chance should always roll true.'

    $corpseStateType = $playfieldType.GetNestedType('DebugCorpseState', [System.Reflection.BindingFlags]'NonPublic')
    $lootItemType = $playfieldType.GetNestedType('DebugCorpseLootItem', [System.Reflection.BindingFlags]'NonPublic')
    Assert-True ($null -ne $corpseStateType) 'DebugCorpseState type is missing.'
    Assert-True ($null -ne $lootItemType) 'DebugCorpseLootItem type is missing.'

    $lootListType = ([System.Collections.Generic.List``1]).MakeGenericType($lootItemType)
    $lootList = [System.Activator]::CreateInstance($lootListType)
    [void]$lootList.Add((New-LootItemState $lootItemType 0))
    [void]$lootList.Add((New-LootItemState $lootItemType 1))

    $corpseState = New-PrivateObject $corpseStateType
    Set-PropertyValue $corpseState 'LootItems' $lootList

    $findCorpseLootItem = Get-RequiredMethod $playfieldType 'FindCorpseLootItem' ([System.Reflection.BindingFlags]'NonPublic, Static')
    $exactSlot = $findCorpseLootItem.Invoke($null, @($corpseState, 0))
    Assert-True ((Get-PropertyValue $exactSlot 'Slot') -eq 0) 'Exact corpse loot slot lookup failed.'
    $oneBasedSlot = $findCorpseLootItem.Invoke($null, @($corpseState, 2))
    Assert-True ((Get-PropertyValue $oneBasedSlot 'Slot') -eq 1) 'One-based corpse loot slot lookup failed.'

    $singleLootList = [System.Activator]::CreateInstance($lootListType)
    [void]$singleLootList.Add((New-LootItemState $lootItemType 0 $true))
    $singleCorpseState = New-PrivateObject $corpseStateType
    Set-PropertyValue $singleCorpseState 'LootItems' $singleLootList
    $missingLooted = $findCorpseLootItem.Invoke($null, @($singleCorpseState, 0))
    Assert-True ($null -eq $missingLooted) 'Looted corpse item should not be returned.'

    Write-Host '[PASS] Combat smoke tests passed.'
}
finally {
    Set-Location $previousLocation
}
