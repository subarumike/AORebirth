# Current Client Data Verification

Generated: 2026-06-13 03:59:14

## Scope

- AO client root: C:\Funcom\Anarchy Online
- AO client version.id: 18.8.62_EP1
- CellAO repo: C:\Users\Mike\Documents\Cellao-AORebirth
- Database read: cellao_codex_clean only

## Summary

| Area | Result |
| --- | ---: |
| Built item templates | 120842 |
| Built nano formulas | 10965 |
| Built playfields | 618 |
| Data file source/runtime/version issues | 0 |
| Live vendor mesh evidence rows not satisfied by item cache | 2 |
| Vendor DB rows with issues | 0 |
| Shop inventory rows with item-cache issues | 0 |
| Vending statels without complete DB shop coverage | 202 |
| Vending statels excluded from coverage | 30 |

## Latest Vendor Import Milestone

- Clan Advanced General Shop import promoted from AOSharp capture 20260613-034740.
- Validated coverage added: 16 1181 ord_smarket_clan_advanced vendor rows, 16 vendor templates, and 11 new shop inventory groups with 505 inventory rows; existing shop inventory hashes Cont, IVM2, IYD4, JTYS, and LJI7 were reused.
- Current-client verification after import showed actionable uncovered statel vendors dropped from 218 to 202. Current live-capture coverage chain: 404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202.

## Coverage Exclusions

| Template | Name | Reason | Excluded statels | Evidence |
| ---: | --- | --- | ---: | --- |
| 155225 | Refreshing Drink | NonShopStatelTemplate | 30 | AOSharp captures 20260612-012644 and 20260612-044234 emitted VendorFullUpdate evidence but no ShopUpdate inventory rows; live operator confirmed the Superior instances were not reachable/openable. |

Excluded statels remain in the raw statel vendor coverage CSV with CoverageExcluded and ExclusionReason fields, but they are excluded from coverage metrics, missing-vendor reports, capture targeting, and import planning.

## Data Files

| File | Source version | Source rows | Built version | Built rows | Matches |
| --- | --- | ---: | --- | ---: | --- |
| items.dat | 18.8.62_EP1 | 120842 | 18.8.62_EP1 | 120842 | True |
| nanos.dat | 18.8.62_EP1 | 10965 | 18.8.62_EP1 | 10965 | True |
| playfields.dat | 18.8.62_EP1 | 618 | 18.8.62_EP1 | 618 | True |

Direct implication: source and built data must match before committing data alignment work. A fresh dev pull uses source data, while Mike's current running folder can contain extractor-generated files.

## Live Vendor Mesh Evidence

| Label | Template | items.dat mesh | Live mesh | Runtime override | Status |
| --- | ---: | ---: | ---: | ---: | --- |
| Basic Bookstore | 155599 | 93106 | 85976 | 85976 | Runtime override matches live |
| Omni Basic Devices | 155603 | 93106 | 85976 | 85976 | Runtime override matches live |

This separates template-cache truth from live runtime instance truth. If items.dat still differs from live but the runtime override matches, that is not an item import failure.

## Highest Value Repair Targets

1. Source/runtime data files are aligned.
2. Vendor DB rows have template/shop coverage.
3. Review statel vendors with no DB row; these spawn as empty statel vendors and can produce missing shop entry behavior.
4. Shop inventory item IDs resolve in current item cache.

## Report Files

- Data file audit: C:\Users\Mike\Documents\Cellao-AORebirth\tools-temp\current-client-data-verification\data-file-version-audit.csv
- Live vendor mesh audit: C:\Users\Mike\Documents\Cellao-AORebirth\tools-temp\current-client-data-verification\live-vendor-mesh-audit.csv
- Vendor DB audit: C:\Users\Mike\Documents\Cellao-AORebirth\tools-temp\current-client-data-verification\vendor-db-audit.csv
- Shop inventory audit: C:\Users\Mike\Documents\Cellao-AORebirth\tools-temp\current-client-data-verification\shop-inventory-template-audit.csv
- Statel vendor coverage: C:\Users\Mike\Documents\Cellao-AORebirth\tools-temp\current-client-data-verification\statel-vendor-coverage.csv
- Vendor scan door locations: C:\Users\Mike\Documents\Cellao-AORebirth\tools-temp\current-client-data-verification\vendor-scan-door-locations.csv

## Top Vendor DB Issues

No vendor DB issues found.

## Top Statel Vendor Coverage Issues

| Playfield | Name | Vendor id | Statel | Template | Coords | Issues |
| ---: | --- | ---: | --- | ---: | --- | --- |
| 500 | Parnassos | 32768000 | 0xC00001F4 | 99571 | 215.737,16.4,162.412 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768001 | 0xC00101F4 | 99599 | 214.427,16.4,162.485 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768002 | 0xC00201F4 | 99635 | 211.938,16.4,162.399 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768005 | 0xC00501F4 | 99570 | 211.355,16.4,160.109 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768007 | 0xC00701F4 | 99598 | 214.657,16.4,159.581 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768009 | 0xC00901F4 | 118287 | 217.19,16.4,159.708 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768011 | 0xC00B01F4 | 99600 | 213.404,16.4,159.507 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768013 | 0xC00D01F4 | 117938 | 217.822,16.4,161.884 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768014 | 0xC00E01F4 | 99503 | 219.649,16.4,160.128 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768015 | 0xC00F01F4 | 99533 | 220.751,16.4,160.499 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768016 | 0xC01001F4 | 99517 | 221.824,16.4,160.883 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768017 | 0xC01101F4 | 99541 | 222.913,16.4,161.281 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768018 | 0xC01201F4 | 99509 | 224.031,16.4,161.614 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768020 | 0xC01401F4 | 99528 | 226.065,16.4,161.447 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768021 | 0xC01501F4 | 99522 | 226.448,16.4,160.231 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768022 | 0xC01601F4 | 99506 | 225.719,16.4,159.308 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768023 | 0xC01701F4 | 117673 | 224.476,16.4,158.955 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768024 | 0xC01801F4 | 99537 | 223.332,16.4,158.564 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768025 | 0xC01901F4 | 99526 | 222.312,16.4,158.193 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768026 | 0xC01A01F4 | 99539 | 221.331,16.4,157.809 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768027 | 0xC01B01F4 | 99540 | 220.278,16.4,157.358 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768028 | 0xC01C01F4 | 121033 | 219.673,16.4,157.879 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768029 | 0xC01D01F4 | 121031 | 219.333,16.4,159.129 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768030 | 0xC01E01F4 | 120511 | 232.893,16.399,165.46 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768031 | 0xC01F01F4 | 117749 | 231.973,16.4,166.237 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768038 | 0xC02601F4 | 99561 | 218.148,16.4,172.2 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768039 | 0xC02701F4 | 99563 | 223.306,16.4,175.132 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768040 | 0xC02801F4 | 99564 | 223.583,16.4,172.203 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768041 | 0xC02901F4 | 99562 | 222.371,16.4,172.19 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768042 | 0xC02A01F4 | 99565 | 221.267,16.4,172.117 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768043 | 0xC02B01F4 | 99566 | 220.09,16.4,172.17 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768044 | 0xC02C01F4 | 99538 | 223.588,16.4,171.086 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768045 | 0xC02D01F4 | 99504 | 222.449,16.4,171.153 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768046 | 0xC02E01F4 | 99534 | 221.286,16.4,171.177 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768047 | 0xC02F01F4 | 99518 | 220.105,16.4,171.236 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768048 | 0xC03001F4 | 99542 | 219.015,16.4,171.115 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768049 | 0xC03101F4 | 99529 | 217.832,16.4,171.123 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768051 | 0xC03301F4 | 99530 | 215.748,16.4,171.108 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768052 | 0xC03401F4 | 99523 | 214.963,16.4,170.276 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768053 | 0xC03501F4 | 99507 | 215.002,16.4,169.185 | no vendors row; runtime spawns empty statel vendor |
