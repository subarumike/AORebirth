# Hardware Dimension - Basic Capture Summary

Capture source: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260610-232330

Normalized output folder: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\vendor-captures\hardware-dimension-basic\20260610-232330

## Session

- Capture start UTC: 2026-06-11T04:23:30.0795288Z
- Local capture start: 2026-06-10T23:23:30.0795288-05:00
- AO client: Anarchy Online - Youwillnuke, PID 479984
- Vendor full-update rows: 8
- Shop update rows: 89
- Unique terminal identities: 4

## Captured Terminals

| VendorId | Template | Terminal | Existing Hash | Rows | Unique Pairs | QL Range | Resolved Rows | Already Mapped |
| --- | --- | --- | --- | ---: | ---: | --- | ---: | --- |
| 299040768 | 99601 | Basic Tools | ToolB | 19 | 19 | 1-50 | 19 | True |
| 299040769 | 99570 | Basic Armor |  | 29 | 25 | 1-50 | 29 | False |
| 299040770 | 99572 | Basic ICC Special Weapons | ICCSpWB | 31 | 31 | 1-91 | 31 | True |
| 299040771 | 99602 | Basic Attacks | AtkB | 10 | 8 | 10-100 | 10 | True |

## Inventory Overlap

- Basic Tools (99601) vs Basic Armor (99570): 0 shared low/high pairs
- Basic Tools (99601) vs Basic ICC Special Weapons (99572): 0 shared low/high pairs
- Basic Tools (99601) vs Basic Attacks (99602): 0 shared low/high pairs
- Basic Armor (99570) vs Basic ICC Special Weapons (99572): 0 shared low/high pairs
- Basic Armor (99570) vs Basic Attacks (99602): 0 shared low/high pairs
- Basic ICC Special Weapons (99572) vs Basic Attacks (99602): 0 shared low/high pairs

## Findings

- The capture was taken in Hardware Dimension - Basic, matched by template and coordinates to playfield 4563.
- Three captured terminals already have vendor mappings: Basic Tools -> ToolB, Basic ICC Special Weapons -> ICCSpWB, and Basic Attacks -> AtkB.
- Basic Armor (template 99570, vendor 299040769) is unmapped in current coverage and now has direct live inventory evidence: 29 captured rows, 25 unique low/high pairs, QL 1-50.
- No captured low/high item pair overlap was found between Basic Armor and the neighboring captured terminal families.
- The captured Basic Armor evidence appears sufficient to design a future Basic Armor vendor template and inventory patch, subject to review of vendor rotation expectations.
- Exact-template future mapping opportunities for template 99570 currently include 4563 Hardware Dimension - Basic, 500 Parnassos, and 565 Newland Desert.

## Generated Files

- hardware-dimension-basic-terminal-summary.csv
- hardware-dimension-basic-inventory-items.csv
- hardware-dimension-basic-capture-summary.md
