# Submitted AOSharp Capture Inventory: Omni-Tek Virtual Training Ground

## Source

- Submitted folder: `For Repo/Omni-Tek Virtual Training Ground - 138.0, 100.9, 13.5 (138.0 100.9 y 13.5 1108028)`
- Original capture folder path in metadata: `C:\Users\kegzi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260622-182442`
- Character: `Hiredhitman`
- Playfield: `Playfield2:10E83C` / `1108028`
- Capture window: `2026-06-22T16:24:42.8184746Z` to `2026-06-22T16:54:07.1170882Z`
- Duration: `1764.299` seconds
- Validation: `complete`, `processingAllowed=true`, no issues

## Files Inspected

- `capture_info.json`
- `capture-health.json`
- `capture-session.json`
- `events.log`
- `packets.hex.log`
- `npc-interactions.log`
- `inventory-updates.csv`
- `shop-updates.csv`
- `vendor-full-updates.csv`
- `enemy-state.csv`
- `enemy-state.json`
- `chat-dialogue.log`
- `system-messages.log`

## Capture Counts

| Field | Count |
| --- | ---: |
| inboundRaw | 13830 |
| outboundRaw | 4435 |
| decodedInboundN3 | 13301 |
| decodedOutboundN3 | 4377 |
| vendorInteractionAttempts | 2 |
| vendorFullUpdateMessages | 17 |
| shopUpdateMessages | 1 |
| shopUpdateRows | 7 |
| npcInteractions | 1659 |
| inventoryUpdateMessages | 116 |
| inventoryUpdateRows | 78 |
| enemyTrackedEntities | 159 |
| enemyStateRows | 3279 |
| enemyCombatEvents | 375 |
| enemyDamageEvents | 127 |
| enemySpawnEvents | 159 |
| enemyDespawnEvents | 1088 |
| enemyHealthUpdates | 1746 |
| enemyPositionUpdates | 702 |

## Useful Evidence

### Corpse Use / Access

The capture repeatedly proves the corpse open/access packet path:

1. `OUT GenericCmd Use` targeting `Corpse:*`
2. `IN InventoryUpdate` with `InventoryIdentity=(Corpse:*)`, `Unknown1=21`, `Unknown2=2`, `Unknown3=1`
3. `IN GenericCmd` success ack with `Temp1=1`

Example empty corpse open:

- `events.log:239` `OUT GenericCmd Use` target `Corpse:F6C003`
- `events.log:242-243` `IN InventoryUpdate` for `Corpse:F6C003`, handle `112`, zero items
- `events.log:244` `IN GenericCmd` success ack

The same path appears throughout `npc-interactions.log` for corpse handles `112` through at least `226`.

### Corpse Inventory Contents

`inventory-updates.csv` is useful for corpse loot-window content fixtures. Examples:

| Corpse | Handle | Sequence | Slots | Items |
| --- | ---: | ---: | ---: | --- |
| `Corpse:F6C008` | 122 | 1037 | 1 | `42640/42640 QL1` |
| `Corpse:F6C003` | 123 | 1046 | 2 | `209491/209491 QL1`, `42640/42640 QL1` |
| `Corpse:F6C004` | 131 | 1832 | 1 | `136644/136645 QL2` |
| `Corpse:F6C009` | 133 | 2001 | 7 | `201135/201136 QL4`, `85655/22104 QL4`, `124579/124580 QL4`, `154070/150196 QL4`, `160448/160441 QL4`, `162623/162623 QL14` x2 |

No clear corpse item-transfer or corpse credit-award sequence was imported. Targeted searches did not find a corpse-linked `ContainerAddItem`, `ClientMoveItemToInventory`, or credit feedback sequence in the inspected logs. Treat this capture as corpse access/open/content evidence, not as final proof of corpse item transfer or corpse credit award behavior.

### Vendor Full Update

`vendor-full-updates.csv` proves two vending machine identities:

| Identity | Template | Position | Owner | Notes |
| --- | ---: | --- | --- | --- |
| `VendingMachine:12EA61A0` | `100035` | `60, 13.9999981, 50` | `0:0` | Has repeated full updates in PF `1108028`. |
| `VendingMachine:12EA61A1` | `209286` | not present in CSV | `50000:2029597735` | Repeated full updates tied to owner identity. |

### Shop Update

`shop-updates.csv` proves a seven-item shop payload for `VendingMachine:12EA61A0` at sequence `10153`:

- slot `0`: `31837/31837 QL1`
- slot `1`: `291082/291082 QL1`
- slot `2`: `291043/291043 QL1`
- slot `3`: `95577/95577 QL1`
- slot `4`: `28564/28564 QL1`
- slot `5`: `161699/161699 QL1`
- slot `6`: `99228/99228 QL1`

This is useful for future vendor/shop update shape validation. It should not be imported as game data without separate identity/content approval.

### NPC Spawn / Combat / Death / Corpse / Despawn

The capture provides useful NPC lifecycle evidence:

- `enemy-state.csv/json`: spawn/update/damage/despawn rows for 159 tracked entities.
- `events.log` and `packets.hex.log`: decoded `Attack`, `SpecialAttackWeapon`, `AttackInfo`, `CharacterAction Death`, `CorpseFullUpdate`, and `Despawn`.
- Example sequence:
  - `OUT Attack` from `SimpleChar:67341BED` to `SimpleChar:78F93437`
  - `IN SpecialAttackWeapon`
  - `IN AttackInfo` amount `4593`, `HitType=Critical`, weapon slot `6`
  - `IN CharacterAction Death` for `SimpleChar:78F93437`, `Parameter2=500`
  - `IN CorpseFullUpdate` for `Corpse:F6C003`, name `Remains of Grass Snake`
  - `IN Despawn` for `Corpse:F6C003`

Note: `capture_info.json` reports `enemyDeathEvents=0`, so use the decoded N3 event logs for death/corpse ordering, not the health-state death counter.

### GenericCmd Use Routing

The capture strongly validates `GenericCmd Action=Use` against corpses and the success ack response. It does not provide enough targeted evidence here to claim a vendor `GenericCmd Use` request path; the vendor/shop evidence is useful as full-update/shop-update evidence.

### Packet Hex Decoder Coverage

`packets.hex.log` is useful as decoder coverage reference for:

- `Attack`
- `SpecialAttackWeapon`
- `AttackInfo`
- `CharacterAction Death`
- `CorpseFullUpdate`
- `InventoryUpdate`
- `GenericCmd`
- `Despawn`
- `VendingMachineFullUpdate`
- `ShopUpdate`
- `TemplateAction`

The raw hex log was not imported because the decoded files and this compact reference capture the actionable evidence without adding large raw packet dumps.

## Not Imported

- Full `events.log`, `packets.hex.log`, and `npc-interactions.log`: too large for repo evidence; the submitted folder remains the raw source.
- Full `enemy-state.csv/json`: useful but large and mostly broad area tracking; not needed until a specific NPC/content task selects identities.
- `chat-dialogue.log`: contains KnuBot answer-list dialogue and general chat, but no current scoped gameplay import target.
- `system-messages.log`: useful for feedback/stat observations, but targeted searches did not identify corpse credit or loot transfer proof.

## Small Reference Fixture

Companion fixture:

- `docs/generated/omni_training_ground_capture_20260622_182442_reference.json`

The fixture contains compact message-order and identity/item evidence only. It is not runtime game data.
