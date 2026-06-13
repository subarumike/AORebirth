# Omni-1 Trade Live Vendor Capture Summary

Capture source: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260611-031405
Normalized output: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\vendor-captures\omni-trade-live-scan\20260611-031405

## User-Reported Location

- Area: Trade District
- Playfield/resource: 710
- Coordinates: 432.1, 361.8, 17.0
- Time: 2026-06-11 08:17:03 UTC
- Screenshot: C:\Users\Mike\AppData\Local\Temp\codex-clipboard-d1cb765a-339a-403f-9615-5d1b4ecabb48.png
- Visible target name from screenshot: OT Tailor

## Capture Counts

- Vendor full-update rows: 2
- Shop-update item rows: 18
- Unique shop identities in ShopUpdate: 1
- Nearby NPC observations: 7

## Shop Updates

| Terminal identity | Item rows | Unique low/high pairs | QL range | Resolved rows | Candidate live vendor |
| --- | ---: | ---: | --- | ---: | --- |
| (VendingMachine:12C4C379) | 18 | 18 | 1-1 | 18 | OT Tailor / nearby Omni-1 Trade NPC vendor candidate |

## Inventory Items

| Slot | LowId | HighId | QL | Name |
| ---: | ---: | ---: | ---: | --- |
| 0 | 27377 | 27377 | 1 | Black Boots |
| 1 | 31506 | 31506 | 1 | Black Omni-Tek Commander Cloak |
| 2 | 27378 | 27378 | 1 | Black Pants |
| 3 | 27379 | 27379 | 1 | Black Shirt |
| 4 | 27380 | 27380 | 1 | Black Sleeves |
| 5 | 27367 | 27367 | 1 | Omni Executive Suit Boots |
| 6 | 27368 | 27368 | 1 | Omni Executive Suit Pants |
| 7 | 27369 | 27369 | 1 | Omni Executive Suit Shirt |
| 8 | 27370 | 27370 | 1 | Omni Executive Suit Sleeves |
| 9 | 27373 | 27373 | 1 | Omni InternOp Suit Boots |
| 10 | 27371 | 27371 | 1 | Omni InternOp Suit Pants |
| 11 | 27387 | 27387 | 1 | Omni-Med Female Suit Shirt |
| 12 | 27382 | 27382 | 1 | Omni-Med Suit Boots |
| 13 | 27381 | 27381 | 1 | Omni-Med Suit Gloves |
| 14 | 27384 | 27384 | 1 | Omni-Med Suit Skirt |
| 15 | 27385 | 27385 | 1 | Omni-Med Suit Sleeves |
| 16 | 27386 | 27386 | 1 | Omni-Med Suit Trousers |
| 17 | 31515 | 31515 | 1 | Red Omni-Tek Commander Cloak |

## Nearby NPC Evidence

| Identity | Name | Level | Position |
| --- | --- | ---: | --- |
| SimpleChar:77B09D66 | Elliot Fairlane | 200 | 449.1163, 17.01, 353.8833 |
| SimpleChar:77B09D6E | Unicorn Squadleader | 250 | 419.7733, 17.01, 365.5539 |
| SimpleChar:77B09D7B | OT Tailor | 193 | 433.2403, 17.01, 360.1576 |
| SimpleChar:77B09D82 | OT Food Provider | 186 | 412.6686, 17.01, 352.1418 |
| SimpleChar:77B09D87 | Lieutenant Antonio Neal | 220 | 410.6783, 17.01, 394.0645 |
| SimpleChar:780B61ED | OT Computer Merchant | 188 | 428.8648, 17.01, 391.0351 |
| SimpleChar:78948D42 | Unicorn Squadleader | 250 | 455.1208, 17.01, 394.3912 |

## Notes

- This capture proves NPC/vendor interactions can emit `ShopUpdate` rows under a `VendingMachine` identity even when the visible live target is an NPC.
- The `ShopUpdate` identity was `(VendingMachine:12C4C379)` with 18 stock rows.
- The nearby visible NPC from the screenshot was `OT Tailor`; nearby packet-observed NPCs also included `OT Food Provider`, `Unicorn Squadleader`, and `Lieutenant Antonio Neal`.
- The only `VendingMachineFullUpdate` rows in this capture were for `(VendingMachine:12C4C380)` / template `120973`; the shop update itself used `(VendingMachine:12C4C379)`, so direct full-update correlation for the opened stock remains incomplete.
- Treat this as evidence for live shop/NPC capture workflow, not as a source SQL patch plan yet.
