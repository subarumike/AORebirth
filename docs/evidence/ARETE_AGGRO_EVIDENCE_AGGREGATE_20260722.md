# Aggregate Arete NPC-first Aggro Evidence

This deterministic aggregate consumes the complete per-capture attack-event projections for the two recovered July 22 Arete captures. Exact NPC metadata constraints are reconciled before runtime promotion.

## Reconciliation

- `20260722-104809`: **18** projected enemy-to-player starts; **18** unique.
- `20260722-152454`: **51** projected enemy-to-player starts; **51** unique.
- All projected enemy-to-player starts: **69**.
- Unique projected starts: **69**.
- Exact duplicate projections collapsed: **0**.
- NPC-first starts proving automatic-aggro eligibility: **50**.
- Exact metadata constraints: **19**.
- Constraints with a measured direct lower bound: **14**.
- Eligibility-only constraints with no invented radius: **5**.
- Captured playfield namespace: **1044525**; runtime content playfield: **6553**.

## Promoted constraints

| Exact NPC constraint | NPC-first starts | Eligibility | Measured direct lower bound (m) | Radius evidence | Contributing captures |
| --- | ---: | --- | ---: | --- | --- |
| Angry Minibull (family=42, template=30360, level=8, captured-pf=1044525) | 2 | proven | none | eligibility only | 20260722-152454 |
| Angry Minibull (family=42, template=30360, level=9, captured-pf=1044525) | 2 | proven | 16.403593 | `20260722-152454` sequence 64480 | 20260722-152454 |
| Angry Minibull (family=42, template=30360, level=10, captured-pf=1044525) | 2 | proven | 16.225999 | `20260722-152454` sequence 63131 | 20260722-152454 |
| Angry Minibull (family=42, template=30360, level=12, captured-pf=1044525) | 2 | proven | 13.393116 | `20260722-152454` sequence 67556 | 20260722-152454 |
| Angry Minibull (family=42, template=30360, level=13, captured-pf=1044525) | 3 | proven | 16.192275 | `20260722-152454` sequence 62632 | 20260722-152454 |
| Cleanmeister Intelligence Robot (family=1019, template=297023, level=2, captured-pf=1044525) | 1 | proven | 0.847455 | `20260722-104809` sequence 60201 | 20260722-104809 |
| Desert Reet (family=53, template=30365, level=5, captured-pf=1044525) | 2 | proven | 1.582614 | `20260722-152454` sequence 35700 | 20260722-152454 |
| Desert Reet (family=53, template=30365, level=6, captured-pf=1044525) | 4 | proven | 23.167874 | `20260722-152454` sequence 35489 | 20260722-152454 |
| Garbage Flea (family=25, template=17657, level=1, captured-pf=1044525) | 3 | proven | none | eligibility only | 20260722-104809 |
| Garbage Flea (family=25, template=17657, level=2, captured-pf=1044525) | 1 | proven | 15.576482 | `20260722-104809` sequence 42087 | 20260722-104809 |
| Gnarl the Roller (family=55, template=17687, level=7, captured-pf=1044525) | 2 | proven | none | eligibility only | 20260722-152454 |
| Kneebreaker Alfonzo Rizzolo (family=137, template=165196, level=4, captured-pf=1044525) | 1 | proven | none | eligibility only | 20260722-152454 |
| Lolly the Reet (family=137, template=30365, level=10, captured-pf=1044525) | 1 | proven | 56.243689 | `20260722-152454` sequence 35955 | 20260722-152454 |
| Robotic Guard Dog (family=1019, template=17720, level=13, captured-pf=1044525) | 2 | proven | 10.535007 | `20260722-152454` sequence 77288 | 20260722-152454 |
| Rollerrat (family=55, template=17687, level=5, captured-pf=1044525) | 9 | proven | 16.639269 | `20260722-152454` sequence 40853 | 20260722-152454 |
| Rollerrat (family=55, template=17687, level=6, captured-pf=1044525) | 9 | proven | 18.747485 | `20260722-152454` sequence 41171 | 20260722-152454 |
| Supreme Collector of Waste (family=1019, template=17714, level=4, captured-pf=1044525) | 2 | proven | 9.240783 | `20260722-152454` sequence 12237 | 20260722-104809, 20260722-152454 |
| Violent Protester (family=103, template=203740, level=3, captured-pf=1044525) | 1 | proven | none | eligibility only | 20260722-152454 |
| Waste Collector (family=1019, template=17714, level=2, captured-pf=1044525) | 1 | proven | 1.332759 | `20260722-104809` sequence 82253 | 20260722-104809 |

## Runtime boundary

- Every listed constraint has at least one exact NPC-first attack packet and is therefore eligible for automatic aggro.
- A direct distance is a measured lower bound, not an inferred full sight radius or probability.
- Duplicate exact metadata constraints are collapsed by summing distinct NPC-first starts and selecting the strongest measured lower bound with a deterministic tie-break.
- Player-first attacks remain in the per-capture event projections but do not establish automatic-aggro eligibility.
- Eligibility-only rows are queryable by runtime, while radius lookup fails closed until a direct lower bound exists.
