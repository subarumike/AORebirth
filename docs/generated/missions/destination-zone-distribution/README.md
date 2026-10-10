# Observed mission destination-zone distribution

Evidence reconstruction on `codex/mission-destination-integration`. Runtime repair `73fc97a75` is preserved.

All tables describe retained observations, not eligibility rules or destination weights. Expected QL is calculated from the current established lookup; original capture labels remain in the offer ledger. Percentages use resolved offers in the stated group; CSV/JSON also provide retained-offer denominators including unresolved rows. Repeated destinations are counted as separate offers. Missing combinations are UNOBSERVED. A/B/C pool captured secondary-slider settings; those settings remain in the per-offer/context data, and matched-setting comparisons are separate. They are not fully controlled character-level comparisons. Exact identity lists and source offer row IDs are complete in the linked CSV and distribution-data.json.

## Reports and datasets

- [A. Character level -> slider -> destination zones](A-character-level-slider-zones.md)
- [B. Expected mission QL -> destination zone distribution](B-mission-ql-zone-distribution.md)
- [C. Destination zone -> observed character levels](C-zone-character-levels.md)
- [Overlapping-level comparisons](overlapping-levels.md)
- [Complete aggregate JSON](distribution-data.json), [exact entrance observations CSV](entrance-observations.csv), [source manifest](source-manifest.json)
- All 93,185 offers: [CSV gzip](offer-records.csv.gz), [JSON Lines gzip](offer-records.jsonl.gz). Both are UTF-8 and losslessly compressed.
- [Original condition metadata JSON Lines gzip](capture-contexts.jsonl.gz), [unresolved offers](unresolved-offers.csv), [QL label differences](ql-label-differences.csv), [validation](validation.json)

## Method and traceability

Read all 77 original event journals, verify their pinned source hashes, pair each captured request/cohort, and validate original response packet bytes and exact offer boundaries with the existing offline decoder. The retained audit inventories are reconciliation controls, not the aggregation inputs. Destination PF/XYZ comes from raw WorldPos and joins the exact placement by PF and all three binary32 coordinate bits; the full entrance identity is the result of that proven placement join, not a guessed wire field. Missing raw packets stay unresolved.

`source_offer_row_ids` indexes `offer_row_id` in either offer ledger. Each ledger row retains session, source line, request/cohort/offer index, original source-row and packet hashes, offer byte range, WorldPos offset and context ID. The source manifest identifies each logical capture and original file hash. `context_id` links complete sanitized original condition metadata; raw packet blobs and local absolute filesystem paths are not copied into this report. Exact entrance IDs use eight hexadecimal digits for both type and unsigned instance. Terminal location and destination location are separate fields throughout.

Easy/Hard is the captured detent 1-11 (1 easiest, 6 center, 11 hardest); no invented physical slider percentage is assigned. Secondary slider bytes are preserved as captured, including raw center 255. Expected QL is not decoded live mission QL.

Zone display names use the public Clicksaver `all_name`; original aliases remain in the manifest. PF635 also has the `tiny_name` alias Stret East Bank GOLD; that naming difference does not split its identity.

## Original mission terminal locations

| Terminal zone | Terminal PF | Terminal identity | Captured terminal XYZ | Character levels | Retained offers |
| --- | --- | --- | --- | --- | --- |
| Andromeda | 655 | 0000DAC1:C000028F | {"x":3236.463,"y":35.11,"z":921.2086} | 2, 7, 13, 25, 37 | 79380 |
| Borealis | 800 | 0000DAC1:C0000320 | {"x":632.6141,"y":72.80022,"z":545.5198} | 35 | 13805 |

These are original captured terminal snapshots, not destination coordinates or the later local Shade test. Request-time snapshots are used where recorded; older records retain their session snapshot provenance in the context data.

## Complete observed level-to-zone summary

| Character level | Resolved / unresolved offers | Terminal PF | Captured positions | Destination zones |
| --- | --- | --- | --- | --- |
| 2 | 16795 / 355 | 655 | 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 | Galway Shire, Lush Fields, Omni-1 Trade |
| 7 | 23315 / 0 | 655 | 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 | Clondyke, Galway Shire, Lush Fields |
| 13 | 11305 / 0 | 655 | 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 | Clondyke, Galway County, Galway Shire, Lush Fields, Mutant Domain |
| 25 | 13805 / 0 | 655 | 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 | Stret East Bank, Andromeda, Clondyke, Galway County, Galway Shire, Lush Fields, Mutant Domain, Omni-1 Trade, 4 Holes |
| 35 | 13805 / 0 | 800 | 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 | West Athens, Athen Shire, Wailing Wastes, Aegean, Wartorn Valley, Varmint Woods, Stret East Bank, Upper Stret East Bank, 4 Holes, Stret West Bank, Holes in the Wall, The Longest Road, Borealis |
| 37 | 13805 / 0 | 655 | 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 | Milky Way, Pleasant Meadows, Stret East Bank, Andromeda, Clondyke, Galway County, Galway Shire, Lush Fields, Mutant Domain, Omni-1 Trade, 4 Holes |

[Level summary CSV](level-summary.csv). Report A expands every level into every observed detent and zone.

## Complete observed zone summary

| PF | Destination zone | Offers | Entrances | Character levels | Observed QL min-max | Exact observed QLs |
| --- | --- | --- | --- | --- | --- | --- |
| 545 | West Athens | 284 | 8 | 35 | 24-45 | 24, 26, 38, 42, 45 |
| 550 | Athen Shire | 2441 | 18 | 35 | 24-45 | 24, 26, 28, 29, 31, 35, 38, 42, 45 |
| 551 | Wailing Wastes | 705 | 42 | 35 | 45-62 | 45, 52, 62 |
| 585 | Aegean | 837 | 14 | 35 | 38-62 | 38, 42, 45, 52, 62 |
| 586 | Wartorn Valley | 61 | 1 | 35 | 52-62 | 52, 62 |
| 600 | Varmint Woods | 17 | 1 | 35 | 62-62 | 62 |
| 625 | Milky Way | 403 | 76 | 37 | 55-66 | 55, 66 |
| 630 | Pleasant Meadows | 222 | 63 | 37 | 66-66 | 66 |
| 635 | Stret East Bank | 1688 | 25 | 25, 35, 37 | 25-66 | 25, 27, 29, 30, 31, 32, 33, 37, 40, 44, 48, 55, 62, 66 |
| 650 | Upper Stret East Bank | 198 | 5 | 35 | 52-62 | 52, 62 |
| 655 | Andromeda | 2894 | 56 | 25, 37 | 27-55 | 27, 29, 30, 31, 32, 33, 37, 40, 44, 48, 55 |
| 670 | Clondyke | 12855 | 82 | 7, 13, 25, 37 | 10-66 | 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 25, 27, 29, 30, 31, 32, 33, 37, 40, 44, 48, 55, 66 |
| 685 | Galway County | 3431 | 13 | 13, 25, 37 | 18-66 | 18, 19, 20, 21, 22, 23, 25, 27, 29, 30, 31, 32, 33, 37, 40, 44, 48, 55, 66 |
| 687 | Galway Shire | 6498 | 4 | 2, 7, 13, 25, 37 | 2-66 | 2, 3, 4, 5, 6, 7, 8, 9, 23, 27, 29, 30, 31, 66 |
| 695 | Lush Fields | 38192 | 113 | 2, 7, 13, 25, 37 | 1-66 | 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 25, 27, 29, 30, 31, 32, 33, 37, 40, 44, 48, 55, 66 |
| 696 | Mutant Domain | 1865 | 6 | 13, 25, 37 | 18-66 | 18, 19, 20, 21, 22, 23, 25, 27, 29, 30, 31, 32, 33, 37, 40, 44, 55, 66 |
| 710 | Omni-1 Trade | 8128 | 60 | 2, 25, 37 | 1-31 | 1, 27, 29, 30, 31 |
| 760 | 4 Holes | 3340 | 15 | 25, 35, 37 | 27-62 | 27, 29, 30, 31, 32, 33, 37, 40, 44, 48, 52, 55, 62 |
| 790 | Stret West Bank | 2664 | 125 | 35 | 24-62 | 24, 26, 28, 29, 31, 35, 38, 42, 45, 52, 62 |
| 791 | Holes in the Wall | 2145 | 24 | 35 | 24-45 | 24, 26, 28, 29, 31, 35, 38, 42, 45 |
| 795 | The Longest Road | 2802 | 54 | 35 | 24-62 | 24, 26, 28, 29, 31, 35, 38, 42, 45, 52, 62 |
| 800 | Borealis | 1160 | 7 | 35 | 24-35 | 24, 26, 28, 29, 31, 35 |

## Validation and evidence gaps

- Retained offers: 93,185; raw-backed exact destinations: 92,830; unresolved missing-raw offers: 355.
- Exactly 812 observed entrance identities and 22 destination playfields. Every resolved offer occurs once in each relevant view; unresolved rows occur once in A/B and their ledger. Views are not added together.
- Original source manifest: 77 logical sessions, 18,642 requests, 18,638 cohorts; one empty cohort and four requests without cohorts are retained in source/context evidence, not fabricated into offers.
- Repeated entrances within or across cohorts remain distinct observations; source copies are not additional experiments.
- 10 offers have differing original/current expected-QL labels. Both labels are retained; see the complete difference CSV. This changes only report grouping, never runtime catalogs or historical records.
- 355 offers lack an original static-QL label. An operator planner target is retained separately and is not promoted to a decoded or static QL. Current expected QL is calculated only from the original level/detent and established lookup.
- The 355 missing-raw offers have level2 in their original session headers. The old eligibility inventory lost that fallback because it inserted a null request-level key. This new extraction preserves the original level and records each historical metadata discrepancy in source-manifest.json; all 355 destinations remain unresolved.
- Level35 is captured at Borealis (PF800); levels2/7/13/25/37 at Andromeda (PF655). Cross-terminal comparisons cannot isolate character level. Other metadata differences also remain confounders.
- UNOBSERVED cells and one-sided sets are missing coverage, not exclusions. QL min/max does not establish eligibility of every intervening QL. Frequencies reflect this deliberately sampled corpus, not Funcom probabilities.
- Original live response QL, independent faction controls and complete level/terminal/secondary controls remain absent. No additional captures were performed.
- Runtime mission generation, DAO/schema, GameData, services and the AO client were not changed or restarted for this task.

## Reproduce

From the repository root, with the original retained captures available:

```cmd
cmd /d /c Tools\mission_destination_distribution.cmd
cmd /d /c Tools\mission_destination_distribution.cmd --check
```

The check repeats original-source verification and requires every output byte to match. Source paths are resolved from the pinned manifest and retained repository journals; no live capture is started.
