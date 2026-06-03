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
$tradeMessageSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3Messages\TradeMessage.cs')
$tradeActionSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3Messages\TradeAction.cs')
$tradeHandlerSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\TradeMessageHandler.cs')
$statHandlerSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\StatMessageHandler.cs')
$statsSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\CellAO.Stats\Stats.cs')
$orgClientSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\PacketHandlers\OrgClient.cs')
$characterSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\CellAO.Core\Entities\Character.cs')
$weaponItemFullUpdateSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\Packets\WeaponItemFullUpdate.cs')
$corpseFullUpdateSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\Packets\CorpseFullUpdate.cs')
$corpseFullUpdateMessageSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Messages\N3Messages\CorpseFullUpdateMessage.cs')
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
$enemyBehaviorContractSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\EnemyBehaviorContract.cs')
$combatTestMobArchetypeSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\CombatTestMobArchetype.cs')
$giveItemCommandSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\ChatCommands\ChatCommandGiveItem.cs')
$zoneProjectSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\ZoneEngine.csproj')
$messagingProjectSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\SmokeLounge.AOtomation.Messaging.csproj')
$messagingSerializerSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Serialization\SerializerResolverBuilder.cs')
$resurrectSerializerSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Serialization\Serializers\Custom\ResurrectMessageSerializer.cs')
$followCoordinateInfoSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\GameData\FollowCoordinateInfo.cs')
$followPositionInfoSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\GameData\FollowPositionInfo.cs')
$followStopInfoSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\GameData\FollowStopInfo.cs')
$followInfoSerializerSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Libraries\Source\AOtomation\AOtomation.Messaging\src\SmokeLounge.AOtomation.Messaging\Serialization\Serializers\Custom\FollowInfoSerializer.cs')
$followTargetHandlerSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\FollowTargetMessageHandler.cs')
$zoneEnemyHintsPath = Join-Path $repoRoot 'CellAO\Documentation\ClientRdbZoneEnemyHints.csv'
$npcTemplateHintsPath = Join-Path $repoRoot 'CellAO\Documentation\ClientRdbNpcTemplateHints.csv'
$enemyCoveragePath = Join-Path $repoRoot 'CellAO\Documentation\ClientHintedEnemyCoverage.csv'
$visualHintsPath = Join-Path $repoRoot 'CellAO\Documentation\MonsterDataCorpseVisualHints.csv'
$livePacketGapsSource = Get-Content -Raw (Join-Path $repoRoot 'CellAO\Documentation\LivePacketImplementationGaps.md')
$chaseObservationSource = Get-Content -Raw (Join-Path $repoRoot 'tools-temp\live-combat-chase-observations\README.md')
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
Assert-SourceMatch $characterActionSource 'case\s+CharacterActionType\.Die:.*?RespawnPlayer\s*\(\s*client\.Controller\.Character\s*\)' 'Current live client Die(152) action should route into the server death/respawn flow instead of being rebroadcast as an unknown action.'
Assert-SourceNoMatch $characterActionSource 'PendingReclaim' 'Modern death/respawn should not use the old pending reclaim experiment.'
Assert-SourceMatch $playfieldSource 'RespawnPlayer\s*\(ICharacter\s+character\).*?ResolvePlayerRespawnLocation.*?AllocateCorpseIdentity.*?Player corpse visual skipped.*?SendDeathSocialStatus.*?MarkPlayerRespawned.*?StopFightingDeadTarget\s*\(\s*character\.Identity\s*\).*?SendCombatStopMessage\s*\(\s*character\s*\).*?TryCompleteDeathRespawnInCurrentPlayfield.*?Teleport\s*\(\s*dynel,\s*destination,\s*character\.RawHeading,\s*destinationPlayfield\s*\)' 'Player respawn should keep the modern live sequence that fixed the white screen: skip stale corpse visual, send SocialStatus=0/respawn stats, then complete current-playfield teleport or N3Teleport fallback.'
Assert-SourceMatch $playfieldSource 'MarkPlayerRespawned\s*\(ICharacter\s+target\).*?CalculateSkills\s*\(\s*\).*?StatIds\.life.*?StatIds\.health.*?Math\.Max\s*\(\s*1,\s*maxHealth\s*/\s*3\s*\).*?StatIds\.deadtimer\]\.Value\s*=\s*0.*?StatIds\.currentmovementmode\]\.Value\s*=\s*\(int\)\s*MoveModes\.Run.*?StatIds\.specialcondition\]\.Value\s*=\s*3.*?StatIds\.deathreason\]\.Value\s*=\s*0' 'Player respawn should restore derived stats, partial live-style health, no-reclaim dead timer, run movement, and clear death reason.'
Assert-SourceMatch $playfieldSource 'SendDeathSocialStatus\s*\(ICharacter\s+target\).*?new\s+StatMessage.*?Unknown\s*=\s*1.*?CharacterStat\.SocialStatus.*?Value2\s*=\s*0' 'Live death/respawn capture sends SocialStatus=0 with Stat unknown=1 before teleport.'
Assert-SourceNoMatch $playfieldSource 'SendPlayerResurrect|PreparePlayerReclaimLogin|SendPlayerReclaimLogin|PendingReclaim' 'Modern live capture did not show ResurrectIIR or pending reclaim reconnect handling in the death/respawn path.'
Assert-SourceMatch $playfieldSource 'ResolvePlayerRespawnLocation\s*\(.*?StatIds\.tempsaveplayfield.*?StatIds\.tempsavex.*?StatIds\.tempsavey.*?destinationPlayfield\s*=\s*new\s+Identity' 'Player respawn should prefer the AO save-point stats when they are present.'
Assert-SourceMatch $playfieldSource 'private\s+const\s+int\s+RubiKaStartPlayfield\s*=\s*4582;.*?RubiKaStartX\s*=\s*939;.*?RubiKaStartY\s*=\s*20;.*?RubiKaStartZ\s*=\s*732;.*?ShadowlandsStartPlayfield\s*=\s*4001;.*?ShadowlandsStartX\s*=\s*850;.*?ShadowlandsStartY\s*=\s*43;.*?ShadowlandsStartZ\s*=\s*565;' 'Player respawn should keep the source-backed character-creation fallback locations in code.'
Assert-SourceMatch $playfieldSource 'ResolvePlayerRespawnLocation\s*\(.*?ResolveStarterRespawnLocation\s*\(\s*character,\s*out\s+destination,\s*out\s+destinationPlayfield\s*\).*?StatIds\.tempsaveplayfield' 'Player respawn should resolve the starter fallback before checking optional save-point stats.'
Assert-SourceNoMatch $playfieldSource 'ResolvePlayerRespawnLocation\s*\(.*?destination\s*=\s*new\s+Coordinate\s*\(\s*character\.RawCoordinates\s*\)' 'Player respawn must not fall back to the death position when no save-point stats are present.'
Assert-SourceMatch $zoneClientSource 'Dispose\s*\(bool\s+disposing\).*?!this\.Controller\.Character\.InLogoutTimerPeriod\s*\(\s*\).*?EnterLogoutSitPosture\s*\(\s*\).*?StartLogoutTimer\s*\(\s*\)' 'Hard network disconnect should enter seated logout state and start the timer when normal logout did not already do it.'
Assert-SourceNoMatch $zoneClientSource 'PendingReclaim|PreparePlayerReclaimLogin' 'Zone reconnect should not carry the removed pending reclaim experiment.'
Assert-SourceNoMatch $clientConnectedSource 'SendPlayerReclaimLogin|PendingReclaim' 'Client reconnect should rely on the normal playfield/full-character stream after death teleport.'
Assert-SourceMatch $messagingProjectSource 'Messages\\N3Messages\\StartLogoutMessage\.cs.*?Messages\\N3Messages\\StopLogoutMessage\.cs.*?Serialization\\Serializers\\Custom\\IdentityOnlyN3MessageSerializer\.cs' 'Messaging project should compile the recovered identity-only logout packet models and serializer.'
Assert-SourceMatch $messagingSerializerSource 'typeof\s*\(\s*StartLogoutMessage\s*\).*?IdentityOnlyN3MessageSerializer.*?typeof\s*\(\s*StopLogoutMessage\s*\).*?IdentityOnlyN3MessageSerializer' 'StartLogout/StopLogout should use the identity-only serializer instead of the generic N3 unknown-byte layout.'
Assert-SourceMatch $messagingProjectSource 'Messages\\N3Messages\\ResurrectMessage\.cs.*?Serialization\\Serializers\\Custom\\ResurrectMessageSerializer\.cs' 'Messaging project should compile the recovered ResurrectIIR packet model and serializer.'
Assert-SourceMatch $messagingSerializerSource 'typeof\s*\(\s*ResurrectMessage\s*\).*?ResurrectMessageSerializer' 'Resurrect should use the fixed 12-byte serializer instead of the generic N3 identity/unknown-byte layout.'
Assert-SourceMatch $resurrectSerializerSource 'WriteInt32\s*\(\s*\(int\)message\.N3MessageType\s*\).*?WriteInt32\s*\(\s*message\.Unknown1\s*\).*?WriteInt32\s*\(\s*message\.Unknown2\s*\)' 'Resurrect serializer should write exactly key plus two recovered int fields.'
Assert-SourceNoMatch $resurrectSerializerSource 'WriteIdentity|WriteByte\s*\(' 'Resurrect serializer must not write generic N3 identity or unknown-byte fields.'
Assert-SourceMatch $messagingProjectSource 'GameData\\FollowStopInfo\.cs' 'Messaging project should compile the capture-backed FollowTarget type-2 stop/settle payload model.'
Assert-SourceMatch $messagingProjectSource 'GameData\\FollowPositionInfo\.cs' 'Messaging project should compile the captured 56-byte FollowTarget type-2 position-stop payload model.'
Assert-SourceMatch $zoneProjectSource 'Core\\EnemyBehaviorContract\.cs' 'ZoneEngine project should compile the source-backed enemy behavior contract.'
Assert-SourceMatch $enemyBehaviorContractSource 'enum\s+EnemyBehaviorState.*?Idle.*?Aggroed.*?Chasing.*?InRangeAttacking.*?Returning.*?Dead' 'Enemy behavior contract should expose the minimal AO-style enemy states.'
Assert-SourceMatch $enemyBehaviorContractSource 'CharDcMoveIirKey\s*=\s*0x54111123.*?RelocateDynelsIirKey\s*=\s*0x264B514B.*?DropDynelIirKey\s*=\s*0x47483633' 'Enemy behavior contract should preserve recovered N3 dynel/movement IIR keys as labels, not guessed runtime packets.'
Assert-SourceMatch $enemyBehaviorContractSource 'NpcMovementActionSpellId\s*=\s*53191.*?NpcWipeHatelistSpellId\s*=\s*53126' 'Enemy behavior contract should preserve Demoder NPC movement/hate semantic labels.'
Assert-SourceMatch $enemyBehaviorContractSource 'OfficialFollowTargetUnknown\s*=\s*0.*?CoordinateFollowInfoType\s*=\s*1.*?RunMoveMode\s*=\s*25.*?CoordinateFollowPointCount\s*=\s*2' 'Enemy behavior contract should preserve official-live coordinate FollowTarget field values.'
Assert-SourceMatch $enemyBehaviorContractSource 'NpcRunSpeedForMaxFollowSpeed\s*=\s*400' 'Enemy behavior contract should align advertised NPC runspeed with local chase speed.'
Assert-SourceMatch $enemyBehaviorContractSource 'MaxPlayerChaseProjectionDistance\s*=\s*3\.0' 'Enemy behavior contract should cap player projection for server-side range authority.'
Assert-SourceNoMatch $enemyBehaviorContractSource 'MinCoordinateFollowRepathSeconds|MinCoordinateFollowTargetDelta|NpcMeleeFollowReengageDistance' 'Enemy behavior contract should not preserve the failed coordinate-repath throttle model.'
Assert-SourceMatch $enemyBehaviorContractSource 'StopFightFromPlayer:.*?HardCorrection:.*?new\s+EnemyBehaviorTransition\s*\(\s*current,\s*"state-preserved"\s*\)' 'Enemy behavior contract should preserve mob aggro across player StopFight and correction packets.'
Assert-SourceNoMatch $enemyBehaviorContractSource 'NpcMeleeCorrectionDistance|MinCoordinateFollowCorrectionSeconds|NpcMeleeCorrectionDirectionDot' 'Normal NPC chase should not carry default sharp-turn correction gates; correction packets stay capture evidence until a targeted runtime trigger is proved.'
Assert-SourceMatch $enemyBehaviorContractSource 'TargetFollow:.*?new\s+EnemyBehaviorTransition\s*\(\s*EnemyBehaviorState\.Chasing,\s*"target-follow"\s*\).*?CoordinateFollowTarget:.*?new\s+EnemyBehaviorTransition\s*\(\s*EnemyBehaviorState\.Chasing,\s*"coordinate-follow"\s*\)' 'Enemy behavior contract should map both target-follow runtime intent and coordinate FollowTarget evidence to chasing.'
Assert-SourceMatch $enemyBehaviorContractSource 'AttackInfo:.*?MissedAttackInfo:.*?HealthDamage:.*?new\s+EnemyBehaviorTransition\s*\(\s*EnemyBehaviorState\.InRangeAttacking,\s*"combat-result"\s*\)' 'Enemy behavior contract should map combat result families to in-range attacking.'
Assert-SourceMatch $enemyBehaviorContractSource 'WipeHatelist:.*?TargetInvalidOrZoned:.*?new\s+EnemyBehaviorTransition\s*\(\s*EnemyBehaviorState\.Idle,\s*"target-cleared"\s*\)' 'Enemy behavior contract should keep explicit target clear/reset conditions.'
Assert-SourceMatch $followCoordinateInfoSource 'public\s+List<Vector3>\s+Coordinates\s*\{\s*get;\s*set;\s*\}' 'FollowCoordinateInfo should support capture-backed multi-point FollowTarget paths.'
Assert-SourceMatch $followPositionInfoSource 'public\s+class\s+FollowPositionInfo\s*:\s*FollowInfo.*?MoveType\s*=\s*25.*?Unknown2\s*=\s*0x40.*?Vector3\s+Coordinates.*?byte\s+Unknown4' 'FollowPositionInfo should model the captured 56-byte type-2 position-stop payload.'
Assert-SourceMatch $followInfoSerializerSource 'for\s*\(int\s+i\s*=\s*0;\s*i\s*<\s*followCoordinateInfo\.CoordinateCount;\s*i\+\+\).*?followCoordinateInfo\.Coordinates\.Add\s*\(\s*this\.ReadVector3\s*\(\s*streamReader\s*\)\s*\)' 'FollowInfoSerializer should deserialize every captured FollowTarget coordinate, not only start/end.'
Assert-SourceMatch $followInfoSerializerSource 'IList<Vector3>\s+coordinates\s*=\s*this\.GetCoordinates\s*\(\s*fcinfo\s*\).*?streamWriter\.WriteByte\s*\(\s*\(byte\)coordinates\.Count\s*\).*?foreach\s*\(Vector3\s+coordinate\s+in\s+coordinates\)' 'FollowInfoSerializer should serialize the real coordinate count and all path points.'
Assert-SourceMatch $followStopInfoSource 'public\s+class\s+FollowStopInfo\s*:\s*FollowInfo.*?MoveType\s*=\s*21.*?Flag\s*=\s*1.*?Vector3\s+Coordinates.*?Vector3\s+ConfirmCoordinates' 'FollowStopInfo should model the captured type-2 stop/settle payload defaults and duplicated coordinate tail.'
Assert-SourceMatch $followInfoSerializerSource 'moveType\s*==\s*25.*?streamReader\.Length\s*-\s*streamReader\.Position\s*\)\s*>=\s*25.*?new\s+FollowPositionInfo.*?Unknown1\s*=\s*streamReader\.ReadInt32\s*\(\s*\).*?Unknown2\s*=\s*streamReader\.ReadInt32\s*\(\s*\).*?Unknown3\s*=\s*streamReader\.ReadInt32\s*\(\s*\).*?Coordinates\s*=\s*this\.ReadVector3\s*\(\s*streamReader\s*\).*?Unknown4\s*=\s*streamReader\.ReadByte\s*\(\s*\)' 'FollowInfoSerializer should deserialize the captured 56-byte type-2 position-stop shape.'
Assert-SourceMatch $followInfoSerializerSource 'moveType\s*==\s*21.*?streamReader\.Length\s*-\s*streamReader\.Position\s*\)\s*>=\s*37.*?new\s+FollowStopInfo.*?Unknown1\s*=\s*streamReader\.ReadInt32\s*\(\s*\).*?Unknown2\s*=\s*streamReader\.ReadInt32\s*\(\s*\).*?Unknown3\s*=\s*streamReader\.ReadInt32\s*\(\s*\).*?Coordinates\s*=\s*this\.ReadVector3\s*\(\s*streamReader\s*\).*?Flag\s*=\s*streamReader\.ReadByte\s*\(\s*\).*?ConfirmCoordinates\s*=\s*this\.ReadVector3\s*\(\s*streamReader\s*\)' 'FollowInfoSerializer should deserialize the captured 68-byte type-2 stop/settle shape.'
Assert-SourceMatch $followInfoSerializerSource 'var\s+fpinfo\s*=\s*value\s+as\s+FollowPositionInfo.*?streamWriter\.WriteByte\s*\(\s*fpinfo\.FollowInfoType\s*\).*?streamWriter\.WriteByte\s*\(\s*fpinfo\.MoveType\s*\).*?streamWriter\.WriteInt32\s*\(\s*fpinfo\.Unknown1\s*\).*?streamWriter\.WriteInt32\s*\(\s*fpinfo\.Unknown2\s*\).*?streamWriter\.WriteInt32\s*\(\s*fpinfo\.Unknown3\s*\).*?this\.WriteVector3\s*\(\s*streamWriter,\s*fpinfo\.Coordinates\s*\).*?streamWriter\.WriteByte\s*\(\s*fpinfo\.Unknown4\s*\)' 'FollowInfoSerializer should serialize the captured 56-byte type-2 position-stop shape.'
Assert-SourceMatch $followInfoSerializerSource 'var\s+fsinfo\s*=\s*value\s+as\s+FollowStopInfo.*?streamWriter\.WriteByte\s*\(\s*fsinfo\.FollowInfoType\s*\).*?streamWriter\.WriteByte\s*\(\s*fsinfo\.MoveType\s*\).*?streamWriter\.WriteInt32\s*\(\s*fsinfo\.Unknown1\s*\).*?streamWriter\.WriteInt32\s*\(\s*fsinfo\.Unknown2\s*\).*?streamWriter\.WriteInt32\s*\(\s*fsinfo\.Unknown3\s*\).*?this\.WriteVector3\s*\(\s*streamWriter,\s*coordinates\s*\).*?streamWriter\.WriteByte\s*\(\s*fsinfo\.Flag\s*\).*?this\.WriteVector3\s*\(\s*streamWriter,\s*confirmCoordinates\s*\)' 'FollowInfoSerializer should serialize the captured type-2 stop/settle shape.'
Assert-SourceMatch $followTargetHandlerSource 'public\s+void\s+Send\s*\(\s*ICharacter\s+character,\s*Vector3\s+stopPosition\s*\).*?this\.SendOfficialPositionStop\s*\(\s*character,\s*stopPosition\s*\)' 'Runtime position-stop FollowTarget packets should use the official-live short type-2 position shape.'
Assert-SourceMatch $followTargetHandlerSource 'FillerOfficialPositionStop.*?new\s+FollowPositionInfo\s*\(\s*\).*?MoveType\s*=\s*EnemyBehaviorContract\.RunMoveMode.*?Unknown1\s*=\s*0.*?Unknown2\s*=\s*0.*?Unknown3\s*=\s*0x40000000.*?Unknown4\s*=\s*0' 'Official position-stop should send the captured 56-byte type-2 FollowTarget body.'
Assert-SourceMatch $followTargetHandlerSource 'FillerCoordinates.*?new\s+FollowCoordinateInfo\s*\(\s*\).*?CoordinateCount\s*=\s*EnemyBehaviorContract\.CoordinateFollowPointCount.*?MoveMode\s*=\s*movetype.*?FollowInfoType\s*=\s*EnemyBehaviorContract\.CoordinateFollowInfoType.*?x\.Unknown\s*=\s*EnemyBehaviorContract\.OfficialFollowTargetUnknown\s*;' 'Runtime coordinate FollowTarget packets should use the official-live N3 unknown byte 0, not the private-capture value 1.'
Assert-SourceMatch $followTargetHandlerSource 'FillerFollowTarget.*?new\s+FollowTargetInfo\s*\(\s*\).*?MoveType\s*=\s*0.*?Target\s*=\s*toFollow.*?Dummy\s*=\s*0x40.*?Dummy1\s*=\s*0x20000000.*?x\.Unknown\s*=\s*0' 'Runtime target-follow packets should use the same payload shape as the working player-follow broadcast.'
Assert-SourceNoMatch ($npcControllerSource + $playfieldSource) 'new\s+CharDCMoveMessage' 'NPC chase should not synthesize mob-owned CharDCMove packets; focused official-live chase used FollowTarget for mobs.'
Assert-SourceOrdered $characterActionSource @(
    'case\s+CharacterActionType\.Logout',
    'ApplySit\s*\(\s*client\s*\)',
    'SendStartLogout\s*\(\s*client\.Controller\.Character\s*\)',
    'StartLogoutTimer\s*\(\s*\)'
) 'Logout flow regression: first X/logout should sit, send StartLogout, and start the normal 30s timer.'
Assert-SourceNoMatch $characterActionSource 'CharacterActionType\.Logout:.*?StatIds\.gmlevel|CharacterActionType\.Logout:.*?StartLogoutTimer\s*\(\s*1000\s*\)' 'Logout flow should not use a GM/admin fast-disconnect shortcut.'
Assert-SourceOrdered $characterActionSource @(
    'case\s+CharacterActionType\.StopLogout',
    'ApplyStand\s*\(\s*client\s*\)',
    'private\s+void\s+ApplyStand\s*\(IZoneClient\s+client\)',
    'UpdateMoveType\s*\(\s*37\s*\)',
    'CharacterActionType\.StandUp',
    'SendPostureMove\s*\(\s*character,\s*37\s*\)',
    'InLogoutTimerPeriod\s*\(\s*\)',
    'SendStopLogout\s*\(\s*character\s*\)',
    'this\.Send\s*\(\s*character,\s*this\.StopLogout\s*\(\s*character\s*\),\s*true\s*\)',
    'StopLogoutTimer\s*\(\s*\)'
) 'Logout flow regression: StopLogout/stand should stand the character, send StopLogout, and cancel the timer.'
Assert-SourceOrdered $characterActionSource @(
    'private\s+void\s+ApplySit\s*\(IZoneClient\s+client\)',
    'character\.EnterLogoutSitPosture\s*\(\s*\)',
    'client\.Controller\.State\s*=\s*CharacterState\.Idle',
    'SendPostureMove\s*\(\s*character,\s*30\s*\)',
    'SimpleCharFullUpdate\.SendToPlayfield\s*\(\s*client\.Controller\.Client\s*\)'
) 'Logout flow regression: ApplySit should enter seated logout posture, idle the controller, broadcast posture movement, and refresh playfield appearance.'
Assert-SourceOrdered $characterSource @(
    'EnterLogoutSitPosture\s*\(\s*\)',
    'StopMovement\s*\(\s*\)',
    'UpdateMoveType\s*\(\s*30\s*\)',
    'Stats\s*\[\s*StatIds\.state\s*\]\.Value\s*=\s*0',
    'Stats\s*\[\s*StatIds\.state\s*\]\.BaseValue\s*=\s*0',
    'Stats\s*\[\s*StatIds\.currentstate\s*\]\.Value\s*=\s*0',
    'Stats\s*\[\s*StatIds\.currentstate\s*\]\.BaseValue\s*=\s*0'
) 'Logout flow regression: seated logout posture should stop movement, apply sit move type, and clear action state values/base values.'
Assert-SourceOrdered $characterSource @(
    'StartLogoutTimer\s*\(int\s+time\s*=\s*30000\)',
    'new\s+Timer\s*\(\s*this\.LogoutTimerCallback,\s*null,\s*time,\s*0\s*\)',
    'StopLogoutTimer\s*\(\s*\)',
    'logoutTimer\.Dispose\s*\(\s*\)',
    'logoutTimer\s*=\s*null',
    'InLogoutTimerPeriod\s*\(\s*\)',
    'return\s+this\.logoutTimer\s*!=\s*null',
    'LogoutTimerCallback\s*\(object\s+sender\)',
    'if\s*\(\s*this\.logoutTimer\s*==\s*null\s*\)',
    'this\.logoutTimer\.Dispose\s*\(\s*\)',
    'this\.logoutTimer\s*=\s*null',
    'this\.Dispose\s*\(\s*\)'
) 'Logout flow regression: timer start/cancel/completion should use a nullable timer and dispose the character only after the timer expires.'
Assert-SourceOrdered $zoneClientSource @(
    'Dispose\s*\(bool\s+disposing\)',
    'this\.Controller\s*!=\s*null',
    'this\.Controller\.Character\s*!=\s*null',
    '!this\.Controller\.Character\.InLogoutTimerPeriod\s*\(\s*\)',
    'EnterLogoutSitPosture\s*\(\s*\)',
    'this\.Controller\.State\s*=\s*CharacterState\.Idle',
    'StartLogoutTimer\s*\(\s*\)',
    'zStream\.Close\s*\(\s*\)',
    'netStream\.Close\s*\(\s*\)'
) 'Logout flow regression: hard socket close should only start seated timed logout when a normal logout is not already active, then close streams.'
Assert-SourceOrdered $messagingSerializerSource @(
    'typeof\s*\(\s*StartLogoutMessage\s*\)',
    'new\s+IdentityOnlyN3MessageSerializer\s*\(\s*typeof\s*\(\s*StartLogoutMessage\s*\)\s*\)',
    'typeof\s*\(\s*StopLogoutMessage\s*\)',
    'new\s+IdentityOnlyN3MessageSerializer\s*\(\s*typeof\s*\(\s*StopLogoutMessage\s*\)\s*\)'
) 'Logout flow regression: StartLogout and StopLogout should stay on the identity-only N3 serializer.'
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
Assert-SourceMatch $clientMoveItemSource 'int\s+ackTargetPlacement\s*=\s*message\.TargetPlacement.*?SendMoveAck\s*\(ICharacter\s+character,\s*Identity\s+sourceContainer,\s*int\s+targetPlacement\).*?SourceContainer\s*=\s*sourceContainer.*?Target\s*=\s*character\.Identity.*?TargetPlacement\s*=\s*targetPlacement' 'ContainerAddItem move-ack should use captured/clientless semantics: source slot first, target character second, and raw 0x6F next-free placement preserved on the wire.'
Assert-SourceMatch $containerAddItemSource 'FillContainerAddItem\s*\(ICharacter\s+character,\s*Identity\s+sourceContainer,\s*int\s+slot\).*?SourceContainer\s*=\s*sourceContainer.*?TargetPlacement\s*=\s*slot.*?Target\s*=\s*character\.Identity' 'ContainerAddItem helper should mirror captured/clientless semantics for inventory acknowledgements.'
Assert-SourceMatch $tradeMessageSource 'AoMember\s*\(\s*2\s*\).*?int\s+Param1.*?AoMember\s*\(\s*3\s*\).*?int\s+Param2.*?AoMember\s*\(\s*4\s*\).*?int\s+Param3.*?AoMember\s*\(\s*5\s*\).*?int\s+Param4' 'TradeMessage should expose the live/AOSharp four-param body instead of forcing every action into two identities.'
Assert-SourceMatch $tradeMessageSource 'public\s+Identity\s+Target\s*\{.*?get.*?Param1.*?Param2.*?set.*?Param1.*?value\.Type.*?Param2.*?value\.Instance.*?\}.*?public\s+Identity\s+Container\s*\{.*?get.*?Param3.*?Param4.*?set.*?Param3.*?value\.Type.*?Param4.*?value\.Instance' 'TradeMessage should keep Target/Container compatibility wrappers over raw params for item/open/remove actions.'
Assert-SourceMatch $tradeActionSource 'Open\s*=\s*0x00.*?Accept\s*=\s*0x01.*?Complete\s*=\s*0x04.*?UpdateCredits\s*=\s*0x07.*?OtherPlayerAddItem\s*=\s*0x08' 'TradeAction should carry AOSharp/current-client semantic names for known player-trade actions.'
Assert-SourceMatch $tradeActionSource 'None\s*=\s*Open.*?End\s*=\s*Accept.*?Unknown\s*=\s*Complete.*?Credits\s*=\s*UpdateCredits' 'TradeAction should keep legacy CellAO aliases while code is migrated to canonical names.'
Assert-SourceMatch $tradeHandlerSource 'Trade action=.*?p1=\{5\} p2=\{6\} p3=\{7\} p4=\{8\}' 'Trade handler logging should expose raw params for packet evidence work.'
Assert-SourceMatch $tradeHandlerSource 'int\s+credits\s*=\s*Math\.Max\s*\(\s*0,\s*message\.Param2\s*\)' 'Player trade credits should read the live-captured UpdateCredits amount from Param2.'
Assert-SourceMatch $tradeHandlerSource 'SetPlayerTradeCredits\s*\(\s*character\.Identity,\s*credits\s*\).*?SendPlayerTradeCredits\s*\(\s*character,\s*character\.Identity,\s*credits\s*\).*?SendPlayerTradeCredits\s*\(\s*otherCharacter,\s*character\.Identity,\s*credits\s*\)' 'Player trade credit updates should echo to both the sender and trade partner like the live server.'
Assert-SourceNoMatch $tradeHandlerSource 'TradeAction\.(None|End|Unknown|Credits)' 'TradeMessageHandler should use canonical trade action names; legacy aliases stay only in the enum for compatibility.'
Assert-SourceMatch $tradeHandlerSource 'PlayerTradeCredits\s*\(Identity\s+offerOwner,\s*int\s+credits\).*?x\.Action\s*=\s*TradeAction\.UpdateCredits.*?x\.Param1\s*=\s*0;.*?x\.Param2\s*=\s*credits;.*?x\.Param3\s*=\s*0;.*?x\.Param4\s*=\s*0;' 'Player trade credit notification should match live UpdateCredits shape: Param1=0, Param2=amount, Param3=0, Param4=0.'
Assert-SourceMatch $statsSource 'this\.cash\s*=\s*new\s+Stat\s*\(\s*this,\s*61,\s*0,\s*true,\s*false,\s*false\s*\)' 'Cash stat should send BaseValue on changed-stat packets, matching FullCharacter login cash.'
Assert-SourceMatch $statHandlerSource 'statsToClient\.TryGetValue\s*\(\s*\(int\)StatIds\.cash,\s*out\s+clientCash\s*\).*?clientCash\s*=\s*character\.Stats\s*\[\s*StatIds\.cash\s*\]\.BaseValue;.*?statsToClient\s*\[\s*\(int\)StatIds\.cash\s*\]\s*=\s*clientCash' 'Bulk cash stat sends should normalize to authoritative BaseValue before serializing.'
Assert-SourceMatch $statHandlerSource 'SendSingle\s*\(ICharacter\s+character,\s*int\s+statId,\s*uint\s+statValue\).*?if\s*\(\s*statId\s*==\s*\(int\)StatIds\.cash\s*\).*?statValue\s*=\s*character\.Stats\s*\[\s*StatIds\.cash\s*\]\.BaseValue;' 'Single cash stat sends should normalize to authoritative BaseValue before serializing.'
Assert-SourceMatch $zoneClientSource 'SendCompressed\s*\(MessageBody\s+messageBody\).*?this\.SendCompressed\s*\(\s*messageBody,\s*this\.server\.Id\s*\).*?SendCompressed\s*\(MessageBody\s+messageBody,\s*int\s+sender\).*?Sender\s*=\s*sender' 'ZoneClient should support explicit AO header senders while preserving the default server-sender path.'
Assert-SourceMatch $tradeHandlerSource 'GetPlayerTradeCreditFailure\s*\(ICharacter\s+character,\s*TemporaryBag\s+shoppingBag\).*?int\s+availableCredits\s*=\s*GetCash\s*\(\s*character\s*\)' 'Player trade credit validation should read authoritative BaseValue through GetCash().'
Assert-SourceNoMatch $tradeHandlerSource 'TransferPlayerTradeCredits\s*\(ICharacter\s+shopper,\s*ICharacter\s+vendor,\s*TemporaryBag\s+shoppingBag\).*?Controller\.SendChangedStats\s*\(\s*\)' 'Player trade credit completion should not send a cash stat packet; live trade completion sends Trade Complete plus SocialStatus, and the client applies the visible credit transfer.'
Assert-SourceMatch $tradeHandlerSource 'SetCash\s*\(\s*client\.Controller\.Character,\s*GetCash\s*\(\s*client\.Controller\.Character\s*\)\s*-\s*cash\s*\)' 'NPC/shop trade cash mutation should use SetCash/GetCash instead of raw cash Value math.'
Assert-SourceMatch $orgClientSource 'Stats\s*\[\s*StatIds\.cash\s*\]\.Set\s*\(\s*clampedCash\s*\).*?SendChangedStats\s*\(\s*\).*?Stats\.Write\s*\(\s*\)' 'Organization bank cash mutation should use Set(), send changed stats, and persist.'
Assert-SourceMatch $giveItemCommandSource 'InventoryError\s+err\s*=\s*container\.BaseInventory\.TryAdd\s*\(\s*item\s*\).*?if\s*\(\s*err\s*!=\s*InventoryError\.OK\s*\).*?container\.BaseInventory\.Write\s*\(\s*\)\s*;.*?AddTemplateMessageHandler\.Default\.Send' 'GM giveitem should persist the added item before notifying a character client.'
Assert-SourceMatch $clientMoveItemSource 'EnsureWeaponVisualMeshes\s*\(ICharacter\s+character,\s*bool\s+announceAppearanceUpdate\).*?WeaponSlots\.Righthand.*?WeaponSlots\.LeftHand' 'Weapon visual repair should cover both right and left hand slots for dual wield.'
Assert-SourceMatch $clientMoveItemSource 'EnsureWeaponMesh\s*\(.*?IItem\s+equippedItem\s*=\s*weaponPage\s*\[\s*slot\s*\]\s*;\s*if\s*\(\s*equippedItem\s*==\s*null\s*\)\s*\{\s*return\s+false\s*;' 'Weapon visual repair should not restore stale meshes for an unequipped hand.'
Assert-SourceMatch $clientMoveItemSource 'EnsureWeaponMesh\s*\(.*?if\s*\(\s*existing\s*!=\s*null\s*\)\s*\{.*?if\s*\(\s*existing\.Mesh\s*>\s*0\s*&&\s*existing\.Mesh\s*!=\s*1234567890\s*\)\s*\{\s*return\s+false\s*;\s*\}.*?RemoveMesh' 'Weapon visual repair should preserve already-valid hand meshes so util/HUD/back equips do not hide equipped weapons.'
Assert-SourceMatch $clientMoveItemSource 'EnsureWeaponMesh\s*\(.*?AddMesh\s*\(\s*meshPosition,\s*meshId,\s*overrideTexture,\s*layer\s*\)\s*;\s*character\.Stats\s*\[\s*meshStat\s*\]\.Value\s*=\s*meshId\s*;' 'Weapon visual repair should apply mesh-layer and matching weapon mesh stat updates together.'
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
Assert-SourceMatch $baseInventorySource 'InventoryItemRules\.HasSameUniqueItem' 'Inventory add path must use shared unique-item rules.'
Assert-SourceMatch $playfieldSource 'InventoryItemRules\.HasSameUniqueItem' 'Corpse loot path must use shared unique-item rules.'
Assert-SourceMatch $playfieldSource 'DespawnNpcImmediately\s*\(' 'Playfield should expose immediate NPC despawn for controlled GM test cleanup.'
Assert-SourceMatch $playfieldSource 'DespawnCorpses\s*\(' 'Playfield should expose corpse cleanup for controlled GM test cleanup.'
Assert-SourceMatch $zoneProjectSource 'Core\\Packets\\CorpseFullUpdate\.cs' 'ZoneEngine project should compile the dedicated CorpseFullUpdate packet builder.'
Assert-SourceMatch $playfieldSource 'target\.Stats\s*\[\s*StatIds\.health\s*\]\.Value\s*=\s*newHealth\s*;\s*target\.SendChangedStats\s*\(\s*\)\s*;.*?new\s+AttackInfoMessage' 'Combat hits, including killing hits, should send the zero-health stat update before death cleanup.'
Assert-SourceMatch $playfieldSource 'private\s+static\s+bool\s+ShouldSendHealthDamage\s*\(CombatDamageSource\s+source\).*?source\s*!=\s*CombatDamageSource\.WeaponAutoAttack.*?source\s*!=\s*CombatDamageSource\.UnarmedAutoAttack' 'HealthDamage gating should explicitly exclude weapon and unarmed auto-attacks.'
Assert-SourceMatch $livePacketGapsSource '## HealthDamage Policy.*?auto-attacks must remain `AttackInfo` only.*?DoCombatTick.*?duplicate combat text.*?targeted capture.*?DoT.*?HoT.*?nano.*?environmental' 'HealthDamage packet policy should document why normal weapon hits stay AttackInfo-only and which future paths need targeted capture evidence.'
Assert-SourceMatch $playfieldSource 'healIntervalSeconds\s*>\s*0.*?healDelta\s*!=\s*0.*?healInterval\.LastTick\s*<\s*DateTime\.UtcNow.*?nanoIntervalSeconds\s*>\s*0.*?nanoDelta\s*!=\s*0.*?nanoInterval\.LastTick\s*<\s*DateTime\.UtcNow' 'Heartbeat regen should treat zero interval or zero delta as disabled instead of spamming stat updates every heartbeat.'
Assert-SourceMatch $livePacketGapsSource 'Heartbeat stat regen.*?HealInterval.*?NanoInterval.*?interval is positive.*?delta is nonzero.*?zero interval is disabled' 'Packet gap notes should document the regen-spam guard observed during player death testing.'
Assert-SourceMatch $chaseObservationSource '## Enemy Behavior Evidence Map.*?Identity_t.*?CharDCMoveIIR_t.*?server-authoritative movement playback.*?not proof that CellAO should synthesize mob-owned `CharDCMove` chase packets.*?RelocateDynelsIIR_t.*?bulk reposition.*?DropDynelIIR_t.*?despawn/lifecycle.*?SetWantedDirectionIIR_t.*?n3LocalityUpdateIIR_t.*?n3TeleportIIR_t.*?Combat event families.*?sticky aggro/long pursuit.*?replay tests' 'Enemy chase notes should preserve the source-backed signal map and block decoded-but-unvalidated packet guesses.'
Assert-SourceNoMatch ($npcControllerSource + $playfieldSource) 'RelocateDynels|DropDynel' 'NPC combat/chase should not emit RelocateDynels or DropDynel until targeted live traces prove exact runtime semantics.'
Assert-SourceMatch $playfieldSource 'MaxMeleeCombatDistance\s*=\s*8\.0' 'Combat ticks should use a conservative melee range gate.'
Assert-SourceMatch $playfieldSource 'MaxMeleeFollowHoldDistance\s*=\s*3\.0' 'Melee chase should separate close visual hold spacing from the wider conservative hit range gate.'
Assert-SourceMatch $playfieldSource 'RegisterNpcHome\s*\(ICharacter\s+character\).*?npcHomeStates\s*\[\s*character\.Identity\.Instance\s*\].*?new\s+Coordinate\s*\(\s*character\.Coordinates\s*\(\s*\)\s*\)' 'Playfield should track spawned NPC home coordinates.'
Assert-SourceNoMatch $playfieldSource 'DoCombatTick\s*\(ICharacter\s+attacker\).*?TryLeashNpcAttacker\s*\(\s*attacker\s*\)' 'Normal NPC combat should not leash mobs home; AO mobs can chase across the playfield.'
Assert-SourceNoMatch $playfieldSource 'TryLeashNpcAttacker|MaxNpcLeashDistance|NPC leashed home' 'Leash enforcement should be removed from the normal NPC combat path.'
Assert-SourceMatch $playfieldSource 'DoCombatTick\s*\(ICharacter\s+attacker\).*?CombatAttackSource\s+attackSource\s*=\s*this\.GetCombatAttackSource\s*\(\s*attacker\s*\).*?!this\.IsInCombatRange\s*\(\s*attacker,\s*target,\s*attackSource\.Range\s*\).*?TryMoveNpcIntoCombatRange\s*\(\s*attacker,\s*target,\s*attackSource\.Range\s*\).*?nextCombatTicks\s*\[\s*attacker\.Identity\.Instance\s*\]\s*=\s*DateTime\.UtcNow\s*\+\s*TimeSpan\.FromSeconds\s*\(\s*OutOfRangeRetrySeconds\s*\).*?return\s*;.*?int\s+currentHealth' 'Combat ticks should use equipped weapon range and avoid dealing damage while attacker and target are out of range.'
Assert-SourceMatch $playfieldSource 'DoCombatTick\s*\(ICharacter\s+attacker\).*?AnnounceCombatDamage\s*\(\s*attacker,\s*target,\s*damage,\s*attackSource,\s*attackSource\.UsesEquippedWeapon\s*\?\s*CombatDamageSource\.WeaponAutoAttack\s*:\s*CombatDamageSource\.UnarmedAutoAttack\s*\)' 'Combat tick auto-attacks should publish through the source-gated damage announcer.'
Assert-SourceMatch $playfieldSource 'GetCombatAttackSource\s*\(ICharacter\s+attacker\).*?if\s*\(\s*equippedWeapon\s*==\s*null\s*\).*?AttackInfoAmmoCount\s*=\s*0,\s*AttackInfoWeaponSlot\s*=\s*0,\s*AttackInfoUnk1\s*=\s*0,\s*AttackInfoHitType\s*=\s*isNpcAttacker\s*\?\s*3\s*:\s*0' 'Unarmed NPC melee AttackInfo metadata should match the captured melee mob rows and avoid caster-style nanobot text for beach-leet attacks.'
Assert-SourceMatch $combatTestMobArchetypeSource 'Prepare\s*\(ICharacter\s+mobCharacter,\s*Entry\s+entry\).*?SetMobStat\s*\(\s*mobCharacter,\s*StatIds\.currentmovementmode,\s*\(int\)MoveModes\.Run\s*\).*?SetMobStat\s*\(\s*mobCharacter,\s*StatIds\.prevmovementmode,\s*\(int\)MoveModes\.Run\s*\).*?SetMobStat\s*\(\s*mobCharacter,\s*StatIds\.runspeed,\s*EnemyBehaviorContract\.NpcRunSpeedForMaxFollowSpeed\s*\).*?SetMobStat\s*\(\s*mobCharacter,\s*StatIds\.mindamage,\s*1\s*\).*?SetMobStat\s*\(\s*mobCharacter,\s*StatIds\.maxdamage,\s*3\s*\).*?SetMobStat\s*\(\s*mobCharacter,\s*StatIds\.damagebonus,\s*0\s*\).*?SetMobStat\s*\(\s*mobCharacter,\s*StatIds\.defaultattacktype,\s*0\s*\).*?SetMobStat\s*\(\s*mobCharacter,\s*StatIds\.damageoverridetype,\s*\(int\)StatIds\.meleeac\s*\).*?SetMobStat\s*\(\s*mobCharacter,\s*StatIds\.damagetype,\s*\(int\)StatIds\.meleeac\s*\)' 'Combat test mobs should explicitly set movement and both recovered melee damage-type stats instead of leaving sentinel/default values.'
Assert-SourceMatch $playfieldSource 'GetEquippedCombatWeapon\s*\(ICharacter\s+attacker\).*?IdentityType\.WeaponPage.*?WeaponSlots\.Righthand.*?WeaponSlots\.LeftHand' 'Combat attack source should read equipped weapons from the weapon page right/left hand slots.'
Assert-SourceMatch $playfieldSource 'private\s+readonly\s+Dictionary<int,\s*int>\s+lastCombatWeaponSlots\s*=\s*new\s+Dictionary<int,\s*int>\s*\(\s*\)\s*;' 'Dual-wield combat should keep per-attacker last-hand state so attack source alternation can be deterministic.'
Assert-SourceMatch $playfieldSource 'GetEquippedCombatWeapon\s*\(ICharacter\s+attacker\).*?rightHandUsable\s*&&\s*leftHandUsable.*?lastCombatWeaponSlots\.TryGetValue\s*\(\s*attackerInstance,\s*out\s+lastSlot\s*\).*?lastSlot\s*==\s*\(int\)WeaponSlots\.Righthand.*?lastCombatWeaponSlots\s*\[\s*attackerInstance\s*\]\s*=\s*\(int\)WeaponSlots\.LeftHand.*?return\s+new\s+EquippedCombatWeapon\s*\{\s*Item\s*=\s*leftHand,\s*Slot\s*=\s*\(int\)WeaponSlots\.LeftHand\s*\}\s*;.*?lastCombatWeaponSlots\s*\[\s*attackerInstance\s*\]\s*=\s*\(int\)WeaponSlots\.Righthand.*?return\s+new\s+EquippedCombatWeapon\s*\{\s*Item\s*=\s*rightHand,\s*Slot\s*=\s*\(int\)WeaponSlots\.Righthand\s*\}\s*;' 'Dual-wield combat source should alternate between right and left hand slots instead of always preferring one hand.'
Assert-SourceMatch $playfieldSource 'StopFightingDeadTarget\s*\(Identity\s+deadTarget\).*?nextCombatTicks\.Remove\s*\(\s*character\.Identity\.Instance\s*\).*?lastCombatWeaponSlots\.Remove\s*\(\s*character\.Identity\.Instance\s*\)' 'Combat stop cleanup should clear dual-wield alternation state when a target dies or combat is interrupted.'
Assert-SourceMatch $playfieldSource 'GetCombatAttackSource\s*\(ICharacter\s+attacker\).*?weapon\.GetAttribute\s*\(\s*\(int\)StatIds\.mindamage\s*\).*?weapon\.GetAttribute\s*\(\s*\(int\)StatIds\.maxdamage\s*\).*?weapon\.GetAttribute\s*\(\s*\(int\)StatIds\.damagebonus\s*\).*?NormalizeCombatRange\s*\(\s*weapon\.GetAttribute\s*\(\s*\(int\)StatIds\.attackrange\s*\)\s*\).*?NormalizeCombatDelaySeconds\s*\(\s*weapon\.GetAttribute\s*\(\s*\(int\)StatIds\.itemdelay\s*\).*?weapon\.GetAttribute\s*\(\s*\(int\)StatIds\.rechargedelay\s*\)' 'Equipped weapon combat should use item damage, range, attack delay, and recharge delay stats.'
Assert-SourceMatch $playfieldSource 'DoCombatTick\s*\(\s*dynel\s*\).*?if\s*\(\s*dynel\.Controller\.IsFollowing\s*\(\s*\)\s*\).*?dynel\.Controller\.DoFollow\s*\(\s*\)' 'NPC heartbeat should stay on the original combat-tick-then-follow order until a capture-backed movement repair replaces it.'
Assert-SourceNoMatch $playfieldSource 'NpcMeleeFollowStopDistance' 'NPC melee follow should not use a calculated stop-distance ring around the player.'
Assert-SourceMatch $playfieldSource 'BuildNpcCombatFollowStopDistance\s*\(double\s+range\s*\).*?range\s*>\s*MaxMeleeCombatDistance\s*\?\s*range\s*:\s*0\.0' 'NPC combat chase should send melee follow directly toward the target and reserve stop distance for ranged attacks.'
Assert-SourceMatch $playfieldSource 'GetCombatPosition\s*\(ICharacter\s+character\).*?character\.Controller\s+is\s+PlayerController.*?character\.RawCoordinates.*?predictedPosition\s*=\s*character\.Coordinates\s*\(\s*\)\.coordinate.*?MoveCombatPositionToward\s*\(\s*rawPosition,\s*predictedPosition,\s*EnemyBehaviorContract\.MaxPlayerChaseProjectionDistance\s*\)' 'NPC combat range checks should use bounded player projection anchored to the raw CharDCMove coordinate.'
Assert-SourceMatch $playfieldSource 'MoveCombatPositionToward\s*\(.*?double\s+step\s*=\s*Math\.Min\s*\(\s*distance,\s*maxDistance\s*\).*?return\s+new\s+CellAO\.Core\.Vector\.Vector3' 'NPC combat player projection should be clamped so prediction cannot drag mobs to an unbounded projected point.'
Assert-SourceMatch $playfieldSource 'IsInCombatRange\s*\(ICharacter\s+attacker,\s*ICharacter\s+target,\s*double\s+range\s*\).*?GetCombatDistance\s*\(\s*attacker,\s*target\s*\)\s*<=\s*range' 'NPC combat range checks should flow through the raw-player combat distance helper.'
Assert-SourceMatch $playfieldSource 'UpdateNpcMeleeFollowHold\s*\(ICharacter\s+attacker,\s*ICharacter\s+target,\s*double\s+range\s*\).*?double\s+distance\s*=\s*GetCombatDistance\s*\(\s*attacker,\s*target\s*\).*?distance\s*<=\s*MaxMeleeFollowHoldDistance.*?npcController\.SuppressMotionSegmentUpdates\s*\(\s*closeEnoughToHold\s*\)' 'Melee NPC attackers should suppress fresh chase packets only while visually close, not for the full hit range.'
Assert-SourceMatch $playfieldSource 'UpdateNpcMeleeFollowHold\s*\(ICharacter\s+attacker,\s*ICharacter\s+target,\s*double\s+range\s*\).*?closeEnoughToHold\s*\|\|\s*npcController\.IsFollowing\s*\(\s*\).*?LogNpcBrain\s*\(\s*"Chasing"\s*,\s*"melee-separated".*?npcController\.Follow\s*\(\s*target\.Identity,\s*BuildNpcCombatFollowStopDistance\s*\(\s*range\s*\)\s*\)' 'Melee NPC attackers should resume follow before the full 8m attack gate when the target separates from close range.'
Assert-SourceMatch $playfieldSource 'DoCombatTick\s*\(.*?attacker\.Controller\s+is\s+NPCController\s+&&\s*!ShouldNpcStopForCombatAttack\s*\(\s*attackSource\s*\).*?UpdateNpcMeleeFollowHold\s*\(\s*attacker,\s*target,\s*attackSource\.Range\s*\)' 'Melee NPC combat should update close-hold suppression while in range instead of waiting for the out-of-range gate.'
Assert-SourceMatch $playfieldSource 'TryMoveNpcIntoCombatRange\s*\(ICharacter\s+attacker,\s*ICharacter\s+target,\s*double\s+range\s*\).*?npcController\.SuppressMotionSegmentUpdates\s*\(\s*false\s*\).*?npcController\.IsFollowing\s*\(\s*\)' 'Out-of-range NPC chase should re-enable motion segment updates before relying on existing follow state.'
Assert-SourceMatch $playfieldSource 'TryMoveNpcIntoCombatRange\s*\(ICharacter\s+attacker,\s*ICharacter\s+target,\s*double\s+range\s*\).*?NPCController\s+npcController\s*=\s*attacker\.Controller\s+as\s+NPCController.*?npcController\s*==\s*null.*?npcController\.IsFollowing\s*\(\s*\).*?return\s*;.*?LogNpcBrain\s*\(\s*"Chasing"\s*,\s*"out-of-range".*?npcController\.Follow\s*\(\s*target\.Identity,\s*BuildNpcCombatFollowStopDistance\s*\(\s*range\s*\)\s*\)' 'Out-of-range NPC attackers should start one sticky follow and avoid restarting while already following.'
Assert-SourceNoMatch $npcControllerSource 'AdvancePredictedPosition' 'NPC follow should not feed generic Character dead-reckoning back into RawCoordinates; playtest showed that compounds cross-playfield warps.'
Assert-SourceMatch $npcControllerSource 'MaxNpcFollowSpeedPerSecond\s*=\s*EnemyBehaviorContract\.MaxNpcFollowSpeedPerSecond' 'NPC chase authority should be capped through the source-backed enemy behavior contract.'
Assert-SourceNoMatch $npcControllerSource 'MinNpcCoordinateFollowSeconds|MinNpcCoordinateFollowDestinationDelta|NpcMeleeFollowReengageDistance|lastFollowPacket|hasFollowPacketDestination|ShouldSendCoordinateFollow|BuildPacketDestination|SendCoordinateFollow' 'NPC chase should not keep the failed coordinate-repath throttle/arrival model.'
Assert-SourceNoMatch $npcControllerSource 'MaxNpcFollowSegmentDistance|MinNpcFollowRepathSeconds|followSegmentEndCoordinates|followDestinationCoordinates|targetOutranDestination' 'NPC chase should not keep the failed coordinate segment/destination state that caused visible spirals and snapping.'
Assert-SourceMatch $npcControllerSource 'GetFollowTargetPosition\s*\(ICharacter\s+target\).*?target\.Controller\s+is\s+PlayerController.*?target\.RawCoordinates.*?predictedPosition\s*=\s*target\.Coordinates\s*\(\s*\)\.coordinate.*?MoveToward\s*\(\s*rawPosition,\s*predictedPosition,\s*MaxPlayerChaseProjectionDistance\s*\)' 'NPC server-side chase authority should use bounded player projection anchored to the raw CharDCMove coordinate.'
Assert-SourceOrdered $npcControllerSource @(
    'MoveToward\s*\(Vector3\s+start,\s*Vector3\s+destination,\s*double\s+maxDistance\s*\)',
    'double\s+step\s*=\s*Math\.Min\s*\(\s*distance,\s*maxDistance\s*\)',
    'double\s+factor\s*=\s*step\s*/\s*distance',
    'return\s+new\s+Vector3'
) 'NPC chase helper should clamp every server-side movement step.'
Assert-SourceOrdered $npcControllerSource @(
    'private\s+struct\s+NpcMotionSegment',
    'public\s+Vector3\s+Start',
    'public\s+Vector3\s+End',
    'public\s+DateTime\s+StartedUtc',
    'public\s+bool\s+Active'
) 'NPC follow should keep one motion segment as the chase position contract.'
Assert-SourceOrdered $npcControllerSource @(
    'CurrentMotionSegmentPosition\s*\(DateTime\s+now\s*\)',
    'this\.followMotionSegment\.Active',
    'double\s+elapsedSeconds\s*=\s*Math\.Max\s*\(\s*0\.0,\s*\(now\s*-\s*this\.followMotionSegment\.StartedUtc\)\.TotalSeconds\s*\)',
    'MoveToward\s*\(\s*this\.followMotionSegment\.Start,\s*this\.followMotionSegment\.End,\s*MaxNpcFollowSpeedPerSecond\s*\*\s*elapsedSeconds\s*\)',
    'UpdateMotionSegmentPosition\s*\(DateTime\s+now\s*\)',
    'this\.Character\.Coordinates\s*\(\s*position\s*\)'
) 'NPC range authority should come from the same active motion segment used for visible chase packets.'
Assert-SourceNoMatch $npcControllerSource 'AdvanceFollowPosition|EstimateVisibleFollowPosition|followStartCoordinates|lastVisibleFollowPacket|hasVisibleFollowPacket' 'NPC chase should not keep separate server-side and guessed-visible movement models.'
Assert-SourceNoMatch $npcControllerSource 'SetFollowSegmentEnd|BuildFollowSegmentEnd|BuildFollowDestination|FollowDestinationStillReachesCombatRange|hasFollowSegmentEndCoordinates|hasFollowDestinationCoordinates' 'NPC follow should not use the failed destination/segment repath helpers.'
Assert-SourceMatch $npcControllerSource 'FaceToward\s*\(Vector3\s+start,\s*Vector3\s+destination\s*\).*?start\.Distance2D\s*\(\s*destination\s*\)\s*<\s*0\.001.*?return;.*?Vector3\s+normalizedDirection\s*=\s*direction\.Normalize\s*\(\s*\).*?GenerateRotationFromDirectionVector\s*\(\s*normalizedDirection\s*\)' 'NPC chase heading should guard zero-length segments before normalizing.'
Assert-SourceNoMatch $npcControllerSource 'this\.SendWantedDirection\s*\(' 'Normal melee chase should not inject SetWantedDirection; focused official chase used FollowTarget movement without that side-channel.'
Assert-SourceMatch $npcControllerSource 'SendWantedDirection\s*\(Vector3\s+direction\s*\).*?new\s+SetWantedDirectionMessage.*?Identity\s*=\s*this\.Character\.Identity.*?Unknown\s*=\s*0.*?DirectinVector\s*=.*?X\s*=\s*direction\.xf.*?Y\s*=\s*direction\.yf.*?Z\s*=\s*direction\.zf' 'The recovered SetWantedDirection packet helper should remain available for capture-specific future use.'
Assert-SourceNoMatch $npcControllerSource 'ShouldSendChaseCorrection|SendOfficialChaseCorrection|DirectionDot2D|lastFollowCorrectionUtc|lastFollowPacketStart|hasFollowPacketStart' 'Normal NPC chase should not inject sharp-turn correction packets; local playtest showed that path caused snap/jitter during circle movement.'
Assert-SourceOrdered $npcControllerSource @(
    'public\s+bool\s+Follow\s*\(\s*Identity\s+target\s*\)',
    'return\s+this\.Follow\s*\(\s*target,\s*0\.0\s*\)',
    'public\s+bool\s+Follow\s*\(\s*Identity\s+target,\s*double\s+stopDistance\s*\)',
    'ICharacter\s+npc\s*=\s*GetCharacterFromPool\s*\(\s*this\.Character\.Playfield\.Identity,\s*target\s*\)',
    'npc\s*==\s*null',
    'this\.StopFollow\s*\(\s*\)',
    'return\s+false',
    'this\.ResetFollowPosition\s*\(\s*\)',
    'this\.followStopDistance\s*=\s*Math\.Max\s*\(\s*0\.0,\s*stopDistance\s*\)',
    'Vector3\s+targetPosition\s*=\s*GetFollowTargetPosition\s*\(\s*npc\s*\)',
    'Vector3\s+start\s*=\s*this\.Character\.RawCoordinates',
    'this\.followCoordinates\s*=\s*targetPosition',
    'this\.followStopDistance\s*>\s*0\.0\s*&&\s*start\.Distance2D\s*\(\s*targetPosition\s*\)\s*<=\s*this\.followStopDistance',
    'this\.FaceToward\s*\(\s*start,\s*targetPosition\s*\)',
    'this\.Run\s*\(\s*\)',
    'this\.FaceToward\s*\(\s*start,\s*targetPosition\s*\)',
    'this\.SendMotionSegmentFollow\s*\(\s*"coordinate-follow",\s*start,\s*targetPosition,\s*now\s*\)'
) 'NPC initial follow should use the validated coordinate FollowTarget payload and null-safe target lookup.'
Assert-SourceMatch $npcControllerSource 'GetCharacterFromPool\s*\(Identity\s+parent,\s*Identity\s+identity\s*\).*?Pool\.Instance\.GetObject\s*\(\s*parent,\s*identity\s*\)\s+as\s+ICharacter' 'NPC follow target lookup should use the null-returning pool path instead of throwing heartbeat exceptions on stale targets.'
Assert-SourceNoMatch $npcControllerSource 'targetFollowMovement|FollowTargetMessageHandler\.Default\.Send\s*\(\s*this\.Character,\s*target\s*\)' 'NPC chase should not use the unproven SimpleChar target-follow path; local test showed it broke visible chase.'
Assert-SourceNoMatch $npcControllerSource 'this\.Walk\s*\(\s*\)\s*;\s*SendOfficialSetPos\s*\(' 'NPC initial follow should not send a bare SetPos before the first coordinate FollowTarget.'
Assert-SourceNoMatch $npcControllerSource 'SendOfficialStopMovingCmd|SendOfficialSetPos|SendOfficialSettle|new\s+StopMovingCmdMessage|new\s+SetPosMessage' 'Official 20260529-212034 target chase sample did not justify StopMovingCmd/SetPos/type-21 settle in local NPC chase.'
Assert-SourceOrdered $npcControllerSource @(
    'public\s+void\s+DoFollow\s*\(\s*\)',
    'Vector3\s+targetPosition\s*=\s*this\.followCoordinates',
    'targetPosition\s*=\s*GetFollowTargetPosition\s*\(\s*targetChar\s*\)',
    'DateTime\s+now\s*=\s*DateTime\.UtcNow',
    'Vector3\s+current\s*=\s*this\.UpdateMotionSegmentPosition\s*\(\s*now\s*\)',
    'this\.followCoordinates\s*=\s*targetPosition',
    'this\.followStopDistance\s*>\s*0\.0\s*&&\s*current\.Distance2D\s*\(\s*targetPosition\s*\)\s*<=\s*this\.followStopDistance',
    'this\.FaceToward\s*\(\s*current,\s*targetPosition\s*\)',
    'this\.suppressMotionSegmentUpdates',
    'return\s*;',
    'this\.ShouldSendMotionSegmentUpdate\s*\(\s*current,\s*targetPosition,\s*now\s*\)',
    'this\.SendMotionSegmentFollow\s*\(\s*"coordinate-update",\s*current,\s*targetPosition,\s*now\s*\)'
) 'NPC continuous chase should advance server authority from the active motion segment and only send coordinate updates through the gated path.'
Assert-SourceNoMatch $npcControllerSource 'Distance to target|bool\s+repathDue|targetMoved\s*\|\||FollowTargetMessageHandler\.Default\.Send\s*\(\s*this\.Character,\s*start,\s*segmentEnd\s*\)' 'NPC DoFollow should not emit per-heartbeat distance spam or failed segment-end repath packets during combat chase.'
Assert-SourceNoMatch $npcControllerSource 'DoFollow\s*\(\s*\).*?StopFollowForCombatRange\s*\(\s*targetPosition\s*\)' 'NPC combat-range follow should not clear follow state; local logs showed that reset produced follow/stop/follow churn.'
Assert-SourceNoMatch $npcControllerSource 'this\.FaceToward\s*\(\s*current,\s*targetPosition\s*\)\s*;\s*this\.StopMovement\s*\(\s*\)\s*;\s*this\.LogChase\s*\(\s*"combat-stop"' 'NPC combat-range stop should use the FollowTarget stop packet without generic forward-stop prediction.'
Assert-SourceNoMatch $npcControllerSource 'PauseFollowForCombatRange|ResumeFollowFromCombatRange|followPausedForCombatRange' 'NPC combat movement should not keep pause/resume state; out of range follows, in range stops and attacks.'
Assert-SourceMatch $npcControllerSource 'public\s+void\s+StopFollow\s*\(\s*\).*?this\.followIdentity\s*=\s*Identity\.None.*?this\.ResetFollowPosition\s*\(\s*\)' 'Full follow cleanup should clear follow position state.'
Assert-SourceNoMatch $npcControllerSource 'targetMoved\s*\|\|\s*repathDue|targetPosition\.Distance2D\s*\(\s*this\.followCoordinates\s*\)\s*>\s*2\.0f' 'NPC target movement should not reintroduce per-move visible repath packets; local logs showed 48ms repaths caused visible snapping.'
Assert-SourceOrdered $playfieldSource @(
    'private\s+void\s+DoCombatTick\s*\(ICharacter\s+attacker\s*\)',
    'ICharacter\s+target\s*=',
    'CombatAttackSource\s+attackSource\s*=\s*this\.GetCombatAttackSource\s*\(\s*attacker\s*\)',
    'attacker\.Controller\s+is\s+NPCController',
    '!this\.IsInCombatRange\s*\(\s*attacker,\s*target,\s*attackSource\.Range\s*\)',
    'this\.TryMoveNpcIntoCombatRange\s*\(\s*attacker,\s*target,\s*attackSource\.Range\s*\)',
    'return\s*;',
    'DateTime\s+nextTick'
) 'NPC combat movement should start chasing before attack cooldown can delay movement state.'
Assert-SourceMatch $playfieldSource 'ShouldNpcStopForCombatAttack\s*\(CombatAttackSource\s+attackSource\s*\).*?attackSource\.Range\s*>\s*MaxMeleeCombatDistance' 'NPCs should only stop movement for ranged combat sources; melee attacks are allowed while moving.'
Assert-SourceMatch $playfieldSource 'attacker\.Controller\s+is\s+NPCController\s*&&\s*ShouldNpcStopForCombatAttack\s*\(\s*attackSource\s*\).*?this\.StopNpcFollowIfInCombatRange\s*\(\s*attacker,\s*target,\s*attackSource\.Range\s*\)' 'NPC combat tick should only clear follow state for ranged attacks, not melee.'
Assert-SourceNoMatch $npcControllerSource 'public\s+void\s+DoFollow\s*\(\s*\).*?SendOfficialSetPos\s*\(' 'NPC DoFollow should not send SetPos on target movement.'
Assert-SourceNoMatch $npcControllerSource 'new\s+SetPosMessage' 'NPC chase correction should not emit SetPos from the local runtime without a target-specific official capture proving that exact path.'
Assert-SourceNoMatch $playfieldSource 'TryHoldNpcCombatMovementBeforeFollow|TryMaintainNpcCombatFacing|nextNpcCombatMoveTicks|NpcCombatMoveRefreshSeconds|NpcCombatReengageDistance' 'Speculative NPC chase/hold/repath helpers should stay out until backed by packet evidence.'
Assert-SourceNoMatch $npcControllerSource 'MoveIntoCombatRange|HoldCombatPosition|FaceTarget|MoveToCombatPosition|NPC combat move' 'Speculative NPC controller combat movement helpers should stay out until backed by packet evidence.'
Assert-SourceMatch $playfieldSource 'if\s*\(\s*killingHit\s*\).*?target\.Controller\s+is\s+NPCController.*?KillNpcTarget\s*\(\s*attacker\s*,\s*target\s*\).*?target\.Controller\s+is\s+PlayerController.*?KillPlayerTarget\s*\(\s*target\s*\)' 'Combat killing hits should route NPC deaths through attacker-aware reward handling and player targets through a real player death branch.'
Assert-SourceMatch $playfieldSource 'KillPlayerTarget\s*\(ICharacter\s+target\).*?MarkPlayerDead\s*\(\s*target\s*\).*?target\.SendChangedStats\s*\(\s*\).*?target\.SetTarget\s*\(\s*Identity\.None\s*\).*?target\.SetFightingTarget\s*\(\s*Identity\.None\s*\).*?StopFightingDeadTarget\s*\(\s*target\.Identity\s*\).*?SendCombatStopMessage\s*\(\s*target\s*\).*?SendPlayerDeathAnimation\s*\(\s*target\s*\)' 'Player death should mark dead stats, stop both sides of combat, and send the death action.'
Assert-SourceMatch $playfieldSource 'MarkPlayerDead\s*\(ICharacter\s+target\).*?Stats\s*\[\s*StatIds\.health\s*\]\.Value\s*=\s*0.*?Stats\s*\[\s*StatIds\.deadtimer\s*\]\.Value\s*=\s*1.*?Stats\s*\[\s*StatIds\.healdelta\s*\]\.Value\s*=\s*0.*?Stats\s*\[\s*StatIds\.nanodelta\s*\]\.Value\s*=\s*0' 'Player death stats should keep the player dead at zero health and stop passive regen.'
Assert-SourceMatch $playfieldSource 'SendPlayerDeathAnimation\s*\(ICharacter\s+target\).*?Action\s*=\s*CharacterActionType\.Death.*?Parameter2\s*=\s*DefaultPlayerDeathAnimationKey' 'Player death should use the player-specific CharacterAction death packet key until a player-death capture proves a different key.'
Assert-SourceMatch $playfieldSource 'KillNpcTarget\s*\(ICharacter\s+attacker,\s*ICharacter\s+target\).*?MarkNpcDead\s*\(\s*target\s*\).*?StopFightingDeadTarget\s*\(\s*target\.Identity\s*\).*?SendNpcDeathAnimation\s*\(\s*target\s*\).*?AwardCombatXp\s*\(\s*attacker,\s*target\s*\).*?ScheduleCorpseSpawn\s*\(\s*target\s*,\s*corpseIdentity\s*\).*?deadNpcDespawnTicks\s*\[\s*target\.Identity\.Instance\s*\]\s*=\s*DateTime\.UtcNow\s*\+\s*DeadNpcDespawnDelay\s*;' 'NPC death should mark dead, stop fighting, play death, award attacker XP, schedule corpse, and delay despawn.'
Assert-SourceMatch $playfieldSource 'AwardCombatXp\s*\(ICharacter\s+attacker,\s*ICharacter\s+target\).*?attacker\.Controller\s+is\s+PlayerController.*?StatIds\.xp.*?StatIds\.lastxp.*?StatIds\.unsavedxp.*?StatMessageHandler\.Default\.SendChanged\s*\(\s*attacker\s*\).*?SendRewardFeedback\s*\(.*?You received \{0\} xp\.' 'Combat XP reward should apply player XP/LastXP/UnsavedXP stats through the normal changed-stat pipeline and send reward feedback text.'
Assert-SourceNoMatch $playfieldSource 'AwardCombatXp\s*\(ICharacter\s+attacker,\s*ICharacter\s+target\).*?Stats\s*\[\s*StatIds\.xp\s*\]\.Changed\s*=\s*false' 'Combat XP reward should not clear XP changed flags manually before the normal stat update pipeline sends them.'
Assert-SourceMatch $playfieldSource 'CalculateCombatXpReward\s*\(ICharacter\s+attacker,\s*ICharacter\s+target\).*?target\.Stats\s*\[\s*StatIds\.xp\s*\]\.Value.*?return\s+targetXp.*?return\s+1.*?return\s+Math\.Max\s*\(\s*1,\s*targetLevel\s*\)' 'Combat XP reward should prefer explicit NPC XP data and otherwise use a minimal local fallback.'
Assert-SourceMatch $playfieldSource 'MarkNpcDead\s*\(ICharacter\s+target\).*?Stats\s*\[\s*StatIds\.deadtimer\s*\]\.Value\s*=\s*1\s*;.*?Stats\s*\[\s*StatIds\.corpseanimkey\s*\]\.Value\s*=\s*DeathAnimationKeyFor\s*\(\s*target\s*\)\s*;.*?Stats\s*\[\s*StatIds\.dieanim\s*\]\.Value\s*=\s*DeathAnimationKeyFor\s*\(\s*target\s*\)\s*;.*?Stats\s*\[\s*StatIds\.healdelta\s*\]\.Value\s*=\s*0\s*;.*?DoNotDoTimers\s*=\s*true\s*;' 'NPC death stats should disable healing/timers and expose death animation keys.'
Assert-SourceMatch $playfieldSource 'TryUseDeadNpcCorpse\s*\(ICharacter\s+looter,\s*Identity\s+deadNpcIdentity,\s*out\s+Identity\s+corpseIdentity\).*?DeadNpcIdentity\.Instance\s*==\s*deadNpcIdentity\.Instance.*?corpseIdentity\s*=\s*corpse\.CorpseIdentity\s*;.*?return\s+this\.TryUseCorpse\s*\(\s*looter\s*,\s*corpse\.CorpseIdentity\s*\)\s*;' 'Using a dead NPC dynel should route to its registered corpse identity.'
Assert-SourceMatch $playfieldSource 'TryUseCorpse\s*\(ICharacter\s+looter,\s*Identity\s+corpseIdentity\).*?corpse\.Opened\s*=\s*true\s*;.*?corpse\.HasUnlootedItems.*?ExtendCorpseLifetime\s*\(\s*corpse\s*,\s*CombatCorpseRules\.ItemLootCorpseLifetime\s*,\s*"corpse-use"\s*\).*?NextUseSendsAccessActionOnly.*?SendCorpseLootAccessAction\s*\(\s*looter\s*,\s*corpse\s*\).*?SendUseActionFinished\s*\(\s*looter\s*\).*?NextUseSendsAccessActionOnly\s*=\s*false\s*;.*?SendCorpseInventoryUpdateAndCredits\s*\(\s*looter\s*,\s*corpse\s*\).*?NextUseSendsAccessActionOnly\s*=\s*true\s*;' 'Corpse reopen with remaining loot should alternate access action and inventory/credit update packets.'
Assert-SourceMatch $playfieldSource 'TryUseCorpse\s*\(ICharacter\s+looter,\s*Identity\s+corpseIdentity\).*?!corpse\.HasUnlootedItems.*?ScheduleCorpseDespawn\s*\(\s*corpse\s*,\s*CombatCorpseRules\.EmptyCorpseCleanupAfterOpenedDelay\s*,\s*"opened-empty"\s*\)' 'Opening an empty corpse should schedule the short cleanup timer.'
Assert-SourceMatch $combatCorpseRulesSource 'EmptyCorpseCleanupAfterOpenedDelay\s*=\s*TimeSpan\.FromSeconds\s*\(\s*3\s*\)' 'Empty opened/looted corpses should use the capture-backed roughly three second cleanup delay.'
Assert-SourceMatch $playfieldSource 'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\).*?sourceContainer\.Type\s*!=\s*IdentityType\.Backpack.*?int\s+corpseInventoryHandle\s*=\s*\(sourceContainer\.Instance\s*>>\s*16\)\s*&\s*0xffff\s*;.*?int\s+requestedLootSlot\s*=\s*sourceContainer\.Instance\s*&\s*0xffff\s*;' 'Corpse item moves should decode the Backpack source container into corpse inventory handle and loot slot.'
Assert-SourceMatch $playfieldSource 'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\).*?target\s*!=\s*looter\.Identity.*?SendUseActionFinished\s*\(\s*looter\s*\)' 'Corpse item moves should reject target mismatches without falling through to normal inventory moves.'
Assert-SourceMatch $playfieldSource 'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\).*?CharacterHasUniqueItemAlready\s*\(\s*looter\s*,\s*lootItem\.Item\s*\).*?You already have this unique item\.' 'Corpse loot should reject duplicate UNIQUE items before adding to inventory.'
Assert-SourceMatch $playfieldSource 'CorpseLootItemIdentityType\s*=\s*0x09000001' 'Corpse loot-window entries should use the captured runtime item identity type 0x09000001.'
Assert-SourceMatch $playfieldSource 'RollCorpseLootItems\s*\(ICharacter\s+target\).*?LootIdentity\s*=\s*this\.AllocateCorpseLootItemIdentity\s*\(\s*\)' 'Corpse loot items should receive stable runtime identities when the corpse loot is rolled.'
Assert-SourceMatch $playfieldSource 'CreateCorpseInventoryEntry\s*\(CorpseLootItem\s+lootItem\).*?Identity\s*=\s*lootItem\.LootIdentity.*?LowId\s*=\s*lootItem\.Item\.LowID.*?HighId\s*=\s*lootItem\.Item\.HighID.*?Quality\s*=\s*lootItem\.Item\.Quality' 'Corpse InventoryUpdate entries should use live-shaped item identity plus low/high/quality fields.'
Assert-SourceNoMatch $playfieldSource 'CreateCorpseInventoryEntry\s*\(CorpseLootItem\s+lootItem\).*?Identity\s*=\s*Identity\.None' 'Corpse InventoryUpdate entries must not advertise loot items as Identity.None.'
Assert-SourceMatch $playfieldSource 'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\).*?lootItem\.Looted\s*=\s*true\s*;.*?SendCorpseContainerAddItem\s*\(\s*looter,\s*sourceContainer,\s*targetPlacement\s*\).*?!corpse\.HasUnlootedItems.*?ScheduleCorpseDespawn\s*\(\s*corpse\s*,\s*CombatCorpseRules\.EmptyCorpseCleanupAfterOpenedDelay\s*,\s*"looted-empty"\s*\).*?ExtendCorpseLifetime\s*\(\s*corpse\s*,\s*CombatCorpseRules\.ItemLootCorpseLifetime\s*,\s*"loot-remaining"\s*\)' 'Successful corpse loot should mark the item, notify the client with the captured 0x6F placement, despawn empty corpses, and keep corpses with remaining loot alive.'
Assert-SourceMatch $playfieldSource 'SendCorpseContainerAddItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*int\s+targetPlacement\).*?SendCompressed\s*\(\s*new\s+ContainerAddItemMessage.*?SourceContainer\s*=\s*sourceContainer.*?TargetPlacement\s*=\s*targetPlacement.*?Target\s*=\s*looter\.Identity.*?looter\.Identity\.Instance\s*\)' 'Corpse loot item ack should match captured live/private shape: source corpse backpack, target player, original 0x6F placement.'
Assert-SourceNoMatch $playfieldSource 'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\)(?:(?!private\s+static\s+bool\s+CharacterHasUniqueItemAlready).)*?StatMessageHandler\.Default\.SendSingle\s*\(' 'Corpse item moves should not send a cash stat update; corpse credits are synchronized on corpse open.'
Assert-SourceMatch $playfieldSource 'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\).*?inventoryError\s*!=\s*InventoryError\.OK.*?return\s+true\s*;.*?looter\.BaseInventory\.Write\s*\(\s*\)\s*;\s*lootItem\.Looted\s*=\s*true' 'Successful corpse loot should persist the added inventory item before marking the corpse item looted.'
Assert-SourceMatch $playfieldSource 'CorpseCreditAwardDelay\s*=\s*TimeSpan\.FromMilliseconds\s*\(\s*500\s*\)' 'Corpse credits should use the captured roughly 500 ms delay after corpse InventoryUpdate.'
Assert-SourceMatch $playfieldSource 'SendCorpseInventoryUpdateAndCredits\s*\(ICharacter\s+looter,\s*CorpseState\s+corpse\).*?SendCorpseInventoryUpdate\s*\(\s*looter,\s*corpse\s*\).*?ScheduleCorpseCreditAward\s*\(\s*looter,\s*corpse\s*\)' 'Corpse open should send InventoryUpdate first and schedule the cash Stat for the captured delayed follow-up.'
Assert-SourceNoMatch $playfieldSource 'SendCorpseInventoryUpdateAndCredits\s*\(ICharacter\s+looter,\s*CorpseState\s+corpse\)(?:(?!private\s+void\s+ScheduleCorpseCreditAward).)*?AwardCorpseCredits\s*\(\s*looter,\s*corpse\s*\)' 'Corpse open should not award/send credits in the same tick as InventoryUpdate.'
Assert-SourceMatch $playfieldSource 'ProcessPendingCorpseCreditAwards\s*\(\s*\).*?pendingCorpseCreditAwards.*?FindByIdentity<ICharacter>\s*\(\s*award\.LooterIdentity\s*\).*?AwardCorpseCredits\s*\(\s*looter,\s*corpse\s*\)' 'Delayed corpse credit awards should resolve the looter and then send the cash stat.'
Assert-SourceMatch $playfieldSource 'AwardCorpseCredits\s*\(ICharacter\s+looter,\s*CorpseState\s+corpse\).*?uint\s+cashBeforeBase\s*=\s*looter\.Stats\s*\[\s*StatIds\.cash\s*\]\.BaseValue;.*?long\s+cashAfterLong\s*=\s*\(long\)cashBefore\s*\+\s*corpse\.Credits;.*?looter\.Stats\s*\[\s*StatIds\.cash\s*\]\.Set\s*\(\s*\(uint\)cashAfter\s*\).*?StatMessageHandler\.Default\.SendChanged\s*\(\s*looter\s*\)' 'Corpse credit award should add from authoritative BaseValue, then Set() and send cash through the normal changed-stat pipeline.'
Assert-SourceNoMatch $playfieldSource 'SendCorpseCashStat\s*\(' 'Corpse cash should not use the old direct player-header cash stat helper.'
Assert-SourceMatch $playfieldSource 'AwardCorpseCredits\s*\(ICharacter\s+looter,\s*CorpseState\s+corpse\).*?StatMessageHandler\.Default\.SendChanged\s*\(\s*looter\s*\).*?SendCorpseCreditFeedback\s*\(.*?You received \{0\} \{1\} from the corpse\.' 'Corpse credit award should send visible corpse-credit text after the authoritative cash stat update.'
Assert-SourceMatch $playfieldSource 'SendCorpseCreditFeedback\s*\(ICharacter\s+character,\s*string\s+text\).*?ChatTextMessageHandler\.Default\.Send\s*\(\s*character\s*,\s*text\s*\)' 'Corpse credit feedback should use the known-visible ChatText path; the current client does not render the explicit FormatFeedback corpse-credit line.'
Assert-SourceMatch $playfieldSource 'SendRewardFeedback\s*\(ICharacter\s+character,\s*string\s+text\).*?new\s+FormatFeedbackMessage.*?Identity\s*=\s*character\.Identity.*?FormattedMessage\s*=\s*text.*?character\.Identity\.Instance' 'XP reward text should use the recovered FormatFeedback N3 packet family with the player header sender.'
Assert-SourceNoMatch $playfieldSource 'AwardCorpseCredits\s*\(ICharacter\s+looter,\s*CorpseState\s+corpse\).*?Stats\s*\[\s*StatIds\.cash\s*\]\.Value' 'Corpse credit award should not read the calculated cash Value path.'
Assert-SourceMatch $playfieldSource 'CharacterHasUniqueItemAlready\s*\(ICharacter\s+character,\s*IItem\s+item\).*?InventoryItemRules\.HasSameUniqueItem.*?BaseInventory.*?Pages\.Values' 'Corpse unique checks should inspect existing inventory items through shared rules.'
Assert-SourceMatch $playfieldSource 'ProcessPendingCorpseSpawns\s*\(\s*\).*?pendingCorpseSpawns\.Remove\s*\(\s*corpse\.DeadNpcIdentity\.Instance\s*\).*?RegisterCorpse\s*\(\s*target\s*,\s*corpse\.CorpseIdentity\s*\).*?SendCorpseFullUpdate\s*\(\s*target\s*,\s*corpse\.CorpseIdentity\s*\)' 'Pending corpse spawns should register state before sending CorpseFullUpdate.'
Assert-SourceMatch $playfieldSource 'SendCorpseFullUpdate\s*\(ICharacter\s+target,\s*Identity\s+corpseIdentity\).*?CorpseFullUpdate\.Build\s*\(\s*target,\s*corpseIdentity,\s*character\.Identity,\s*this\.server\.Id,\s*corpseCatMesh,\s*corpseMonsterData,\s*this\.CorpseCreditsFor\s*\(\s*corpseIdentity\s*\)\s*\)' 'Playfield corpse lifecycle should delegate raw CorpseFullUpdate construction to the packet builder with the registered corpse credit value.'
Assert-SourceNoMatch $playfieldSource 'BuildDebugCorpseFullUpdate|HexToBytes\s*\(' 'Playfield should not own raw CorpseFullUpdate template bytes.'
Assert-SourceMatch $corpseFullUpdateMessageSource 'Obsolete\s*\(\s*"Placeholder only\..*?ZoneEngine\.Core\.Packets\.CorpseFullUpdate\.Build.*?capture-backed serializer exists\.",\s*false\s*\)' 'AOtomation CorpseFullUpdateMessage should stay clearly marked as a placeholder until a capture-backed serializer exists.'
Assert-SourceNoMatch $messagingSerializerSource 'typeof\s*\(\s*CorpseFullUpdateMessage\s*\)' 'SerializerResolverBuilder should not register the placeholder CorpseFullUpdateMessage for generic sends.'
Assert-SourceMatch $corpseFullUpdateSource 'public\s+static\s+class\s+CorpseFullUpdate.*?public\s+static\s+byte\[\]\s+Build\s*\(.*?ICharacter\s+deadNpc.*?Identity\s+corpseIdentity.*?Identity\s+receiver.*?int\s+serverId.*?int\s+corpseCatMesh.*?int\s+corpseMonsterData.*?int\s+corpseCredits' 'CorpseFullUpdate packet builder should expose a named build surface for runtime corpse values.'
Assert-SourceMatch $corpseFullUpdateSource 'CorpseFullUpdate resumes immediately after the encoded string.*?WritePacketLength\s*\(buffer,\s*buffer\.Length\).*?WriteInt32\s*\(buffer,\s*CorpseInstanceOffset,\s*corpseIdentity\.Instance\).*?WriteInt32\s*\(buffer,\s*CorpseCatMeshOffset,\s*corpseCatMesh\).*?WriteInt32\s*\(buffer,\s*CorpseMonsterDataOffset\s*\+\s*afterNameDelta,\s*corpseMonsterData\)' 'CorpseFullUpdate builder should keep the live-shaped variable-name tail and explicit visual offsets.'
Assert-SourceMatch $corpseFullUpdateSource 'CorpseCashValueOffset\s*=\s*211.*?WriteInt32\s*\(\s*buffer,\s*CorpseCashValueOffset,\s*Math\.Max\s*\(\s*0,\s*corpseCredits\s*\)\s*\)' 'CorpseFullUpdate should write the registered corpse credit amount into the corpse dynel Cash stat instead of using the raw template value.'
Assert-SourceNoMatch $corpseFullUpdateSource '0000003d0000006f' 'CorpseFullUpdate template must not preserve the old hardcoded Cash=111 value.'
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
    'KillNpcTarget\s*\(ICharacter\s+attacker,\s*ICharacter\s+target\)',
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
    'InventoryHandle\s*=\s*this\.AllocateCorpseInventoryHandle\s*\(\s*\)',
    'corpses\s*\[\s*corpseIdentity\.Instance\s*\]\s*=\s*state',
    'corpseDespawnTicks\s*\[\s*corpseIdentity\.Instance\s*\]\s*=\s*expiresAtUtc'
) 'Corpse flow regression: RegisterCorpse should roll loot, allocate a loot-window slot, and arm the despawn tick.'
Assert-SourceOrdered $genericCmdSource @(
    'case\s+GenericCmdAction\.Use',
    'target\.Type\s*==\s*IdentityType\.Corpse',
    'TryUseCorpse\s*\(',
    'AcknowledgeCorpseUseDelayed\s*\(',
    'target\.Type\s*==\s*IdentityType\.CanbeAffected',
    'TryRouteDeadNpcCorpseUse\s*\(',
    'AcknowledgeCorpseUseDelayed\s*\('
) 'Corpse flow regression: GenericCmd Use should handle corpse identities, route dead NPC dynels, and delay the corpse-use ack until after the captured cash-stat follow-up.'
Assert-SourceMatch $genericCmdSource 'CorpseUseAcknowledgeDelay\s*=\s*TimeSpan\.FromMilliseconds\s*\(\s*550\s*\)' 'Corpse GenericCmd ack should trail the captured roughly 500 ms cash-stat follow-up.'
Assert-SourceOrdered $playfieldSource @(
    'TryUseCorpse\s*\(ICharacter\s+looter,\s*Identity\s+corpseIdentity\)',
    'corpse\.Opened\s*=\s*true',
    'corpse\.HasUnlootedItems',
    'ExtendCorpseLifetime\s*\(\s*corpse\s*,\s*CombatCorpseRules\.ItemLootCorpseLifetime\s*,\s*"corpse-use"\s*\)',
    'SendCorpseLootAccessAction\s*\(\s*looter\s*,\s*corpse\s*\)',
    'SendUseActionFinished\s*\(\s*looter\s*\)',
    'SendCorpseInventoryUpdateAndCredits\s*\(\s*looter\s*,\s*corpse\s*\)',
    'ScheduleCorpseDespawn\s*\(\s*corpse\s*,\s*CombatCorpseRules\.EmptyCorpseCleanupAfterOpenedDelay\s*,\s*"opened-empty"\s*\)'
) 'Corpse flow regression: corpse use should open loot, alternate access/inventory responses, and short-despawn empty corpses.'
Assert-SourceOrdered $playfieldSource @(
    'SendCorpseInventoryUpdate\s*\(ICharacter\s+looter,\s*CorpseState\s+corpse\)',
    'Where\s*\(x\s*=>\s*!x\.Looted\)',
    'new\s+InventoryUpdateMessage',
    'NumberOfSlots\s*=\s*CombatCorpseRules\.CorpseInventorySlots',
    'Entries\s*=\s*entries',
    'BagIdentity\s*=\s*corpse\.CorpseIdentity',
    'SlotnumberInMainInventory\s*=\s*corpse\.InventoryHandle'
) 'Corpse flow regression: corpse use should send InventoryUpdate for only unlooted items on the corpse bag identity.'
Assert-SourceMatch $playfieldSource 'RegisterCorpse\s*\(ICharacter\s+target,\s*Identity\s+corpseIdentity\).*?InventoryHandle\s*=\s*this\.AllocateCorpseInventoryHandle\s*\(\s*\)' 'Corpse inventory handle should be allocated once at corpse registration and reused for InventoryUpdate.'
Assert-SourceMatch $playfieldSource 'nextCorpseInventoryHandle\s*=\s*0x70' 'Corpse inventory handles should use the captured temp-container handle range starting at 0x70.'
Assert-SourceMatch $playfieldSource 'nextCorpseInventoryHandle\s*>\s*0xff' 'Corpse inventory handles should wrap inside the captured low temp-container range.'
Assert-SourceNoMatch $playfieldSource 'nextCorpseInventoryHandle\s*=\s*1' 'Corpse inventory handles should not use the disproven low-handle workaround.'
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
    'corpseInventoryHandle\s*=\s*\(sourceContainer\.Instance\s*>>\s*16\)\s*&\s*0xffff',
    'requestedLootSlot\s*=\s*sourceContainer\.Instance\s*&\s*0xffff',
    'FindCorpseLootItem\s*\(\s*corpse\s*,\s*requestedLootSlot\s*\)',
    'CharacterHasUniqueItemAlready\s*\(\s*looter\s*,\s*lootItem\.Item\s*\)',
    'TryResolveLootTargetSlot\s*\(',
    'looter\.BaseInventory\.AddToPage\s*\(',
    'looter\.BaseInventory\.Write\s*\(\s*\)',
    'lootItem\.Looted\s*=\s*true',
    'SendCorpseContainerAddItem\s*\(',
    'ScheduleCorpseDespawn\s*\(\s*corpse\s*,\s*CombatCorpseRules\.EmptyCorpseCleanupAfterOpenedDelay\s*,\s*"looted-empty"\s*\)',
    'ExtendCorpseLifetime\s*\(\s*corpse\s*,\s*CombatCorpseRules\.ItemLootCorpseLifetime\s*,\s*"loot-remaining"\s*\)'
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
Assert-SourceMatch $playfieldSource 'SendCorpseInventoryUpdate\s*\(ICharacter\s+looter,\s*CorpseState\s+corpse\).*?Where\s*\(x\s*=>\s*!x\.Looted\).*?NumberOfSlots\s*=\s*CombatCorpseRules\.CorpseInventorySlots.*?BagIdentity\s*=\s*corpse\.CorpseIdentity.*?SlotnumberInMainInventory\s*=\s*corpse\.InventoryHandle' 'Corpse InventoryUpdate should expose only unlooted items on the corpse bag identity and handle.'
Assert-SourceMatch $spawnCommandSource '"status"' 'Spawn command should support combat test status.'
Assert-SourceMatch $spawnCommandSource '"lootstatus"' 'Spawn command should support DB loot coverage diagnostics.'
Assert-SourceMatch $spawnCommandSource '"clear"' 'Spawn command should support combat test cleanup.'
Assert-SourceMatch $spawnCommandSource 'if\s*\(\s*args\.Length\s*==\s*2\s*\).*?return\s+true\s*;' 'Spawn command should accept DB template hash-only commands like /command spawn A004.'
Assert-SourceMatch $spawnCommandSource 'mobCharacter\.Stats\s*\[\s*StatIds\.health\s*\]\.Value\s*=\s*mobCharacter\.Stats\s*\[\s*StatIds\.life\s*\]\.Value\s*;.*?SimpleCharFullUpdate\.ConstructMessage\s*\(\s*mobCharacter\s*\)' 'DB-spawned mobs should have current health populated before SimpleCharFullUpdate is sent.'
Assert-SourceMatch $spawnCommandSource 'new\s+CharInPlayMessage\s*\{\s*Identity\s*=\s*mobCharacter\.Identity\s*,\s*Unknown\s*=\s*0x00\s*\}' 'DB-spawned mobs should send CharInPlay after SimpleCharFullUpdate.'
Assert-SourceMatch $spawnCommandSource 'SpawnClientHintedMobs\s*\(ICharacter\s+character\).*?SpawnPopulationMob\s*\(.*?ZonePopulationOffsets.*?Spawned\s*\{0\}\s*DB population mobs' 'Spawn zone should create a DB-backed population pack rather than another single test mob.'
Assert-SourceMatch $spawnCommandSource 'SpawnPopulationMob\s*\(.*?NonPlayerCharacterHandler\.SpawnMobFromTemplate.*?CombatTestMobArchetype\.Prepare\s*\(\s*mobCharacter,\s*entry\s*\).*?SimpleCharFullUpdate\.ConstructMessage\s*\(\s*mobCharacter\s*\).*?DB population mob spawned' 'Population mobs should use real DB templates, prepared combat/death stats, and visible client spawn packets.'
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
    $messagingAssembly = [System.Reflection.Assembly]::LoadFrom((Join-Path $builtDir 'SmokeLounge.AOtomation.Messaging.dll'))
    $archetypeType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatTestMobArchetype'
    $rulesType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatCorpseRules'
    $damageRulesType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatDamageRules'
    $npcAiProfileType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.NpcAiProfile'
    $npcAiProfilesType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.NpcAiProfiles'
    $enemyBehaviorContractType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.EnemyBehaviorContract'
    $enemyBehaviorStateType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.EnemyBehaviorState'
    $enemyBehaviorSignalType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.EnemyBehaviorSignal'
    $visualsType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatCorpseVisuals'
    $lootCatalogType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatTestLootCatalog'
    $mobLootCatalogType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.CombatMobLootCatalog'
    $playfieldType = Get-RequiredType $zoneAssembly 'CellAO.Core.Playfields.Playfield'
    $characterActionHandlerType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.MessageHandlers.CharacterActionMessageHandler'
    $zoneClientType = Get-RequiredType $zoneAssembly 'ZoneEngine.Core.ZoneClient'
    $characterType = Get-RequiredType $coreAssembly 'CellAO.Core.Entities.Character'
    $inventoryRulesType = Get-RequiredType $coreAssembly 'CellAO.Core.Inventory.InventoryItemRules'
    $mobTemplateType = Get-RequiredType $databaseAssembly 'CellAO.Database.Dao.DBMobTemplate'
    $mobDropType = Get-RequiredType $databaseAssembly 'CellAO.Database.Entities.DBMobDroptable'
    $startLogoutType = Get-RequiredType $messagingAssembly 'SmokeLounge.AOtomation.Messaging.Messages.N3Messages.StartLogoutMessage'
    $stopLogoutType = Get-RequiredType $messagingAssembly 'SmokeLounge.AOtomation.Messaging.Messages.N3Messages.StopLogoutMessage'
    $identityOnlySerializerType = Get-RequiredType $messagingAssembly 'SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom.IdentityOnlyN3MessageSerializer'

    Get-RequiredMethod $playfieldType 'TryUseCorpse' ([System.Reflection.BindingFlags]'Public, Instance') | Out-Null
    Get-RequiredMethod $playfieldType 'TryUseDeadNpcCorpse' ([System.Reflection.BindingFlags]'Public, Instance') | Out-Null
    Get-RequiredMethod $playfieldType 'TryLootCorpseItem' ([System.Reflection.BindingFlags]'Public, Instance') | Out-Null
    Get-RequiredMethod $playfieldType 'RegisterCorpse' ([System.Reflection.BindingFlags]'NonPublic, Instance') | Out-Null
    Get-RequiredMethod $playfieldType 'SendCorpseFullUpdate' ([System.Reflection.BindingFlags]'NonPublic, Instance') | Out-Null
    Get-RequiredMethod $playfieldType 'SendCorpseInventoryUpdate' ([System.Reflection.BindingFlags]'NonPublic, Instance') | Out-Null
    Get-RequiredMethod $playfieldType 'DespawnCorpse' ([System.Reflection.BindingFlags]'NonPublic, Instance') | Out-Null
    Get-RequiredMethod $characterActionHandlerType 'ApplySit' ([System.Reflection.BindingFlags]'NonPublic, Instance') | Out-Null
    Get-RequiredMethod $characterActionHandlerType 'ApplyStand' ([System.Reflection.BindingFlags]'NonPublic, Instance') | Out-Null
    Get-RequiredMethod $characterActionHandlerType 'SendStartLogout' ([System.Reflection.BindingFlags]'NonPublic, Instance') | Out-Null
    Get-RequiredMethod $characterActionHandlerType 'SendStopLogout' ([System.Reflection.BindingFlags]'NonPublic, Instance') | Out-Null
    Get-RequiredMethod $characterActionHandlerType 'StopLogout' ([System.Reflection.BindingFlags]'NonPublic, Instance') | Out-Null
    Get-RequiredMethod $zoneClientType 'Dispose' ([System.Reflection.BindingFlags]'NonPublic, Instance') | Out-Null
    Get-RequiredMethod $characterType 'EnterLogoutSitPosture' ([System.Reflection.BindingFlags]'Public, Instance') | Out-Null
    Get-RequiredMethod $characterType 'StartLogoutTimer' ([System.Reflection.BindingFlags]'Public, Instance') | Out-Null

    $applyEnemyContract = Get-RequiredMethod $enemyBehaviorContractType 'Apply' ([System.Reflection.BindingFlags]'Public, Static')
    $idleState = [System.Enum]::Parse($enemyBehaviorStateType, 'Idle')
    $aggroedState = [System.Enum]::Parse($enemyBehaviorStateType, 'Aggroed')
    $chasingState = [System.Enum]::Parse($enemyBehaviorStateType, 'Chasing')
    $inRangeState = [System.Enum]::Parse($enemyBehaviorStateType, 'InRangeAttacking')
    $deadState = [System.Enum]::Parse($enemyBehaviorStateType, 'Dead')
    $addThreatSignal = [System.Enum]::Parse($enemyBehaviorSignalType, 'AddThreat')
    $targetFollowSignal = [System.Enum]::Parse($enemyBehaviorSignalType, 'TargetFollow')
    $coordinateFollowSignal = [System.Enum]::Parse($enemyBehaviorSignalType, 'CoordinateFollowTarget')
    $attackInfoSignal = [System.Enum]::Parse($enemyBehaviorSignalType, 'AttackInfo')
    $playerStopFightSignal = [System.Enum]::Parse($enemyBehaviorSignalType, 'StopFightFromPlayer')
    $deathActionSignal = [System.Enum]::Parse($enemyBehaviorSignalType, 'DeathAction')

    $transition = $applyEnemyContract.Invoke($null, @($idleState, $addThreatSignal))
    Assert-True ((Get-PropertyValue $transition 'State').Equals($aggroedState)) 'Enemy contract should move Idle -> Aggroed on threat.'
    $transition = $applyEnemyContract.Invoke($null, @($aggroedState, $targetFollowSignal))
    Assert-True ((Get-PropertyValue $transition 'State').Equals($chasingState)) 'Enemy contract should map target-follow runtime intent to Chasing.'
    $transition = $applyEnemyContract.Invoke($null, @($aggroedState, $coordinateFollowSignal))
    Assert-True ((Get-PropertyValue $transition 'State').Equals($chasingState)) 'Enemy contract should map coordinate FollowTarget to Chasing.'
    $transition = $applyEnemyContract.Invoke($null, @($chasingState, $attackInfoSignal))
    Assert-True ((Get-PropertyValue $transition 'State').Equals($inRangeState)) 'Enemy contract should map combat results to InRangeAttacking.'
    $transition = $applyEnemyContract.Invoke($null, @($inRangeState, $playerStopFightSignal))
    Assert-True ((Get-PropertyValue $transition 'State').Equals($inRangeState)) 'Enemy contract should preserve mob state on player StopFight.'
    $transition = $applyEnemyContract.Invoke($null, @($inRangeState, $deathActionSignal))
    Assert-True ((Get-PropertyValue $transition 'State').Equals($deadState)) 'Enemy contract should map death action to Dead.'
    Get-RequiredMethod $characterType 'StopLogoutTimer' ([System.Reflection.BindingFlags]'Public, Instance') | Out-Null
    Get-RequiredMethod $characterType 'InLogoutTimerPeriod' ([System.Reflection.BindingFlags]'Public, Instance') | Out-Null
    Get-RequiredMethod $identityOnlySerializerType 'Serialize' ([System.Reflection.BindingFlags]'Public, Instance') | Out-Null
    Get-RequiredMethod $identityOnlySerializerType 'Deserialize' ([System.Reflection.BindingFlags]'Public, Instance') | Out-Null
    Assert-True ($startLogoutType.GetConstructor(@()) -ne $null) 'StartLogoutMessage should keep a public default constructor for serialization.'
    Assert-True ($stopLogoutType.GetConstructor(@()) -ne $null) 'StopLogoutMessage should keep a public default constructor for serialization.'

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

    $enemyMovementReplay = Join-Path $repoRoot 'tools-temp\enemy-movement-replay\Test-EnemyMovementReplay.ps1'
    if (Test-Path $enemyMovementReplay) {
        & $enemyMovementReplay
    }

    Write-Host '[PASS] Combat smoke tests passed.'
}
finally {
    Set-Location $previousLocation
}
