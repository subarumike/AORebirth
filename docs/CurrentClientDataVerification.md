# Current Client Data Verification

Generated: 2026-06-14 01:51:12

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
| Vending statels without complete DB shop coverage | 27 |
| Vending statels excluded from coverage | 169 |

## Latest Vendor Import Milestone

- Neutral Training Startup Equipment import promoted from AOSharp capture 20260614-002319.
- Validated coverage added: 2 vendor rows in 954 Neutral Training, 1 vendor template, and 1 new shop inventory group with 9 inventory rows.
- The imported rows cover both Basic Startup Equipment statels; both have direct VendorFull and ShopUpdate evidence and share exact inventory hash WHBW.
- Current-client verification after import showed actionable uncovered statel vendors dropped from 29 to 27. Current coverage/actionability chain: 404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96 -> 93 -> 89 -> 29 -> 27.

## Coverage Exclusions

| Scope | Id | Name | Reason | Excluded statels | Evidence |
| --- | ---: | --- | --- | ---: | --- |
| Template | 155225 | Refreshing Drink | NonShopStatelTemplate | 30 | AOSharp captures 20260612-012644 and 20260612-044234 emitted VendorFullUpdate evidence but no ShopUpdate inventory rows; live operator confirmed the Superior instances were not reachable/openable. |
| Playfield | 500 | Parnassos | InaccessibleGmOnlyPlayfield | 140 | Operator verification confirmed there is no practical live-client access path for capture; these GM-only statels are kept in raw audit output only. |

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
| 665 | Broken Shores | 43581441 | 0xC0010299 | 99522 | 729.875,22.01,1541.501 | no vendors row; runtime spawns empty statel vendor |
| 952 | Clan Training | 62390272 | 0xC00003B8 | 100034 | 48,14,62 | no vendors row; runtime spawns empty statel vendor |
| 1426 | Clan Registration dng | 93454336 | 0xC0000592 | 25885 | 166.991,5.01,235.899 | no vendors row; runtime spawns empty statel vendor |
| 1426 | Clan Registration dng | 93454337 | 0xC0010592 | 81799 | 171.952,5.01,238.745 | no vendors row; runtime spawns empty statel vendor |
| 1427 | Omni registration dng | 93519872 | 0xC0000593 | 81799 | 201.952,5,172.745 | no vendors row; runtime spawns empty statel vendor |
| 1428 | Neutral organisation dng | 93585408 | 0xC0000594 | 81799 | 201.952,5,162.745 | no vendors row; runtime spawns empty statel vendor |
| 4364 | Unicorn Outpost | 285999104 | 0xC000110C | 256457 | 146.464,100.907,156.153 | no vendors row; runtime spawns empty statel vendor |
| 4364 | Unicorn Outpost | 285999105 | 0xC001110C | 287037 | 143.328,100.886,152.997 | no vendors row; runtime spawns empty statel vendor |
| 4704 | Tower Shop (dungeon) | 308281349 | 0xC0051260 | 249724 | 171,5.01,166.966 | no vendors row; runtime spawns empty statel vendor |
| 4704 | Tower Shop (dungeon) | 308281366 | 0xC0161260 | 295890 | 189,5.01,167 | no vendors row; runtime spawns empty statel vendor |
| 4704 | Tower Shop (dungeon) | 308281368 | 0xC0181260 | 295892 | 185,5.01,167 | no vendors row; runtime spawns empty statel vendor |
| 6007 | BS Signup (dng) | 393674752 | 0xC0001777 | 266562 | 220.01,6.02,232.985 | no vendors row; runtime spawns empty statel vendor |
| 6007 | BS Signup (dng) | 393674753 | 0xC0011777 | 266563 | 222,6.02,232.985 | no vendors row; runtime spawns empty statel vendor |
| 6007 | BS Signup (dng) | 393674754 | 0xC0021777 | 266569 | 224.027,6.02,232.985 | no vendors row; runtime spawns empty statel vendor |
| 6007 | BS Signup (dng) | 393674755 | 0xC0031777 | 266564 | 226.01,6.02,232.982 | no vendors row; runtime spawns empty statel vendor |
| 6007 | BS Signup (dng) | 393674756 | 0xC0041777 | 266565 | 228.01,6.02,232.985 | no vendors row; runtime spawns empty statel vendor |
| 6007 | BS Signup (dng) | 393674757 | 0xC0051777 | 266566 | 230.01,6.02,232.985 | no vendors row; runtime spawns empty statel vendor |
| 6007 | BS Signup (dng) | 393674758 | 0xC0061777 | 266567 | 232.01,6.02,232.985 | no vendors row; runtime spawns empty statel vendor |
| 6007 | BS Signup (dng) | 393674759 | 0xC0071777 | 266568 | 234.01,6.02,232.985 | no vendors row; runtime spawns empty statel vendor |
| 6007 | BS Signup (dng) | 393674760 | 0xC0081777 | 266570 | 236.01,6.02,232.985 | no vendors row; runtime spawns empty statel vendor |
| 6007 | BS Signup (dng) | 393674762 | 0xC00A1777 | 266572 | 240.01,6.02,232.985 | no vendors row; runtime spawns empty statel vendor |
| 6007 | BS Signup (dng) | 393674763 | 0xC00B1777 | 266574 | 241.988,6.02,233.019 | no vendors row; runtime spawns empty statel vendor |
| 6007 | BS Signup (dng) | 393674764 | 0xC00C1777 | 266573 | 244.01,6.02,233.014 | no vendors row; runtime spawns empty statel vendor |
| 6007 | BS Signup (dng) | 393674765 | 0xC00D1777 | 266575 | 246.01,6.02,232.985 | no vendors row; runtime spawns empty statel vendor |
| 6131 | ICC Holodeck Alien Training | 401801216 | 0xC00017F3 | 287476 | 195.672,6.017,130.331 | no vendors row; runtime spawns empty statel vendor |
| 7011 | Freelancers Inc. HQ - Rome | 459472896 | 0xC0001B63 | 285348 | 93.972,2.01,73.734 | no vendors row; runtime spawns empty statel vendor |
| 7012 | Freelancers Inc. HQ - Old Athen | 459538432 | 0xC0001B64 | 284692 | 92.2,2.01,102 | no vendors row; runtime spawns empty statel vendor |
