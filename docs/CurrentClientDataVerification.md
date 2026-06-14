# Current Client Data Verification

Generated: 2026-06-14 00:11:01

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
| Vending statels without complete DB shop coverage | 96 |
| Vending statels excluded from coverage | 30 |

## Latest Vendor Import Milestone

- Jobe Basic dimensions import promoted from AOSharp capture 20260614-000058.
- Validated coverage added: 3 vendor rows across 4563 Hardware Dimension - Basic and 4567 Dimensional Shift - Basic, 3 vendor templates, and 3 new shop inventory groups with 98 inventory rows.
- The imported rows cover Jobe Hardware Basic Armor, Jobe Dimensional Basic Regenerative Supplies, and Jobe Dimensional Basic Implants. Same-template Advanced dimensional targets were not imported without direct capture or explicit inference approval.
- Current-client verification after import showed actionable uncovered statel vendors dropped from 99 to 96. Current coverage chain: 404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96.

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
| 500 | Parnassos | 32768005 | 0xC00501F4 | 99570 | 211.355,16.4,160.109 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768007 | 0xC00701F4 | 99598 | 214.657,16.4,159.581 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768009 | 0xC00901F4 | 118287 | 217.19,16.4,159.708 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768011 | 0xC00B01F4 | 99600 | 213.404,16.4,159.507 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768013 | 0xC00D01F4 | 117938 | 217.822,16.4,161.884 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768014 | 0xC00E01F4 | 99503 | 219.649,16.4,160.128 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768017 | 0xC01101F4 | 99541 | 222.913,16.4,161.281 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768021 | 0xC01501F4 | 99522 | 226.448,16.4,160.231 | no vendors row; runtime spawns empty statel vendor |
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
| 500 | Parnassos | 32768048 | 0xC03001F4 | 99542 | 219.015,16.4,171.115 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768052 | 0xC03401F4 | 99523 | 214.963,16.4,170.276 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768054 | 0xC03601F4 | 117674 | 223.993,16.4,169.768 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768055 | 0xC03701F4 | 99531 | 223.792,16.4,168.819 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768056 | 0xC03801F4 | 99499 | 235.707,16.4,177.596 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768060 | 0xC03C01F4 | 99536 | 238.744,16.4,181.014 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768064 | 0xC04001F4 | 99489 | 236.378,16.4,180.767 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768066 | 0xC04201F4 | 117648 | 237.855,16.4,181.098 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768067 | 0xC04301F4 | 120977 | 113.4,32,422.44 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768068 | 0xC04401F4 | 120973 | 113.4,32,420.4 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768081 | 0xC05101F4 | 155595 | 421.521,32.8,360.723 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768084 | 0xC05401F4 | 155498 | 468.2,37.001,343.7 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768087 | 0xC05701F4 | 155313 | 473.8,37.001,343.7 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768103 | 0xC06701F4 | 155296 | 508.824,37.001,343.7 | no vendors row; runtime spawns empty statel vendor |
| 500 | Parnassos | 32768104 | 0xC06801F4 | 155297 | 510.4,37.001,343.735 | no vendors row; runtime spawns empty statel vendor |
