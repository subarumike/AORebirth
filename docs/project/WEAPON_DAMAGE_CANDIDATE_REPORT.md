# Weapon Damage Candidate Report

Current result: no candidate formula is proven.

First controlled campaign status: prepared/operator-pending. The selected primary fixture is QL1 Solar-Powered Pistol `121567`; the optional confirmation fixture is QL1 Worn Oak Bo `121565`; the first target fixture is an isolated private-server `Malfunctioning Cleaning Robot` accepted only when diagnostics prove normal-hit status, target AC, health continuity, and no overlap.

The framework now represents candidate families needed for ordinary weapon-hit parity:

| Area | Represented candidates | Production status |
| --- | --- | --- |
| AR ordering | base plus truncated AR, full expression truncation, explicit multiplier | inactive |
| AC ordering | none, subtract AC/10 before floor, subtract after floor, critical interaction tags | inactive |
| Minimum floor | after-AC floor and disabled/report-only alternatives | inactive |
| Critical | max-plus-bonus, roll-plus-bonus, scaled/unscaled bonus, AC-reduced bonus, critical floor tag | inactive |
| Add damage | before/after/floor-adjacent tags, AR-scaled and unscaled | inactive |
| AMSCap | missing no cap, zero no cap, zero literal zero, negative invalid, cap before post-1000 | inactive |

The synthetic fixture set covers evaluator behavior for templates `121567`, `121565`, `100240`, and `121572`, but those rows are `CONTROLLED_TEST_CONFIRMED` controls only. They are not live observations and do not prove AO formula behavior.

The captured Subway Thief row is `PROVEN_CAPTURED_BEHAVIOR` for fixed `9` damage only. It remains a bounded fixed-damage contract and must not be generalized into ordinary weapon formula math.

Known unresolved blockers:

- critical bonus source and hit-state seam are still unavailable for ordinary callers;
- AMSCap absence and zero semantics are unresolved; a read-only `items.dat` audit found `17,574` min/max weapon templates, with `7,388` missing stat `538`, `10,186` positive stat `538`, and no zero or negative stat `538` rows;
- target armor stats are available as stats, but caller timing and exact formula semantics remain partial;
- Add All Off ordering relative to weighted skills is unproven;
- universal add-damage source is not proven;
- no complete live ordinary weapon-hit observation matrix exists yet.
- first campaign has not collected observations yet; candidate matching remains pending.
