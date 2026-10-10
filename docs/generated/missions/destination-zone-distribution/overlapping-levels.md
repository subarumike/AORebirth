# Same expected QL across different character levels

All tables describe retained observations, not eligibility rules or destination weights. Expected QL is calculated from the current established lookup; original capture labels remain in the offer ledger. Percentages use resolved offers in the stated group; CSV/JSON also provide retained-offer denominators including unresolved rows. Repeated destinations are counted as separate offers. Missing combinations are UNOBSERVED. A/B/C pool captured secondary-slider settings; those settings remain in the per-offer/context data, and matched-setting comparisons are separate. They are not fully controlled character-level comparisons. Exact identity lists and source offer row IDs are complete in the linked CSV and distribution-data.json.

[Complete comparison CSV, including every shared and one-sided exact entrance identity](overlap-comparisons.csv)

Each row compares observed sets. A one-sided identity is UNOBSERVED in the other sample, not proven excluded. Matching terminal and secondary settings still does not control breed/profession or prove level causation. All captures are Omni; no faction comparison is available. Terminal PF655 is Andromeda; PF800 is Borealis. Destination PF names are listed in the complete zone table in README.md.

## ALL_SECONDARY_SETTINGS

| QL | Levels | Offers left/right | Terminal PF left/right | Secondary bytes | Shared PFs | Left-only PFs | Right-only PFs | Entrances left/right/shared | Geography |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 9 | 7/13 | 1380/1260 | [655]/[655] | MIXED | 687, 695 |  |  | 73/73/73 | SAME_OBSERVED_TERMINAL |
| 10 | 7/13 | 1380/1255 | [655]/[655] | MIXED | 670, 695 |  |  | 85/85/85 | SAME_OBSERVED_TERMINAL |
| 25 | 25/37 | 1255/1255 | [655]/[655] | MIXED | 670, 685, 695, 696 |  | 635 | 121/124/121 | SAME_OBSERVED_TERMINAL |
| 27 | 25/37 | 1255/1255 | [655]/[655] | MIXED | 635, 655, 670, 685, 687, 695, 696, 710, 760 |  |  | 223/221/200 | SAME_OBSERVED_TERMINAL |
| 29 | 35/37 | 1255/1255 | [800]/[655] | MIXED |  | 550, 790, 791, 795, 800 | 635, 655, 670, 685, 687, 695, 696, 710, 760 | 196/215/0 | DIFFERENT_TERMINALS_CONFOUNDED |
| 31 | 35/37 | 1255/1255 | [800]/[655] | MIXED |  | 550, 790, 791, 795, 800 | 635, 655, 670, 685, 687, 695, 696, 710, 760 | 188/224/0 | DIFFERENT_TERMINALS_CONFOUNDED |
| 37 | 25/37 | 1255/1255 | [655]/[655] | MIXED | 635, 655, 670, 685, 695, 696, 760 |  |  | 216/220/209 | SAME_OBSERVED_TERMINAL |
| 44 | 25/37 | 1255/1255 | [655]/[655] | MIXED | 635, 655, 670, 685, 695, 696, 760 |  |  | 204/203/198 | SAME_OBSERVED_TERMINAL |

## SAME_TERMINAL_AND_SECONDARY_SETTINGS

| QL | Levels | Offers left/right | Terminal PF left/right | Secondary bytes | Shared PFs | Left-only PFs | Right-only PFs | Entrances left/right/shared | Geography |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 9 | 7/13 | 5/10 | [655]/[655] | 100, 156, 100, 156, 255, 156 | 687, 695 |  |  | 4/10/0 | SAME_OBSERVED_TERMINAL |
| 9 | 7/13 | 1250/1250 | [655]/[655] | 255, 255, 255, 255, 255, 255 | 687, 695 |  |  | 73/73/73 | SAME_OBSERVED_TERMINAL |
| 10 | 7/13 | 5/5 | [655]/[655] | 100, 156, 100, 156, 255, 156 | 670, 695 |  |  | 4/5/0 | SAME_OBSERVED_TERMINAL |
| 10 | 7/13 | 1250/1250 | [655]/[655] | 255, 255, 255, 255, 255, 255 | 670, 695 |  |  | 85/85/85 | SAME_OBSERVED_TERMINAL |
| 25 | 25/37 | 1250/1250 | [655]/[655] | 100, 156, 100, 156, 255, 156 | 670, 685, 695, 696 |  | 635 | 121/124/121 | SAME_OBSERVED_TERMINAL |
| 25 | 25/37 | 5/5 | [655]/[655] | 255, 255, 255, 255, 255, 255 | 670, 695 | 685 | 635 | 5/5/0 | SAME_OBSERVED_TERMINAL |
| 27 | 25/37 | 1250/1250 | [655]/[655] | 100, 156, 100, 156, 255, 156 | 635, 655, 670, 685, 687, 695, 696, 710, 760 |  |  | 223/221/200 | SAME_OBSERVED_TERMINAL |
| 27 | 25/37 | 5/5 | [655]/[655] | 255, 255, 255, 255, 255, 255 | 685, 710 | 696, 760 | 635, 655 | 5/5/0 | SAME_OBSERVED_TERMINAL |
| 37 | 25/37 | 1250/1250 | [655]/[655] | 100, 156, 100, 156, 255, 156 | 635, 655, 670, 685, 695, 696, 760 |  |  | 216/220/209 | SAME_OBSERVED_TERMINAL |
| 37 | 25/37 | 5/5 | [655]/[655] | 255, 255, 255, 255, 255, 255 | 695, 696, 760 | 655 | 670 | 5/5/0 | SAME_OBSERVED_TERMINAL |
| 44 | 25/37 | 1250/1250 | [655]/[655] | 100, 156, 100, 156, 255, 156 | 635, 655, 670, 685, 695, 696, 760 |  |  | 204/203/198 | SAME_OBSERVED_TERMINAL |
| 44 | 25/37 | 5/5 | [655]/[655] | 255, 255, 255, 255, 255, 255 | 670, 760 | 635 | 695 | 4/5/0 | SAME_OBSERVED_TERMINAL |
