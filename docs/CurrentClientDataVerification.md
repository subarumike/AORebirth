# Current Client Data Verification

Generated: 2026-06-14 02:34:33

## Scope

- AO client root: C:\Funcom\Anarchy Online
- AO client version.id: 18.8.62_EP1
- AO Rebirth repo: C:\Users\Mike\Documents\AORebirth
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
| Vending statels without complete DB shop coverage | 26 |
| Vending statels excluded from coverage | 169 |

## Latest Vendor Import Milestone

- Freelancers Inc. HQ - Rome Agency Shop import promoted from AOSharp capture 20260614-022639.
- Validated coverage added: 1 `7011 Freelancers Inc. HQ - Rome` vendor row, 1 vendor template, and 1 new shop inventory group with 26 inventory rows.
- The imported row covers Agency Shop template `285348` at X 93.972 Y 2.01 Z 73.734 with direct VendorFull and ShopUpdate evidence; no incidental vendors were imported.
- Current-client verification after import showed actionable uncovered statel vendors dropped from 27 to 26. Current coverage/actionability chain: 404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96 -> 93 -> 89 -> 29 -> 27 -> 26.

## Vendor Coverage Campaign Freeze

Status: COMPLETE (LIVE COVERAGE).

Vendor coverage campaign complete for all practical live-accessible vendors. Remaining vendors require setup-specific access and are deferred.

Final state:

- Current uncovered count: 26.
- Covered: all practical live-accessible vendors reached during the campaign.
- Deferred: access-restricted, setup-specific, profession-locked, sided, special-location, or current-client divergence vendors.
- No SQL was generated for this freeze.
- No capture was run for this freeze.
- Existing mappings were not modified.

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
3. Deferred statel vendors with no DB row remain visible for audit, but they are not active capture work unless setup-specific access is intentionally prepared.
4. Shop inventory item IDs resolve in current item cache.

## Report Files

- Data file audit: C:\Users\Mike\Documents\AORebirth\tools-temp\current-client-data-verification\data-file-version-audit.csv
- Live vendor mesh audit: C:\Users\Mike\Documents\AORebirth\tools-temp\current-client-data-verification\live-vendor-mesh-audit.csv
- Vendor DB audit: C:\Users\Mike\Documents\AORebirth\tools-temp\current-client-data-verification\vendor-db-audit.csv
- Shop inventory audit: C:\Users\Mike\Documents\AORebirth\tools-temp\current-client-data-verification\shop-inventory-template-audit.csv
- Statel vendor coverage: C:\Users\Mike\Documents\AORebirth\tools-temp\current-client-data-verification\statel-vendor-coverage.csv
- Vendor scan door locations: C:\Users\Mike\Documents\AORebirth\tools-temp\current-client-data-verification\vendor-scan-door-locations.csv

## Top Vendor DB Issues

No vendor DB issues found.

## Deferred Vendor Backlog

These statels remain in raw verification output as missing vendor rows, but they are deferred from active capture/import planning because they require setup-specific access.

| Category | Playfield | Name | VendorId | TemplateId | Statel | Coords | Reason blocked | Required setup |
| --- | ---: | --- | ---: | ---: | --- | --- | --- | --- |
| Clan-only vendors | 665 | Broken Shores | 43581441 | 99522 | 0xC0010299 | 729.875,22.01,1541.501 | Clan-side shop access friction; not practical from current Omni-focused sweep. | Leveled/access-capable Clan character. |
| Clan-only vendors | 952 | Clan Training | 62390272 | 100034 | 0xC00003B8 | 48,14,62 | Clan starter/training access requires a Clan character in the correct area. | Clan character with access to Clan Training. |
| Clan-only vendors | 1426 | Clan Registration dng | 93454336 | 25885 | 0xC0000592 | 166.991,5.01,235.899 | Clan registration interior; outside current Omni/non-swap scope. | Clan character and registration interior access. |
| Clan-only vendors | 1426 | Clan Registration dng | 93454337 | 81799 | 0xC0010592 | 171.952,5.01,238.745 | Clan registration interior; outside current Omni/non-swap scope. | Clan character and registration interior access. |
| Clan-only vendors | 7012 | Freelancers Inc. HQ - Old Athen | 459538432 | 284692 | 0xC0001B64 | 92.2,2.01,102 | Old Athen/Clan-side Freelancers access requires separate Clan setup. | Clan character able to reach Old Athen Freelancers HQ. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674752 | 266562 | 0xC0001777 | 220.01,6.02,232.985 | OFAB terminal is profession-locked. | Adventurer character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674753 | 266563 | 0xC0011777 | 222,6.02,232.985 | OFAB terminal is profession-locked. | Agent character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674754 | 266569 | 0xC0021777 | 224.027,6.02,232.985 | OFAB terminal is profession-locked. | Bureaucrat character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674755 | 266564 | 0xC0031777 | 226.01,6.02,232.982 | OFAB terminal is profession-locked. | Doctor character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674756 | 266565 | 0xC0041777 | 228.01,6.02,232.985 | OFAB terminal is profession-locked. | Enforcer character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674757 | 266566 | 0xC0051777 | 230.01,6.02,232.985 | OFAB terminal is profession-locked. | Engineer character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674758 | 266567 | 0xC0061777 | 232.01,6.02,232.985 | OFAB terminal is profession-locked. | Fixer character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674759 | 266568 | 0xC0071777 | 234.01,6.02,232.985 | OFAB terminal is profession-locked. | Keeper character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674760 | 266570 | 0xC0081777 | 236.01,6.02,232.985 | OFAB terminal is profession-locked. | Martial Artist character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674762 | 266572 | 0xC00A1777 | 240.01,6.02,232.985 | OFAB terminal is profession-locked. | Nano-Technician character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674763 | 266574 | 0xC00B1777 | 241.988,6.02,233.019 | OFAB terminal is profession-locked. | Shade character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674764 | 266573 | 0xC00C1777 | 244.01,6.02,233.014 | OFAB terminal is profession-locked. | Soldier character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674765 | 266575 | 0xC00D1777 | 246.01,6.02,232.985 | OFAB terminal is profession-locked. | Trader character with BS Signup access. |
| Tower Shop sided/org-dependent | 4704 | Tower Shop (dungeon) | 308281349 | 249724 | 0xC0051260 | 171,5.01,166.966 | Terminal did not open during live attempts. | Unknown; revisit only with explicit tower-shop access investigation. |
| Tower Shop sided/org-dependent | 4704 | Tower Shop (dungeon) | 308281366 | 295890 | 0xC0161260 | 189,5.01,167 | Sided city-building terminal; not openable from current character side. | Clan-side or appropriate city-building access setup. |
| Tower Shop sided/org-dependent | 4704 | Tower Shop (dungeon) | 308281368 | 295892 | 0xC0181260 | 185,5.01,167 | Sided city-building terminal; not openable from current character side. | Neutral-side or appropriate city-building access setup. |
| ICC Holodeck / Arete divergence | 6131 | ICC Holodeck Alien Training | 401801216 | 287476 | 0xC00017F3 | 195.672,6.017,130.331 | Current-client Arete/Holodeck access diverges from legacy AO Rebirth playfield assumptions. | Separate current-client source-data investigation, not normal vendor capture. |
| Unicorn / registration / special terminals | 1427 | Omni registration dng | 93519872 | 81799 | 0xC0000593 | 201.952,5,172.745 | Special registration interior; not part of practical shop sweep. | Registration-interior access investigation. |
| Unicorn / registration / special terminals | 1428 | Neutral organisation dng | 93585408 | 81799 | 0xC0000594 | 201.952,5,162.745 | Special registration interior; access unknown. | Neutral organisation registration access investigation. |
| Unicorn / registration / special terminals | 4364 | Unicorn Outpost | 285999104 | 256457 | 0xC000110C | 146.464,100.907,156.153 | Special outpost terminal; access/route not confirmed during campaign. | Unicorn Outpost access and terminal capture plan. |
| Unicorn / registration / special terminals | 4364 | Unicorn Outpost | 285999105 | 287037 | 0xC001110C | 143.328,100.886,152.997 | Special outpost terminal; access/route not confirmed during campaign. | Unicorn Outpost access and terminal capture plan. |
