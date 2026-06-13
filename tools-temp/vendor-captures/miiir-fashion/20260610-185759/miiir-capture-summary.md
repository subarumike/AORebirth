# Miiir Fashion Live Capture Normalization

Source capture: `C:\Users\Mike\Documents\Cellao-Clean\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260610-185759`

Generated evidence files:

- `miiir-terminal-summary.csv`
- `miiir-inventory-items.csv`
- `miiir-capture-summary.md`

## Summary

- Captured terminals: 8
- Captured inventory rows: 401
- Unique exact item pairs: 391
- Resolved item rows: 401
- Unresolved item rows: 0
- All captured item rows were QL 1.
- No shop hash or price field was present in the decoded capture.

## Terminal Summary

| Template | Terminal family | Terminal identity | Playfield | Position | Rows | Unique items | QL range |
| ---: | --- | --- | ---: | --- | ---: | ---: | --- |
| 99543 | Miiir Armwear | `(VendingMachine:12E3A07A)` | 1042439 | 142.999832,5.01014376,201.282928 | 75 | 75 | 1-1 |
| 99544 | Miiir Footwear | `(VendingMachine:12E3A075)` | 1042439 | 143.000671,5.01102734,198.99852 | 57 | 57 | 1-1 |
| 99545 | Miiir Legwear | `(VendingMachine:12E3A077)` | 1042439 | 142.999832,5.009972,203.282928 | 79 | 79 | 1-1 |
| 99546 | Miiir Handwear | `(VendingMachine:12E3A076)` | 1042439 | 142.999832,5.0100255,195.357925 | 10 | 10 | 1-1 |
| 99547 | Miiir Swimwear | `(VendingMachine:12E3A078)` | 1042439 | 158.999878,5.00999,196.0102 | 37 | 37 | 1-1 |
| 99548 | Miiir Chestwear | `(VendingMachine:12E3A079)` | 1042439 | 142.999832,5.009972,197.282928 | 79 | 79 | 1-1 |
| 99550 | Miiir Backwear | `(VendingMachine:12E3A07B)` | 1042439 | 159.000122,5.00992155,201.95488 | 40 | 40 | 1-1 |
| 99554 | Miiir Headwear | `(VendingMachine:12E3A074)` | 1042439 | 158.999985,5.01022863,198.998566 | 24 | 24 | 1-1 |

## Inventory Overlap

| Terminal A | Terminal B | Shared exact item pairs | Shared names |
| --- | --- | ---: | --- |
| Miiir Backwear (99550) | Miiir Headwear (99554) | 5 | Aquaan Trenchcoat Hood; Lava Trenchcoat Hood; Nanofreak Trenchcoat Hood; Sabretooth Trenchcoat Hood; White Trenchcoat Hood |
| Miiir Footwear (99544) | Miiir Legwear (99545) | 4 | Blue Fret Thigh High Boots; Red Fret Thigh High Boots; Red Twil Thigh High Boots; Yellow Fret Thigh High Boots |
| Miiir Legwear (99545) | Miiir Backwear (99550) | 1 | Black Trenchskirt |

## Family Analysis

The captured terminals resolve to eight Miiir body-slot families. The inventories are not one shared shop duplicated across all terminals; they are split by body slot/template. Exact item overlap is limited to explicit shared cosmetic accessories where present, while each terminal has its own item list and count.

This supports future creation of separate Miiir vendor templates and shop inventory groups, pending review of the normalized CSV rows. It does not by itself define final shop hashes; those should be chosen deliberately during a later SQL planning pass.

## Name Resolution

Item names were resolved against `CellAO/Libraries/Source/CellAO.Database/SqlTables/itemnames.sql`. Rows with unresolved names remain in `miiir-inventory-items.csv` with `NameStatus=unresolved`.

## Recommended Next Step

Review `miiir-terminal-summary.csv` and `miiir-inventory-items.csv`, then create a narrow patch plan for new Miiir `vendortemplate` and `shopinventorytemplates` rows only if the terminal/category split is accepted.
