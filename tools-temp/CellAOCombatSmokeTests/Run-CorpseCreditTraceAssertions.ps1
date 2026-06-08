param(
    [string]$RepoRoot
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

function Get-RequiredSource {
    param(
        [string]$Root,
        [string]$RelativePath
    )

    $path = Join-Path $Root $RelativePath
    Assert-True (Test-Path -LiteralPath $path) "Missing source file: $RelativePath"
    return Get-Content -LiteralPath $path -Raw
}

function Get-HexTemplateBytes {
    param(
        [string]$Text
    )

    $match = [System.Text.RegularExpressions.Regex]::Match(
        $Text,
        'HexToBytes\(\s*"([0-9a-f]+)"',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    Assert-True $match.Success 'CorpseFullUpdate template hex should be present for offset assertions.'

    $hex = $match.Groups[1].Value
    $bytes = New-Object byte[] ($hex.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $bytes[$i] = [Convert]::ToByte($hex.Substring($i * 2, 2), 16)
    }

    return $bytes
}

function Read-Int32BigEndian {
    param(
        [byte[]]$Bytes,
        [int]$Offset
    )

    Assert-True ($Offset -ge 0 -and $Offset + 3 -lt $Bytes.Length) "Offset $Offset is outside template bounds."
    return (($Bytes[$Offset] -shl 24) -bor ($Bytes[$Offset + 1] -shl 16) -bor ($Bytes[$Offset + 2] -shl 8) -bor $Bytes[$Offset + 3])
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}
else {
    $RepoRoot = (Resolve-Path $RepoRoot).Path
}

$playfieldSource = Get-RequiredSource $RepoRoot 'CellAO\Server\ZoneEngine\Core\Playfields\Playfield.cs'
$combatCorpseRulesSource = Get-RequiredSource $RepoRoot 'CellAO\Server\ZoneEngine\Core\CombatCorpseRules.cs'
$corpseFullUpdateSource = Get-RequiredSource $RepoRoot 'CellAO\Server\ZoneEngine\Core\Packets\CorpseFullUpdate.cs'
$genericCmdSource = Get-RequiredSource $RepoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\GenericCmdMessageHandler.cs'
$clientMoveItemSource = Get-RequiredSource $RepoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\ClientMoveItemToInventoryMessageHandler.cs'
$containerAddItemSource = Get-RequiredSource $RepoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\ContainerAddItemMessageHandler.cs'
$statHandlerSource = Get-RequiredSource $RepoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\StatMessageHandler.cs'
$chatTextHandlerSource = Get-RequiredSource $RepoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\ChatTextMessageHandler.cs'
$statsSource = Get-RequiredSource $RepoRoot 'CellAO\Libraries\Source\CellAO.Stats\Stats.cs'
$statSource = Get-RequiredSource $RepoRoot 'CellAO\Libraries\Source\CellAO.Stats\Stat.cs'
$corpseFullUpdateTemplate = Get-HexTemplateBytes $corpseFullUpdateSource

Assert-SourceMatch $combatCorpseRulesSource 'ObservedCorpseCreditRule\("Beach Leet",\s*17655,\s*1,\s*1\).*?ObservedCorpseCreditRule\("Reef Salamander",\s*30354,\s*23,\s*29\)' 'Observed corpse credit rules should keep known low credit ranges; these rules cannot produce the old 111-credit symptom.'
Assert-SourceNoMatch $combatCorpseRulesSource 'ObservedCorpseCreditRule\([^)]*,\s*111\s*(?:,|\))|ObservedCorpseCreditRule\([^)]*,\s*\d+\s*,\s*111\s*(?:,|\))' 'Observed corpse credit rules should not encode 111 as a min or max credit value.'
Assert-SourceMatch $combatCorpseRulesSource 'RollObservedCredits\s*\(string\s+targetName,\s*int\s+monsterData,\s*Func<int,\s*int>\s+nextRandom\).*?rule\s*==\s*null.*?return\s+0;.*?return\s+rule\.MinCredits\s*\+\s*nextRandom\s*\(\s*rule\.MaxCredits\s*-\s*rule\.MinCredits\s*\+\s*1\s*\)' 'Corpse credit rolls should either return zero for unknown mobs or remain inside the observed min/max range.'

Assert-SourceMatch $playfieldSource 'RegisterCorpse\s*\(ICharacter\s+target,\s*Identity\s+corpseIdentity\).*?int\s+credits\s*=\s*RollCorpseCredits\s*\(\s*target\s*\).*?Credits\s*=\s*credits.*?corpses\s*\[\s*corpseIdentity\.Instance\s*\]\s*=\s*state' 'Corpse registration should store exactly the rolled credit value in CorpseState before the corpse is visible.'
Assert-SourceMatch $playfieldSource 'Corpse registered corpse=\{0\} deadNpc=\{1\} lifetimeSeconds=\{2\} lootClass=\{3\} credits=\{4\}' 'Corpse registration log should include the registered credit value for playtest trace comparison.'
Assert-SourceMatch $playfieldSource 'SendCorpseFullUpdate\s*\(ICharacter\s+target,\s*Identity\s+corpseIdentity\).*?CorpseFullUpdate\.Build\s*\(.*?this\.CorpseCreditsFor\s*\(\s*corpseIdentity\s*\)\s*\)' 'CorpseFullUpdate should receive the registered corpse credit value from CorpseState, not a template default.'
Assert-True ((Read-Int32BigEndian $corpseFullUpdateTemplate 203) -eq 61) 'CorpseFullUpdate template offset 203 should be the Cash stat id 61.'
Assert-True ((Read-Int32BigEndian $corpseFullUpdateTemplate 207) -eq 0) 'CorpseFullUpdate template offset 207 should be a zero cash value placeholder before runtime patching.'
Assert-True ((Read-Int32BigEndian $corpseFullUpdateTemplate 207) -ne 111) 'CorpseFullUpdate template offset 207 must not preserve the old hardcoded 111 cash value.'
Assert-True ((Read-Int32BigEndian $corpseFullUpdateTemplate 211) -ne 61) 'CorpseFullUpdate template offset 211 should not be treated as a Cash stat id.'
Assert-SourceMatch $corpseFullUpdateSource 'private\s+const\s+int\s+CorpseCashValueOffset\s*=\s*207\s*;.*?WriteInt32\s*\(\s*buffer,\s*CorpseCashValueOffset,\s*Math\.Max\s*\(\s*0,\s*corpseCredits\s*\)\s*\)' 'CorpseFullUpdate should write the supplied corpse credit value at offset 207, the value word after Cash stat id 61.'
Assert-SourceNoMatch $corpseFullUpdateSource 'CorpseCashValueOffset\s*=\s*211|WriteInt32\s*\(\s*buffer,\s*211\s*,\s*Math\.Max\s*\(\s*0,\s*corpseCredits\s*\)\s*\)' 'CorpseFullUpdate should not treat offset 211 as the corpse cash value.'
Assert-SourceNoMatch $corpseFullUpdateSource '0000003d0000006f' 'CorpseFullUpdate template must not preserve the old hardcoded Cash=111 value.'

Assert-SourceMatch $genericCmdSource 'case\s+GenericCmdAction\.Use:.*?target\.Type\s*==\s*IdentityType\.Corpse.*?TryUseCorpse\s*\(.*?target\s*\).*?AcknowledgeCorpseUseDelayed' 'GenericCmd Use against a corpse should route through TryUseCorpse and delayed acknowledgement.'
Assert-SourceMatch $genericCmdSource 'TryRouteDeadNpcCorpseUse\s*\(.*?TryUseDeadNpcCorpse\s*\(.*?out\s+routedCorpseIdentity\s*\)' 'GenericCmd Use against a dead NPC should route to the registered corpse identity.'
Assert-SourceMatch $playfieldSource 'TryUseCorpse\s*\(ICharacter\s+looter,\s*Identity\s+corpseIdentity\).*?SendCorpseInventoryUpdateAndCredits\s*\(\s*looter,\s*corpse\s*\)' 'Corpse open should reach the combined InventoryUpdate plus delayed-credit path.'
Assert-SourceOrdered $playfieldSource @(
    'SendCorpseInventoryUpdateAndCredits\s*\(ICharacter\s+looter,\s*CorpseState\s+corpse\)',
    'SendCorpseInventoryUpdate\s*\(\s*looter,\s*corpse\s*\)',
    'ScheduleCorpseCreditAward\s*\(\s*looter,\s*corpse\s*\)'
) 'Corpse open must send InventoryUpdate before scheduling the delayed credit award.'
Assert-SourceNoMatch $playfieldSource 'SendCorpseInventoryUpdateAndCredits\s*\(ICharacter\s+looter,\s*CorpseState\s+corpse\)(?:(?!private\s+void\s+ScheduleCorpseCreditAward).)*?AwardCorpseCredits\s*\(\s*looter,\s*corpse\s*\)' 'Corpse open should not award credits in the same tick as InventoryUpdate.'
Assert-SourceMatch $playfieldSource 'CorpseCreditAwardDelay\s*=\s*TimeSpan\.FromMilliseconds\s*\(\s*500\s*\)' 'Corpse credits should remain on the narrow delayed-award path after corpse InventoryUpdate.'

Assert-SourceOrdered $playfieldSource @(
    'ProcessPendingCorpseCreditAwards\s*\(\s*\)',
    'pendingCorpseCreditAwards\.Values\.Where',
    'pendingCorpseCreditAwards\.Remove\s*\(\s*award\.CorpseInstance\s*\)',
    'corpses\.TryGetValue\s*\(\s*award\.CorpseInstance',
    'FindByIdentity<ICharacter>\s*\(\s*award\.LooterIdentity\s*\)',
    'AwardCorpseCredits\s*\(\s*looter,\s*corpse\s*\)'
) 'Delayed corpse credit processing should remove the pending award, resolve corpse and looter, then award credits.'
Assert-SourceMatch $playfieldSource 'AwardCorpseCredits\s*\(ICharacter\s+looter,\s*CorpseState\s+corpse\).*?uint\s+cashBeforeBase\s*=\s*looter\.Stats\s*\[\s*StatIds\.cash\s*\]\.BaseValue;.*?int\s+cashBefore\s*=\s*CashStatRules\.Clamp\s*\(\s*cashBeforeBase\s*\);.*?int\s+cashAfter\s*=\s*CashStatRules\.Clamp\s*\(\s*\(long\)cashBefore\s*\+\s*corpse\.Credits\s*\);.*?looter\.Stats\s*\[\s*StatIds\.cash\s*\]\.Set\s*\(\s*\(uint\)cashAfter\s*\).*?StatMessageHandler\.Default\.SendChanged\s*\(\s*looter\s*\)' 'Corpse credit award should add from authoritative cash BaseValue, mutate cash once, and send the normal changed-stat packet.'
Assert-SourceMatch $playfieldSource 'AwardCorpseCredits\s*\(ICharacter\s+looter,\s*CorpseState\s+corpse\).*?StatMessageHandler\.Default\.SendChanged\s*\(\s*looter\s*\).*?SendCorpseCreditFeedback\s*\(.*?"You received \{0\} \{1\} from the corpse\.",\s*corpse\.Credits' 'Corpse credit chat text should use the same corpse.Credits value after the cash Stat send.'
Assert-SourceMatch $playfieldSource 'Corpse credits awarded corpse=\{0\} looter=\{1\} credits=\{2\} cashBeforeBase=\{3\} cashAfter=\{4\} inventoryHandle=\{5\}' 'Corpse credit award log should include credit value, prior cash, final cash, and inventory handle for trace comparison.'
Assert-SourceMatch $playfieldSource 'SendCorpseCreditFeedback\s*\(ICharacter\s+character,\s*string\s+text\).*?ChatTextMessageHandler\.Default\.Send\s*\(\s*character,\s*text\s*\)' 'Corpse credit feedback should stay on the visible ChatText path.'

Assert-SourceMatch $statsSource 'this\.cash\s*=\s*new\s+Stat\s*\(\s*this,\s*61,\s*0,\s*true,\s*false,\s*false\s*\)' 'Cash stat should send BaseValue through changed-stat packets.'
Assert-SourceMatch $statSource 'public\s+void\s+Set\s*\(uint\s+value,\s*bool\s+starting\s*=\s*false\).*?if\s*\(\s*value\s*!=\s*this\.BaseValue\s*\).*?this\.Changed\s*=\s*true;.*?this\.BaseValue\s*=\s*max' 'Stat.Set should mark changed and update BaseValue when corpse credits mutate cash.'
Assert-SourceMatch $statsSource 'GetChangedStats\s*\(Dictionary<int,\s*uint>\s+toPlayer,\s*Dictionary<int,\s*uint>\s+toPlayfield\).*?stat\.SendBaseValue\s*\?\s*stat\.BaseValue\s*:\s*\(uint\)stat\.Value.*?stat\.Changed\s*=\s*false' 'Changed cash stats should serialize BaseValue and clear the changed flag through the normal pipeline.'
Assert-SourceMatch $statHandlerSource 'if\s*\(\s*statsToClient\.TryGetValue\s*\(\s*\(int\)StatIds\.cash,\s*out\s+clientCash\s*\)\s*\).*?clientCash\s*=\s*CashStatRules\.Normalize\s*\(\s*character\s*\).*?statsToClient\s*\[\s*\(int\)StatIds\.cash\s*\]\s*=\s*clientCash' 'Bulk cash stat sends should normalize to authoritative cash before serializing.'
Assert-SourceMatch $chatTextHandlerSource 'public\s+void\s+Send\s*\(ICharacter\s+character,\s*string\s+text.*?this\.Send\s*\(\s*character,\s*Filler\s*\(\s*character,\s*text.*?x\.Text\s*=\s*text' 'ChatText handler should send the exact corpse credit text string it is given.'

Assert-SourceOrdered $clientMoveItemSource @(
    'Read\s*\(ClientMoveItemToInventoryMessage\s+message,\s*IZoneClient\s+client\)',
    'TryLootCorpseItem\s*\(',
    'return\s*;',
    'TryMoveOwnedInventoryItem\s*\('
) 'ClientMoveItemToInventory should offer loot-window moves to corpse handling before normal inventory moves.'
Assert-SourceOrdered $containerAddItemSource @(
    'Read\s*\(ContainerAddItemMessage\s+message,\s*IZoneClient\s+client\)',
    'TryLootCorpseItem\s*\(',
    'return\s*;',
    'Pool\.Instance\.GetObject<IInventoryPage>'
) 'ContainerAddItem should offer loot-window moves to corpse handling before normal inventory moves.'
Assert-SourceNoMatch $playfieldSource 'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\)(?:(?!private\s+static\s+bool\s+CharacterHasUniqueItemAlready).)*?Stats\s*\[\s*StatIds\.cash\s*\]\.Set\s*\(' 'Corpse item loot should not mutate player cash.'
Assert-SourceNoMatch $playfieldSource 'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\)(?:(?!private\s+static\s+bool\s+CharacterHasUniqueItemAlready).)*?StatMessageHandler\.Default\.Send(?:Changed|Single)\s*\(' 'Corpse item loot should not emit cash stat packets.'
Assert-SourceNoMatch $playfieldSource 'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\)(?:(?!private\s+static\s+bool\s+CharacterHasUniqueItemAlready).)*?AwardCorpseCredits\s*\(' 'Corpse item loot should not call the delayed corpse credit award path.'
Assert-SourceMatch $playfieldSource 'TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\).*?looter\.BaseInventory\.Write\s*\(\s*\)\s*;\s*lootItem\.Looted\s*=\s*true;.*?SendCorpseContainerAddItem\s*\(\s*looter,\s*sourceContainer,\s*targetPlacement\s*\)' 'Corpse item loot should persist inventory, mark the item looted, and only emit the item movement ack.'

Write-Host 'Corpse credit trace source assertions passed.'
