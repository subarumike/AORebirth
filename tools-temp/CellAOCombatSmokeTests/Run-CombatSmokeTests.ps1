param(
    [switch]$SkipBuild,
    [string]$BuiltDir
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

function Assert-SourceOrdered {
    param(
        [string]$Text,
        [string[]]$Patterns,
        [string]$Message
    )

    $offset = 0
    foreach ($pattern in $Patterns) {
        $remaining = $Text.Substring($offset)
        $match = [System.Text.RegularExpressions.Regex]::Match($remaining, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
        Assert-True $match.Success "$Message Missing or out of order pattern: $pattern"
        $offset += $match.Index + [Math]::Max($match.Length, 1)
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

function Convert-EnumerableToArray {
    param(
        [object]$Enumerable
    )

    $items = @()
    foreach ($item in ([System.Collections.IEnumerable]$Enumerable)) {
        $items += $item
    }

    return @($items)
}

function Stop-BuiltOutputProcesses {
    param(
        [string]$BuiltDirectory
    )

    $normalizedBuiltDir = ([System.IO.Path]::GetFullPath($BuiltDirectory)).TrimEnd('\') + '\'
    $staleProcesses = Get-Process -ErrorAction SilentlyContinue | Where-Object {
        try {
            if (-not $_.Path) {
                return $false
            }

            $processPath = [System.IO.Path]::GetFullPath($_.Path)
            return $processPath.StartsWith($normalizedBuiltDir, [System.StringComparison]::OrdinalIgnoreCase)
        }
        catch {
            return $false
        }
    }

    if ($staleProcesses) {
        $staleProcesses | ForEach-Object {
            Write-Host "Stopping stale built-output process $($_.ProcessName) (pid=$($_.Id))"
        }
        $staleProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$zoneProject = Join-Path $repoRoot 'CellAO\Server\ZoneEngine\ZoneEngine.csproj'
if ([string]::IsNullOrWhiteSpace($BuiltDir)) {
    $builtDir = Join-Path $repoRoot 'CellAO\Built\Debug'
}
else {
    $builtDir = (Resolve-Path $BuiltDir).Path
}
$zoneEngine = Join-Path $builtDir 'ZoneEngine.exe'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'

$fullCharacterSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\FullCharacterMessageHandler.cs')
$clientConnectedSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\PacketHandlers\ClientConnected.cs')
$clientMoveItemSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\ClientMoveItemToInventoryMessageHandler.cs')
$characterSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\CellAO.Core\Entities\Character.cs')
$weaponItemFullUpdateSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\Packets\WeaponItemFullUpdate.cs')
$corpseFullUpdateSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\Packets\CorpseFullUpdate.cs')
$combatCorpseRulesSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\CombatCorpseRules.cs')
$equipSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\Packets\Equip.cs')
$unEquipSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\Packets\UnEquip.cs')
$baseInventorySource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\CellAO.Core\Inventory\BaseInventoryPages.cs')
$playfieldSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\Playfields\Playfield.cs')
$spawnCommandSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\ChatCommands\Spawn.cs')
$attackMessageSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\AttackMessageHandler.cs')
$genericCmdSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\GenericCmdMessageHandler.cs')
$containerAddItemSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\ContainerAddItemMessageHandler.cs')
$characterActionSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\CharacterActionMessageHandler.cs')
$zoneClientSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\ZoneClient.cs')
$npcControllerSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\Controllers\NPCController.cs')
$npcAiProfileSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\NpcAiProfile.cs')
$giveItemCommandSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\ChatCommands\ChatCommandGiveItem.cs')
$zoneProjectSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\ZoneEngine.csproj')
$messagingProjectSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\SmokeLounge.AOtomation.Messaging.csproj')
$messagingSerializerSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Serialization\SerializerResolverBuilder.cs')
$zoneEnemyHintsPath = Join-Path $repoRoot 'CellAO\Documentation\ClientRdbZoneEnemyHints.csv'
$npcTemplateHintsPath = Join-Path $repoRoot 'CellAO\Documentation\ClientRdbNpcTemplateHints.csv'
$enemyCoveragePath = Join-Path $repoRoot 'CellAO\Documentation\ClientHintedEnemyCoverage.csv'
$visualHintsPath = Join-Path $repoRoot 'CellAO\Documentation\MonsterDataCorpseVisualHints.csv'
$livePacketGapsSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Documentation\LivePacketImplementationGaps.md')
$enemyNpcMapSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Documentation\EnemyNpcDllAodbMap.md')
$mobLootDataSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Documentation\MobLootData.md')

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
Assert-SourceMatch $characterSource 'Read\s*\(\s*\).*?this\.BaseInventory\.Read\s*\(\s*\)\s*;\s*base\.Read\s*\(\s*\)' 'Character login should reload persisted inventory before derived stats and appearance are rebuilt.'
Assert-SourceMatch $characterSource 'Write\s*\(\s*\).*?this\.BaseInventory\.Write\s*\(\s*\)' 'Character logout/save should persist equipped inventory pages.'
Assert-SourceMatch $characterSource 'EnterLogoutSitPosture\s*\(\s*\).*?StopMovement\s*\(\s*\).*?UpdateMoveType\s*\(\s*30\s*\).*?StatIds\.state.*?StatIds\.currentstate' 'Logout posture should use the same sit move type and reset actionable state before saving or timer logout.'
Assert-SourceMatch $characterActionSource 'case\s+CharacterActionType\.Logout:.*?ApplySit\s*\(\s*client\s*\).*?SendStartLogout\s*\(\s*client\.Controller\.Character\s*\).*?StartLogoutTimer\s*\(\s*\)' 'First X/logout should sit the character, send StartLogout, and start the timed logout.'
Assert-SourceMatch $characterActionSource 'SendStartLogout\s*\(ICharacter\s+character\).*?new\s+StartLogoutMessage.*?Identity\s*=\s*character\.Identity' 'Timed logout start should send the recovered identity-only StartLogout N3 packet.'
Assert-SourceMatch $characterActionSource 'SendStopLogout\s*\(ICharacter\s+character\).*?new\s+StopLogoutMessage.*?Identity\s*=\s*character\.Identity' 'Logout cancel should send the recovered identity-only StopLogout N3 packet.'
Assert-SourceMatch $zoneClientSource 'Dispose\s*\(bool\s+disposing\).*?!this\.Controller\.Character\.InLogoutTimerPeriod\s*\(\s*\).*?EnterLogoutSitPosture\s*\(\s*\).*?StartLogoutTimer\s*\(\s*\)' 'Hard network disconnect should enter seated logout state and start the timer when normal logout did not already do it.'
Assert-SourceMatch $messagingProjectSource 'Messages\\N3Messages\\StartLogoutMessage\.cs.*?Messages\\N3Messages\\StopLogoutMessage\.cs.*?Serialization\\Serializers\\Custom\\IdentityOnlyN3MessageSerializer\.cs' 'Messaging project should compile the recovered identity-only logout packet models and serializer.'
Assert-SourceMatch $messagingSerializerSource 'typeof\s*\(\s*StartLogoutMessage\s*\).*?IdentityOnlyN3MessageSerializer.*?typeof\s*\(\s*StopLogoutMessage\s*\).*?IdentityOnlyN3MessageSerializer' 'StartLogout/StopLogout should use the identity-only serializer instead of the generic N3 unknown-byte layout.'
Assert-SourceMatch $clientConnectedSource 'Packets\.WeaponItemFullUpdate\.SendWeaponDefinitions\s*\(\s*client\.Controller\.Character\s*\)\s*;\s*FullCharacterMessageHandler\.Default\.Send' 'Login should send persisted equipped weapon definitions before FullCharacter.'
Assert-SourceMatch $clientConnectedSource 'CalculateSkills\s*\(\s*\)\s*;\s*ClientMoveItemToInventoryMessageHandler\.EnsureWeaponVisualMeshes\s*\(\s*client\.Controller\.Character\s*,\s*false\s*\)\s*;\s*AppearanceUpdateMessageHandler\.Default\.Send' 'Login should follow persisted equipment -> CalculateSkills -> EnsureWeaponVisualMeshes(false) -> AppearanceUpdate.'
Assert-SourceMatch $clientMoveItemSource 'public\s+static\s+void\s+EnsureWeaponVisualMeshes\s*\(\s*ICharacter\s+character\s*,\s*bool\s+announceAppearanceUpdate\s*\)' 'Weapon mesh repair should be reusable by login and manual equip paths.'
Assert-SourceMatch $clientMoveItemSource 'IsAppearanceEquipmentPage\s*\(IInventoryPage\s+page\).*?WeaponInventoryPage.*?ArmorInventoryPage.*?SocialArmorInventoryPage' 'Weapon, util/HUD, back, armor, and social equipment changes should be treated as appearance-affecting.'
Assert-SourceMatch $clientMoveItemSource 'Equip\.Send\s*\(client,\s*receivingPage,\s*toPlacement\)\s*;\s*character\.CalculateSkills\s*\(\s*\)\s*;\s*EnsureWeaponVisualMeshes\s*\(\s*character,\s*true\s*\)' 'Manual equip should recalculate item stats, then repair/announce weapon hand meshes.'
Assert-SourceMatch $clientMoveItemSource 'UnEquip\.Send\s*\(client,\s*sendingPage,\s*fromPlacement\)\s*;\s*unequipFrom\.Unequip\s*\(fromPlacement,\s*receivingPage,\s*toPlacement\)\s*;\s*this\.SendMoveAck.*?character\.CalculateSkills\s*\(\s*\)\s*;\s*EnsureWeaponVisualMeshes\s*\(\s*character,\s*true\s*\)' 'Manual unequip should recalculate item stats, then leave removed hand meshes cleared before relog.'
Assert-SourceMatch $clientMoveItemSource 'EnsureWeaponVisualMeshes\s*\(\s*character,\s*true\s*\)\s*;\s*this\.PersistCharacterInventory\s*\(\s*character,\s*"equip"\s*\)' 'Successful manual equips should persist inventory immediately after the visual/stat update path.'
Assert-SourceMatch $clientMoveItemSource 'EnsureWeaponVisualMeshes\s*\(\s*character,\s*true\s*\)\s*;\s*this\.PersistCharacterInventory\s*\(\s*character,\s*"unequip"\s*\)' 'Successful manual unequips should persist inventory immediately after the visual/stat update path.'
Assert-SourceMatch $clientMoveItemSource 'sendingPage\.Remove\s*\(\s*fromPlacement\s*\)\s*;\s*receivingPage\.Add\s*\(\s*toPlacement,\s*itemFrom\s*\)\s*;\s*this\.SendMoveAck.*?this\.PersistCharacterInventory\s*\(\s*character,\s*"move"\s*\)' 'Successful plain inventory moves should persist inventory immediately after moving the item.'
Assert-SourceMatch $clientMoveItemSource 'PersistCharacterInventory\s*\(ICharacter\s+character,\s*string\s+reason\).*?character\.BaseInventory\.Write\s*\(\s*\)' 'ClientMoveItemToInventory persistence helper should write the full character inventory.'
Assert-SourceMatch $giveItemCommandSource 'InventoryError\s+err\s*=\s*container\.BaseInventory\.TryAdd\s*\(\s*item\s*\).*?if\s*\(\s*err\s*!=\s*InventoryError\.OK\s*\).*?container\.BaseInventory\.Write\s*\(\s*\)\s*;.*?AddTemplateMessageHandler\.Default\.Send' 'GM giveitem should persist the added item before notifying a character client.'
Assert-SourceMatch $clientMoveItemSource 'EnsureWeaponVisualMeshes\s*\(ICharacter\s+character,\s*bool\s+announceAppearanceUpdate\).*?WeaponSlots\.Righthand.*?WeaponSlots\.LeftHand' 'Weapon visual repair should cover both right and left hand slots for dual wield.'
Assert-SourceMatch $clientMoveItemSource 'EnsureWeaponMesh\s*\(.*?IItem\s+equippedItem\s*=\s*weaponPage\s*\[\s*slot\s*\]\s*;\s*if\s*\(\s*equippedItem\s*==\s*null\s*\)\s*\{\s*return\s+false\s*;' 'Weapon visual repair should not restore stale meshes for an unequipped hand.'
Assert-SourceMatch $clientMoveItemSource 'if\s*\(\s*changed\s*\).*?character\.ChangedAppearance\s*=\s*true;.*?if\s*\(\s*announceAppearanceUpdate\s*\).*?AnnounceAppearanceUpdate\s*\(\s*character\s*\)' 'Login repair should be silent while manual equip/unequip repair announces appearance changes.'
Assert-SourceMatch $characterSource 'CalculateSkills\s*\(\s*\).*?string\s+appearanceBefore\s*=\s*this\.GetAppearanceSignature\s*\(\s*\).*?this\.meshLayer\.Clear\s*\(\s*\).*?this\.BaseInventory\.CalculateModifiers\s*\(\s*this\s*\).*?appearanceBefore\s*!=\s*this\.GetAppearanceSignature\s*\(\s*\)' 'CalculateSkills should rebuild appearance from current equipped items and detect unequip/relog visual changes.'
Assert-SourceMatch $weaponItemFullUpdateSource 'SendWeaponDefinitions\s*\(ICharacter\s+character\).*?SendForSlot\s*\(character,\s*page,\s*slot\)' 'Login weapon definition export should include weapon-like items from persisted equipment/inventory pages.'
Assert-SourceMatch $weaponItemFullUpdateSource 'Send\s*\(IZoneClient\s+client\).*?WeaponSlots\.Righthand.*?WeaponSlots\.LeftHand' 'Explicit weapon full update should cover both hands for dual wield.'
Assert-SourceMatch $equipSource 'IsWeaponHandSlot\s*\(IInventoryPage\s+page,\s*int\s+slotNumber\).*?WeaponSlots\.Righthand.*?WeaponSlots\.LeftHand' 'Equip packet flow should use the weapon-hand CharacterAction path for either dual-wield hand.'
Assert-SourceMatch $unEquipSource 'IsWeaponHandSlot\s*\(IInventoryPage\s+page,\s*int\s+slotNumber\).*?WeaponSlots\.Righthand.*?WeaponSlots\.LeftHand' 'Unequip packet flow should clear either dual-wield hand with the weapon-hand CharacterAction path.'
Assert-SourceMatch $clientConnectedSource 'DefaultForPlayfield\s*\(' 'ClientConnected debug enemy spawn should choose a client-hinted default for the current playfield.'
Assert-SourceMatch $clientConnectedSource 'existingTestMobs\.Count\s*>\s*0' 'ClientConnected debug enemy spawn should not add a default mob when any live test mob already exists.'
Assert-SourceMatch $attackMessageSource 'EngageNpcTarget\s*\(character,\s*target\)' 'Starting an attack against an NPC should engage server-side retaliation.'
Assert-SourceMatch $zoneProjectSource 'Core\\NpcAiProfile\.cs' 'ZoneEngine project should compile the NPC AI profile helper.'
Assert-SourceMatch $npcAiProfileSource 'enum\s+NpcAiProfile.*?Passive.*?Aggressive.*?Social' 'NPC AI profiles should distinguish passive, aggressive, and social NPCs.'
Assert-SourceMatch $npcAiProfileSource 'CanRetaliate\s*\(NpcAiProfile\s+profile\).*?NpcAiProfile\.Passive.*?NpcAiProfile\.Aggressive' 'Passive and aggressive NPCs should be allowed to retaliate.'
Assert-SourceMatch $npcAiProfileSource 'CanProximityAggro\s*\(NpcAiProfile\s+profile\).*?NpcAiProfile\.Aggressive' 'Only aggressive NPCs should be eligible for future proximity aggro.'
Assert-SourceMatch $npcControllerSource 'AiProfile\s*\{\s*get;\s*set;\s*\}\s*=\s*NpcAiProfile\.Passive' 'NPC controllers should default to passive behavior.'
Assert-SourceMatch $npcControllerSource 'SetKnuBot\s*\(BaseKnuBot\s+knubot\).*?AiProfile\s*=\s*NpcAiProfile\.Social' 'KnuBot/dialog NPCs should be marked social.'
Assert-SourceMatch $attackMessageSource 'EngageNpcTarget\s*\(ICharacter\s+character,\s*ICharacter\s+target\).*?target\.Controller\s+as\s+NPCController.*?npcController\.KnuBot\s*!=\s*null.*?!NpcAiProfiles\.CanRetaliate\s*\(\s*npcController\.AiProfile\s*\).*?target\.Stats\s*\[\s*StatIds\.health\s*\]\.Value\s*<=\s*0.*?target\.FightingTarget\.Instance\s*!=\s*0.*?target\.SetTarget\s*\(\s*character\.Identity\s*\).*?target\.SetFightingTarget\s*\(\s*character\.Identity\s*\)' 'NPC retaliation should be gated by AI profile and only engage live non-KnuBot NPCs that are not already fighting.'
Assert-SourceMatch $clientConnectedSource 'RegisterNpcHome\s*\(\s*existingMob\s*\)' 'Existing login debug mobs should have home positions registered for leash behavior.'
Assert-SourceMatch $clientConnectedSource 'RegisterNpcHome\s*\(\s*mobCharacter\s*\)' 'New login debug mobs should have home positions registered for leash behavior.'
Assert-SourceMatch $baseInventorySource 'InventoryItemRules\.HasSameUniqueItem' 'Inventory add path must use shared unique-item rules.'
Assert-SourceMatch $playfieldSource 'InventoryItemRules\.HasSameUniqueItem' 'Corpse loot path must use shared unique-item rules.'
Assert-SourceMatch $playfieldSource 'DespawnNpcImmediately\s*\(' 'Playfield should expose immediate NPC despawn for controlled GM test cleanup.'
Assert-SourceMatch $playfieldSource 'DespawnCorpses\s*\(' 'Playfield should expose corpse cleanup for controlled GM test cleanup.'
Assert-SourceMatch $zoneProjectSource 'Core\\Packets\\CorpseFullUpdate\.cs' 'ZoneEngine project should compile the dedicated CorpseFullUpdate packet builder.'
Assert-SourceMatch $playfieldSource 'target\.Stats\s*\[\s*StatIds\.health\s*\]\.Value\s*=\s*newHealth\s*;\s*target\.SendChangedStats\s*\(\s*\)\s*;.*?new\s+AttackInfoMessage' 'Combat hits, including killing hits, should send the zero-health stat update before death cleanup.'
Assert-SourceNoMatch $playfieldSource 'DoCombatTick\s*\(ICharacter\s+attacker\).*?HealthDamage' 'Normal auto-attacks must stay AttackInfo-only; HealthDamage belongs only in capture-backed non-weapon damage/heal/status paths.'
Assert-SourceMatch $livePacketGapsSource '## HealthDamage Policy.*?auto-attacks must remain `AttackInfo` only.*?DoCombatTick.*?duplicate combat text.*?targeted capture.*?DoT.*?HoT.*?nano.*?environmental' 'HealthDamage packet policy should document why normal weapon hits stay AttackInfo-only and which future paths need targeted capture evidence.'
Assert-SourceMatch $playfieldSource 'MaxMeleeCombatDistance\s*=\s*8\.0' 'Combat ticks should use a conservative melee range gate.'
Assert-SourceMatch $playfieldSource 'MaxNpcLeashDistance\s*=\s*40\.0' 'NPC combat should have a conservative home leash distance.'
Assert-SourceMatch $playfieldSource 'RegisterNpcHome\s*\(ICharacter\s+character\).*?npcHomeStates\s*\[\s*character\.Identity\.Instance\s*\].*?new\s+Coordinate\s*\(\s*character\.Coordinates\s*\(\s*\)\s*\)' 'Playfield should track spawned NPC home coordinates.'
Assert-SourceMatch $playfieldSource 'DoCombatTick\s*\(ICharacter\s+attacker\).*?TryLeashNpcAttacker\s*\(\s*attacker\s*\).*?return\s*;.*?!this\.IsInCombatRange' 'NPC leash checks should run before range-based pursuit/damage.'
Assert-SourceMatch $playfieldSource 'DoCombatTick\s*\(ICharacter\s+attacker\).*?CombatAttackSource\s+attackSource\s*=\s*this\.GetCombatAttackSource\s*\(\s*attacker\s*\).*?!this\.IsInCombatRange\s*\(\s*attacker,\s*target,\s*attackSource\.Range\s*\).*?TryMoveNpcIntoCombatRange\s*\(\s*attacker,\s*target\s*\).*?nextCombatTicks\s*\[\s*attacker\.Identity\.Instance\s*\]\s*=\s*DateTime\.UtcNow\s*\+\s*TimeSpan\.FromSeconds\s*\(\s*OutOfRangeRetrySeconds\s*\).*?return\s*;.*?int\s+currentHealth' 'Combat ticks should use equipped weapon range and avoid dealing damage while attacker and target are out of range.'
Assert-SourceMatch $playfieldSource 'GetEquippedCombatWeapon\s*\(ICharacter\s+attacker\).*?IdentityType\.WeaponPage.*?WeaponSlots\.Righthand.*?WeaponSlots\.LeftHand' 'Combat attack source should read equipped weapons from the weapon page right/left hand slots.'
Assert-SourceMatch $playfieldSource 'GetCombatAttackSource\s*\(ICharacter\s+attacker\).*?weapon\.GetAttribute\s*\(\s*\(int\)StatIds\.mindamage\s*\).*?weapon\.GetAttribute\s*\(\s*\(int\)StatIds\.maxdamage\s*\).*?weapon\.GetAttribute\s*\(\s*\(int\)StatIds\.damagebonus\s*\).*?NormalizeCombatRange\s*\(\s*weapon\.GetAttribute\s*\(\s*\(int\)StatIds\.attackrange\s*\)\s*\).*?NormalizeCombatDelaySeconds\s*\(\s*weapon\.GetAttribute\s*\(\s*\(int\)StatIds\.itemdelay\s*\).*?weapon\.GetAttribute\s*\(\s*\(int\)StatIds\.rechargedelay\s*\)' 'Equipped weapon combat should use item damage, range, attack delay, and recharge delay stats.'
Assert-SourceMatch $playfieldSource 'TryMoveNpcIntoCombatRange\s*\(ICharacter\s+attacker,\s*ICharacter\s+target\).*?attacker\.Controller\s+is\s+NPCController.*?attacker\.Controller\.IsFollowing\s*\(\s*\).*?attacker\.Controller\.Follow\s*\(\s*target\.Identity\s*\)' 'Out-of-range NPC attackers should pursue their combat target instead of landing free hits.'
Assert-SourceMatch $playfieldSource 'TryLeashNpcAttacker\s*\(ICharacter\s+attacker\).*?attacker\.Controller\s+as\s+NPCController.*?Distance2D\s*\(\s*homeState\.Coordinates\.coordinate\s*\).*?MaxNpcLeashDistance.*?attacker\.SetTarget\s*\(\s*Identity\.None\s*\).*?attacker\.SetFightingTarget\s*\(\s*Identity\.None\s*\).*?StopFightingDeadTarget\s*\(\s*attacker\.Identity\s*\).*?npcController\.MoveTo' 'NPC attackers should clear combat and return home after exceeding their leash distance.'
Assert-SourceMatch $playfieldSource 'KillNpcTarget\s*\(ICharacter\s+target\).*?MarkNpcDead\s*\(\s*target\s*\).*?StopFightingDeadTarget\s*\(\s*target\.Identity\s*\).*?SendNpcDeathAnimation\s*\(\s*target\s*\).*?ScheduleCorpseSpawn\s*\(\s*target\s*,\s*corpseIdentity\s*\).*?deadNpcDespawnTicks\s*\[\s*target\.Identity\.Instance\s*\]\s*=\s*DateTime\.UtcNow\s*\+\s*DeadNpcDespawnDelay\s*;' 'NPC death should mark dead, stop fighting, play death, schedule corpse, and delay despawn.'
Assert-SourceMatch $playfieldSource 'MarkNpcDead\s*\(ICharacter\s+target\).*?Stats\s*\[\s*StatIds\.deadtimer\s*\]\.Value\s*=\s*1\s*;.*?Stats\s*\[\s*StatIds\.corpseanimkey\s*\]\.Value\s*=\s*DeathAnimationKeyFor\s*\(\s*target\s*\)\s*;.*?Stats\s*\[\s*StatIds\.dieanim\s*\]\.Value\s*=\s*DeathAnimationKeyFor\s*\(\s*target\s*\)\s*;.*?Stats\s*\[\s*StatIds\.healdelta\s*\]\.Value\s*=\s*0\s*;.*?DoNotDoTimers\s*=\s*true\s*;' 'NPC death stats should disable healing/timers and expose death animation keys.'
Assert-SourceMatch $playfieldSource 'TryUseDeadNpcCorpse\s*\(ICharacter\s+looter,\s*Identity\s+deadNpcIdentity,\s*out\s+Identity\s+corpseIdentity\).*?DeadNpcIdentity\.Instance\s*==\s*deadNpcIdentity\.Instance.*?corpseIdentity\s*=\s*corpse\.CorpseIdentity\s*;.*?return\s+this\.TryUseCorpse\s*\(\s*looter\s*,\s*corpse\.CorpseIdentity\s*\)\s*;' 'Using a dead NPC dynel should route to its registered corpse identity.'
Assert-SourceMatch $playfieldSource 'TryUseCorpse\s*\(ICharacter\s+looter,\s*Identity\s+corpseIdentity\).*?corpse\.Opened\s*=\s*true\s*;.*?corpse\.HasUnlootedItems.*?ExtendCorpseLifetime\s*\(\s*corpse\s*,\s*CombatCorpseRules\.ItemLootCorpseLifetime\s*,\s*"corpse-use"\s*\).*?NextUseSendsAccessActionOnly.*?SendCorpseLootAccessAction\s*\(\s*looter\s*,\s*corpse\s*\).*?SendUseActionFinished\s*\(\s*looter\s*\).*?NextUseSendsAccessActionOnly\s*=\s*false\s*;.*?SendCorpseInventoryUpdate\s*\(\s*looter\s*,\s*corpse\s*\).*?NextUseSendsAccessActionOnly\s*=\s*true\s*;' 'Corpse reopen with remaining loot should alternate access action and inventory update packets.'
Assert-SourceMatch $playfieldSource 'TryUseCorpse\s*\(ICharacter\s+looter,\s*Identity\s+corpseIdentity\).*?!corpse\.HasUnlootedItems.*?ScheduleCorpseDespawn\s*\(\s*corpse\s*,\s*CombatCorpseRules\.EmptyCorpseCleanupAfterOpenedDelay\s*,\s*"opened-empty"\s*\)' 'Opening an empty corpse should schedule the short cleanup timer.'
Assert-SourceMatch $combatCorpseRulesSource 'EmptyCorpseCleanupAfterOpenedDelay\s*=\s*TimeSpan\.FromSeconds\s*\(\s*3\s*\)' 'Empty opened/looted corpses should use the capture-backed roughly three second cleanup delay.'
Assert-SourceMatch $playfieldSource 'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\).*?sourceContainer\.Type\s*!=\s*IdentityType\.Backpack.*?int\s+corpseInventorySlot\s*=\s*\(sourceContainer\.Instance\s*>>\s*16\)\s*&\s*0xffff\s*;.*?int\s+requestedLootSlot\s*=\s*sourceContainer\.Instance\s*&\s*0xffff\s*;' 'Corpse item moves should decode the Backpack source container into corpse inventory and loot slots.'
Assert-SourceMatch $playfieldSource 'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\).*?target\s*!=\s*looter\.Identity.*?SendUseActionFinished\s*\(\s*looter\s*\)' 'Corpse item moves should reject target mismatches without falling through to normal inventory moves.'
Assert-SourceMatch $playfieldSource 'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\).*?CharacterHasUniqueItemAlready\s*\(\s*looter\s*,\s*lootItem\.Item\s*\).*?You already have this unique item\.' 'Corpse loot should reject duplicate UNIQUE items before adding to inventory.'
Assert-SourceMatch $playfieldSource 'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\).*?lootItem\.Looted\s*=\s*true\s*;.*?ContainerAddItemMessageHandler\.Default\.Send\s*\(.*?!corpse\.HasUnlootedItems.*?ScheduleCorpseDespawn\s*\(\s*corpse\s*,\s*CombatCorpseRules\.EmptyCorpseCleanupAfterOpenedDelay\s*,\s*"looted-empty"\s*\).*?ExtendCorpseLifetime\s*\(\s*corpse\s*,\s*CombatCorpseRules\.ItemLootCorpseLifetime\s*,\s*"loot-remaining"\s*\)' 'Successful corpse loot should mark the item, notify the client, despawn empty corpses, and keep corpses with remaining loot alive.'
Assert-SourceMatch $playfieldSource 'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\).*?inventoryError\s*!=\s*InventoryError\.OK.*?return\s+true\s*;.*?looter\.BaseInventory\.Write\s*\(\s*\)\s*;\s*lootItem\.Looted\s*=\s*true' 'Successful corpse loot should persist the added inventory item before marking the corpse item looted.'
Assert-SourceMatch $playfieldSource 'CharacterHasUniqueItemAlready\s*\(ICharacter\s+character,\s*IItem\s+item\).*?InventoryItemRules\.HasSameUniqueItem.*?BaseInventory.*?Pages\.Values' 'Corpse unique checks should inspect existing inventory items through shared rules.'
Assert-SourceMatch $playfieldSource 'ProcessPendingCorpseSpawns\s*\(\s*\).*?pendingCorpseSpawns\.Remove\s*\(\s*corpse\.DeadNpcIdentity\.Instance\s*\).*?RegisterCorpse\s*\(\s*target\s*,\s*corpse\.CorpseIdentity\s*\).*?SendCorpseFullUpdate\s*\(\s*target\s*,\s*corpse\.CorpseIdentity\s*\)' 'Pending corpse spawns should register state before sending CorpseFullUpdate.'
Assert-SourceMatch $playfieldSource 'SendCorpseFullUpdate\s*\(ICharacter\s+target,\s*Identity\s+corpseIdentity\).*?CorpseFullUpdate\.Build\s*\(\s*target,\s*corpseIdentity,\s*character\.Identity,\s*this\.server\.Id,\s*corpseCatMesh,\s*corpseMonsterData\s*\)' 'Playfield corpse lifecycle should delegate raw CorpseFullUpdate construction to the packet builder.'
Assert-SourceNoMatch $playfieldSource 'BuildDebugCorpseFullUpdate|HexToBytes\s*\(' 'Playfield should not own raw CorpseFullUpdate template bytes.'
Assert-SourceMatch $corpseFullUpdateSource 'public\s+static\s+class\s+CorpseFullUpdate.*?public\s+static\s+byte\[\]\s+Build\s*\(.*?ICharacter\s+deadNpc.*?Identity\s+corpseIdentity.*?Identity\s+receiver.*?int\s+serverId.*?int\s+corpseCatMesh.*?int\s+corpseMonsterData' 'CorpseFullUpdate packet builder should expose a named build surface for runtime corpse values.'
Assert-SourceMatch $corpseFullUpdateSource 'CorpseFullUpdate resumes immediately after the encoded string.*?WritePacketLength\s*\(buffer,\s*buffer\.Length\).*?WriteInt32\s*\(buffer,\s*CorpseInstanceOffset,\s*corpseIdentity\.Instance\).*?WriteInt32\s*\(buffer,\s*CorpseCatMeshOffset,\s*corpseCatMesh\).*?WriteInt32\s*\(buffer,\s*CorpseMonsterDataOffset\s*\+\s*afterNameDelta,\s*corpseMonsterData\)' 'CorpseFullUpdate builder should keep the live-shaped variable-name tail and explicit visual offsets.'
Assert-SourceMatch $playfieldSource 'ProcessCorpseDespawns\s*\(\s*\).*?corpseDespawnTicks.*?Where\s*\(x\s*=>\s*x\.Value\s*<=\s*DateTime\.UtcNow\).*?DespawnCorpse\s*\(\s*corpseInstance\s*\)' 'Expired corpses should be despawned by the playfield timer.'
Assert-SourceMatch $playfieldSource 'RegisterCorpse\s*\(ICharacter\s+target,\s*Identity\s+corpseIdentity\).*?RollCorpseLootItems\s*\(\s*target\s*\).*?CorpseLootClassFor\s*\(\s*target\s*,\s*lootItems\s*\).*?CorpseLifetimeFor\s*\(\s*lootClass\s*\).*?corpses\s*\[\s*corpseIdentity\.Instance\s*\]\s*=\s*state\s*;.*?corpseDespawnTicks\s*\[\s*corpseIdentity\.Instance\s*\]\s*=\s*expiresAtUtc\s*;' 'Registered corpses should roll loot, choose a lifetime, and register the despawn tick.'
Assert-SourceMatch $playfieldSource 'RollCorpseLootItems\s*\(ICharacter\s+target\).*?DebugLootTable.*?matchingEntries\.Count\s*==\s*0.*?GetDatabaseLootTable\s*\(\s*\)' 'Corpse loot should keep deterministic test loot first and fall back to DB-backed loot for real configured mobs.'
Assert-SourceMatch $playfieldSource 'RollCorpseLootItems\s*\(ICharacter\s+target\).*?matchingEntries\.Count\s*==\s*0.*?Loot roll found no configured table entries' 'Corpse loot should log when neither deterministic nor DB-backed loot entries match a mob.'
Assert-SourceMatch $playfieldSource 'GetDatabaseLootTable\s*\(\s*\).*?MobTemplateDao\.Instance\.GetAll\s*\(\s*\).*?MobDroptableDao\.Instance\.GetAll\s*\(\s*\).*?CombatMobLootCatalog\.BuildEntries' 'DB-backed mob loot should load through the existing mobtemplate/mobdroptable DAOs.'
Assert-SourceMatch $playfieldSource 'CreateLootItem\s*\(CombatLootTableEntry\s+entry,\s*int\s+targetLevel\).*?entry\.ItemTemplates.*?CreateLootItem\s*\(entry\.ItemTemplates,\s*targetLevel\).*?CreateLootItem\s*\(entry\.ItemTemplateIds,\s*entry\.Quality\)' 'Loot item creation should support DB drop candidates and existing deterministic template fallback.'
Assert-SourceMatch $livePacketGapsSource 'DB-backed corpse loot is now wired.*?CombatTestLootCatalog.*?falls back to parsed `mobtemplate`/`mobdroptable` entries.*?`A004` Beach Leet' 'Live packet gap docs should describe the current DB-backed loot fallback and the verified local Beach Leet setup.'
Assert-SourceNoMatch $livePacketGapsSource 'working local loot path is deterministic test loot, not real mob drop tables|Only after corpse tests are stable, wire a DB-backed loot roller' 'Live packet gap docs should not claim DB-backed corpse loot is still unwired.'
Assert-SourceMatch $enemyNpcMapSource 'DB-backed corpse loot rolling is wired.*?CombatTestLootCatalog.*?falls back to parsed `mobtemplate`/`mobdroptable` entries' 'Enemy/NPC map should describe the current DB-backed corpse loot path.'
Assert-SourceNoMatch $enemyNpcMapSource 'real DB-backed corpse loot rolling is not wired yet|Real loot tables exist but are unused|next non-test loot step is translating' 'Enemy/NPC map should not preserve stale DB-loot-unwired notes.'
Assert-SourceMatch $mobLootDataSource 'local `cellao_codex_clean` database.*?`A004` Beach Leet.*?real DB-backed mob loot is modeled and now wired' 'Mob loot data docs should remain the detailed source for verified local DB loot coverage.'
Assert-SourceOrdered $playfieldSource @(
    'KillNpcTarget\s*\(ICharacter\s+target\)',
    'CanBuildKnownCorpseVisual\s*\(\s*target\s*\)',
    'AllocateCorpseIdentity\s*\(\s*\)',
    'MarkNpcDead\s*\(\s*target\s*\)',
    'StopFightingDeadTarget\s*\(\s*target\.Identity\s*\)',
    'SendNpcDeathAnimation\s*\(\s*target\s*\)',
    'ScheduleCorpseSpawn\s*\(\s*target\s*,\s*corpseIdentity\s*\)',
    'deadNpcDespawnTicks\s*\[\s*target\.Identity\.Instance\s*\]'
) 'Corpse flow regression: NPC death should allocate/register corpse work before delayed dead-NPC despawn.'
Assert-SourceOrdered $playfieldSource @(
    'ProcessPendingCorpseSpawns\s*\(\s*\)',
    'pendingCorpseSpawns\.Remove\s*\(\s*corpse\.DeadNpcIdentity\.Instance\s*\)',
    'FindByIdentity<ICharacter>\s*\(\s*corpse\.DeadNpcIdentity\s*\)',
    'RegisterCorpse\s*\(\s*target\s*,\s*corpse\.CorpseIdentity\s*\)',
    'SendCorpseFullUpdate\s*\(\s*target\s*,\s*corpse\.CorpseIdentity\s*\)'
) 'Corpse flow regression: pending corpse processing should register server state before sending CorpseFullUpdate.'
Assert-SourceOrdered $playfieldSource @(
    'RegisterCorpse\s*\(ICharacter\s+target,\s*Identity\s+corpseIdentity\)',
    'RollCorpseLootItems\s*\(\s*target\s*\)',
    'CorpseLootClassFor\s*\(\s*target,\s*lootItems\s*\)',
    'CorpseLifetimeFor\s*\(\s*lootClass\s*\)',
    'InventorySlot\s*=\s*this\.AllocateCorpseInventorySlot\s*\(\s*\)',
    'corpses\s*\[\s*corpseIdentity\.Instance\s*\]\s*=\s*state',
    'corpseDespawnTicks\s*\[\s*corpseIdentity\.Instance\s*\]\s*=\s*expiresAtUtc'
) 'Corpse flow regression: RegisterCorpse should roll loot, allocate a loot-window slot, and arm the despawn tick.'
Assert-SourceOrdered $genericCmdSource @(
    'case\s+GenericCmdAction\.Use',
    'target\.Type\s*==\s*IdentityType\.Corpse',
    'TryUseCorpse\s*\(',
    'AcknowledgeCorpseUse\s*\(',
    'target\.Type\s*==\s*IdentityType\.CanbeAffected',
    'TryRouteDeadNpcCorpseUse\s*\(',
    'AcknowledgeCorpseUse\s*\('
) 'Corpse flow regression: GenericCmd Use should handle corpse identities and route dead NPC dynels before statel fallback.'
Assert-SourceOrdered $playfieldSource @(
    'TryUseCorpse\s*\(ICharacter\s+looter,\s*Identity\s+corpseIdentity\)',
    'corpse\.Opened\s*=\s*true',
    'corpse\.HasUnlootedItems',
    'ExtendCorpseLifetime\s*\(\s*corpse,\s*CombatCorpseRules\.ItemLootCorpseLifetime,\s*"corpse-use"\s*\)',
    'SendCorpseLootAccessAction\s*\(\s*looter,\s*corpse\s*\)',
    'SendUseActionFinished\s*\(\s*looter\s*\)',
    'SendCorpseInventoryUpdate\s*\(\s*looter,\s*corpse\s*\)',
    'ScheduleCorpseDespawn\s*\(\s*corpse,\s*CombatCorpseRules\.EmptyCorpseCleanupAfterOpenedDelay,\s*"opened-empty"\s*\)'
) 'Corpse flow regression: corpse use should open loot, alternate access/inventory responses, and short-despawn empty corpses.'
Assert-SourceOrdered $playfieldSource @(
    'SendCorpseInventoryUpdate\s*\(ICharacter\s+looter,\s*CorpseState\s+corpse\)',
    'Where\s*\(x\s*=>\s*!x\.Looted\)',
    'corpse\.InventorySlot\s*=\s*this\.AllocateCorpseInventorySlot\s*\(\s*\)',
    'new\s+InventoryUpdateMessage',
    'NumberOfSlots\s*=\s*CombatCorpseRules\.CorpseInventorySlots',
    'Entries\s*=\s*entries',
    'BagIdentity\s*=\s*corpse\.CorpseIdentity',
    'SlotnumberInMainInventory\s*=\s*corpse\.InventorySlot'
) 'Corpse flow regression: corpse use should send InventoryUpdate for only unlooted items on the corpse bag identity.'
Assert-SourceOrdered $clientMoveItemSource @(
    'Read\s*\(ClientMoveItemToInventoryMessage\s+message,\s*IZoneClient\s+client\)',
    'TryLootCorpseItem\s*\(',
    'return\s*;',
    'TryMoveOwnedInventoryItem\s*\('
) 'Corpse flow regression: ClientMoveItemToInventory should offer loot-window moves to corpse handling before normal inventory moves.'
Assert-SourceOrdered $containerAddItemSource @(
    'Read\s*\(ContainerAddItemMessage\s+message,\s*IZoneClient\s+client\)',
    'TryLootCorpseItem\s*\(',
    'return\s*;',
    'Pool\.Instance\.GetObject<IInventoryPage>'
) 'Corpse flow regression: ContainerAddItem should offer loot-window moves to corpse handling before normal inventory moves.'
Assert-SourceOrdered $playfieldSource @(
    'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\)',
    'sourceContainer\.Type\s*!=\s*IdentityType\.Backpack',
    'corpseInventorySlot\s*=\s*\(sourceContainer\.Instance\s*>>\s*16\)\s*&\s*0xffff',
    'requestedLootSlot\s*=\s*sourceContainer\.Instance\s*&\s*0xffff',
    'FindCorpseLootItem\s*\(\s*corpse,\s*requestedLootSlot\s*\)',
    'CharacterHasUniqueItemAlready\s*\(\s*looter,\s*lootItem\.Item\s*\)',
    'TryResolveLootTargetSlot\s*\(',
    'looter\.BaseInventory\.AddToPage\s*\(',
    'looter\.BaseInventory\.Write\s*\(\s*\)',
    'lootItem\.Looted\s*=\s*true',
    'ContainerAddItemMessageHandler\.Default\.Send\s*\(',
    'ScheduleCorpseDespawn\s*\(\s*corpse,\s*CombatCorpseRules\.EmptyCorpseCleanupAfterOpenedDelay,\s*"looted-empty"\s*\)',
    'ExtendCorpseLifetime\s*\(\s*corpse,\s*CombatCorpseRules\.ItemLootCorpseLifetime,\s*"loot-remaining"\s*\)'
) 'Corpse flow regression: corpse item moves should decode source slots, persist inventory before consuming loot, notify the client, and update corpse lifetime.'
Assert-SourceOrdered $playfieldSource @(
    'ProcessCorpseDespawns\s*\(\s*\)',
    'corpseDespawnTicks',
    'Where\s*\(x\s*=>\s*x\.Value\s*<=\s*DateTime\.UtcNow\)',
    'DespawnCorpse\s*\(\s*corpseInstance\s*\)',
    'DespawnCorpse\s*\(int\s+corpseInstance\)',
    'this\.Despawn\s*\(\s*corpseIdentity\s*\)',
    'corpseDespawnTicks\.Remove\s*\(\s*corpseInstance\s*\)',
    'corpses\.Remove\s*\(\s*corpseInstance\s*\)'
) 'Corpse flow regression: expired corpses should despawn and clear both corpse registries.'
Assert-SourceMatch $playfieldSource 'StopFightingDeadTarget\s*\(Identity\s+deadTarget\).*?character\.FightingTarget\s*==\s*deadTarget.*?SetFightingTarget\s*\(\s*Identity\.None\s*\).*?nextCombatTicks\.Remove\s*\(\s*character\.Identity\.Instance\s*\).*?SendCombatStopMessage\s*\(\s*character\s*\)' 'Killing an NPC should clear attackers from fight stance and stop their combat tick.'
Assert-SourceMatch $playfieldSource 'SendCorpseInventoryUpdate\s*\(ICharacter\s+looter,\s*CorpseState\s+corpse\).*?Where\s*\(x\s*=>\s*!x\.Looted\).*?NumberOfSlots\s*=\s*CombatCorpseRules\.CorpseInventorySlots.*?BagIdentity\s*=\s*corpse\.CorpseIdentity.*?SlotnumberInMainInventory\s*=\s*corpse\.InventorySlot' 'Corpse InventoryUpdate should expose only unlooted items on the corpse bag identity.'
Assert-SourceMatch $spawnCommandSource '"status"' 'Spawn command should support combat test status.'
Assert-SourceMatch $spawnCommandSource '"lootstatus"' 'Spawn command should support DB loot coverage diagnostics.'
Assert-SourceMatch $spawnCommandSource '"clear"' 'Spawn command should support combat test cleanup.'
Assert-SourceMatch $spawnCommandSource 'if\s*\(\s*args\.Length\s*==\s*2\s*\).*?return\s+true\s*;' 'Spawn command should accept DB template hash-only commands like /command spawn A004.'
Assert-SourceMatch $spawnCommandSource 'mobCharacter\.Stats\s*\[\s*StatIds\.health\s*\]\.Value\s*=\s*mobCharacter\.Stats\s*\[\s*StatIds\.life\s*\]\.Value\s*;.*?SimpleCharFullUpdate\.ConstructMessage\s*\(\s*mobCharacter\s*\)' 'DB-spawned mobs should have current health populated before SimpleCharFullUpdate is sent.'
Assert-SourceMatch $spawnCommandSource 'new\s+CharInPlayMessage\s*\{\s*Identity\s*=\s*mobCharacter\.Identity\s*,\s*Unknown\s*=\s*0x00\s*\}' 'DB-spawned mobs should send CharInPlay after SimpleCharFullUpdate.'
Assert-SourceMatch $spawnCommandSource 'SpawnClientHintedMobs\s*\(ICharacter\s+character\).*?SpawnPopulationMob\s*\(.*?ZonePopulationOffsets.*?Spawned\s*\{0\}\s*DB population mobs' 'Spawn zone should create a DB-backed population pack rather than another single test mob.'
Assert-SourceMatch $spawnCommandSource 'SpawnPopulationMob\s*\(.*?NonPlayerCharacterHandler\.SpawnMobFromTemplate.*?CombatTestMobArchetype\.Prepare\s*\(\s*mobCharacter,\s*entry\s*\).*?SimpleCharFullUpdate\.ConstructMessage\s*\(\s*mobCharacter\s*\).*?DB population mob spawned' 'Population mobs should use real DB templates, prepared combat/death stats, and visible client spawn packets.'
Assert-SourceMatch $spawnCommandSource 'RegisterNpcHome\s*\(\s*mobCharacter\s*\)' 'GM-spawned test and DB mobs should register home positions for leash behavior.'
Assert-SourceMatch $spawnCommandSource 'ShowLootStatus\s*\(ICharacter\s+character\).*?MobTemplateDao\.Instance\.GetAll\s*\(\s*\).*?MobDroptableDao\.Instance\.GetAll\s*\(\s*\).*?CombatMobLootCatalog\.BuildEntries.*?configuredTemplates.*?parsed entries' 'Spawn lootstatus should report mobtemplate/mobdroptable coverage and parsed DB loot entries.'
Assert-SourceMatch $spawnCommandSource 'HasDropConfiguration\s*\(DBMobTemplate\s+template\).*?DropHashes.*?DropSlots.*?DropRates' 'Spawn lootstatus should treat any configured drop field as loot configuration.'
Assert-True (Test-Path $zoneEnemyHintsPath) 'Client RDB zone enemy hint catalog is missing.'
Assert-True (Test-Path $npcTemplateHintsPath) 'Client RDB NPC template hint catalog is missing.'
Assert-True (Test-Path $enemyCoveragePath) 'Client hinted enemy coverage catalog is missing.'
Assert-True (Test-Path $visualHintsPath) 'MonsterData corpse visual hint catalog is missing.'

$zoneEnemyHints = @(Import-Csv $zoneEnemyHintsPath)
$npcTemplateHints = @(Import-Csv $npcTemplateHintsPath)
$enemyCoverage = @(Import-Csv $enemyCoveragePath)
$visualHints = @(Import-Csv $visualHintsPath)
$newlandDesertHints = $zoneEnemyHints | Where-Object { [int]$_.PlayfieldId -eq 565 } | Select-Object -First 1
Assert-True ($null -ne $newlandDesertHints) 'Client RDB hints should include Newland Desert playfield 565.'
Assert-True ($newlandDesertHints.EnemyKeywords -match 'rhinoman') 'Newland Desert hints should include rhinoman.'
Assert-True ($newlandDesertHints.EnemyKeywords -match 'leet') 'Newland Desert hints should include leet.'
Assert-True ($newlandDesertHints.EnemyKeywords -match 'snake') 'Newland Desert hints should include snake.'
Assert-True ($newlandDesertHints.EnemyKeywords -match 'flea') 'Newland Desert hints should include flea.'
Assert-True ($newlandDesertHints.EnemyKeywords -match 'lizard') 'Newland Desert hints should include lizard.'
Assert-True ($newlandDesertHints.EnemyKeywords -match 'salamander') 'Newland Desert hints should include salamander.'
Assert-True ($null -ne ($npcTemplateHints | Where-Object { [int]$_.TemplateId -eq 17655 -and $_.Name -eq 'leet' } | Select-Object -First 1)) 'Client RDB NPC hints should include leet template 17655.'
Assert-True ($null -ne ($npcTemplateHints | Where-Object { [int]$_.TemplateId -eq 17687 -and $_.Name -eq 'rollerrat' } | Select-Object -First 1)) 'Client RDB NPC hints should include rollerrat template 17687.'
Assert-True ($null -ne ($npcTemplateHints | Where-Object { [int]$_.TemplateId -eq 30252 -and $_.Name -eq 'giant snake' } | Select-Object -First 1)) 'Client RDB NPC hints should include giant snake template 30252.'
Assert-True ($null -ne ($npcTemplateHints | Where-Object { [int]$_.TemplateId -eq 31114 -and $_.Name -eq 'rhinoman female' } | Select-Object -First 1)) 'Client RDB NPC hints should include rhinoman female template 31114.'

$newlandCoverage = $enemyCoverage | Where-Object { [int]$_.PlayfieldId -eq 565 } | Select-Object -First 1
$omnilabCoverage = $enemyCoverage | Where-Object { [int]$_.PlayfieldId -eq 346 } | Select-Object -First 1
$neutralInsuranceCoverage = $enemyCoverage | Where-Object { [int]$_.PlayfieldId -eq 1510 } | Select-Object -First 1
Assert-True ($null -ne $newlandCoverage) 'Enemy coverage should include Newland Desert playfield 565.'
Assert-True ($newlandCoverage.SupportedTestMobKeys -match 'beachleet') 'Newland Desert coverage should include beachleet.'
Assert-True ($newlandCoverage.SupportedTestMobKeys -match 'shoresnake') 'Newland Desert coverage should include shoresnake.'
Assert-True ($newlandCoverage.SupportedTestMobKeys -match 'duneflea') 'Newland Desert coverage should include duneflea.'
Assert-True ($newlandCoverage.SupportedTestMobKeys -match 'surflizard') 'Newland Desert coverage should include surflizard.'
Assert-True ($newlandCoverage.SupportedTestMobKeys -match 'reefsalamander') 'Newland Desert coverage should include reefsalamander.'
Assert-True ($newlandCoverage.MissingTemplateOrVisualKeywords -match 'rhinoman') 'Newland Desert coverage should keep unsupported rhinoman visible.'
Assert-True ($null -ne $omnilabCoverage -and $omnilabCoverage.SupportedTestMobKeys -match 'alienspider') 'Omnilab coverage should include the mapped alien spider test mob.'
Assert-True ($null -ne $neutralInsuranceCoverage -and $neutralInsuranceCoverage.IgnoredWeakEvidenceKeywords -match 'spider') 'Statel-only spider-like text should stay weak evidence, not supported spawn coverage.'
Assert-True ($null -ne ($visualHints | Where-Object { $_.FamilyKeyword -eq 'flea' -and [int]$_.MonsterData -eq 17657 -and [int]$_.CatMesh -eq 15231 } | Select-Object -First 1)) 'Visual hints should map flea MonsterData 17657 to CATMesh 15231.'
Assert-True ($null -ne ($visualHints | Where-Object { $_.FamilyKeyword -eq 'lizard' -and [int]$_.MonsterData -eq 22794 -and [int]$_.CatMesh -eq 22773 } | Select-Object -First 1)) 'Visual hints should map lizard MonsterData 22794 to CATMesh 22773.'
Assert-True ($null -ne ($visualHints | Where-Object { $_.FamilyKeyword -eq 'malle' -and [int]$_.MonsterData -eq 17660 -and [int]$_.CatMesh -eq 15239 } | Select-Object -First 1)) 'Visual hints should map malle MonsterData 17660 to CATMesh 15239.'
Assert-True ($null -ne ($visualHints | Where-Object { $_.FamilyKeyword -eq 'salamander' -and [int]$_.MonsterData -eq 30354 -and [int]$_.CatMesh -eq 23344 } | Select-Object -First 1)) 'Visual hints should map salamander MonsterData 30354 to CATMesh 23344.'
Assert-True ($null -ne ($visualHints | Where-Object { $_.FamilyKeyword -eq 'spider' -and [int]$_.MonsterData -eq 247728 -and [int]$_.CatMesh -eq 31774 } | Select-Object -First 1)) 'Visual hints should map spider MonsterData 247728 to CATMesh 31774.'

if (-not $SkipBuild) {
    Assert-True (Test-Path $msbuild) "MSBuild was not found at $msbuild"
    Stop-BuiltOutputProcesses -BuiltDirectory $builtDir
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
    $databaseAssembly = [System.Reflection.Assembly]::LoadFrom((Join-Path $builtDir 'CellAO.Database.dll'))
    $archetypeType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatTestMobArchetype'
    $rulesType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatCorpseRules'
    $damageRulesType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatDamageRules'
    $npcAiProfileType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.NpcAiProfile'
    $npcAiProfilesType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.NpcAiProfiles'
    $visualsType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatCorpseVisuals'
    $lootCatalogType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatTestLootCatalog'
    $mobLootCatalogType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatMobLootCatalog'
    $playfieldType = Get-RequiredType $zoneAssembly 'CellAO.Core.Playfields.Playfield'
    $inventoryRulesType = Get-RequiredType $coreAssembly 'CellAO.Core.Inventory.InventoryItemRules'
    $mobTemplateType = Get-RequiredType $databaseAssembly 'CellAO.Database.Dao.DBMobTemplate'
    $mobDropType = Get-RequiredType $databaseAssembly 'CellAO.Database.Entities.DBMobDroptable'

    Get-RequiredMethod $playfieldType 'TryUseCorpse' ([System.Reflection.BindingFlags]'Public, Instance') | Out-Null
    Get-RequiredMethod $playfieldType 'TryUseDeadNpcCorpse' ([System.Reflection.BindingFlags]'Public, Instance') | Out-Null
    Get-RequiredMethod $playfieldType 'TryLootCorpseItem' ([System.Reflection.BindingFlags]'Public, Instance') | Out-Null
    Get-RequiredMethod $playfieldType 'RegisterCorpse' ([System.Reflection.BindingFlags]'NonPublic, Instance') | Out-Null
    Get-RequiredMethod $playfieldType 'SendCorpseFullUpdate' ([System.Reflection.BindingFlags]'NonPublic, Instance') | Out-Null
    Get-RequiredMethod $playfieldType 'SendCorpseInventoryUpdate' ([System.Reflection.BindingFlags]'NonPublic, Instance') | Out-Null
    Get-RequiredMethod $playfieldType 'DespawnCorpse' ([System.Reflection.BindingFlags]'NonPublic, Instance') | Out-Null

    $isUniqueFlags = Get-RequiredMethod $inventoryRulesType 'IsUniqueFlags' ([System.Reflection.BindingFlags]'Public, Static')
    $isSameTemplateIds = Get-RequiredMethod $inventoryRulesType 'IsSameTemplateIdPair' ([System.Reflection.BindingFlags]'Public, Static')
    Assert-True ([bool]$isUniqueFlags.Invoke($null, @(0x08000000))) 'Unique item flag should be detected.'
    Assert-True (-not [bool]$isUniqueFlags.Invoke($null, @(0))) 'Zero item flags should not be unique.'
    Assert-True ([bool]$isSameTemplateIds.Invoke($null, @(100, 101, 100, 101))) 'Matching low/high template ids should be treated as the same item.'
    Assert-True (-not [bool]$isSameTemplateIds.Invoke($null, @(100, 101, 100, 102))) 'Different high template ids should not be treated as the same item.'
    Assert-True (-not [bool]$isSameTemplateIds.Invoke($null, @(100, 101, 102, 101))) 'Different low template ids should not be treated as the same item.'

    $passiveProfile = [System.Enum]::Parse($npcAiProfileType, 'Passive')
    $aggressiveProfile = [System.Enum]::Parse($npcAiProfileType, 'Aggressive')
    $socialProfile = [System.Enum]::Parse($npcAiProfileType, 'Social')
    $canRetaliate = Get-RequiredMethod $npcAiProfilesType 'CanRetaliate' ([System.Reflection.BindingFlags]'Public, Static')
    $canProximityAggro = Get-RequiredMethod $npcAiProfilesType 'CanProximityAggro' ([System.Reflection.BindingFlags]'Public, Static')
    Assert-True ([bool]$canRetaliate.Invoke($null, @($passiveProfile))) 'Passive NPCs should retaliate when attacked.'
    Assert-True ([bool]$canRetaliate.Invoke($null, @($aggressiveProfile))) 'Aggressive NPCs should retaliate when attacked.'
    Assert-True (-not [bool]$canRetaliate.Invoke($null, @($socialProfile))) 'Social NPCs should not enter normal combat.'
    Assert-True (-not [bool]$canProximityAggro.Invoke($null, @($passiveProfile))) 'Passive NPCs should not proximity aggro.'
    Assert-True ([bool]$canProximityAggro.Invoke($null, @($aggressiveProfile))) 'Aggressive NPCs should be eligible for proximity aggro.'
    Assert-True (-not [bool]$canProximityAggro.Invoke($null, @($socialProfile))) 'Social NPCs should not proximity aggro.'

    $allField = $archetypeType.GetField('All', [System.Reflection.BindingFlags]'Public, Static')
    Assert-True ($null -ne $allField) 'CombatTestMobArchetype.All is missing.'
    $entries = @($allField.GetValue($null))
    Assert-True ($entries.Count -ge 9) "Expected at least 9 combat test mobs, found $($entries.Count)."

    $defaultProperty = $archetypeType.GetProperty('Default', [System.Reflection.BindingFlags]'Public, Static')
    Assert-True ($null -ne $defaultProperty) 'CombatTestMobArchetype.Default is missing.'
    $defaultEntry = $defaultProperty.GetValue($null, $null)
    Assert-True ([object]::ReferenceEquals($defaultEntry, $entries[0])) 'Default combat test mob should be the first catalog entry.'

    $tryGetByAlias = Get-RequiredMethod $archetypeType 'TryGetByAlias' ([System.Reflection.BindingFlags]'Public, Static')
    $tryGetByName = Get-RequiredMethod $archetypeType 'TryGetByName' ([System.Reflection.BindingFlags]'Public, Static')
    $forPlayfield = Get-RequiredMethod $archetypeType 'ForPlayfield' ([System.Reflection.BindingFlags]'Public, Static')
    $defaultForPlayfield = Get-RequiredMethod $archetypeType 'DefaultForPlayfield' ([System.Reflection.BindingFlags]'Public, Static')
    $isCombatTestCorpseName = Get-RequiredMethod $archetypeType 'IsCombatTestCorpseName' ([System.Reflection.BindingFlags]'Public, Static')

    $seenKeys = @{}
    $seenAliases = @{}
    $seenMonsterData = @{}
    foreach ($entry in $entries) {
        $key = Get-PropertyValue $entry 'Key'
        $displayName = Get-PropertyValue $entry 'DisplayName'
        $runtimeName = Get-PropertyValue $entry 'RuntimeName'
        $aliases = @(Get-PropertyValue $entry 'Aliases')
        $clientHintPlayfieldIds = @(Get-PropertyValue $entry 'ClientHintPlayfieldIds')
        $monsterData = [int](Get-PropertyValue $entry 'MonsterData')
        $corpseCatMesh = [int](Get-PropertyValue $entry 'CorpseCatMesh')
        $aiProfile = Get-PropertyValue $entry 'AiProfile'

        Assert-True (-not [string]::IsNullOrWhiteSpace($key)) 'Combat test mob key is blank.'
        Assert-True (-not [string]::IsNullOrWhiteSpace($displayName)) "DisplayName is blank for $key."
        Assert-True (-not [string]::IsNullOrWhiteSpace($runtimeName)) "RuntimeName is blank for $key."
        Assert-True (-not $runtimeName.StartsWith('Codex Test ')) "RuntimeName should be the real DB mob name for $displayName."
        Assert-True ($aliases.Count -gt 0) "No aliases configured for $displayName."
        Assert-True (($clientHintPlayfieldIds | Select-Object -Unique).Count -eq $clientHintPlayfieldIds.Count) "Duplicate client hint playfield mapping for $displayName."
        Assert-True ($monsterData -gt 0) "MonsterData must be positive for $displayName."
        Assert-True ($corpseCatMesh -gt 0) "CorpseCatMesh must be positive for $displayName."
        Assert-True ($aiProfile.Equals($passiveProfile)) "Current test mob $displayName should stay passive until proximity aggro is explicitly mapped."
        Assert-True (-not $seenKeys.ContainsKey($key.ToLowerInvariant())) "Duplicate combat test mob key $key."
        Assert-True (-not $seenMonsterData.ContainsKey($monsterData)) "Duplicate MonsterData $monsterData."

        $seenKeys[$key.ToLowerInvariant()] = $true
        $seenMonsterData[$monsterData] = $true

        $nameArgs = @($displayName, $null)
        Assert-True ([bool]$tryGetByName.Invoke($null, $nameArgs)) "TryGetByName failed for $displayName."
        Assert-True ([object]::ReferenceEquals($entry, $nameArgs[1])) "TryGetByName returned the wrong entry for $displayName."

        $runtimeNameArgs = @($runtimeName, $null)
        Assert-True ([bool]$tryGetByName.Invoke($null, $runtimeNameArgs)) "TryGetByName failed for runtime name $runtimeName."
        Assert-True ([object]::ReferenceEquals($entry, $runtimeNameArgs[1])) "TryGetByName returned the wrong entry for runtime name $runtimeName."

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

    $hintedNewlandDesert = Convert-EnumerableToArray ($forPlayfield.Invoke($null, @(565)))
    $hintedAegean = Convert-EnumerableToArray ($forPlayfield.Invoke($null, @(585)))
    $hintedWailingWastes = Convert-EnumerableToArray ($forPlayfield.Invoke($null, @(551)))
    $hintedOmnilab = Convert-EnumerableToArray ($forPlayfield.Invoke($null, @(346)))
    $hintedVarmintWoods = Convert-EnumerableToArray ($forPlayfield.Invoke($null, @(600)))
    $hintedBelialForest = Convert-EnumerableToArray ($forPlayfield.Invoke($null, @(605)))
    $hintedOmniForest = Convert-EnumerableToArray ($forPlayfield.Invoke($null, @(716)))
    $hintedShuttleport = Convert-EnumerableToArray ($forPlayfield.Invoke($null, @(4582)))
    $hintedUnknown = Convert-EnumerableToArray ($forPlayfield.Invoke($null, @(999999)))
    Assert-True (($hintedNewlandDesert | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'beachleet' }).Count -eq 1) 'Newland Desert should map to the client-hinted test leet.'
    Assert-True (($hintedNewlandDesert | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'shoresnake' }).Count -eq 1) 'Newland Desert should map to the client-hinted test snake.'
    Assert-True (($hintedNewlandDesert | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'duneflea' }).Count -eq 1) 'Newland Desert should map to the client-hinted test flea.'
    Assert-True (($hintedNewlandDesert | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'surflizard' }).Count -eq 1) 'Newland Desert should map to the client-hinted test lizard.'
    Assert-True (($hintedNewlandDesert | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'reefsalamander' }).Count -eq 1) 'Newland Desert should map to the client-hinted test salamander.'
    Assert-True (($hintedAegean | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'rollerrat' }).Count -eq 1) 'Aegean should map to the client-hinted test rollerrat.'
    Assert-True (($hintedAegean | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'duneflea' }).Count -eq 1) 'Aegean should map to the client-hinted test flea.'
    Assert-True (($hintedWailingWastes | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'rollerrat' }).Count -eq 1) 'Wailing Wastes should map to the client-hinted test rollerrat.'
    Assert-True (($hintedWailingWastes | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'alienspider' }).Count -eq 1) 'Wailing Wastes should map to the client-hinted test spider.'
    Assert-True ($hintedOmnilab.Count -eq 1 -and (Get-PropertyValue $hintedOmnilab[0] 'Key') -eq 'alienspider') 'Omnilab should map to only the supported test spider.'
    Assert-True (($hintedVarmintWoods | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'alienspider' }).Count -eq 1) 'Varmint Woods should map to the client-hinted test spider.'
    Assert-True (($hintedBelialForest | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'surflizard' }).Count -eq 1) 'Belial Forest should map to the client-hinted test lizard.'
    Assert-True (($hintedOmniForest | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'cliffmalle' }).Count -eq 1) 'Omni Forest should map to the client-hinted test malle.'
    Assert-True (($hintedShuttleport | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'beachleet' }).Count -eq 1) 'ICC Shuttleport should map to a real Beach Leet population mob.'
    Assert-True (($hintedShuttleport | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'islandreet' }).Count -eq 1) 'ICC Shuttleport should map to a real Island Reet population mob.'
    Assert-True (($hintedShuttleport | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'shoresnake' }).Count -eq 1) 'ICC Shuttleport should map to a real Shore Snake population mob.'
    Assert-True (($hintedShuttleport | Where-Object { (Get-PropertyValue $_ 'Key') -eq 'surflizard' }).Count -eq 1) 'ICC Shuttleport should map to a real Surf Lizard population mob.'
    Assert-True ($hintedUnknown.Count -eq 0) 'Unknown playfields should not invent client-hinted test mobs.'
    Assert-True ((Get-PropertyValue ($defaultForPlayfield.Invoke($null, @(551))) 'Key') -eq 'rollerrat') 'Wailing Wastes login default should use its client-hinted rollerrat.'
    Assert-True ((Get-PropertyValue ($defaultForPlayfield.Invoke($null, @(605))) 'Key') -eq 'shoresnake') 'Belial Forest login default should use the first client-hinted supported mob.'
    Assert-True ([object]::ReferenceEquals($defaultForPlayfield.Invoke($null, @(999999)), $defaultEntry)) 'Unknown playfields should fall back to the global default combat test mob.'
    Assert-True ([bool]$isCombatTestCorpseName.Invoke($null, @('Remains of Codex Test Beach Leet'))) 'Combat test corpse names should be recognized for cleanup.'
    Assert-True ([bool]$isCombatTestCorpseName.Invoke($null, @('Remains of Beach Leet'))) 'Real DB population corpse names should be recognized for cleanup.'
    Assert-True (-not [bool]$isCombatTestCorpseName.Invoke($null, @('Remains of Random Live Mob'))) 'Non-test corpse names should not be recognized for cleanup.'
    Assert-True (-not [bool]$isCombatTestCorpseName.Invoke($null, @('Codex Test Beach Leet'))) 'Live mob names should not be treated as corpse names.'

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
    $cleanupLootTemplateIds = @(27350, 27351, 27352)

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

        foreach ($templateId in $cleanupLootTemplateIds) {
            Assert-True (($matches | Where-Object { @($_.ItemTemplateIds) -contains $templateId }).Count -gt 0) "Test loot for $displayName should include non-unique cleanup template $templateId."
        }

        Assert-True (($matches | Where-Object { @($_.ItemTemplateIds) -contains 0x4545F -or @($_.ItemTemplateIds) -contains 0x4545A }).Count -eq 0) "Broad cleanup test loot for $displayName should not include duplicate UNIQUE templates."
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
    $shouldDropBasisPoints = Get-RequiredMethod $rulesType 'ShouldDropBasisPoints' ([System.Reflection.BindingFlags]'Public, Static')
    $lowRoll = [System.Func[int,int]]{ param([int]$max) 0 }
    $highRoll = [System.Func[int,int]]{ param([int]$max) $max - 1 }
    Assert-True (-not [bool]$shouldDrop.Invoke($null, @(0, $lowRoll))) '0 percent loot chance should never roll true.'
    Assert-True ([bool]$shouldDrop.Invoke($null, @(100, $highRoll))) '100 percent loot chance should always roll true.'
    Assert-True ([bool]$shouldDrop.Invoke($null, @(50, $lowRoll))) 'Low random roll should pass a 50 percent drop.'
    Assert-True (-not [bool]$shouldDrop.Invoke($null, @(50, $highRoll))) 'High random roll should fail a 50 percent drop.'
    Assert-True (-not [bool]$shouldDropBasisPoints.Invoke($null, @(0, $lowRoll))) '0 basis-point loot chance should never roll true.'
    Assert-True ([bool]$shouldDropBasisPoints.Invoke($null, @(10000, $highRoll))) '10000 basis points should always roll true.'
    Assert-True ([bool]$shouldDropBasisPoints.Invoke($null, @(1250, $lowRoll))) 'Low random roll should pass a 12.50 percent drop.'
    Assert-True (-not [bool]$shouldDropBasisPoints.Invoke($null, @(1250, $highRoll))) 'High random roll should fail a 12.50 percent drop.'

    $mobTemplate = [System.Activator]::CreateInstance($mobTemplateType)
    Set-PropertyValue $mobTemplate 'Hash' 'TST1'
    Set-PropertyValue $mobTemplate 'Name' 'Parser Test Leet'
    Set-PropertyValue $mobTemplate 'MonsterData' 17655
    Set-PropertyValue $mobTemplate 'NPCFamily' 42
    Set-PropertyValue $mobTemplate 'DropHashes' 'A001+B002,EMPTY'
    Set-PropertyValue $mobTemplate 'DropSlots' '7,8'
    Set-PropertyValue $mobTemplate 'DropRates' '1250,10000'

    $dropOne = [System.Activator]::CreateInstance($mobDropType)
    Set-PropertyValue $dropOne 'Hash' 'A001'
    Set-PropertyValue $dropOne 'LowId' 259990
    Set-PropertyValue $dropOne 'HighId' 259990
    Set-PropertyValue $dropOne 'MinQl' 1
    Set-PropertyValue $dropOne 'MaxQl' 1
    Set-PropertyValue $dropOne 'RangeCheck' 0

    $dropTwo = [System.Activator]::CreateInstance($mobDropType)
    Set-PropertyValue $dropTwo 'Hash' 'B002'
    Set-PropertyValue $dropTwo 'LowId' 42640
    Set-PropertyValue $dropTwo 'HighId' 42640
    Set-PropertyValue $dropTwo 'MinQl' 1
    Set-PropertyValue $dropTwo 'MaxQl' 5
    Set-PropertyValue $dropTwo 'RangeCheck' 1

    $templateArray = [System.Array]::CreateInstance($mobTemplateType, 1)
    $templateArray.SetValue($mobTemplate, 0)
    $dropArray = [System.Array]::CreateInstance($mobDropType, 2)
    $dropArray.SetValue($dropOne, 0)
    $dropArray.SetValue($dropTwo, 1)

    $buildMobLootEntries = Get-RequiredMethod $mobLootCatalogType 'BuildEntries' ([System.Reflection.BindingFlags]'Public, Static')
    $dbLootEntries = Convert-EnumerableToArray ($buildMobLootEntries.Invoke($null, @($templateArray, $dropArray)))
    Assert-True ($dbLootEntries.Count -eq 1) "Expected one parsed DB loot entry, found $($dbLootEntries.Count)."
    $dbLootEntry = $dbLootEntries[0]
    Assert-True ($dbLootEntry.Matches('Parser Test Leet', 17655, 42)) 'Parsed DB loot entry should match the source template.'
    Assert-True (-not $dbLootEntry.Matches('Parser Test Leet', 17656, 42)) 'Parsed DB loot entry should not match the wrong MonsterData.'
    Assert-True ([int](Get-PropertyValue $dbLootEntry 'Slot') -eq 7) 'Parsed DB loot entry should preserve DropSlots.'
    Assert-True ([int](Get-PropertyValue $dbLootEntry 'DropChanceBasisPoints') -eq 1250) 'Parsed DB loot entry should preserve DropRates basis points.'
    Assert-True ([int](Get-PropertyValue $dbLootEntry 'EffectiveDropChanceBasisPoints') -eq 1250) 'Parsed DB loot entry should expose effective basis points.'
    $dbItemTemplates = @(Get-PropertyValue $dbLootEntry 'ItemTemplates')
    Assert-True ($dbItemTemplates.Count -eq 2) 'Hash unions should produce candidate items from both drop groups.'
    Assert-True (($dbItemTemplates | Where-Object { [int](Get-PropertyValue $_ 'LowId') -eq 259990 }).Count -eq 1) 'First drop group item is missing.'
    Assert-True (($dbItemTemplates | Where-Object { [int](Get-PropertyValue $_ 'LowId') -eq 42640 -and [int](Get-PropertyValue $_ 'RangeCheck') -eq 1 }).Count -eq 1) 'Second drop group range-checked item is missing.'

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
    $missingSlot = $findLootItem.Invoke($null, @($lootItems, 99, $slotSelector, $lootedSelector))
    Assert-True ($null -eq $missingSlot) 'Unknown corpse loot slot should not return an item when multiple choices remain.'

    $singleRemaining = [object[]]@(
        [pscustomobject]@{ Slot = 5; Looted = $false }
    )
    $singleFallback = $findLootItem.Invoke($null, @($singleRemaining, 1, $slotSelector, $lootedSelector))
    Assert-True ([int]$singleFallback.Slot -eq 5) 'Single remaining corpse loot fallback failed.'
    $singleZeroFallback = $findLootItem.Invoke($null, @($singleRemaining, 0, $slotSelector, $lootedSelector))
    Assert-True ([int]$singleZeroFallback.Slot -eq 5) 'Single remaining corpse loot zero-slot fallback failed.'
    $singleTooHigh = $findLootItem.Invoke($null, @($singleRemaining, 2, $slotSelector, $lootedSelector))
    Assert-True ($null -eq $singleTooHigh) 'Single remaining corpse loot fallback should only apply to low requested slots.'

    $allLooted = [object[]]@(
        [pscustomobject]@{ Slot = 0; Looted = $true }
    )
    $missingLooted = $findLootItem.Invoke($null, @($allLooted, 0, $slotSelector, $lootedSelector))
    Assert-True ($null -eq $missingLooted) 'Looted corpse item should not be returned.'
    $nullLootItems = $findLootItem.Invoke($null, @($null, 0, $slotSelector, $lootedSelector))
    Assert-True ($null -eq $nullLootItems) 'Null corpse loot item collections should return null.'

    $entryCountFor = Get-RequiredMethod $rulesType 'InventoryEntryCountFor' ([System.Reflection.BindingFlags]'Public, Static')
    Assert-True ([int]$entryCountFor.Invoke($null, @(-5)) -eq 1) 'Negative stack count should display as one.'
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
