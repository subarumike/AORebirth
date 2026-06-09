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

function Get-RequiredSourceSegment {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    $match = [System.Text.RegularExpressions.Regex]::Match($Text, $Pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    Assert-True $match.Success $Message
    return $match.Value
}

function Count-SourceMatches {
    param(
        [string]$Text,
        [string]$Pattern
    )

    return [System.Text.RegularExpressions.Regex]::Matches($Text, $Pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline).Count
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}
else {
    $RepoRoot = (Resolve-Path $RepoRoot).Path
}

$clientMoveItemSource = Get-RequiredSource $RepoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\ClientMoveItemToInventoryMessageHandler.cs'
$containerAddItemSource = Get-RequiredSource $RepoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\ContainerAddItemMessageHandler.cs'
$playfieldSource = Get-RequiredSource $RepoRoot 'CellAO\Server\ZoneEngine\Core\Playfields\Playfield.cs'
$tradeHandlerSource = Get-RequiredSource $RepoRoot 'CellAO\Server\ZoneEngine\Core\MessageHandlers\TradeMessageHandler.cs'

$tryMoveOwnedInventoryItemSource = Get-RequiredSourceSegment `
    $clientMoveItemSource `
    'private\s+bool\s+TryMoveOwnedInventoryItem\s*\(.*?(?=\r?\n\s*private\s+bool\s+TryGetSourcePage)' `
    'TryMoveOwnedInventoryItem method should be present.'
$sendMoveAckSource = Get-RequiredSourceSegment `
    $clientMoveItemSource `
    'private\s+void\s+SendMoveAck\s*\(ICharacter\s+character,\s*Identity\s+sourceContainer,\s*int\s+targetPlacement\).*?(?=\r?\n\s*private\s+bool\s+IsAppearanceEquipmentPage)' `
    'ClientMoveItemToInventory SendMoveAck helper should be present.'
$clientMovePersistSource = Get-RequiredSourceSegment `
    $clientMoveItemSource `
    'private\s+void\s+PersistCharacterInventory\s*\(ICharacter\s+character,\s*string\s+reason\).*?(?=\r?\n\s*private\s+void\s+WaitForEquipVisualSync)' `
    'ClientMoveItemToInventory persistence helper should be present.'
$ensureWeaponVisualMeshesSource = Get-RequiredSourceSegment `
    $clientMoveItemSource `
    'public\s+static\s+void\s+EnsureWeaponVisualMeshes\s*\(ICharacter\s+character,\s*bool\s+announceAppearanceUpdate\).*?(?=\r?\n\s*private\s+static\s+bool\s+EnsureWeaponMesh)' `
    'EnsureWeaponVisualMeshes helper should be present.'
$corpseLootSource = Get-RequiredSourceSegment `
    $playfieldSource `
    'public\s+bool\s+TryLootCorpseItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*Identity\s+target,\s*int\s+targetPlacement\).*?(?=\r?\n\s*private\s+static\s+bool\s+CharacterHasUniqueItemAlready)' `
    'TryLootCorpseItem method should be present.'
$sendCorpseContainerAddItemSource = Get-RequiredSourceSegment `
    $playfieldSource `
    'private\s+void\s+SendCorpseContainerAddItem\s*\(ICharacter\s+looter,\s*Identity\s+sourceContainer,\s*int\s+targetPlacement\).*?(?=\r?\n\s*private\s+bool\s+ProcessDeadNpc)' `
    'SendCorpseContainerAddItem helper should be present.'
$trySetPlayerTradeCreditsSource = Get-RequiredSourceSegment `
    $tradeHandlerSource `
    'private\s+bool\s+TrySetPlayerTradeCredits\s*\(.*?(?=\r?\n\s*private\s+bool\s+TryEndPlayerTrade)' `
    'TrySetPlayerTradeCredits method should be present.'
$completePlayerTradeSource = Get-RequiredSourceSegment `
    $tradeHandlerSource `
    'private\s+bool\s+CompletePlayerTrade\s*\(TemporaryBag\s+shoppingBag\).*?(?=\r?\n\s*private\s+bool\s+TryDeclinePlayerTrade)' `
    'CompletePlayerTrade method should be present.'
$tryDeclinePlayerTradeSource = Get-RequiredSourceSegment `
    $tradeHandlerSource `
    'private\s+bool\s+TryDeclinePlayerTrade\s*\(ICharacter\s+character,\s*TemporaryBag\s+shoppingBag\).*?(?=\r?\n\s*private\s+bool\s+IsPlayerTrade)' `
    'TryDeclinePlayerTrade method should be present.'
$sendPlayerTradeCompleteCloseSource = Get-RequiredSourceSegment `
    $tradeHandlerSource `
    'private\s+void\s+SendPlayerTradeCompleteClose\s*\(ICharacter\s+viewer,\s*ICharacter\s+partner\).*?(?=\r?\n\s*private\s+void\s+SendPlayerTradeDeclineClose)' `
    'SendPlayerTradeCompleteClose method should be present.'
$sendPlayerTradeDeclineCloseSource = Get-RequiredSourceSegment `
    $tradeHandlerSource `
    'private\s+void\s+SendPlayerTradeDeclineClose\s*\(ICharacter\s+first,\s*ICharacter\s+second\).*?(?=\r?\n\s*private\s+void\s+SendPlayerTradeInventoryInvalidation)' `
    'SendPlayerTradeDeclineClose method should be present.'
$playerTradeCreditsSource = Get-RequiredSourceSegment `
    $tradeHandlerSource `
    'private\s+MessageDataFiller\s+PlayerTradeCredits\s*\(Identity\s+offerOwner,\s*int\s+credits\).*?(?=\r?\n\s*private\s+void\s+SendPlayerTradeItemDefinition)' `
    'PlayerTradeCredits filler should be present.'
$transferPlayerTradeOffersSource = Get-RequiredSourceSegment `
    $tradeHandlerSource `
    'private\s+void\s+TransferPlayerTradeOffers\s*\(ICharacter\s+from,\s*ICharacter\s+to,\s*TemporaryBag\s+shoppingBag\).*?(?=\r?\n\s*private\s+bool\s+TransferPlayerTradeCredits)' `
    'TransferPlayerTradeOffers method should be present.'
$transferPlayerTradeCreditsSource = Get-RequiredSourceSegment `
    $tradeHandlerSource `
    'private\s+bool\s+TransferPlayerTradeCredits\s*\(ICharacter\s+shopper,\s*ICharacter\s+vendor,\s*TemporaryBag\s+shoppingBag\).*?(?=\r?\n\s*private\s+int\s+CalculateVendorBuyTotal)' `
    'TransferPlayerTradeCredits method should be present.'
$returnPlayerTradeOffersSource = Get-RequiredSourceSegment `
    $tradeHandlerSource `
    'private\s+void\s+ReturnPlayerTradeOffers\s*\(ICharacter\s+owner,\s*TemporaryBag\s+shoppingBag\).*?(?=\r?\n\s*private\s+void\s+ReturnAllPlayerTradeOffers)' `
    'ReturnPlayerTradeOffers method should be present.'
$returnAllPlayerTradeOffersSource = Get-RequiredSourceSegment `
    $tradeHandlerSource `
    'private\s+void\s+ReturnAllPlayerTradeOffers\s*\(TemporaryBag\s+shoppingBag,\s*string\s+reason\).*?(?=\r?\n\s*private\s+void\s+SendTradeWindowMoveToInventory)' `
    'ReturnAllPlayerTradeOffers method should be present.'
$vendorAcceptSource = Get-RequiredSourceSegment `
    $tradeHandlerSource `
    'case\s+TradeAction\.Accept:\s*\r?\n\s*case\s+TradeAction\.Confirm:.*?(?=\r?\n\s*case\s+TradeAction\.UpdateCredits:)' `
    'Vendor accept/confirm trade block should be present.'
$vendorAddItemSource = Get-RequiredSourceSegment `
    $tradeHandlerSource `
    'case\s+TradeAction\.AddItem:.*?(?=\r?\n\s*case\s+TradeAction\.RemoveItem:)' `
    'Trade AddItem block should be present.'
$vendorDeclineSource = Get-RequiredSourceSegment `
    $tradeHandlerSource `
    'case\s+TradeAction\.Decline:.*?else\s*\r?\n\s*\{.*?SendVendorShopDeclineClose\s*\(client\.Controller\.Character\).*?\}\s*\r?\n\s*break\s*;\s*\r?\n\s*\}' `
    'Trade Decline block should be present.'
$sendVendorShopDeclineCloseSource = Get-RequiredSourceSegment `
    $tradeHandlerSource `
    'private\s+void\s+SendVendorShopDeclineClose\s*\(ICharacter\s+character\).*?(?=\r?\n\s*private\s+MessageDataFiller\s+EndTrade)' `
    'SendVendorShopDeclineClose method should be present.'
$tradePersistSource = Get-RequiredSourceSegment `
    $tradeHandlerSource `
    'private\s+void\s+PersistCharacterInventory\s*\(ICharacter\s+character,\s*string\s+reason\).*?(?=\r?\n\s*private\s+ICharacter\s+GetOtherPlayerTradeCharacter)' `
    'Trade persistence helper should be present.'

Assert-SourceOrdered $clientMoveItemSource @(
    'Read\s*\(ClientMoveItemToInventoryMessage\s+message,\s*IZoneClient\s+client\)',
    'TryLootCorpseItem\s*\(',
    'return\s*;',
    'TryMoveOwnedInventoryItem\s*\('
) 'ClientMoveItemToInventory should route corpse loot moves before normal inventory moves.'
Assert-SourceOrdered $tryMoveOwnedInventoryItemSource @(
    'int\s+ackTargetPlacement\s*=\s*message\.TargetPlacement',
    'if\s*\(\s*toPlacement\s*==\s*\(int\)IdentityType\.TradeWindow\s*\)',
    'toPlacement\s*=\s*receivingPage\.FindFreeSlot\s*\(',
    'sendingPage\.Remove\s*\(\s*fromPlacement\s*\)',
    'receivingPage\.Add\s*\(\s*toPlacement,\s*itemFrom\s*\)',
    'this\.SendMoveAck\s*\(\s*character,\s*message\.SourceContainer,\s*ackTargetPlacement\s*\)',
    'this\.PersistCharacterInventory\s*\(\s*character,\s*"move"\s*\)'
) 'Normal inventory moves should preserve raw ack placement, resolve 0x6F server-side, move once, ack, then persist.'
Assert-SourceMatch $sendMoveAckSource 'new\s+ContainerAddItemMessage.*?Identity\s*=\s*character\.Identity.*?SourceContainer\s*=\s*sourceContainer.*?Target\s*=\s*character\.Identity.*?TargetPlacement\s*=\s*targetPlacement.*?Unknown\s*=\s*0' 'Normal inventory move acknowledgements should use source container, target character, target placement, and Unknown=0.'
Assert-SourceMatch $clientMovePersistSource 'character\.BaseInventory\.Write\s*\(\s*\)' 'ClientMoveItemToInventory should persist successful owned inventory moves.'

Assert-SourceOrdered $tryMoveOwnedInventoryItemSource @(
    'WeaponItemFullUpdate\.SendWeaponDefinition\s*\(\s*character,\s*itemFrom\s*\)',
    'equipTo\.Equip\s*\(\s*sendingPage,\s*fromPlacement,\s*toPlacement\s*\)',
    'this\.SendMoveAck\s*\(\s*character,\s*message\.SourceContainer,\s*ackTargetPlacement\s*\)',
    'Equip\.Send\s*\(\s*client,\s*receivingPage,\s*toPlacement\s*\)',
    'character\.CalculateSkills\s*\(\s*\)',
    'EnsureWeaponVisualMeshes\s*\(\s*character,\s*true\s*\)',
    'this\.PersistCharacterInventory\s*\(\s*character,\s*"equip"\s*\)'
) 'Equipment moves should send the move ack, equipment ack, stat recalculation, visual repair, and persistence in order.'
Assert-SourceOrdered $tryMoveOwnedInventoryItemSource @(
    'UnEquip\.Send\s*\(\s*client,\s*sendingPage,\s*fromPlacement\s*\)',
    'unequipFrom\.Unequip\s*\(\s*fromPlacement,\s*receivingPage,\s*toPlacement\s*\)',
    'this\.SendMoveAck\s*\(\s*character,\s*message\.SourceContainer,\s*ackTargetPlacement\s*\)',
    'character\.CalculateSkills\s*\(\s*\)',
    'EnsureWeaponVisualMeshes\s*\(\s*character,\s*true\s*\)',
    'this\.PersistCharacterInventory\s*\(\s*character,\s*"unequip"\s*\)'
) 'Unequipment moves should send unequip, move ack, stat recalculation, visual repair, and persistence in order.'
Assert-SourceMatch $ensureWeaponVisualMeshesSource 'WeaponSlots\.Righthand.*?WeaponSlots\.LeftHand' 'Equipment visual repair should continue covering both weapon hands.'
Assert-SourceMatch $ensureWeaponVisualMeshesSource 'character\.ChangedAppearance\s*=\s*true.*?if\s*\(\s*announceAppearanceUpdate\s*\).*?AnnounceAppearanceUpdate\s*\(\s*character\s*\)' 'Equipment visual repair should mark appearance changed and only announce when requested.'

Assert-SourceOrdered $containerAddItemSource @(
    'Read\s*\(ContainerAddItemMessage\s+message,\s*IZoneClient\s+client\)',
    'TryLootCorpseItem\s*\(',
    'return\s*;',
    'Pool\.Instance\.GetObject<IInventoryPage>'
) 'ContainerAddItem should route corpse loot moves before normal container moves.'
Assert-SourceOrdered $corpseLootSource @(
    'sourceContainer\.Type\s*!=\s*IdentityType\.Backpack',
    'corpseInventoryHandle\s*=\s*\(sourceContainer\.Instance\s*>>\s*16\)\s*&\s*0xffff',
    'requestedLootSlot\s*=\s*sourceContainer\.Instance\s*&\s*0xffff',
    'FindCorpseLootItem\s*\(\s*corpse,\s*requestedLootSlot\s*\)',
    'CharacterHasUniqueItemAlready\s*\(\s*looter,\s*lootItem\.Item\s*\)',
    'TryResolveLootTargetSlot\s*\(',
    'looter\.BaseInventory\.AddToPage\s*\(',
    'looter\.BaseInventory\.Write\s*\(\s*\)',
    'lootItem\.Looted\s*=\s*true',
    'this\.SendCorpseContainerAddItem\s*\(\s*looter,\s*sourceContainer,\s*targetPlacement\s*\)'
) 'Corpse item loot should decode the corpse backpack source, add the item once, persist before marking looted, then send the item ack.'
Assert-True ((Count-SourceMatches $corpseLootSource 'BaseInventory\.AddToPage\s*\(') -eq 1) 'Corpse item loot should have one successful inventory add call in the source path.'
Assert-SourceNoMatch $corpseLootSource 'Stats\s*\[\s*StatIds\.cash\s*\]\.Set\s*\(|StatMessageHandler\.Default\.Send(?:Changed|Single)\s*\(|AwardCorpseCredits\s*\(' 'Corpse item loot should not mutate cash, emit cash stat packets, or call the corpse credit award path.'
Assert-SourceMatch $sendCorpseContainerAddItemSource 'new\s+ContainerAddItemMessage.*?Identity\s*=\s*looter\.Identity.*?SourceContainer\s*=\s*sourceContainer.*?TargetPlacement\s*=\s*targetPlacement.*?Target\s*=\s*looter\.Identity.*?Unknown\s*=\s*0' 'Corpse item ack should preserve source container, raw target placement, target looter, and Unknown=0.'

Assert-SourceMatch $trySetPlayerTradeCreditsSource 'int\s+credits\s*=\s*Math\.Max\s*\(\s*0,\s*message\.Param2\s*\)' 'Player trade UpdateCredits should read the visible amount from Param2.'
Assert-SourceOrdered $trySetPlayerTradeCreditsSource @(
    'shoppingBag\.SetPlayerTradeCredits\s*\(\s*character\.Identity,\s*credits\s*\)',
    'TRADE_CREDIT_SET',
    'this\.SendPlayerTradeCredits\s*\(\s*character,\s*character\.Identity,\s*credits\s*\)',
    'this\.SendPlayerTradeCredits\s*\(\s*otherCharacter,\s*character\.Identity,\s*credits\s*\)'
) 'Player trade credit updates should store the amount, keep trace logging, and echo to both participants.'
Assert-SourceMatch $playerTradeCreditsSource 'x\.Action\s*=\s*TradeAction\.UpdateCredits.*?x\.Param1\s*=\s*0;.*?x\.Param2\s*=\s*credits;.*?x\.Param3\s*=\s*0;.*?x\.Param4\s*=\s*0;' 'Outbound player trade credit updates should carry credits in Param2 only.'
Assert-SourceOrdered $completePlayerTradeSource @(
    'TransferPlayerTradeCredits\s*\(\s*shopper,\s*vendor,\s*shoppingBag\s*\)',
    'TransferPlayerTradeOffers\s*\(\s*shopper,\s*vendor,\s*shoppingBag\s*\)',
    'TransferPlayerTradeOffers\s*\(\s*vendor,\s*shopper,\s*shoppingBag\s*\)',
    'SendPlayerTradeCompleteClose\s*\(\s*shopper,\s*vendor\s*\)',
    'SendPlayerTradeCompleteClose\s*\(\s*vendor,\s*shopper\s*\)',
    'SendPlayerTradeSocialStatus\s*\(\s*shopper\s*\)',
    'SendPlayerTradeSocialStatus\s*\(\s*vendor\s*\)',
    'PersistCharacterInventory\s*\(\s*shopper,\s*"player trade complete"\s*\)',
    'PersistCharacterInventory\s*\(\s*vendor,\s*"player trade complete"\s*\)',
    'shoppingBag\.Dispose\s*\(\s*\)'
) 'Player trade completion should commit credits once, transfer offers both directions, close both panes, clear social status, persist both inventories, and dispose the bag.'
Assert-True ((Count-SourceMatches $completePlayerTradeSource 'TransferPlayerTradeCredits\s*\(') -eq 1) 'Player trade completion should call credit transfer exactly once.'
Assert-True ((Count-SourceMatches $completePlayerTradeSource 'TransferPlayerTradeOffers\s*\(') -eq 2) 'Player trade completion should call item transfer exactly once per direction.'
Assert-SourceOrdered $transferPlayerTradeOffersSource @(
    'offerPage\.Remove\s*\(\s*offer\.Key\s*\)',
    'to\.BaseInventory\.AddToPage\s*\(\s*to\.BaseInventory\.StandardPage,\s*targetSlot,\s*offer\.Value\s*\)',
    'TRADE_ITEM_COMMIT'
) 'Player trade item commit should remove from the offer page, add to the recipient inventory, and keep item commit trace logging.'
Assert-SourceNoMatch $transferPlayerTradeOffersSource 'SetCash\s*\(|StatMessageHandler\.Default\.Send|SendChangedStats\s*\(' 'Player trade item transfer should not mutate or emit cash.'
Assert-SourceOrdered $transferPlayerTradeCreditsSource @(
    'int\s+shopperCredits\s*=\s*shoppingBag\.GetPlayerTradeCredits\s*\(\s*shopper\.Identity\s*\)',
    'int\s+vendorCredits\s*=\s*shoppingBag\.GetPlayerTradeCredits\s*\(\s*vendor\.Identity\s*\)',
    'int\s+shopperCash\s*=\s*GetCash\s*\(\s*shopper\s*\)',
    'int\s+vendorCash\s*=\s*GetCash\s*\(\s*vendor\s*\)',
    'SetCash\s*\(\s*shopper,\s*shopperFinalCash\s*\)',
    'SetCash\s*\(\s*vendor,\s*vendorFinalCash\s*\)',
    'shopper\.Stats\.Write\s*\(\s*\)',
    'vendor\.Stats\.Write\s*\(\s*\)',
    'TRADE_CREDIT_COMMIT'
) 'Player trade credit transfer should use stored offers, authoritative cash, one SetCash per side, stat persistence, and trace logging.'
Assert-True ((Count-SourceMatches $transferPlayerTradeCreditsSource 'SetCash\s*\(') -eq 2) 'Player trade credit transfer should set cash exactly once per participant.'
Assert-SourceOrdered $tryDeclinePlayerTradeSource @(
    'ReturnPlayerTradeOffers\s*\(\s*shopper,\s*shoppingBag\s*\)',
    'PersistCharacterInventory\s*\(\s*shopper,\s*"player trade decline"\s*\)',
    'ReturnPlayerTradeOffers\s*\(\s*vendor,\s*shoppingBag\s*\)',
    'PersistCharacterInventory\s*\(\s*vendor,\s*"player trade decline"\s*\)',
    'SendPlayerTradeDeclineClose\s*\(\s*character,\s*otherCharacter\s*\)',
    'shoppingBag\.Dispose\s*\(\s*\)'
) 'Player trade decline should return offers, persist both sides, send close/invalidation, and dispose the bag.'
Assert-SourceOrdered $returnPlayerTradeOffersSource @(
    'offerPage\.Remove\s*\(\s*offer\.Key\s*\)',
    'owner\.BaseInventory\s*\[\s*owner\.BaseInventory\.StandardPage\s*\]\.Add\s*\(\s*targetSlot,\s*offer\.Value\s*\)',
    'TRADE_DECLINE_RETURN',
    'SendTradeWindowMoveToInventory\s*\(\s*owner,\s*IdentityType\.KnuBotTradeWindow,\s*offer\.Key,\s*targetSlot\s*\)'
) 'Player trade decline return should move offered items back to inventory and notify the client.'
Assert-SourceOrdered $returnAllPlayerTradeOffersSource @(
    'ReturnPlayerTradeOffers\s*\(\s*shopper,\s*shoppingBag\s*\)',
    'PersistCharacterInventory\s*\(\s*shopper,\s*reason\s*\)',
    'ReturnPlayerTradeOffers\s*\(\s*vendor,\s*shoppingBag\s*\)',
    'PersistCharacterInventory\s*\(\s*vendor,\s*reason\s*\)'
) 'Player trade failure return should persist returned offers for both participants.'
Assert-SourceMatch $sendPlayerTradeCompleteCloseSource 'TRADE_COMPLETE_SEND.*?PlayerTradeClose\s*\(\s*viewer\.Identity,\s*TradeAction\.Complete,\s*partner\.Identity,\s*partner\.Identity\s*\).*?TRADE_COMPLETE_SEND.*?PlayerTradeClose\s*\(\s*partner\.Identity,\s*TradeAction\.Complete,\s*viewer\.Identity,\s*viewer\.Identity\s*\)' 'Player trade complete close should keep paired complete frames for both panes.'
Assert-SourceMatch $sendPlayerTradeDeclineCloseSource 'TradeAction\.Decline.*?SendPlayerTradeInventoryInvalidation\s*\(\s*first,\s*second\s*\).*?SendPlayerTradeInventoryInvalidation\s*\(\s*second,\s*first\s*\).*?SendPlayerTradeSocialStatus\s*\(\s*first\s*\).*?SendPlayerTradeSocialStatus\s*\(\s*second\s*\)' 'Player trade decline close should invalidate both inventories and clear social status.'
Assert-SourceMatch $tradePersistSource 'character\.BaseInventory\.Write\s*\(\s*\)' 'Player trade item paths should retain the shared inventory persistence helper.'

Assert-SourceOrdered $vendorAcceptSource @(
    'boughtItems\s*=\s*items',
    'soldItems\s*=\s*shoppingBag\.GetSoldItems\s*\(\s*\)',
    'buyTotal\s*=',
    'sellTotal\s*=',
    'int\s+cash\s*=\s*buyTotal\s*-\s*sellTotal',
    'int\s+currentCash\s*=\s*GetCash\s*\(\s*client\.Controller\.Character\s*\)',
    'HasFreeInventorySlots\s*\(\s*client\.Controller\.Character,\s*boughtItems\.Length\s*\)',
    'foreach\s*\(\s*IItem\s+item\s+in\s+boughtItems\s*\)',
    'issuer\.BaseInventory\.AddToPage\s*\(',
    'AddTemplateMessageHandler\.Default\.Send\s*\(\s*client\.Controller\.Character,\s*\(Item\)item\s*\)',
    'SetCash\s*\(',
    'TradeAction\.Complete',
    'client\.Controller\.SendChangedStats\s*\(\s*\)',
    'shoppingBag\.Dispose\s*\(\s*\)'
) 'Vendor accept should calculate buy/sell totals, add bought items, mutate cash, send changed stats, complete, and dispose the temp bag.'
Assert-SourceMatch $vendorAddItemSource 'shoppingBag\.Add\s*\(\s*message\.Target,\s*issuer\.BaseInventory\.RemoveItem\s*\(\s*\(int\)message\.Container\.Type,\s*message\.Container\.Instance\s*\)\s*\)' 'Vendor sell staging should remove the sold item from player inventory into the temporary bag.'
Assert-SourceOrdered $vendorAcceptSource @(
    'soldItems\s*=\s*shoppingBag\.GetSoldItems\s*\(\s*\)',
    'CalculateVendorSellTotal\s*\(',
    'int\s+cash\s*=\s*buyTotal\s*-\s*sellTotal',
    'SetCash\s*\(',
    'client\.Controller\.SendChangedStats\s*\(\s*\)',
    'shoppingBag\.Dispose\s*\(\s*\)'
) 'Vendor sell accept should include sold items in the cash delta and close the temporary bag after cash mutation.'
Assert-SourceOrdered $vendorDeclineSource @(
    'SendVendorShopDeclineClose\s*\(\s*client\.Controller\.Character\s*\)',
    'IItem\[\]\s+items\s*=\s*shoppingBag\.GetSoldItems\s*\(\s*\)',
    'foreach\s*\(\s*IItem\s+item\s+in\s+items\s*\)',
    'FindFreeSlot\s*\(\s*\)',
    'issuer\.BaseInventory\s*\[\s*issuer\.BaseInventory\.StandardPage\s*\]\.Add\s*\(\s*nextSlot,\s*item\s*\)',
    'finally',
    'shoppingBag\.Dispose\s*\(\s*\)'
) 'Vendor decline/close should return staged sold items safely before disposing the temporary bag.'
Assert-SourceMatch $sendVendorShopDeclineCloseSource 'TradeAction\.Decline.*?Identity\.None.*?StatMessageHandler\.Default\.SendSingle\s*\(\s*character,\s*\(int\)StatIds\.socialstatus,\s*4\s*\).*?ArmPostZoneCollisionGrace\s*\(\s*character\s*\)' 'Vendor decline/close should send the close action and restore post-zone interaction state.'

<#
Live/client-only checklist, not proven by source assertions:
- Normal inventory move visibly lands in the expected target slot and does not create a stale ghost item.
- Equip/unequip visibly updates the character model and item panes before and after relog.
- Corpse item loot removes the item from the loot window and adds exactly one inventory item.
- Corpse credit client text and visible cash remain covered by Run-CorpseCreditTraceAssertions.ps1 plus playtest.
- Player trade complete and decline panes clear for both clients.
- Vendor buy/sell/close panes clear and visible cash/inventory match the transaction.

Read-only DB checklist, not performed by this script:
- After relog, moved, equipped, looted, traded, and vendor-bought items have one expected inventory row.
- Vendor-sold items are absent from the seller inventory after accept and present again after close/decline.
- Cash persists after corpse credit, player trade credit, and vendor buy/sell transactions.
#>

Write-Host 'Inventory/container regression source assertions passed.'
