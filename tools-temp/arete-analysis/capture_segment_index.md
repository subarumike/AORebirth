# Arete Capture Segment Index

Generated: 2026-06-15T04:09:07.8868850Z

Scope: capture folders `20260614-194454` through `20260614-221915` under `tools-temp\AOSharpLiveCapture\bin\Debug\captures`.

This index contains file presence and row/line counts only. It does not parse quest chains, generate SQL, or modify game data.

## Summary

- Segments found: `17`
- Clean: `13`
- Usable partial: `2`
- Incomplete: `2`

## Totals

| File | Total rows/lines |
| --- | ---: |
| chat-dialogue.log | 1982 |
| npc-interactions.log | 9140 |
| system-messages.log | 46745 |
| enemy-state.csv | 74780 |
| inventory-updates.csv | 114 |
| vendor-full-updates.csv | 1044 |
| shop-updates.csv | 880 |

## Segment Index

| Folder | Class | capture_info.json | capture-health.json | Health status | Chat | NPC | System | Enemy | Inventory | VendorFull | Shop | Notes |
| --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 20260614-194454 | clean | True | True | complete | 78 | 530 | 2610 | 2766 | 8 | 0 | 0 |  |
| 20260614-195107 | clean | True | True | complete | 124 | 484 | 4752 | 5516 | 0 | 0 | 0 |  |
| 20260614-195725 | clean | True | True | complete | 112 | 454 | 3858 | 5696 | 12 | 32 | 0 |  |
| 20260614-200311 | clean | True | True | complete | 36 | 298 | 1314 | 2667 | 0 | 18 | 0 |  |
| 20260614-200850 | clean | True | True | complete | 78 | 796 | 2732 | 5541 | 0 | 26 | 10 |  |
| 20260614-202500 | usable-partial | True | False |  | 26 | 130 | 268 | 1672 | 0 | 80 | 4 | Raw rows exist, but no final capture-health.json. |
| 20260614-203038 | clean | True | True | complete | 78 | 190 | 624 | 1437 | 0 | 22 | 0 |  |
| 20260614-203631 | usable-partial | True | False |  | 182 | 1438 | 5198 | 9024 | 28 | 228 | 37 | Raw rows exist, but no final capture-health.json. |
| 20260614-205724 | clean | True | True | complete | 242 | 1498 | 6926 | 9634 | 40 | 182 | 39 |  |
| 20260614-211754 | clean | True | True | complete | 140 | 368 | 1297 | 3321 | 4 | 62 | 0 |  |
| 20260614-212335 | incomplete | True | True | incomplete | 122 | 208 | 966 | 1030 | 0 | 0 | 537 | Vendor/shop interactions were observed, but vendor-full-updates.csv has no vendor full-update entries. |
| 20260614-212914 | clean | True | True | complete | 116 | 310 | 1630 | 4182 | 0 | 88 | 243 |  |
| 20260614-213857 | clean | True | True | complete | 50 | 150 | 790 | 776 | 0 | 0 | 0 |  |
| 20260614-214357 | clean | True | True | complete | 98 | 268 | 1050 | 1326 | 0 | 48 | 0 |  |
| 20260614-214819 | clean | True | True | complete | 138 | 704 | 5774 | 10837 | 0 | 122 | 10 |  |
| 20260614-215831 | incomplete | True | True | incomplete | 180 | 928 | 5284 | 6858 | 6 | 84 | 0 | Vendor/shop interactions were observed, but shop-updates.csv has no stock rows. |
| 20260614-221915 | clean | True | True | complete | 182 | 386 | 1672 | 2497 | 16 | 52 | 0 |  |

## Recommended Next Batch Order

1. Clean segments first: `20260614-194454`, `20260614-195107`, `20260614-195725`, `20260614-200311`, `20260614-200850`, `20260614-203038`, `20260614-205724`, `20260614-211754`, `20260614-212914`, `20260614-213857`, `20260614-214357`, `20260614-214819`, `20260614-221915`.
2. Usable partial segments second: `20260614-202500`, `20260614-203631`.
3. Incomplete segments last: `20260614-212335`, `20260614-215831`.

Vendor-specific caution: segments marked incomplete should not be used as primary vendor reconstruction evidence without checking adjacent clean segments for the missing vendor-full or shop stock side.
