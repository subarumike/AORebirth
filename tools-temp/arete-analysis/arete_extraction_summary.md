# Arete Extraction Summary

Generated UTC: 2026-06-15T04:46:50.394Z

Scope: summary only. No SQL, runtime code, or game data changes were generated.

## Inputs

- tools-temp/arete-analysis/npc_list.json
- tools-temp/arete-analysis/dialogue_trees.json
- tools-temp/arete-analysis/quest_chains.json
- tools-temp/arete-analysis/enemy_observations.json
- tools-temp/arete-analysis/inventory_reward_evidence.json
- tools-temp/arete-analysis/vendor_observations.json

## Summary

| Area | Extracted coverage | Notes |
| --- | ---: | --- |
| NPCs | 26 groups | 25 named groups; 4 groups have unresolved or ambiguous identity/name confidence. |
| Quests | 48 mission groups | 201 raw quest/mission events; 49 completion/removal evidence events. |
| Dialogue | 26 NPC groups, 468 events | 422 KnuBot events and 46 NpcMessage events retained. |
| Enemies | 607 entities | 201 combat-observed, 406 lifecycle-only. |
| Enemy combat | 1353 damage events | 47 death events and 7998 despawn events. |
| Rewards/inventory | 61 inventory rows, 42 item groups | 59 corpse loot-window rows; 2 possible container item-gain rows; confirmed gains/removals remain 0 from allowed logs. |
| XP/Cash | 219 stat changes | 163 XP changes and 48 Cash changes; quest links are temporal/uncertain. |
| Vendors | 24 vendors | 9 have VendorFull + shop stock; 15 are VendorFull-only; 320 unique stock rows. |

## Dialogue Coverage

The strongest dialogue coverage is KnuBot branch/answer evidence. The extraction retained answer option lists, selected answer indexes, open/close events, and non-empty KnuBot trade/interact text where present. NPC spoken prompt bodies are not fully expanded in the allowed logs, so dialogue reconstruction should preserve uncertainty around missing prompt text.

| NPC | Events | Option lists | Answers | NpcMessages | Identities | Folders |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| Vernon Godfray | 44 | 14 | 14 | 0 | (SimpleChar:782DE56D) | 20260614-211754 |
| Marcus Stone | 40 | 11 | 9 | 7 | (SimpleChar:782DE567) | 20260614-195107 |
| Patrick Sun | 34 | 12 | 12 | 0 | (SimpleChar:782DE580) | 20260614-214357 |
| Stanley Goodman | 30 | 9 | 9 | 0 | (SimpleChar:782DE56A) | 20260614-202500, 20260614-203038 |
| Alex Gibbs | 29 | 9 | 9 | 0 | (SimpleChar:782DE566) | 20260614-195725, 20260614-200850 |
| Mario Carles | 29 | 0 | 0 | 29 | (SimpleChar:78D3AA6E), (SimpleChar:78D45A2D), (SimpleChar:78D45A3E), (SimpleChar:78D45A50) | 20260614-221915 |
| Sarah Greene | 26 | 9 | 7 | 0 | (SimpleChar:782DE56E) | 20260614-203631, 20260614-205724 |
| Lorelei the Bartender | 25 | 10 | 7 | 1 | (SimpleChar:782DE570) | 20260614-212914 |
| Desmond Calitri | 24 | 8 | 5 | 0 | (SimpleChar:782DE57C) | 20260614-214819 |
| Rex Larsson | 21 | 8 | 7 | 0 | (SimpleChar:782DE568) | 20260614-194454 |

## Quest Coverage

The quest pass found 48 mission identities and 49 quest delete/completion-removal events. The first captured mission identities are (Mission:5514B18C), (Mission:5514B18D), (Mission:5514B18E) from the Rex Larsson segment.

Quest titles, objective text, and source-NPC relationships are the biggest unresolved quest fields. They were marked `unavailable-in-allowed-logs` or `uncertain-not-linked-by-allowed-logs` rather than inferred. This means the capture is good enough to define ordering and state transitions, but not yet enough to author final player-facing quest journal text without a packet-aware/title decode pass.

## Enemy Coverage

Enemy state coverage is broad: 607 entities, 201 with combat evidence, and 7998 despawn events. Names are unavailable in the allowed enemy/system files, so enemies are currently identity/level/HP/location based.

| Identity | Levels | Damage | Death | Despawn | Folders |
| --- | --- | ---: | ---: | ---: | --- |
| (SimpleChar:78D3A818) | 7 | 87 | 0 | 10 | 20260614-205724, 20260614-212914, 20260614-221915 |
| (SimpleChar:78D2E01B) | 1, 2 | 46 | 0 | 16 | 20260614-200850, 20260614-203631, 20260614-205724, 20260614-211754 |
| (SimpleChar:78A9AF94) | 3 | 44 | 0 | 19 | 20260614-195107, 20260614-195725, 20260614-200311, 20260614-200850, 20260614-211754, 20260614-214819 |
| (SimpleChar:78D3AEF3) | 7 | 43 | 0 | 12 | 20260614-203631, 20260614-205724 |
| (SimpleChar:78D2E018) | 1, 2, 3, 4 | 38 | 0 | 14 | 20260614-194454, 20260614-195107, 20260614-195725, 20260614-200311 |
| (SimpleChar:78D3AE41) | 7 | 28 | 0 | 2 | 20260614-203038 |
| (SimpleChar:78D45806) | 6 | 26 | 0 | 12 | 20260614-205724, 20260614-211754, 20260614-212914, 20260614-213857, 20260614-214357, 20260614-214819 |
| (SimpleChar:78D3AEBD) | 7 | 21 | 0 | 4 | 20260614-203631 |

## Reward And Inventory Coverage

Inventory evidence is useful but conservative. Most item rows are corpse loot-window observations, not confirmed player inventory gains. The two container rows are possible item-gain or container-state evidence and are explicitly uncertain. XP and Cash stat changes are present, but the first observed values are baselines and later changes need quest/combat association before becoming rewards.

- Inventory rows: 61
- Item groups: 42
- Corpse loot-window rows: 59
- Possible item-gain/container rows: 2
- Confirmed item gains from allowed logs: 0
- Confirmed item removals from allowed logs: 0
- XP changes: 163
- Cash changes: 48

## Vendor Coverage

Vendor evidence is solid for terminal identity/template/stock reconstruction where both VendorFull and ShopUpdate exist. There are 9 complete vendor observations and 15 VendorFull-only observations. Vendor display names are unavailable from the allowed CSV logs.

| Vendor identity | Template | Evidence | Stock rows | Folders |
| --- | ---: | --- | ---: | --- |
| (VendingMachine:12D1BF1C) | 297321 | complete-vendor-full-and-shop-stock-observed | 166 | 20260614-202500, 20260614-203631, 20260614-205724, 20260614-211754, 20260614-212914, 20260614-214357, 20260614-214819, 20260614-221915 |
| (VendingMachine:12D1BF1D) | 297320 | complete-vendor-full-and-shop-stock-observed | 39 | 20260614-202500, 20260614-203631, 20260614-205724, 20260614-211754, 20260614-212914, 20260614-214357, 20260614-214819, 20260614-221915 |
| (VendingMachine:12D1BF24) | 297371 | complete-vendor-full-and-shop-stock-observed | 38 | 20260614-202500, 20260614-203631, 20260614-205724, 20260614-211754, 20260614-212914, 20260614-214819, 20260614-221915 |
| (VendingMachine:12D1BF23) | 295748 | complete-vendor-full-and-shop-stock-observed | 23 | 20260614-195725, 20260614-200311, 20260614-200850, 20260614-202500, 20260614-203631, 20260614-205724, 20260614-211754, 20260614-212914, 20260614-214357, 20260614-214819 |
| (VendingMachine:12D1BF26) | 248368 | complete-vendor-full-and-shop-stock-observed | 16 | 20260614-195725, 20260614-200311, 20260614-200850, 20260614-202500, 20260614-203038, 20260614-203631, 20260614-205724, 20260614-211754, 20260614-212914, 20260614-214357, 20260614-214819, 20260614-221915 |
| (VendingMachine:12D1BF2B) | 248371 | complete-vendor-full-and-shop-stock-observed | 14 | 20260614-202500, 20260614-203038, 20260614-203631, 20260614-205724, 20260614-211754, 20260614-212914, 20260614-214357, 20260614-214819, 20260614-221915 |
| (VendingMachine:12D1BF22) | 297281 | complete-vendor-full-and-shop-stock-observed | 10 | 20260614-195725, 20260614-200311, 20260614-200850, 20260614-211754, 20260614-214819 |
| (VendingMachine:12D1BF27) | 121036 | complete-vendor-full-and-shop-stock-observed | 10 | 20260614-195725, 20260614-200311, 20260614-200850, 20260614-202500, 20260614-203038, 20260614-203631, 20260614-205724, 20260614-211754, 20260614-212914, 20260614-214357, 20260614-214819, 20260614-221915 |

## Gaps

- Quest titles and objectives are not decoded in the allowed logs. A later packet-aware pass is needed before final quest journal text can be authored.
- KnuBot option lists are strong, but many NPC prompt bodies are missing or not independently expanded. Dialogue should start as branch/option reconstruction, not final prose.
- Several NPC identities/names remain ambiguous: Lolly the Reet, Mario Carles, Wounded Dockworker, and one unknown KnuBot target need cleanup before final content generation.
- Enemy and vendor names are unavailable in the enemy/vendor allowed files. Enemy implementation should not start from this extraction alone unless names are supplied by a separate allowed evidence pass.
- Enemy hostility is not explicit for lifecycle-only entities. Only combat-observed identities should be treated as likely combat actors at first.
- Inventory rows mostly show corpse loot-window contents, not confirmed player gains. Reward item grants/removals should stay uncertain until player inventory deltas are captured or decoded.
- XP and Cash stat changes exist, but quest/combat attribution is temporal and uncertain unless it shares exact timestamps with quest evidence.
- Vendor stock has 9 complete VendorFull + ShopUpdate terminals, but 15 VendorFull-only terminals should not receive final stock until direct shop evidence is available.

## Recommended First Coding Target

Start with a narrow Rex Larsson Arete dialogue/quest vertical slice, not the full Arete chain.

Recommended scope:

- Use Rex Larsson `(SimpleChar:782DE568)` from capture folder `20260614-194454` as the first NPC target.
- Reconstruct only the captured KnuBot branch/options for Rex: 21 dialogue events, 8 option lists, and 7 selected-answer events.
- Wire the first observed mission state transitions around `(Mission:5514B18C)`, `(Mission:5514B18D)`, and `(Mission:5514B18E)` as a proof-of-life quest flow, with titles/objectives left as placeholders or internal labels until decoded evidence exists.
- Keep rewards out of the first slice except for clearly observed stat-change plumbing; do not hardcode item rewards yet.
- Add focused validation around KnuBot answer-list emission, selected answer handling, quest accept/complete transition, and no regression to login/zone/vendor behavior.

Why this target first: Rex is early, bounded, named, identity-backed, and mostly dialogue/quest-state oriented. It exercises the systems Arete needs without depending on the weakest evidence areas: enemy names, final quest titles/objectives, confirmed reward item deltas, or full vendor imports.

Do not start with full Arete rewards, enemy placement, or vendor stock as the first coding step. Those areas have useful evidence, but they still need name/title/item attribution cleanup before becoming stable game data.
