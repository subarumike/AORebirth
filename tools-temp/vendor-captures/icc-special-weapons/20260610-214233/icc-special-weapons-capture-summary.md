# ICC Special Weapons Capture Summary

Capture folder: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260610-214233
Normalized output folder: C:\Users\Mike\Documents\Cellao-Clean\tools-temp\vendor-captures\icc-special-weapons\20260610-214233

## Scope

- Required terminals captured: Basic ICC Special Weapons, Advanced ICC Special Weapons, Superior ICC Special Weapons.
- Optional comparison terminals captured: ICC Ammunition, ICC Martial Arts Attacks.
- No source, database, vendor template, or inventory SQL changes were made by this normalization step.

## Capture Counts

- Vendor-full-update rows for the five terminals: 10
- Shop-update bursts for the five terminals: 5
- Shop-update item rows for the five terminals: 210
- Required Special Weapons item rows: 113

## Terminal Summary

| Terminal | Statel ID | Template ID | Captured identity | Position | Rows | Unique pairs | QL range | Resolved rows |
| --- | ---: | ---: | --- | --- | ---: | ---: | --- | ---: |
| ICC Ammunition | 135004160 | 297459 | (VendingMachine:12E38D1B) | 173,5.01,167 | 13 | 13 | 1-1 | 13 |
| ICC Martial Arts Attacks | 135004161 | 297466 | (VendingMachine:12E38D1C) | 164.96637,5.01,167 | 84 | 28 | 8-300 | 84 |
| Superior ICC Special Weapons | 135004162 | 297470 | (VendingMachine:12E38D1D) | 167,5.00979328,157 | 52 | 40 | 200-297 | 52 |
| Basic ICC Special Weapons | 135004163 | 99572 | (VendingMachine:12E38D1E) | 171,5.01,157 | 31 | 30 | 1-93 | 31 |
| Advanced ICC Special Weapons | 135004164 | 99573 | (VendingMachine:12E38D1F) | 169,5.01,157 | 30 | 30 | 101-200 | 30 |

## Inventory Overlap

- ICC Ammunition vs ICC Martial Arts Attacks: 0 shared low/high pairs
- ICC Ammunition vs Superior ICC Special Weapons: 0 shared low/high pairs
- ICC Ammunition vs Basic ICC Special Weapons: 0 shared low/high pairs
- ICC Ammunition vs Advanced ICC Special Weapons: 0 shared low/high pairs
- ICC Martial Arts Attacks vs Superior ICC Special Weapons: 0 shared low/high pairs
- ICC Martial Arts Attacks vs Basic ICC Special Weapons: 0 shared low/high pairs
- ICC Martial Arts Attacks vs Advanced ICC Special Weapons: 0 shared low/high pairs
- Superior ICC Special Weapons vs Basic ICC Special Weapons: 0 shared low/high pairs
- Superior ICC Special Weapons vs Advanced ICC Special Weapons: 0 shared low/high pairs
- Basic ICC Special Weapons vs Advanced ICC Special Weapons: 0 shared low/high pairs

## Interpretation

- The three ICC Special Weapons terminals are not a single shared inventory: each captured terminal has its own item rows and no shared low/high item pairs with the other Special Weapons terminals in this capture.
- The captured QL bands line up with a basic/advanced/superior progression: Basic 1-93, Advanced 101-200, Superior 200-297 in this session.
- The optional ICC Ammunition terminal matches the known static QL 1 shape and is useful as a control.
- ICC Martial Arts Attacks is a separate comparison inventory, not part of the Special Weapons family.

## Next Step

Use this evidence to design review-only SQL rows for three new vendortemplate entries and three separate shop inventory hashes for templates 99572, 99573, and 297470. Do not reuse existing generic weapon hashes without a separate review.
