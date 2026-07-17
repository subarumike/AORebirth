# Subway Bloodcreeper and Disobedient Bot Loot Audit

## Scope and conclusion

This audit reconstructs corpse-item evidence from the complete existing AOSharp
capture corpus before any new gameplay capture is requested. Enemy identity,
death, corpse identity, corpse inventory, and transfer traffic are joined in
that order. Display names, proximity, database membership, and inventory rows
from another entity are not accepted as drop proof.

- **Disobedient Bot:** two item identities have complete capture-backed
  membership proof and may be activated. The observed strict sample is one
  Small Power Supply, one Eye Implant, and five item-empty inventories.
- **Bloodcreeper:** no item identity is proven. Its item pool remains explicitly
  unresolved and inactive. Two complete empty snapshots do not prove a
  universally empty item pool.
- No population row was activated. The population boundary remains 260
  represented, 222 active, and 38 quarantined.
- This offline audit is not Bloodcreeper private-client acceptance.

The item database was used only to resolve display names after an item identity
was proven. Database presence was not treated as enemy-drop evidence.

## Corpus audited

Primary capture root:

`C:\Users\Mike\Documents\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures`

Raw and derived evidence included `packets.hex.log`, `raw-packets.csv`,
`events.log`, `corpse-full-updates.csv`, `npc-lifecycle.csv`,
`inventory-updates.csv`, `corpse-loot-observations.csv`, enemy dossiers,
capture manifests and health reports, generated reports, current loot data,
tests, and item-name lookup data. Raw packets remained authoritative when a
derived projection was incomplete or marked unlinked.

### Bloodcreeper sessions

- `20260709-222339`: SCFU and survey evidence only.
- `20260709-225408`: one exact Bloodcreeper corpse; inventory not observed.
- `20260712-223719`: one exact death and corpse; inventory not observed.
- `20260712-224608`, `20260712-224840`, `20260712-232137`,
  `20260712-232711`, `20260712-232848`, and `20260712-234401`:
  visibility or movement evidence only.
- `20260716-033326` and `20260716-034104`: focused fights with complete,
  item-empty corpse inventories.
- `20260716-034433`, `20260716-034559`, and `20260716-034656`:
  visibility or full-update evidence only.
- `20260528-191120` contains **Boneheaded Bloodcreeper**, a different enemy,
  and was rejected from the Bloodcreeper sample.

### Disobedient Bot sessions

- `20260708-143600`: two corpses.
- `20260709-205921`: one corpse.
- `20260709-210452`: three corpses.
- `20260709-220439`: three corpses.
- `20260712-153918`: one corpse.
- `20260712-160257`: one corpse.
- `20260713-013906`: one corpse.
- `20260713-014714`: one corpse.
- `20260713-033511`: one corpse.

## Evidence counts and strict denominators

### Bloodcreeper

| Measure | Count |
| --- | ---: |
| Death/corpse generations | 4 |
| Explicit raw Death packets | 3 |
| Additional corpse-implied death | 1 |
| Exact Bloodcreeper corpses | 4 |
| Complete corpse inventories | 2 |
| Proven local corpse opens | 2 |
| Additional derived `open=True` without inventory | 1 |
| Item-empty complete inventories | 2 |
| Item transfers | 0 |
| Inventory-ambiguous corpses | 2 |
| Distinct proven or probable item identities | 0 |
| Strict complete denominator | 2 |
| Duplicate loot projections removed from the denominator | 6 |

Each focused empty snapshot appeared in the raw packet log, raw packet index,
decoded event log, and derived loot projection. Those four representations were
collapsed to one corpse sample, removing three duplicate projections per
snapshot. No independent Bloodcreeper corpse generation was a duplicate.

### Disobedient Bot

| Measure | Count |
| --- | ---: |
| Direct raw Death events | 13 |
| Additional corpse-implied death | 1 |
| Distinct Disobedient Bot corpses | 14 |
| Complete corpse inventories | 8 |
| Corpses with observed Use/open traffic | 9 |
| Item-empty complete inventories | 5 |
| Successful observed item transfers | 3 |
| Incomplete or ambiguous item outcomes | 7 |
| Candidate item identities | 3 |
| Fully proven item identities | 2 |
| Duplicate raw corpse observations removed | 1 |
| Strict complete identity-linked denominator | 7 |

The strict denominator contains the two fully linked item outcomes and five
fully linked item-empty outcomes. It excludes six corpses with no inventory
snapshot and the `20260713-013906` inventory whose capture lacks the preceding
enemy identity and Death chain. That ambiguous transferred item remains useful
research evidence but cannot affect runtime loot.

## Bloodcreeper identity linkage

Four independent Bloodcreeper corpse generations were recovered:

| Session | Enemy identity | Death evidence | Corpse identity and evidence | Inventory result |
| --- | --- | --- | --- | --- |
| `20260709-225408` | `SimpleChar:795451C5` | Corpse-implied only | `Corpse:F6E016`, `packets.hex.log:14024`; `events.log:15374` | No local Use or InventoryUpdate; ambiguous |
| `20260712-223719` | `SimpleChar:7960785D` | `packets.hex.log:1291` | `Corpse:F6C002`, `packets.hex.log:1308`; `events.log:190-192` | Derived `open=True`, but no packet-backed local Use or InventoryUpdate; ambiguous |
| `20260716-033326` | `SimpleChar:796CD798` | `packets.hex.log:539`; `events.log:1169` | `Corpse:F69003`, `packets.hex.log:562`; `events.log:1196` | Use at `packets.hex.log:1039`; complete `Items=count=0[]` at `:1041` and `events.log:2148-2150`, handle 112 |
| `20260716-034104` | `SimpleChar:796D4099` | `packets.hex.log:2766`; `events.log:5501` | `Corpse:F69004`, `packets.hex.log:2789`; `events.log:5542` | Use at `packets.hex.log:2867`; complete `Items=count=0[]` at `:2869` and `events.log:5706-5708`, handle 116 |

The two modern derived loot rows were marked unlinked by the capture projection,
but the raw death, exact corpse full update, Use request, and exact corpse
InventoryUpdate form complete manual chains. They are valid empty observations
and do not require recapture. Neither contains an item slot or transfer.

### Bloodcreeper classification

- `Proven enemy corpse item`: none.
- `Proven transferred enemy corpse item`: none.
- `Probable but identity linkage incomplete`: none.
- Complete item-empty enemy corpse: two.
- `Unresolved`: the complete Bloodcreeper item pool.

No Bloodcreeper item entry may become active from this corpus. The current empty
Bloodcreeper loot-evidence array is not proof of `NoneProven`; runtime metadata
must preserve an unresolved item-pool state while retaining existing corpse and
150-credit behavior.

## Proven Disobedient Bot items

### Small Power Supply

- Identity: `234877/234877`
- Quality: QL1
- Count: 1
- Source slot: 0
- Classification: `Proven transferred enemy corpse item`

Raw chain in `20260709-210452`:

1. `events.log:37`: `SimpleChar:794E807A`, Disobedient Bot, level 5,
   MonsterData `17649`.
2. `packets.hex.log:3819`, raw packet `#3595`: Death.
3. `packets.hex.log:3827`, raw packet `#3603`: `Corpse:F6E030`, dead NPC
   `794E807A`.
4. `packets.hex.log:4006`, raw packet `#3770`: InventoryUpdate containing
   `234877/234877`, QL1, count 1.
5. `inventory-updates.csv:10`: matching decoded slot.
6. `packets.hex.log:4050`, outbound packet `#238`:
   `ClientMoveItemToInventory`.
7. `packets.hex.log:4057`, inbound packet `#3819`: `ContainerAddItem`.

### Eye Implant: Pharma Tech, Bright

- Identity: `104683/104684`
- Quality: QL10
- Count: 1
- Source slot: 0
- Classification: `Proven transferred enemy corpse item`

Raw chain in `20260713-033511`:

1. `events.log:115`: `SimpleChar:79607E2C`, Disobedient Bot, level 8,
   MonsterData `17649`.
2. `packets.hex.log:1357`, raw packet `#1293`: Death.
3. `packets.hex.log:1369`, raw packet `#1305`: `Corpse:F6C003`, dead NPC
   `79607E2C`.
4. `packets.hex.log:1466`, raw packet `#1392`: InventoryUpdate containing
   `104683/104684`, QL10, count 1.
5. `inventory-updates.csv:2`: matching decoded slot.
6. `packets.hex.log:1499`, outbound packet `#75`:
   `ClientMoveItemToInventory`.
7. `packets.hex.log:1501`, inbound packet `#1426`: `ContainerAddItem`.

## Rejected and ambiguous candidates

### A Burnt Out Memory Chip

- Identity: `234876/234876`
- Quality: QL1
- Count: 1
- Classification: `Probable but identity linkage incomplete`
- Runtime status: inactive and unresolved

Evidence in `20260713-013906`:

- `packets.hex.log:124`, raw packet `#122`: `Corpse:F6C001`, named
  `Remains of Disobedient Bot`, MonsterData `17649`, dead NPC `79607B0F`.
- `packets.hex.log:725`, raw packet `#702`: InventoryUpdate containing
  `234876/234876`, QL1.
- `packets.hex.log:780`, raw packet `#757`: successful `ContainerAddItem`.
- `corpse-full-updates.csv:2` and `npc-lifecycle.csv:70` mirror the corpse.

The capture begins after death, contains no preceding `SimpleChar:79607B0F`
enemy timeline or Death packet, and retained a non-final `running` manifest.
The corpse and transfer are real, but the required complete chain is absent.
This item cannot become active until another occurrence proves enemy identity,
death, corpse, inventory, and transfer in one complete session.

### Other-enemy and non-enemy noise

- In `20260716-034104`, `Corpse:F69003` belongs to **Molested Molecules**,
  `SimpleChar:796CD747`, despite that corpse instance having represented
  Bloodcreeper in the earlier `20260716-033326` session. Its items
  `27199/27199` QL10, `121743/121744` QL25, and `301712/301712` QL1 at
  `inventory-updates.csv:4-6` are `Corpse item from another enemy`.
- In `20260709-225408`, `101675/101676` QL25 at
  `inventory-updates.csv:41` belongs to `Corpse:F6E002`, **Lost Thought**,
  proven by `packets.hex.log:13727`. Bloodcreeper's `Corpse:F6E016`
  appeared later at `packets.hex.log:14024`.
- `20260712-153918` reused `Corpse:F6C00D`: the Bot corpse ended before a
  Filth Flea reused the identity. The Flea's `103049/103050` QL4 item is not
  Bot loot.
- `20260709-210452` reused `Corpse:F6E030`: an earlier Filth Flea's
  `101507/101508` QL6 item belongs to that earlier generation, not the Bot.
- Other `234876` or `234877` observations belonged to Filth Flea, Shadow,
  Workman Striker, Slum Runner, Architect Striker, or later player inventory
  and delete traffic.
- `Container:B9C6B7F` and other static-container inventories were rejected by
  identity type. Player inventories, vendor/shop inventories, and unrelated
  corpse identities were excluded.
- `20260709-220439` repeated the exact
  `Corpse:F6E01C`/`SimpleChar:7953AB08` full update at raw lines `10192` and
  `12675`; it is one corpse sample.
- Derived event, lifecycle, corpse, and inventory projections are mirrors of
  raw events, not additional samples.
- The `104683/104684` ten-of-ten case in `SubwayLootPoolRulesTests` is a
  synthetic rule fixture, not ten captured drops.

## Disobedient Bot strict observation and provisional runtime policy

The strict seven-corpse denominator is:

- 1 Small Power Supply outcome;
- 1 Eye Implant outcome;
- 5 complete item-empty outcomes.

The five exact empty inventories are:

- `20260708-143600`, `Corpse:F6E009`, raw inventory line `3722`.
- `20260708-143600`, `Corpse:F6E00D`, raw inventory line `10769`.
- `20260709-210452`, `Corpse:F6E01D`, raw inventory line `4163`.
- `20260709-210452`, `Corpse:F6E02B`, raw inventory line `4817`.
- `20260709-220439`, `Corpse:F6E009`, raw inventory line `6165`.

The evidence proves membership for the two items and proves that item-empty
outcomes occur. It does **not** prove official probabilities. Where the runtime
requires weights, this slice uses an isolated provisional private-project
policy with relative weights:

- Small Power Supply: `1`
- Eye Implant: `1`
- Empty outcome: `5`

This `1 + 1 + 5` policy mirrors the strict observed outcomes only. It is not a
claim of official AO drop rates, does not make either item guaranteed, and must
remain separately labeled from the capture-proven item identities. The
`234876/234876` candidate is excluded from both membership and weights.

## Minimum future loot captures

No capture is started or requested as part of this offline task. These are the
bounded requirements if the unresolved pools are continued later.

### Bloodcreeper

- Evidence minimum: **8 additional independent kills**, bringing the strict
  complete denominator from 2 to the practical target of 10.
- Open every corpse after its exact corpse full update.
- Preserve an initial empty snapshot when no items appear.
- When items appear, transfer every item separately, reopen the corpse, and
  continue until the empty state is recorded.
- Keep other enemy corpses out of the sampling sequence or retain enough raw
  identity evidence to exclude them deterministically.
- Record one dedicated session label such as
  `bloodcreeper-loot-remaining-8` in the handoff/notes.
- No combat, geometry, LOS, navigation, leash, or respawn capture is needed.

### Disobedient Bot

- Evidence minimum: **3 additional independent kills**, bringing the strict
  complete denominator from 7 to 10.
- Open every corpse and transfer every item; retain item-empty snapshots.
- Record one dedicated session label such as
  `disobedient-bot-loot-remaining-3` in the handoff/notes.
- A new fully linked `234876/234876` occurrence is still required before that
  item can become active. If it does not recur, it remains unresolved.
- No combat, geometry, LOS, chase, leash, or respawn capture is needed.

The approved comprehensive launcher remains:

```cmd
cmd /d /c tools-temp\start-aosharp-live-capture.cmd --title "<AO window title>"
```

The session-local validation mode remains:

```cmd
cmd /d /c tools-temp\start-aosharp-live-capture.cmd --title "<AO window title>" --loot-10
```

`--loot-10` currently requires ten initial corpse snapshots in one session. It
therefore exceeds the remaining evidence minimums of eight and three unless the
validator gains an explicit remaining-sample target. The evidentiary minimum
must not be mislabeled as ten new kills. A future session is accepted only when
the authoritative raw traffic is preserved, the capture finalizes cleanly, and
every required enemy-to-corpse-to-inventory chain passes completeness checks.

## Bloodcreeper private smoke checklist

This later smoke test is gameplay validation, not another required packet
capture unless a failure creates a new evidence question.

- Displayed level is within the configured inclusive 15-25 range.
- Skinspider Bite occurs.
- Skinspider Spit occurs.
- Movement and chase work without changing the accepted shared navigation.
- Corpse opens normally.
- Corpse exposes the existing 150-credit behavior.
- Item behavior remains unresolved/empty unless later evidence activates an
  item; if an item is activated, it appears with the proven identity and QL.
- Item transfer removes only the transferred content.
- Closing and reopening preserves any remaining content.
- Fully empty corpse cleanup is near immediate.
- Respawn occurs after the existing explicit 240-second policy.
- Exactly one new spawn generation appears; no duplicate respawn occurs.

Until this checklist is completed on the private server, Bloodcreeper must not
be described as privately accepted.
