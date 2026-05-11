# Client RDB Enemy Spawn Hints

The AO client does not appear to contain exact live spawn tables, but it does contain useful evidence for which enemy families belong to which playfields and districts.

Generated files:

- `ClientRdbZoneEnemyHints.csv`: playfield-level enemy hints from client district, area, and statel records.
- `ClientRdbNpcTemplateHints.csv`: NPC/monster template-name hints from client RDB type `1040023`.
- `ClientHintedEnemyCoverage.csv`: client-hinted enemy families compared against the current supported combat test catalog.
- `MonsterDataCorpseVisualHints.csv`: CellAO mob templates matched to local model-viewer `MonsterData -> CatMesh` data.

Extractor:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools-temp\ao-client-rdb-hints\Export-AOClientZoneEnemyHints.ps1
```

Default client input:

- `C:\Funcom\Anarchy Online\cd_image\data\db\ResourceDatabase.idx`

Default AODB reader input:

- `C:\Users\Mike\Documents\AO programs\aodb-master\AODB\bin\Debug`

## Record Types

| Type | Meaning |
| ---: | --- |
| `1000001` | Playfield name records |
| `1000014` | Playfield district records |
| `1000029` | Area description records |
| `1000026` | Statel/door/object text records |
| `1040023` | NPC/monster template-name records |

## Useful Early Rows

| Playfield | Name | Enemy hints |
| ---: | --- | --- |
| `952` | Clan Training | monster-level district hints |
| `954` | Neutral Training | monster/MOB district hints |
| `565` | Newland Desert | rhinoman, leet, snake, lizard, bronto, flea, scorpiod, salamander |
| `585` | Aegean | snake, rhinoman, hound, mechdog, anun, leet, flea, rollerrat |
| `600` | Varmint Woods | rhinoman, bronto, lizard, spider, leet |

## Useful NPC Template Rows

| TemplateId | Name |
| ---: | --- |
| `17655` | leet |
| `17687` | rollerrat |
| `30252` | giant snake |
| `31114` | rhinoman female |
| `32419` | sewer snake |
| `40063` | rollerrat queen |

## How To Use This

Use this as a source-of-truth hint layer when choosing what enemy families to seed into a playfield. Do not treat it as final spawn truth for position, density, respawn timers, or pathing; those still need live packet evidence, server-side data, or explicit design choices.

## Current Server Use

The GM combat-test spawn path now uses these hints for supported low-level test mobs:

- `/command spawn hints`: lists the supported combat test mobs mapped to the current playfield by the client hint catalog.
- `/command spawn zone`: spawns one of each supported mapped test mob near the GM for the current playfield.
- `/command spawn status`: shows live combat test mobs in the current playfield.
- `/command spawn clear`: despawns live combat test mobs and their test corpses in the current playfield without changing DB rows.

The login debug enemy path uses the same playfield hint selector. If no live test mob already exists in the playfield, it spawns the first supported client-hinted test mob for that playfield, falling back to the global beach leet only when the playfield has no supported hints yet.

Currently supported families: leet, reet, snake, rollerrat, flea, lizard, malle, salamander.

This is intentionally a test harness, not a final world-population system. It lets us quickly validate client-visible mob families in the right zones while we continue mapping real spawn positions, density, pathing, and respawn rules.
