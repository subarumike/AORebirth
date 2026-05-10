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

function Assert-SourceMatch {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    Assert-True ([System.Text.RegularExpressions.Regex]::IsMatch($Text, $Pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)) $Message
}

function Assert-SourceNoMatch {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    Assert-True (-not [System.Text.RegularExpressions.Regex]::IsMatch($Text, $Pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)) $Message
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

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$zoneProject = Join-Path $repoRoot 'CellAO\Server\ZoneEngine\ZoneEngine.csproj'
$builtDir = Join-Path $repoRoot 'CellAO\Built\Debug'
$zoneEngine = Join-Path $builtDir 'ZoneEngine.exe'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'

$fullCharacterSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\FullCharacterMessageHandler.cs')
$clientConnectedSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs')
$baseInventorySource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\CellAO.Core\Inventory\BaseInventoryPages.cs')
$playfieldSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\Playfields\Playfield.cs')
$zoneEnemyHintsPath = Join-Path $repoRoot 'CellAO\Documentation\ClientRdbZoneEnemyHints.csv'
$npcTemplateHintsPath = Join-Path $repoRoot 'CellAO\Documentation\ClientRdbNpcTemplateHints.csv'

Assert-SourceMatch $fullCharacterSource 'MsgVersion\s*=\s*26\s*;' 'FullCharacter login packet must stay on live-compatible MsgVersion 26.'
Assert-SourceMatch $clientConnectedSource 'InitializeActionableState\s*\(\s*client\s*\)\s*;' 'ClientConnected must initialize the live-style actionable login state.'
Assert-SourceNoMatch $clientConnectedSource 'new\s+ActionMessage\b' 'ClientConnected should not send fake server-side Action bootstrap packets.'
Assert-SourceMatch $clientConnectedSource 'SetStat\s*\(\s*client\s*,\s*StatIds\.state\s*,\s*0\s*\)\s*;' 'Login state should stay 0.'
Assert-SourceMatch $clientConnectedSource 'SetStat\s*\(\s*client\s*,\s*StatIds\.currentmovementmode\s*,\s*\(int\)\s*MoveModes\.Run\s*\)\s*;' 'CurrentMovementMode should initialize to Run.'
Assert-SourceMatch $clientConnectedSource 'SetStat\s*\(\s*client\s*,\s*StatIds\.prevmovementmode\s*,\s*\(int\)\s*MoveModes\.Run\s*\)\s*;' 'PrevMovementMode should initialize to Run.'
Assert-SourceMatch $clientConnectedSource 'SetStat\s*\(\s*client\s*,\s*StatIds\.currentstate\s*,\s*0\s*\)\s*;' 'CurrentState should initialize to 0.'
Assert-SourceMatch $clientConnectedSource 'SetStat\s*\(\s*client\s*,\s*StatIds\.waitstate\s*,\s*0\s*\)\s*;' 'WaitState should initialize to 0.'
Assert-SourceMatch $clientConnectedSource 'SetStat\s*\(\s*client\s*,\s*StatIds\.socialstatus\s*,\s*4\s*\)\s*;' 'SocialStatus should initialize to 4.'
Assert-SourceMatch $clientConnectedSource 'SetStat\s*\(\s*client\s*,\s*StatIds\.specialcondition\s*,\s*3\s*\)\s*;' 'SpecialCondition should initialize to 3.'
Assert-SourceMatch $clientConnectedSource 'SetStat\s*\(\s*client\s*,\s*StatIds\.actioncategory\s*,\s*0\s*\)\s*;' 'ActionCategory should initialize to 0.'
Assert-SourceMatch $clientConnectedSource 'Stats\s*\[\s*stat\s*\]\.Value\s*=\s*value\s*;' 'Login SetStat helper should update the live stat value.'
Assert-SourceMatch $clientConnectedSource 'Stats\s*\[\s*stat\s*\]\.BaseValue\s*=\s*\(uint\)\s*value\s*;' 'Login SetStat helper should update the live stat base value.'
Assert-SourceMatch $baseInventorySource 'InventoryItemRules\.HasSameUniqueItem' 'Inventory add path must use shared unique-item rules.'
Assert-SourceMatch $playfieldSource 'InventoryItemRules\.HasSameUniqueItem' 'Corpse loot path must use shared unique-item rules.'
Assert-True (Test-Path $zoneEnemyHintsPath) 'Client RDB zone enemy hint catalog is missing.'
Assert-True (Test-Path $npcTemplateHintsPath) 'Client RDB NPC template hint catalog is missing.'

$zoneEnemyHints = @(Import-Csv $zoneEnemyHintsPath)
$npcTemplateHints = @(Import-Csv $npcTemplateHintsPath)
$newlandDesertHints = $zoneEnemyHints | Where-Object { [int]$_.PlayfieldId -eq 565 } | Select-Object -First 1
Assert-True ($null -ne $newlandDesertHints) 'Client RDB hints should include Newland Desert playfield 565.'
Assert-True ($newlandDesertHints.EnemyKeywords -match 'rhinoman') 'Newland Desert hints should include rhinoman.'
Assert-True ($newlandDesertHints.EnemyKeywords -match 'leet') 'Newland Desert hints should include leet.'
Assert-True ($newlandDesertHints.EnemyKeywords -match 'snake') 'Newland Desert hints should include snake.'
Assert-True ($null -ne ($npcTemplateHints | Where-Object { [int]$_.TemplateId -eq 17655 -and $_.Name -eq 'leet' } | Select-Object -First 1)) 'Client RDB NPC hints should include leet template 17655.'
Assert-True ($null -ne ($npcTemplateHints | Where-Object { [int]$_.TemplateId -eq 17687 -and $_.Name -eq 'rollerrat' } | Select-Object -First 1)) 'Client RDB NPC hints should include rollerrat template 17687.'
Assert-True ($null -ne ($npcTemplateHints | Where-Object { [int]$_.TemplateId -eq 30252 -and $_.Name -eq 'giant snake' } | Select-Object -First 1)) 'Client RDB NPC hints should include giant snake template 30252.'
Assert-True ($null -ne ($npcTemplateHints | Where-Object { [int]$_.TemplateId -eq 31114 -and $_.Name -eq 'rhinoman female' } | Select-Object -First 1)) 'Client RDB NPC hints should include rhinoman female template 31114.'

if (-not $SkipBuild) {
    Assert-True (Test-Path $msbuild) "MSBuild was not found at $msbuild"
    # Roslyn on older project graphs can throw MSB3883 when both Path and PATH
    # exist in process environment with different casing. Keep only Path.
    if ($env:Path) {
        [System.Environment]::SetEnvironmentVariable('Path', $env:Path, 'Process')
    }
    [System.Environment]::SetEnvironmentVariable('PATH', $null, 'Process')

    & $msbuild $zoneProject /t:Build /p:Configuration=Debug /p:Platform='AnyCPU' /m:1
    if ($LASTEXITCODE -ne 0) {
        throw "ZoneEngine project build failed with exit code $LASTEXITCODE."
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
    $coreAssembly = [System.Reflection.Assembly]::LoadFrom((Join-Path $builtDir 'CellAO.Core.dll'))
    $archetypeType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatTestMobArchetype'
    $rulesType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatCorpseRules'
    $damageRulesType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatDamageRules'
    $visualsType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatCorpseVisuals'
    $lootCatalogType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatTestLootCatalog'
    $inventoryRulesType = Get-RequiredType $coreAssembly 'CellAO.Core.Inventory.InventoryItemRules'

    $isUniqueFlags = Get-RequiredMethod $inventoryRulesType 'IsUniqueFlags' ([System.Reflection.BindingFlags]'Public, Static')
    $isSameTemplateIds = Get-RequiredMethod $inventoryRulesType 'IsSameTemplateIdPair' ([System.Reflection.BindingFlags]'Public, Static')
    Assert-True ([bool]$isUniqueFlags.Invoke($null, @(0x08000000))) 'Unique item flag should be detected.'
    Assert-True (-not [bool]$isUniqueFlags.Invoke($null, @(0))) 'Zero item flags should not be unique.'
    Assert-True ([bool]$isSameTemplateIds.Invoke($null, @(100, 101, 100, 101))) 'Matching low/high template ids should be treated as the same item.'
    Assert-True (-not [bool]$isSameTemplateIds.Invoke($null, @(100, 101, 100, 102))) 'Different high template ids should not be treated as the same item.'
    Assert-True (-not [bool]$isSameTemplateIds.Invoke($null, @(100, 101, 102, 101))) 'Different low template ids should not be treated as the same item.'

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

    $buildMap = Get-RequiredMethod $visualsType 'BuildMonsterDataToCorpseCatMeshMap' ([System.Reflection.BindingFlags]'Public, Static')
    $monsterToCorpseMap = $buildMap.Invoke($null, @())
    foreach ($entry in $entries) {
        $monsterData = [int](Get-PropertyValue $entry 'MonsterData')
        $corpseCatMesh = [int](Get-PropertyValue $entry 'CorpseCatMesh')
        Assert-True ([bool]$monsterToCorpseMap.ContainsKey($monsterData)) "MonsterData $monsterData is missing from the corpse visual map."
        Assert-True ([int]$monsterToCorpseMap[$monsterData] -eq $corpseCatMesh) "MonsterData $monsterData maps to the wrong corpse CATMesh."
    }

    $buildLootEntries = Get-RequiredMethod $lootCatalogType 'BuildEntries' ([System.Reflection.BindingFlags]'Public, Static')
    $lootEntries = @($buildLootEntries.Invoke($null, @()))
    Assert-True ($lootEntries.Count -eq ($entries.Count * 3)) 'Combat test loot catalog should have three entries per test mob.'

    foreach ($entry in $entries) {
        $displayName = Get-PropertyValue $entry 'DisplayName'
        $monsterData = [int](Get-PropertyValue $entry 'MonsterData')
        $npcFamily = [int](Get-PropertyValue $entry 'NpcFamily')
        $matches = @($lootEntries | Where-Object {
            $_.Matches($displayName, $monsterData, $npcFamily)
        })
        Assert-True ($matches.Count -eq 3) "Expected three loot table matches for $displayName, found $($matches.Count)."

        foreach ($lootEntry in $matches) {
            Assert-True (-not $lootEntry.Matches('Wrong Mob', $monsterData, $npcFamily)) "Loot entry for $displayName matched a wrong name."
            Assert-True (-not $lootEntry.Matches($displayName, $monsterData + 1, $npcFamily)) "Loot entry for $displayName matched wrong MonsterData."
            Assert-True ([int]$lootEntry.DropChancePercent -eq 100) "Test loot entry for $displayName must stay deterministic at 100 percent."
            Assert-True ([int]$lootEntry.Quality -eq 1) "Test loot entry for $displayName should use quality 1."
            Assert-True (@($lootEntry.ItemTemplateIds).Count -gt 0) "Test loot entry for $displayName has no item templates."
        }
    }

    $lifetimeMethod = Get-RequiredMethod $rulesType 'LifetimeFor' ([System.Reflection.BindingFlags]'Public, Static')
    $lootClassType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatCorpseLootClass'
    Assert-True ($null -ne $lootClassType) 'CorpseLootClass enum is missing.'
    $creditsOnly = [System.Enum]::Parse($lootClassType, 'CreditsOnly')
    $itemLoot = [System.Enum]::Parse($lootClassType, 'ItemLoot')
    $majorBoss = [System.Enum]::Parse($lootClassType, 'MajorBoss')
    Assert-True ($lifetimeMethod.Invoke($null, @($creditsOnly)).TotalSeconds -eq 30) 'Credits-only corpses should live for 30 seconds.'
    Assert-True ($lifetimeMethod.Invoke($null, @($itemLoot)).TotalSeconds -eq 60) 'Item-loot corpses should live for 60 seconds.'
    Assert-True ($lifetimeMethod.Invoke($null, @($majorBoss)).TotalMinutes -eq 30) 'Major boss corpses should live for 30 minutes.'

    $lootClassFor = Get-RequiredMethod $rulesType 'LootClassFor' ([System.Reflection.BindingFlags]'Public, Static')
    Assert-True ($lootClassFor.Invoke($null, @(0, $false)).Equals($creditsOnly)) 'No item loot should classify as CreditsOnly.'
    Assert-True ($lootClassFor.Invoke($null, @(1, $false)).Equals($itemLoot)) 'Unlooted items should classify as ItemLoot.'
    Assert-True ($lootClassFor.Invoke($null, @(0, $true)).Equals($majorBoss)) 'Boss corpses should classify as MajorBoss.'

    $shouldDrop = Get-RequiredMethod $rulesType 'ShouldDrop' ([System.Reflection.BindingFlags]'Public, Static')
    $lowRoll = [System.Func[int,int]]{ param([int]$max) 0 }
    $highRoll = [System.Func[int,int]]{ param([int]$max) $max - 1 }
    Assert-True (-not [bool]$shouldDrop.Invoke($null, @(0, $lowRoll))) '0 percent loot chance should never roll true.'
    Assert-True ([bool]$shouldDrop.Invoke($null, @(100, $highRoll))) '100 percent loot chance should always roll true.'
    Assert-True ([bool]$shouldDrop.Invoke($null, @(50, $lowRoll))) 'Low random roll should pass a 50 percent drop.'
    Assert-True (-not [bool]$shouldDrop.Invoke($null, @(50, $highRoll))) 'High random roll should fail a 50 percent drop.'

    $isUsableVisual = Get-RequiredMethod $visualsType 'IsUsableVisualId' ([System.Reflection.BindingFlags]'Public, Static')
    Assert-True (-not [bool]$isUsableVisual.Invoke($null, @(0))) 'Zero is not a usable visual id.'
    Assert-True (-not [bool]$isUsableVisual.Invoke($null, @(1234567890))) 'AO sentinel value is not a usable visual id.'
    Assert-True ([bool]$isUsableVisual.Invoke($null, @(1))) 'Positive visual ids should be usable.'

    $corpseCatMeshFor = Get-RequiredMethod $visualsType 'CorpseCatMeshFor' ([System.Reflection.BindingFlags]'Public, Static')
    $corpseMonsterDataFor = Get-RequiredMethod $visualsType 'CorpseMonsterDataFor' ([System.Reflection.BindingFlags]'Public, Static')
    $deathAnimationKeyFor = Get-RequiredMethod $visualsType 'DeathAnimationKeyFor' ([System.Reflection.BindingFlags]'Public, Static')
    $firstEntry = $entries[0]
    $firstMonsterData = [int](Get-PropertyValue $firstEntry 'MonsterData')
    $firstCorpseCatMesh = [int](Get-PropertyValue $firstEntry 'CorpseCatMesh')
    Assert-True ([int]$corpseCatMeshFor.Invoke($null, @(999, $firstMonsterData, $monsterToCorpseMap)) -eq 999) 'Usable CATMesh should win over MonsterData mapping.'
    Assert-True ([int]$corpseCatMeshFor.Invoke($null, @(0, $firstMonsterData, $monsterToCorpseMap)) -eq $firstCorpseCatMesh) 'MonsterData mapping should provide corpse CATMesh fallback.'
    Assert-True ([int]$corpseMonsterDataFor.Invoke($null, @(0, $firstCorpseCatMesh)) -eq $firstCorpseCatMesh) 'Corpse monster data should fall back to CATMesh.'
    Assert-True ([int]$deathAnimationKeyFor.Invoke($null, @(777, 888, 0x1F7)) -eq 777) 'Corpse animation key should win over item animation.'
    Assert-True ([int]$deathAnimationKeyFor.Invoke($null, @(0, 888, 0x1F7)) -eq 888) 'Item animation should be the second death animation choice.'
    Assert-True ([int]$deathAnimationKeyFor.Invoke($null, @(0, 0, 0x1F7)) -eq 0x1F7) 'Default death animation should be used when no visual keys exist.'

    $findLootItemGeneric = Get-RequiredMethod $rulesType 'FindLootItem' ([System.Reflection.BindingFlags]'Public, Static')
    $findLootItem = $findLootItemGeneric.MakeGenericMethod([object])
    $slotSelector = [System.Func[object,int]]{ param($x) [int]$x.Slot }
    $lootedSelector = [System.Func[object,bool]]{ param($x) [bool]$x.Looted }

    $lootItems = [object[]]@(
        [pscustomobject]@{ Slot = 0; Looted = $false },
        [pscustomobject]@{ Slot = 1; Looted = $false }
    )
    $exactSlot = $findLootItem.Invoke($null, @($lootItems, 0, $slotSelector, $lootedSelector))
    Assert-True ([int]$exactSlot.Slot -eq 0) 'Exact corpse loot slot lookup failed.'
    $oneBasedSlot = $findLootItem.Invoke($null, @($lootItems, 2, $slotSelector, $lootedSelector))
    Assert-True ([int]$oneBasedSlot.Slot -eq 1) 'One-based corpse loot slot lookup failed.'

    $singleRemaining = [object[]]@(
        [pscustomobject]@{ Slot = 5; Looted = $false }
    )
    $singleFallback = $findLootItem.Invoke($null, @($singleRemaining, 1, $slotSelector, $lootedSelector))
    Assert-True ([int]$singleFallback.Slot -eq 5) 'Single remaining corpse loot fallback failed.'

    $allLooted = [object[]]@(
        [pscustomobject]@{ Slot = 0; Looted = $true }
    )
    $missingLooted = $findLootItem.Invoke($null, @($allLooted, 0, $slotSelector, $lootedSelector))
    Assert-True ($null -eq $missingLooted) 'Looted corpse item should not be returned.'

    $entryCountFor = Get-RequiredMethod $rulesType 'InventoryEntryCountFor' ([System.Reflection.BindingFlags]'Public, Static')
    Assert-True ([int]$entryCountFor.Invoke($null, @(0)) -eq 1) 'Zero stack count should display as one.'
    Assert-True ([int]$entryCountFor.Invoke($null, @(1234567890)) -eq 1) 'AO sentinel stack count should display as one.'
    Assert-True ([int]$entryCountFor.Invoke($null, @(7)) -eq 7) 'Normal stack count should be preserved.'
    Assert-True ([int]$entryCountFor.Invoke($null, @(40000)) -eq [System.Int16]::MaxValue) 'Large stack count should clamp to Int16 max.'

    $calculateDamage = Get-RequiredMethod $damageRulesType 'Calculate' ([System.Reflection.BindingFlags]'Public, Static')
    Assert-True ([int]$calculateDamage.Invoke($null, @(0, 0, 0, 1, $true)) -eq 15) 'Player fallback damage should prevent 1-damage regressions.'
    Assert-True ([int]$calculateDamage.Invoke($null, @(0, 0, 0, 1, $false)) -eq 1) 'Level 1 NPC fallback damage should be 1.'
    Assert-True ([int]$calculateDamage.Invoke($null, @(0, 0, 0, 4, $false)) -eq 4) 'NPC level should drive fallback damage above level 1.'
    Assert-True ([int]$calculateDamage.Invoke($null, @(0, 5, 0, 1, $true)) -eq 15) 'Player fallback should still beat weak max damage.'
    Assert-True ([int]$calculateDamage.Invoke($null, @(0, 20, 3, 1, $true)) -eq 23) 'Max damage plus bonus should beat player fallback.'
    Assert-True ([int]$calculateDamage.Invoke($null, @(10, 5, 2, 1, $false)) -eq 12) 'Min damage should raise lower max damage before bonus.'
    Assert-True ([int]$calculateDamage.Invoke($null, @(-10, -5, -3, 1, $true)) -eq 15) 'Negative combat stats should not lower player fallback damage.'

    Write-Host '[PASS] Combat smoke tests passed.'
}
finally {
    Set-Location $previousLocation
}
