# Project State

Primary Codex memory file for AO Rebirth. This top section is the current source of truth. Historical sections below are retained for evidence and provenance; older Rex/B18D/B18E/gate notes in those sections may be superseded by the current state here.

## Current Baseline

- HEAD baseline: `0946690`.
- Repository purpose: local C#/.NET Framework-era Anarchy Online server workspace for Mike's current AO client and local `cellao_codex_clean` MySQL database; this is a legacy database name retained for local compatibility.
- Current stable approach: evidence-backed packet/gameplay/data repair, current-client parity over legacy assumptions, and identity-first capture-derived reconstruction.
- Documentation split: `docs/ai/CURRENT_TASK.md` remains the active task handoff; this file is the stable project memory; `docs/generated/` contains historical result reports only.
- Active cleanup note: `docs/ai/CURRENT_TASK.md` has been updated and now states: "No active implementation task selected. Await user instruction." Do not invent or reference `docs/generated/rex_default_enabled_gate_fix_result.md` until that file exists.

## Current Hard Rules

- Do not use raw packet replay for Rex/Arete mission packets. Use decoded DTO/body serializer paths only.
- Do not guess packet behavior; unresolved packet semantics must stay unresolved until evidence-backed.
- Capture-derived content must be identity-first. Display names, proximity, screenshots, or plausible templates cannot define runtime data.
- Cargo Box identity is exactly `Terminal:56D9B4AF`; do not substitute nearby terminals, rendered labels, templates, meshes, or inferred anchors.
- `CharacterAction` action `59` remains unresolved. Do not treat it as offer, accept, complete, fail, abandon, reward, or persistence semantics.
- Rex chain state is process-local/in-memory only. There is no DB mission persistence.
- Do not change database schemas or perform destructive database operations without explicit approval.
- Marcus Stone full quest chain must not be treated as fully implemented. The Marcus dirty vertical slice was rolled back.

## Current Arete Environment Gate Semantics

- Missing or empty Arete/Rex environment variables default enabled for local/dev.
- Explicit falsey values disable: `0`, `false`, `no`, `off`.
- Explicit truthy values enable: `1`, `true`, `yes`, `on`.
- Other non-empty values remain disabled.
- Current Rex gates using this model: `AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING`, `AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW`, `AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS`, `AO_REBIRTH_ENABLE_ARETE_REX_B18D_PREVIEW`, and `AO_REBIRTH_ENABLE_ARETE_REX_B18E_COMPLETION`.

## Current AO Arete / Rex State

- `6553 Arete Landing` is enabled and is the active Rex test playfield.
- Rex Larsson identity is `SimpleChar:782DE568`.
- Rex checked-in content lives under `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson`.
- Rex works through B18F on the current baseline.
- B18C, B18D, B18E, and B18F handoff paths are implemented through safe DTO/body packet construction.
- B18C: Rex dialogue can offer `Mission:5514B18C`; B18C counts five `Malfunctioning Cleaning Robot` kills; captured per-kill feedback is emitted; final handoff sends captured mission-window sequence to B18D.
- B18C runtime targets are five evidence-backed `Malfunctioning Cleaning Robot` rows in playfield `6553`; the local spawn repair uses heartbeat-safe actor-baseline stats and preserves captured HP/level/monster data.
- B18D: exact Cargo Box use target is `Terminal:56D9B4AF`; use records B18D progress, cleans up B18D with DTO-built `QuestMessage Action=Delete`, and emits B18E `QuestFullUpdate`.
- B18E: returning to Rex from B18E state starts the captured return branch, deletes B18E with DTO-built `QuestMessage Action=Delete`, grants actual `+290 XP`, grants `+1040` credits, sends reward feedback, and emits B18F `QuestFullUpdate`.
- B18F: handoff is implemented as `Mission:5514B18F` / `Talk to Marcus Stone`. Marcus Stone identity evidence is `SimpleChar:782DE567`.
- Reward feedback text is `Received reward: 1281 XP, 1040 credits.` The `1281 XP` value is display metadata only and must not be applied as actual XP.
- Marcus Stone static B18F dialogue visibility is implemented for `SimpleChar:782DE567` in playfield `6553`, using captured `20260614-195107` B18F prompt/options.
- Marcus B18F -> B194 transition is implemented only for node `marcus_195107_b18f_002`, answer index `0`, option text `So, let me guess... You need some help with the fire?`. It requires Rex chain state `B18FPreviewed` or later, uses a process-local duplicate guard, sends DTO-built B18F `QuestMessage Action=Delete`, and sends DTO-built B194 `QuestFullUpdate`.
- Item `296780` handout is deferred. No item reward, inventory mutation, raw replay, DB mission persistence, full Marcus quest chain, gas-fire use, trade, rewards, or follow-up mission is implemented.
- Historical stale Marcus runtime hook cleanup remains preserved: `ZoneEngine.csproj` no longer includes missing `MarcusStoneQuestChainHandler.cs`, and runtime router code no longer references `MarcusStoneQuestChainHandler`. Current Marcus static dialogue is registered through content-driven dialogue and loads the checked-in `Content/Arete/marcus-stone/manifest.json`; Marcus quest chain remains future work, gate behavior is unchanged, focused ZoneEngine build passed, and `git diff --check` passed for the cleanup.

## Current Arete / Rex Source Documents

- Current Rex content pack: `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson/manifest.json`.
- Key Rex/Arete result history: `docs/generated/rex_b18c_robot_progress_smoke_result.md`, `docs/generated/rex_b18d_to_b18e_safe_handoff_result.md`, `docs/generated/rex_mission_window_cleanup_return_state_result.md`, `docs/generated/rex_b18e_completion_b18f_handoff_result.md`, `docs/generated/rex_b18e_credit_reward_message_result.md`, and `docs/generated/arete_malfunctioning_cleaning_robot_spawn_result.md`.
- Older generated reports may describe earlier disabled-by-default gates, missing B18D cleanup, missing B18E completion, missing credits, or missing B18F handoff. Treat those as historical phase notes superseded by this top section unless a newer file says otherwise.

## Current Working Systems Summary

- Login, chat, and zone engines build/run locally in documented prior validations.
- Engine launcher cleanup is implemented and validated: `start-engines.ps1` defaults to detached hidden Chat/Login/Zone headless startup, records stdout/stderr logs and PID metadata under `logs/engines`, waits for ports `6996`, `7012`, `7500`, and `7501`, and `stop-engines.ps1` stops engines through shutdown metadata before fallback. `status-engines.ps1` reports process and port state. `-WithWeb` starts and stops WebEngine on port `8181`. `-Visible` starts the debug-mode path and validated process/port startup in Codex, but this host reported `MainWindowHandle=0`, so desktop window visibility should be manually observed if window chrome matters.
- Runtime startup branding and shared server version baseline cleanup is implemented and validated: hidden Chat/Login/Zone logs show revision/banner branding as `AO Rebirth`, startup text uses `AO Rebirth Dev Team`, displayed version is `1.0.0.0`, and the Funcom/Anarchy Online notice remains unchanged.
- MySqlConnector migration and DAO transaction handling are repaired for login select/zone redirect.
- Current-client `FullCharacter` version 26 and live-style login state are locked decisions.
- Sit/stand, equipment visuals, inventory move, equip/unequip, corpse item/credit loot, player trade item/credit/cancel, vendor buy/sell/close, and death/respawn have passing documented validation for their repaired scopes.
- Vendor coverage is complete for practical live-accessible vendors; remaining 26 statel vendors are deferred setup/access backlog.

## Current Open Risks

- `Quest Delete` gameplay cause remains unresolved; current use is packet-level mission-window cleanup only.
- `CharacterAction` action `59` remains unresolved.
- Rex/Mission state is not persisted to DB and will not survive process restart as mission state.
- Marcus Stone quest chain beyond the B194 mission-window preview is not implemented.
- NPC chase/movement remains high risk and should not be changed without replay/capture evidence.
- `PlayfieldAnarchyF` remains a current-client structure mismatch.
- Full gameplay systems for missions, quests, perks, research, pets, PvP/towers, teams, and organizations remain incomplete outside the documented repaired slices.

# Historical State Log

Historical notes below are preserved for provenance. Any older Rex/B18D/B18E statements about disabled-by-default gates, missing B18D cleanup, missing B18E completion, missing credits, or missing B18F handoff are superseded by the current memory section above. Historical `cellao_codex_clean` database references and old `Cellao-Clean` backup paths in this log are retained as exact historical provenance, not current repo naming guidance.

## Historical Current Status Snapshot

AO Rebirth is a local C#/.NET Framework-era Anarchy Online server workspace. Current work is focused on making the server compatible with Mike's current AO client and local `cellao_codex_clean` MySQL database; this is a legacy database name retained for local compatibility while evidence-backed packet, gameplay, and data repairs continue.

Capture-derived content reconstruction now has mandatory identity-first rules in `docs/project/KNOWN_DECISIONS.md` and `docs/ai/WORKFLOW.md`: captured AO identity is the primary key, complete relevant capture sets must be searched before declaring evidence missing, identity-linked full-update/stat evidence outranks names/screenshots/proximity, evidence tables are required before SQL or game-data edits, and uncertain fields must fail closed instead of being guessed.

Repository licensing now uses a dual-license structure: inherited CellAO portions remain under the CellAO BSD-style license terms, while AO Rebirth additions are proprietary. Root `LICENSE` and `NOTICE` files document the split and attribution.

Final runtime third-party attribution is documented in the root `NOTICE`: CellAO, bundled runtime source components, and all detected runtime NuGet dependencies are attributed; `tools-temp`, AOSharp, EasyHook, test packages, and historical captures remain excluded from runtime distribution.

# Working Systems

- Login, chat, and zone engines build and run locally.
- AOSharp Live Capture remains isolated under `tools-temp` and now builds as a fuller passive data logger: new sessions standardize raw packet, event, vendor, shop, system-message, chat/dialogue, NPC-interaction, inventory, enemy-state, metadata, and health-validation outputs without changing AO client behavior or runtime server behavior.
- Arete dialogue/quest framework scaffolding now exists under ZoneEngine `Core/Arete` as inactive models, JSON file/directory/manifest loaders, registries, validators, aggregate content validation, aggregate validation reporting, synthetic condition-reference validation, dialogue session services, no-op condition/action helpers, in-memory mission-state services, a synthetic dialogue-action to mission-state adapter, inactive dialogue action reference validation, file-loaded action reference validation coverage, inactive objective playback, and an unused zero-pack bootstrap helper. A PowerShell validation harness under `tools-temp/arete-framework-validation` covers 131 synthetic in-memory, file-loaded, directory-loaded, manifest-loaded, dialogue-session, mission-state, dialogue-action adapter, content action reference, file-loaded action reference, aggregate content validation, condition-reference, and aggregate report cases. The first inactive captured Rex Larsson content draft now exists under `AORebirth/Server/ZoneEngine/Content/Arete/rex-larsson` with a manifest, one dialogue pack, one quest pack, eight captured answer-list nodes, eight recovered `KnubotAppendText` prompt nodes, fifteen visible options, and three QuestFullUpdate-decoded mission definitions. `Mission:5514B18C`, `Mission:5514B18D`, and `Mission:5514B18E` now have decoded titles, objectives, QuestFullUpdate evidence metadata, cautious in-pack packet-sequence links, non-executable objective trigger evidence, and inactive objective playback for B18C kill-count robot/death observations, B18D GenericCmd Use against packet identity `Terminal:56D9B4AF`, and B18E Rex KnuBotOpenChatWindow return signal. Later Arete captures resolved the B18D static dynel identity/full-update data for `Terminal:56D9B4AF`; Rex packet semantics review confirmed mission-targeted action parameters and packet-level `QuestAction.Delete`, but action `59`, delete gameplay meaning, executable mission transition semantics, exact objective progress mapping beyond the current gated B18C/B18D smoke paths, and dialogue-to-mission routing remain unresolved. Rex aggregate validation and dry-run pass; outside the separate gated B18C quest-preview packet path, no quest packet emission, SQL, schema, rewards, inventory, XP, credits, character mutation, persistence, executable mission action, real condition semantics, or gameplay behavior is wired yet.
- Rex Larsson has a controlled live dialogue route behind disabled-by-default environment gate `AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING`. The route uses the shared `ContentDrivenNpcDialogueRouter` registration model, while Rex remains the only registered content-driven NPC. When enabled, only `SimpleChar:782DE568` in playfield `6553 Arete Landing` loads the captured Rex Arete manifest and uses the existing KnuBot open/append-text/answer-list/close packet path to show captured dialogue prompt text/options and advance an in-memory dialogue session. Legacy `KnuBotScriptName` NPCs still fall through to the compiled KnuBot path. A second disabled-by-default gate, `AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW`, controls the captured `Mission:5514B18C` `QuestFullUpdate` preview. Manual smoke on 2026-06-17 showed raw captured frame replay causes a hard client hang, so raw replay remains blocked. The safe B18C DTO/body serializer sends the decoded QuestFullUpdate through normal `ZoneClient.SendCompressed(MessageBody)` framing; live smoke confirmed B18C appears in the client mission window without a client hang. A third disabled-by-default gate, `AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS`, now enables B18C kill progress only after the safe preview is emitted for that player. The progress observer hooks the existing `Playfield.KillNpcTarget` death point and counts only `Malfunctioning Cleaning Robot` deaths for `Mission:5514B18C`; it sends captured per-kill feedback from capture `20260614-194454`. Kills `1/5` through `4/5` send the captured encoded `FormatFeedbackMessage` remaining-count payload plus captured `FeedbackMessage CategoryId=110 MessageId=249817907`; kill `5/5` sends the captured generic feedback and then a one-time captured mission-window handoff sequence: `CharacterAction` action `59` targeting `Mission:5514B18C`, `Quest` action `Delete` for `Mission:5514B18C`, and next `QuestFullUpdate` for `Mission:5514B18D`. This B18C handoff is still packet-level only: no rewards, inventory, XP/credits, DB writes, persistence, or action/delete gameplay interpretation is enabled. Capture `20260614-194454` contains named robot spawn/full-update/death/corpse evidence, including level, HP, `monsterData=297023`, and coordinates. Five evidence-backed captured robot rows are staged in `tools-temp/sql-staging/arete_malfunctioning_cleaning_robot_mobspawns.sql` and applied locally to `mobspawns` for playfield `6553`; DB verification shows Rex plus five target robots and zero unrelated `6553` rows. Follow-up load-screen smoke showed captured SCFU-visible stats alone were not enough for the old heartbeat and SimpleCharFullUpdate paths, so the staged robot SQL includes separated runtime actor-baseline scaffold stats; each robot has 27 stat rows while retaining the same captured spawn positions, HP, level, and monster data. Manual smoke confirmed the B18C handoff advances the client into `Mission:5514B18D`. The B18D static dynel row for DB-backed `Terminal:56D9B4AF` in playfield `6553` has been reset to exact captured packet evidence from later Arete capture segments. Capture `20260614-194454` proves the B18D `GenericCmd Action=Use` target, while captures `20260614-205724`, `20260614-214819`, and `20260614-215831` contain repeated `SimpleItemFullUpdate` packets for the same identity. The corrected row uses captured position `(3621.576, 51.745, 780.4768)`, rotation `(0, -0.7101817, 0, 0.7040185)`, and captured stats `Flags=139265`, `StaticInstance=297277`, `ACGItemLevel=1`, `ACGItemTemplateID=297277`, `ACGItemTemplateID2=297277`, `MultipleCount=1`, `AnimPlay=0`, and `AnimPos=0`. Rejected local smoke attempts using nearby `Terminal:57369E8E` `Junk`, template `285300`, or explicit `Mesh=18794` are not represented in the corrected row. A fourth disabled-by-default gate, `AO_REBIRTH_ENABLE_ARETE_REX_B18D_PREVIEW`, now enables a narrow exact-target `GenericCmd Action=Use` route for `Terminal:56D9B4AF` in Arete after the player has received the B18D preview. When all Rex gates are enabled, the route acknowledges the click, records B18D objective observed/complete in memory as preview-only progress `1/1`, and sends a DTO-built B18E `QuestFullUpdate` from captured packet `20260614-194454/packets.hex.log:5767` so `Mission:5514B18E` can appear as `Return to Rex Larsson`. The B18E DTO body matches captured packet `#5339` byte-for-byte from the N3 body onward. No B18D `Quest Delete`, B18D action `59`, rewards, inventory, XP/credits, DB writes beyond the placement SQL, persistence, general StaticDynel event execution, B18E completion, or action/delete gameplay interpretation is enabled.
- NPC dialogue starts now request a visible face-toward-player update through the existing recovered `SetWantedDirection` support for legacy KnuBot conversations and the gated Rex Arete route. Manual in-client smoke passed. Normal NPC chase movement remains on the existing coordinate `FollowTarget` path and was not changed.
- Update 2026-06-18: the Rex live-route status above is superseded for B18D/B18E cleanup. B18D now sends DTO-built `QuestMessage Action=Delete` for only `Mission:5514B18D` after exact Cargo Box use, and B18E completion is now available behind `AO_REBIRTH_ENABLE_ARETE_REX_B18E_COMPLETION`. The B18E path sends DTO-built `QuestMessage Action=Delete` for only `Mission:5514B18E`, grants the proven actual `+290 XP`, grants captured `+1040` credits, sends reward feedback text `Received reward: 1281 XP, 1040 credits.`, and sends DTO-built `Mission:5514B18F` / `Talk to Marcus Stone` QuestFullUpdate. The `1281 XP` value is display metadata and is not applied as actual XP. No B18D/B18E action `59`, item rewards, inventory mutation, DB mission persistence, Marcus Stone dialogue, SQL/schema changes, or raw packet replay is enabled.
- `6553 Arete Landing` is enabled in `ZoneEngine/XML Data/Playfields.xml` so the debug `/tp` command can pass the `Playfields.ValidPlayfield` allow-list check and reach the current-client playfield data already present in `playfields.dat`.
- Dependency cleanup for proprietary-readiness is in progress/completed for the requested GPL/unlicensed targets: `MySql.Data` was replaced by `MySqlConnector`, WCell-derived `Cell.Core`/`Cell.Util` compiled sources were replaced with clean implementations, and AOSharp remains isolated to `tools-temp`/capture provenance rather than the main solution build.
- Post-MySqlConnector login select is repaired: the generic DAO helper now passes the active transaction to Dapper, allowing character select `SetOnline` to complete and LoginEngine to redirect the client to Zone.
- DAO transaction handling is hardened after the MySqlConnector migration: locally owned DAO transactions commit only after successful work, roll back on failure, and nested DAO write/read helpers in transaction scopes receive the active connection/transaction.
- Post-sweep live validation passed for login, zone entry, shop open, vendor buying, and timed logout, with no current-window MySqlConnector/Dapper/transaction errors in engine logs.
- Current-client `FullCharacter` version 26 and live-style login state are locked project decisions.
- Sit/stand behavior is repaired.
- Weapon and armor equipment visuals are repaired for the current test scope.
- Equipped items persist across relog in the documented test scope.
- Inventory Move Live Verification result: PASS. A junk item moved correctly between inventory slots before relog and remained in the correct slot after relog.
- Equip Item Live Verification result: PASS. Item equipped correctly before relog, no duplicate remained in inventory, and after relog the item remained equipped in the correct equipment slot.
- Unequip Item Live Verification result: PASS. Item moved from equipment slot to inventory correctly, the equipment slot became empty, no duplicate remained equipped, and after relog the item stayed in inventory while the equipment slot stayed empty.
- Corpse Item Loot Live Verification result: PASS. Non-credit corpse item appeared in inventory correctly, the corpse no longer offered the looted item, no duplicate item appeared, cash did not change from item loot, and the item remained in inventory after relog.
- Corpse Credit Loot Live Verification result: PASS. One correct corpse credit message displayed, cash increased by exactly the awarded amount, no inventory item was created from credit loot, increased cash persisted after relog, and no duplicate corpse credit feedback was observed.
- Player Trade Item Live Verification result: PASS. Item left player A inventory correctly, appeared in player B inventory correctly, no duplicate item existed, cash remained unchanged, and after relog the item remained only with player B.
- Player Trade Credits Live Verification result: PASS. Player A cash decreased by the expected amount, player B cash increased by the expected amount, no inventory items moved, appeared, or disappeared, cash values persisted after relog, and no duplicate cash behavior was observed.
- Player Trade Cancel/Decline Live Verification result: PASS. Trade panes closed correctly, the offered item remained with the original player, cash remained unchanged, no duplicate item or cash behavior occurred, and state persisted correctly after relog.
- Vendor Buy Live Verification result: PASS. Purchased item appeared in inventory correctly, cash decreased by the exact purchase price, no duplicate item appeared, and after relog the purchased item and reduced cash value both persisted.
- Vendor Sell Live Verification result: PASS. Sold item left inventory correctly, cash increased by the exact sale price, no duplicate item appeared, and after relog the sold item remained absent and increased cash value persisted.
- Vendor Close/Cancel Live Verification result: PASS. Pending vendor transaction state closed without accepting, cash stayed unchanged, items remained with their original owner/location, no duplicate item appeared, and the same item/cash state persisted after relog.
- Live Persistence Verification complete: inventory move, equip item, unequip item, corpse item loot, corpse credit loot, player trade item, player trade credits, player trade cancel/decline, vendor buy, vendor sell, and vendor close/cancel all matched expected client-visible behavior and survived relog.
- Death/respawn white-screen behavior is repaired.
- Corpse use, item loot, credit loot, XP text, and corpse despawn have working documented paths. The completed corpse credit investigation fixed the `CorpseFullUpdate` cash offset, removed duplicate manual corpse credit chat, retained focused assertions, and passed Cliff Malle playtest verification.
- Player trade item and credit transfer have been repaired and verified in the documented test scope. Credit-only, item-only, mixed item-plus-credit, and cancel/decline trades behaved as expected, and no player trade display or commit defect was reproduced. Temporary `TRADE_*` logging remains available for future trade investigation.
- Broad combat smoke `-SkipBuild`, focused corpse credit assertions, and inventory/container regression assertions pass after stale harness assertions were cleaned up. The cleanup changed harness expectations only, not gameplay behavior.
- Vendor shop buy, sell, close, and current-client ICC shop stock coverage have been repaired for the captured Fair Trade areas.
- Omni Basic General Shop live-capture import completed from AOSharp capture `20260612-012644`. The validated staged SQL added 23 `1183 ord_smarket_omni_basic` vendor rows, 16 vendor templates, and 16 shop inventory groups with 690 inventory rows. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 381`; total uncovered statel vendors dropped from `404` to `381`, and `1183 ord_smarket_omni_basic` dropped from `39` to `16`. No runtime vendor behavior changed.
- Non-shop statel template `155225` (`Refreshing Drink`) is excluded from vendor coverage metrics, missing-vendor reports, capture targeting, and import planning while remaining visible in raw statel coverage output. AOSharp captures `20260612-012644` and `20260612-044234` showed VendorFullUpdate evidence but no ShopUpdate inventory rows, and live operator verification found the Superior instances were not reachable/openable. Verification now shows `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 351`, and `StatelVendorExclusions = 30`. No SQL, vendor mappings, imports, or runtime vendor behavior changed.
- Inaccessible playfield `500 Parnassos` is excluded from active vendor coverage metrics, missing-vendor reports, capture targeting, and import planning while remaining visible in raw statel coverage output. Operator verification confirmed there is no practical live-client access path for capture. Current verification shows `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 29`, and `StatelVendorExclusions = 169`; the actionable capture backlog dropped from `89` to `29`. No SQL, vendor mappings, imports, or runtime vendor behavior changed.
- Neutral Training Startup Equipment import completed from AOSharp capture `20260614-002319`. The validated staged SQL added 2 `954 Neutral Training` vendor rows, 1 vendor template, and 1 new shop inventory group with 9 inventory rows. Both Basic Startup Equipment statels have direct VendorFull and ShopUpdate evidence and share exact inventory hash `WHBW`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 27`, and `StatelVendorExclusions = 169`; actionable uncovered statel vendors dropped from `29` to `27`. Current coverage/actionability chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96 -> 93 -> 89 -> 29 -> 27`. No runtime vendor behavior changed.
- Freelancers Inc. HQ - Rome Agency Shop import completed from AOSharp capture `20260614-022639`. The validated staged SQL added 1 `7011 Freelancers Inc. HQ - Rome` vendor row, 1 vendor template, and 1 new shop inventory group with 26 inventory rows. The imported row covers Agency Shop template `285348` at X 93.972 Y 2.01 Z 73.734 with direct VendorFull and ShopUpdate evidence. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 26`, and `StatelVendorExclusions = 169`; actionable uncovered statel vendors dropped from `27` to `26`. Current coverage/actionability chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96 -> 93 -> 89 -> 29 -> 27 -> 26`. No runtime vendor behavior changed.
- Vendor coverage campaign freeze completed. Status: COMPLETE (LIVE COVERAGE). The campaign is complete for all practical live-accessible vendors. The remaining `26` uncovered statel vendors are deferred because they require setup-specific access: BS Signup profession-locked terminals, sided/org-dependent Tower Shop terminals, Clan-only shops, ICC Holodeck / Arete divergence, Unicorn Outpost, and special registration interiors. No SQL, capture, import, mapping change, or runtime vendor behavior change was made for the freeze.
- Omni Superior General Shop live-capture import completed from AOSharp capture `20260612-044234`. The validated v2 staged SQL added 27 `1185 ord_smarket_omni_sup` vendor rows, 20 vendor templates, and 19 new shop inventory groups while reusing existing map shop hash `LJI7`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 324`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `351` to `324`. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324`. No runtime vendor behavior changed.
- Clan Basic General Shop live-capture import completed from AOSharp capture `20260612-225855`. The validated staged SQL added 29 `1180 ord_smarket_clan_basic` vendor rows, 29 vendor templates, and 25 new shop inventory groups with 1575 inventory rows while reusing existing shop hashes `G4XZ`, `HYDQ`, `LJI7`, and `R5R7`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 295`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `324` to `295`. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295`. No runtime vendor behavior changed.
- Clan Superior General Shop live-capture import completed from AOSharp capture `20260612-232439`. The validated staged SQL added 19 `1182 ord_smarket_clan_sup` vendor rows, 19 vendor templates, and 14 new shop inventory groups with 594 inventory rows while reusing existing shop hashes `LJI7`, `CHHQ`, `OHOO`, `JYPE`, and `Cont`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 276`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `295` to `276`. Current live-capture coverage chain is `404 ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ 381 ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ 351 ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ 324 ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ 295 ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ 276`. No runtime vendor behavior changed.
- Omni Advanced General Shop live-capture import completed from AOSharp capture `20260613-002828`. The validated staged SQL added 23 `1184 ord_smarket_omni_advanced` vendor rows, 16 vendor templates, and 15 new shop inventory groups with 760 inventory rows while reusing existing shop hash `LJI7`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 253`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `276` to `253`, and `1184 ord_smarket_omni_advanced` has no remaining vendor-scan targets. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253`. No runtime vendor behavior changed.
- Omni Basic Implant Terminals live-capture import completed from AOSharp capture `20260613-005616`. The validated staged SQL added 13 `1183 ord_smarket_omni_basic` implant vendor rows and 13 vendor templates, with no new shop inventory groups because existing implant shop hashes `5BUX`, `5M5F`, `6MQN`, `6YPW`, `7LZ3`, `A32J`, `JWHR`, `KV75`, `KVVT`, `O3KI`, `RNWW`, `RO4Q`, and `SBQ6` were reused. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 240`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `253` to `240`, and `1183 ord_smarket_omni_basic` has no remaining vendor-scan targets. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240`. No runtime vendor behavior changed.
- Neutral Basic General/Specialty Shop live-capture import completed from AOSharp captures `20260613-012810` and `20260613-014033`. The validated staged SQL added 6 `1193 spec_smarket_neut_basic` vendor rows, 6 vendor templates, and 6 new shop inventory groups with 64 inventory rows; Specialist Commerce required Trader access. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 234`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `240` to `234`, and `1193 spec_smarket_neut_basic` has no remaining vendor-scan targets. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234`. No runtime vendor behavior changed.
- spec_smarket specialty import completed from operator-approved inferred reuse of Neutral Basic/Specialty captures `20260613-012810` and `20260613-014033`. The validated staged SQL added 16 vendor rows across `1189 spec_smarket_clan_advanced`, `1190 spec_smarket_clan_sup`, `1191 spec_smarket_omni_advanced`, and `1192 spec_smarket_omni_sup`, plus 12 vendor templates. No new shop inventory groups were added; existing shop hashes `I3E4`, `7ATH`, `7X7Q`, `PX4X`, `FBQ3`, and `FLEW` were reused. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 218`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `234` to `218`, and the four spec_smarket playfields have no remaining vendor-scan targets. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218`. No runtime vendor behavior changed.
- Clan Advanced General Shop live-capture import completed from AOSharp capture `20260613-034740`. The validated staged SQL added 16 `1181 ord_smarket_clan_advanced` vendor rows, 16 vendor templates, and 11 new shop inventory groups with 505 inventory rows while reusing existing shop hashes `Cont`, `IVM2`, `IYD4`, `JTYS`, and `LJI7`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 202`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `218` to `202`, and `1181 ord_smarket_clan_advanced` has no remaining vendor-scan targets. Current live-capture coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202`. No runtime vendor behavior changed.
- Overnight exact-template inferred vendor import completed from existing captured/inferred template evidence. The validated staged SQL added 31 vendor rows only, reusing existing `vendortemplate` hashes and existing `shopinventorytemplates` hashes; no new vendor templates or shop inventory groups were added. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 171`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `202` to `171`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171`. No runtime vendor behavior changed.
- Neutral ICC implant/cluster import completed from AOSharp capture `20260613-170220`. The validated staged SQL added 24 vendor rows across `2064 neut_basic_implants_shop` and `2073 neut_advanced_implants_shop`, 12 vendor templates, and 12 new shop inventory groups with 1876 inventory rows. The `2064` rows are captured mappings; the `2073` rows are high-confidence exact-template reuse from the captured `2064` template evidence, matching the existing ICC pharmacy reuse pattern across these interiors. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 147`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `171` to `147`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147`. No runtime vendor behavior changed.
- Arete ICC implant/cluster import completed from AOSharp capture `20260613-172753`. The validated staged SQL added 5 `6553 Arete Landing` vendor rows, 5 vendor templates, and 5 new shop inventory groups with 573 inventory rows. The imported core targets are ICC Basic Implants, ICC Faded Clusters, ICC Bright Clusters, ICC Shiny Clusters, and ICC Pharmacy; incidental nearby capture evidence was intentionally excluded. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 142`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `147` to `142`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142`. No runtime vendor behavior changed.
- Newland + Omni startup import completed from AOSharp capture `20260613-185338`. The validated staged SQL added 9 vendor rows across `565 Newland Desert` and `710 Omni-1 Trade`, 6 vendor templates, and 6 new shop inventory groups with 232 inventory rows. The imported rows cover Newland Basic Armor, Newland Basic Startup Equipment, Newland Basic Nano Clusters, Food, Drinks, and four OT Basic Startup Equipment statels; the four Omni startup vendors share one deduplicated inventory group. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 133`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `142` to `133`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133`. No runtime vendor behavior changed.
- Clan Basic Startup Equipment import completed from AOSharp capture `20260613-211234`. The validated staged SQL added 4 `540 Old Athen` vendor rows, 1 vendor template, and 1 new shop inventory group with 9 inventory rows. The four Old Athen Clan Basic Startup Equipment statels share one deduplicated inventory group; `0xC000021C` had captured inventory but no direct VendorFull and was correlated by template `99569` plus exact inventory match with the three VendorFull-confirmed startup terminals. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 129`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `133` to `129`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129`. No runtime vendor behavior changed.
- Broken Shores + Lush Fields live capture import completed from AOSharp capture `20260613-215211`. The validated staged SQL added 2 vendor rows across `665 Broken Shores` and `695 Lush Fields`, 2 vendor templates, and 2 new shop inventory groups with 190 inventory rows. The imported rows cover Broken Shores OT Advanced Trade Skills and Lush Fields Basic Startup Equipment; the Lush Fields startup inventory was captured as a new group because it differs from Newland startup by QL. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 127`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `129` to `127`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127`. No runtime vendor behavior changed.
- Holes in the Wall live capture import completed from AOSharp capture `20260613-221619`. The validated staged SQL added 3 vendor rows across `791 Holes in the Wall` and `4565 Hardware Dimenion - Superior`, 2 vendor templates, and 1 new shop inventory group with 87 inventory rows while reusing existing shop hash `Cont`. The two Holes in the Wall rows use captured ShopUpdate inventory on exact statel identities plus current-client statel target metadata because the client crash prevented VendorFull rows; the Hardware Dimension row is high-confidence exact-template inference for template `151974`. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 124`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `127` to `124`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124`. The incidental inventory-only identity `(VendingMachine:12E4CE58)` had no target correlation and was not imported. No runtime vendor behavior changed.
- Tower Shop + BS Signup live capture import completed from AOSharp capture `20260613-223554`. The validated staged SQL added 18 vendor rows across `4704 Tower Shop (dungeon)` and `6007 BS Signup (dng)`, 18 vendor templates, and 18 new shop inventory groups with 2047 inventory rows. The imported rows cover 14 Tower Shop terminals and 4 BS Signup OFAB terminals; Clan City Buildings, Neutral City Buildings, and Leets-R-Us were VendorFull-only / not openable and were not imported, and remaining BS Signup profession-locked terminals require matching professions. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 106`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `124` to `106`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106`. No runtime vendor behavior changed.
- Omni Training Startup Shop import completed from AOSharp capture `20260613-231115`. The validated staged SQL added 1 `950 Omni Training` vendor row, 1 vendor template, and 1 new shop inventory group with 7 inventory rows. The imported row covers Startup Shop! template `100035`; VendorFull evidence was captured on playfield entry/dynel spawn and ShopUpdate evidence was captured by opening the terminal. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 105`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `106` to `105`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105`. No runtime vendor behavior changed.
- Treepine Hut OT Clothes import completed from AOSharp capture `20260613-233535`. The validated staged SQL added 1 `1887 Treepine Hut` vendor row, 1 vendor template, and 1 new shop inventory group with 16 inventory rows. The imported row covers OT Clothes template `99490`; incidental already-covered Treepine captures were intentionally not imported. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 104`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `105` to `104`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104`. No runtime vendor behavior changed.
- Uncle Bazzit's Workshop live capture import completed from AOSharp capture `20260613-184615`. The validated staged SQL added 5 `4354 Uncle Bazzits Workshop (Dng)` vendor rows, 5 vendor templates, and 4 new shop inventory groups with 129 new inventory rows while reusing existing exact shop hash `Fash` for Maria's Fashion. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 99`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `104` to `99`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99`. No runtime vendor behavior changed.
- Jobe Basic dimensions live capture import completed from AOSharp capture `20260614-000058`. The validated staged SQL added 3 vendor rows across `4563 Hardware Dimension - Basic` and `4567 Dimensional Shift - Basic`, 3 vendor templates, and 3 new shop inventory groups with 98 inventory rows. The imported rows cover Basic Armor, Costly Regenerative Supplies --- 1-90, and Basic Implants; same-template Advanced dimensional targets were intentionally not imported without direct capture or explicit inference approval. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 96`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `99` to `96`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96`. No runtime vendor behavior changed.
- Jobe Advanced dimensions live capture import completed from AOSharp capture `20260614-002319`. The validated staged SQL added 3 vendor rows across `4564 Hardware Dimension - Advanced` and `4568 Dimensional Shift - Advanced`, 3 vendor templates, and 2 new shop inventory groups with 68 new inventory rows while reusing exact shop hash `HMIZ` for regenerative supplies. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 93`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `96` to `93`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96 -> 93`. No runtime vendor behavior changed.
- Jobe Superior dimensions live capture import completed from AOSharp capture `20260614-002319`. The validated staged SQL added 4 vendor rows across `4565 Hardware Dimension - Superior` and `4569 Dimensional Shift - Superior`, 4 vendor templates, and 4 new shop inventory groups with 116 inventory rows. Imported targets were Superior Armor, Superior Equipment for Nano Specialists, Costly Regenerative Supplies --- 100-175, and Superior Implants. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 89`, and `StatelVendorExclusions = 30`; actionable uncovered statel vendors dropped from `93` to `89`. Current coverage chain is `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96 -> 93 -> 89`. Incidental Heavenly Business capture evidence was already covered and not imported. No runtime vendor behavior changed.
- `1183 ord_smarket_omni_basic` static vendor coverage was expanded with 20 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `730` to `710`, and `1183 ord_smarket_omni_basic` dropped from `77` to `57`. No runtime vendor behavior changed.
- `1184 ord_smarket_omni_advanced` static vendor coverage was expanded with 21 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `710` to `689`, and `1184 ord_smarket_omni_advanced` dropped from `68` to `47`. No runtime vendor behavior changed.
- `1185 ord_smarket_omni_sup` static vendor coverage was expanded with 21 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `689` to `668`, and `1185 ord_smarket_omni_sup` dropped from `68` to `47`. No runtime vendor behavior changed.
- `500 Parnassos` static vendor coverage was expanded with 25 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `668` to `643`, and `500 Parnassos` dropped from `140` to `115`. No runtime vendor behavior changed.
- `1182 ord_smarket_clan_sup` static vendor coverage was expanded with 17 approved mappings. The approved rows are present in `cellao_codex_clean.vendors`; the latest import run did not insert duplicates because all 17 IDs already existed. Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `643` to `626`, and `1182 ord_smarket_clan_sup` dropped from `44` to `27`. No runtime vendor behavior changed.
- `655 Andromeda` static vendor coverage was expanded with 16 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `626` to `610`, and `655 Andromeda` dropped from `17` to `1`. Template `151987` remains unknown. No runtime vendor behavior changed.
- `1180 ord_smarket_clan_basic` static vendor coverage was expanded with 4 approved mappings. The targeted import backed up `vendors`, inserted only those rows into `cellao_codex_clean.vendors`, and verified `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`. Total uncovered statel vendors dropped from `610` to `606`, and `1180 ord_smarket_clan_basic` dropped from `43` to `39`. No runtime vendor behavior changed.
- `1181 ord_smarket_clan_advanced` static vendor coverage was expanded with 4 approved mappings. Commit `fbcc1a4` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 602`. Total uncovered statel vendors dropped from `606` to `602`, and `1181 ord_smarket_clan_advanced` dropped from `30` to `26`. No runtime vendor behavior changed.
- `2064 neut_basic_implants_shop` static vendor coverage was expanded with 3 approved mappings. Commit `ed869d5` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 599`. Total uncovered statel vendors dropped from `602` to `599`, and `2064 neut_basic_implants_shop` dropped from `15` to `12`. No runtime vendor behavior changed.
- `2073 neut_advanced_implants_shop` static vendor coverage was expanded with 3 approved mappings. Commit `a79b5ec` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 596`. Total uncovered statel vendors dropped from `599` to `596`, and `2073 neut_advanced_implants_shop` dropped from `15` to `12`. No runtime vendor behavior changed.
- `565 Newland Desert` static vendor coverage was expanded with 3 approved mappings. Commit `2bb7ad5` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 593`. Total uncovered statel vendors dropped from `596` to `593`, and `565 Newland Desert` dropped from `9` to `6`. No runtime vendor behavior changed.
- `2096 4holes Fashion` static vendor coverage was expanded with 3 approved mappings. Commit `0522ffb` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 590`. Total uncovered statel vendors dropped from `593` to `590`, and `2096 4holes Fashion` dropped from `7` to `4`. No runtime vendor behavior changed.
- `4567 Dimensional Shift - Basic` static vendor coverage was expanded with 3 approved mappings. Commit `7c10b5a` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 587`. Total uncovered statel vendors dropped from `590` to `587`, and `4567 Dimensional Shift - Basic` dropped from `5` to `2`. No runtime vendor behavior changed.
- `4568 Dimensional Shift - Advanced` static vendor coverage was expanded with 3 approved mappings. Commit `5e5303b` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 584`. Total uncovered statel vendors dropped from `587` to `584`, and `4568 Dimensional Shift - Advanced` dropped from `5` to `2`. No runtime vendor behavior changed.
- `4569 Dimensional Shift - Superior` static vendor coverage was expanded with 3 approved mappings. Commit `abee0ce` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 581`. Total uncovered statel vendors dropped from `584` to `581`, and `4569 Dimensional Shift - Superior` dropped from `5` to `2`. No runtime vendor behavior changed.
- `4563 Hardware Dimension - Basic` static vendor coverage was expanded with 2 approved mappings. Commit `0ded4a9` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 579`. Total uncovered statel vendors dropped from `581` to `579`, and `4563 Hardware Dimension - Basic` dropped from `4` to `2`. No runtime vendor behavior changed.
- `6553 Arete Landing` static vendor coverage was expanded with 2 approved mappings. Commit `389e8b3` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 577`. Total uncovered statel vendors dropped from `579` to `577`, and `6553 Arete Landing` dropped from `8` to `6`. No runtime vendor behavior changed.
- `4564 Hardware Dimension - Advanced` static vendor coverage was expanded with 2 approved mappings. Commit `aa62dcd` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 575`. Total uncovered statel vendors dropped from `577` to `575`, and `4564 Hardware Dimension - Advanced` dropped from `4` to `2`. No runtime vendor behavior changed.
- `4565 Hardware Dimension - Superior` static vendor coverage was expanded with 2 approved mappings. Commit `1810408` added the source SQL rows, the targeted import inserted only those rows into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 573`. Total uncovered statel vendors dropped from `575` to `573`, and `4565 Hardware Dimension - Superior` dropped from `5` to `3`. No runtime vendor behavior changed.
- `2060 neut_basic_weapon_shop` static vendor coverage was expanded with 1 approved mapping. Commit `83fc74f` added the source SQL row, the targeted import inserted only that row into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 572`. Total uncovered statel vendors dropped from `573` to `572`, and `2060 neut_basic_weapon_shop` dropped from `5` to `4`. No runtime vendor behavior changed.
- `2070 neut_advanced_weapons_shop` static vendor coverage was expanded with 1 approved mapping. Commit `9c41ed9` added the source SQL row, the targeted import inserted only that row into `cellao_codex_clean.vendors`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 571`. Total uncovered statel vendors dropped from `572` to `571`, and `2070 neut_advanced_weapons_shop` dropped from `5` to `4`. Backup: `C:\Users\Mike\Documents\Cellao-Clean\tools-temp\db-backups\vendors_before_2070_neut_advanced_weapons_shop_20260610_040826.sql`. Rejected candidates `135659521`/`297466`, `135659522`/`297470`, `135659523`/`99572`, and `135659524`/`99573` remain uncovered until matching `vendortemplate` evidence is found. No runtime vendor behavior changed.
- `600 Varmint Woods` static vendor coverage was expanded with 1 approved mapping. Commit `e197b9f` added the source SQL row, the targeted import inserted only that row into `cellao_codex_clean.vendors`, query-back confirmed `39321612 | 600 | 93063 | AdvOA`, and verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 570`. Total uncovered statel vendors dropped from `571` to `570`, and `600 Varmint Woods` dropped from `3` to `2`. Backup: `C:\Users\Mike\Documents\Cellao-Clean\tools-temp\db-backups\vendors_before_600_varmint_woods_20260610_052107.sql`. Rejected candidates `39321600`/`99479` and `39321601`/`99482` remain uncovered until matching `vendortemplate.ItemTemplate` evidence is found. No runtime vendor behavior changed.
- Surgery clinic and implant flows have documented repaired behavior.

# Partially Working Systems

- Inventory, corpse item loot, corpse credit loot, player trade item/credit/cancel, and vendor buy/sell/close persistence flows have passing source assertion coverage where available and completed live-client relog verification for the documented repaired paths.
- Combat works for basic weapon/NPC test scenarios, but packet semantics are not complete.
- Corpse visuals and `CorpseFullUpdate` remain areas for broader cleanup, but the corpse cash value offset is repaired and guarded by focused assertions.
- Shop/vendor database coverage is complete for practical live-accessible vendors. The remaining 26 statel coverage gaps are deferred access/setup backlog, not active capture work.
- Playfield/interior mapping has repaired fixtures and remaining audit candidates.
- Enemy spawn testing has supported low-level families, but final spawn tables are not complete.
- DB-backed mob loot is modeled and partially wired, with limited reviewed data.
- Nanos, tradeskills, teams, organizations, pets, missions, quests, perks, research, bank, bags, stacks, and containers need separate focused work.

# Known Broken Systems

- NPC chase/movement is high risk and not gameplay-ready.
- `PlayfieldAnarchyF` is documented as a current-client structure mismatch.
- Some packet classes are missing, under-modeled, or awaiting capture-backed runtime use.
- Broad static vendor coverage remains incomplete.
- Full gameplay systems for missions, quests, perks, research, pets, PvP/towers, teams, and organizations are not complete.

# Current Development Focus

Current work is focused on gated Rex Larsson B18E completion and B18F handoff smoke. Rex dialogue, safe B18C preview, B18C kill feedback/progress, B18D preview display, exact Cargo Box identity/use routing, B18D cleanup, and safe B18E `QuestFullUpdate` emission are wired behind disabled-by-default gates. The B18E completion gate `AO_REBIRTH_ENABLE_ARETE_REX_B18E_COMPLETION` now triggers only from the captured Rex return branch after `B18EPreviewed` state, sends DTO-built `QuestMessage Action=Delete` for only `Mission:5514B18E`, grants actual `+290 XP`, grants `+1040` credits, sends captured-equivalent reward feedback text, and sends DTO-built `QuestFullUpdate` for `Mission:5514B18F` / `Talk to Marcus Stone`. Action `59`, item rewards, inventory mutation, DB mission persistence, Marcus Stone dialogue, SQL/schema changes, raw packet replay, and broader live NPC integration remain unresolved.

Update 2026-06-18: the Rex B18D cleanup pass now adds a DTO-built `QuestMessage Action=Delete` for only `Mission:5514B18D`, sourced from `20260614-194454/packets.hex.log:5765`, after exact `Terminal:56D9B4AF` use. This is treated only as captured B18D mission-window cleanup; `Quest Delete` gameplay meaning remains unresolved. Rex chain state is in-memory only and routes `B18EPreviewed` players to captured return dialogue node `rex_194454_006` so Rex does not offer B18C again in that process-local state. No rewards, inventory, XP/credits, DB persistence, B18E completion, action `59`, SQL, schema changes, or raw packet replay are implemented.

Update 2026-06-18: B18E completion is now implemented as a gated preview/handoff path only. The B18E delete DTO body matches captured packet `#5495`, and the B18F QuestFullUpdate DTO body matches captured packet `#5497`, both byte-for-byte from the N3 body onward. The handler uses in-memory per-character completion flags to prevent duplicate B18E delete, XP grant, or B18F send. Manual in-client smoke is still required to verify visible B18E removal, XP increase, B18F appearance, and client stability.

# Last Completed Milestone

Rex Larsson B18C gated live objective progress completed:

- Added disabled-by-default progress gate `AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS`.
- All three Rex gates must be enabled for live objective progress: dialogue routing, B18C quest preview, and B18C progress.
- Successful safe B18C `QuestFullUpdate` preview now activates an in-memory `Mission:5514B18C` progress record for the source player only.
- The live death observer hooks `Playfield.KillNpcTarget` after the existing death animation send, where attacker, target identity, target name, and playfield are all available.
- Matching behavior is intentionally narrow: player attacker in Arete Landing, active B18C preview state for that player, target name exactly `Malfunctioning Cleaning Robot`, cap at `5/5`.
- Progress is logged server-side under `ARETE_REX_B18C_PROGRESS`; no mission-window progress packet is emitted because refresh fields are not proven.
- At `5/5`, only the in-memory objective complete flag is set. No Quest Delete, mission completion, rewards, inventory, XP/credit implementation, DB writes, B18D offer, chain progression, action `59`, or persistent mission state was added.
- Capture `20260614-194454` contains named `Malfunctioning Cleaning Robot` evidence with level `1`, HP `12/12`, `monsterData=297023`, and coordinates; representative raw references include `events.log:63`, `64`, `2719-2722`, and `3390-3408`.
- Five captured robot observations were promoted into isolated local runtime spawn data for playfield `6553`, using the minimum selected set with SimpleCharFullUpdate plus death evidence and 11 captured stat rows per robot. Local DB verification now shows Rex plus five target robots and zero unrelated `6553` rows.
- Focused ZoneEngine build passed, Rex inactive content dry-run passed, Arete validation harness passed 131 cases, and `git diff --check` passed with line-ending warnings only.

Prior Rex Larsson objective playback service completed:

- Target NPC: Rex Larsson, `SimpleChar:782DE568`.
- Target missions: `Mission:5514B18C`, `Mission:5514B18D`, and `Mission:5514B18E`.
- Temporary capture decoder `tools-temp/arete-analysis/scripts/decode_rex_questfullupdate.ps1` decodes Rex `QuestFullUpdate` packets from `packets.hex.log` using the existing x86 AOSharp capture assemblies.
- `Mission:5514B18C` title decoded as `Terminate 5 Malfunctioning Cleaning Robots`; objective decoded as `Kill 5 Malfunctining Cleaning Robots.` with captured spelling preserved.
- `Mission:5514B18D` title decoded as `Open the Cargo Box`; objective decoded as `Use (Right Click) the Cargo Box to open it.`
- `Mission:5514B18E` title decoded as `Return to Rex Larsson`; objective decoded as `Talk to Rex Larsson.`
- Rex content was updated with non-executable QuestFullUpdate evidence metadata, decoded titles/objectives, source identity linkage to `SimpleChar:782DE568`, and cautious packet-sequence links for `B18C -> B18D` and `B18D -> B18E`. `Mission:5514B18F` was observed as the next QuestFullUpdate after `B18E` delete but was not added because it is outside the target scope.
- Objective trigger metadata is now represented as non-executable evidence only: `B18C` records target name `Malfunctioning Cleaning Robot`, required count `5`, and `CharacterAction Action=Death` evidence; `B18D` records `GenericCmd Action=Use` against packet identity `Terminal:56D9B4AF`, with exact `SimpleItemFullUpdate` identity/position/rotation/stat evidence later recovered from Arete captures; `B18E` records `KnuBotOpenChatWindow` against Rex and adjacent return dialogue evidence.
- Inactive objective playback now replays stored Rex objective evidence into in-memory progress only: `B18C` reaches `5/5` with 9 matching robot death observations, `B18D` records `1/1` use interaction against packet identity `Terminal:56D9B4AF`, and `B18E` records `1/1` Rex talk observation. Later capture review found exact `SimpleItemFullUpdate` evidence for `Terminal:56D9B4AF` in Arete capture segments after the first Rex quest capture.
- `CharacterAction` action `59` remains unresolved because neither local AOtomation nor tool-side AOSharp names decimal `59` (`0x3B`), and no ZoneEngine handler maps it to offer, accept, complete, abandon, fail, or reward behavior.
- Tool-side AOSharp defines `QuestAction.Delete = 0x01`, so `Quest Delete` is packet-level delete/removal evidence. The gameplay cause remains unresolved. B18C per-kill progress mapping is partially implemented from capture, B18D uses packet identity `Terminal:56D9B4AF` with later exact full-update evidence, and B18D inventory effect plus B18E completion semantics remain unresolved.
- Focused ZoneEngine build, Rex aggregate validation/dry-run, Arete validation harness, and `git diff --check` passed. The dry-run left all three Rex missions `NotStarted`, executed 0 mission transitions, and kept objective playback separate from live character state.
- No runtime behavior, SQL, schema, live NPC wiring, packet emission, KnuBot behavior, persistence, inventory, XP, credits, rewards, or character mutation changed.

Neutral Training Startup Equipment import completed:

- Source capture: AOSharp capture `20260614-002319`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 2 vendor rows in `954 Neutral Training`, 1 vendor template, and 1 new shop inventory group with 9 inventory rows.
- Imported targets: two Basic Startup Equipment statels, vendor IDs `62521344` and `62521345`.
- Evidence note: both rows have direct VendorFull and ShopUpdate evidence from capture `20260614-002319`; both exact inventories deduplicate to shop hash `WHBW`.
- A test DB backup was created before import under `tools-temp/db-backups/neutral_training_startup_before_import_*.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 27`, and `StatelVendorExclusions = 169`.
- Total actionable uncovered statel vendors dropped from `29` to `27`.
- Current coverage/actionability chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96 -> 93 -> 89 -> 29 -> 27`.
- No runtime vendor behavior changed.

Prior Jobe Advanced dimensions live capture import completed:`r`n`r`n- Source capture: AOSharp capture `20260614-002319`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 3 vendor rows across `4564 Hardware Dimension - Advanced` and `4568 Dimensional Shift - Advanced`, 3 vendor templates, and 2 new shop inventory groups with 68 new inventory rows.
- Imported targets: Jobe Hardware Advanced Armor, Jobe Dimensional Advanced Regenerative Supplies, and Jobe Dimensional Advanced Implants.
- Evidence note: all three rows have direct VendorFull and ShopUpdate evidence from capture `20260614-002319`; regenerative supplies reused existing exact shop hash `HMIZ`.
- A test DB backup was created before import under `tools-temp/db-backups/jobe_advanced_dimensions_before_import_*.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 93`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `96` to `93`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96 -> 93`.
- No runtime vendor behavior changed.

Prior Jobe Basic dimensions live capture import completed:

- Source capture: AOSharp capture `20260614-000058`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 3 vendor rows across `4563 Hardware Dimension - Basic` and `4567 Dimensional Shift - Basic`, 3 vendor templates, and 3 new shop inventory groups with 98 inventory rows.
- Imported targets: Jobe Hardware Basic Armor, Jobe Dimensional Basic Regenerative Supplies, and Jobe Dimensional Basic Implants.
- Evidence note: all three rows have direct VendorFull and ShopUpdate evidence from capture `20260614-000058`; same-template Advanced dimensional targets were not imported without direct capture or explicit inference approval.
- A test DB backup was created before import under `tools-temp/db-backups/jobe_basic_dimensions_before_import_*.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 96`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `99` to `96`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99 -> 96`.
- No runtime vendor behavior changed.

Prior Uncle Bazzit's Workshop live capture import completed:

- Source capture: AOSharp capture `20260613-184615`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 5 `4354 Uncle Bazzits Workshop (Dng)` vendor rows, 5 vendor templates, and 4 new shop inventory groups with 129 new inventory rows.
- Imported targets: Maria's Fashion, Uncle Bazzit's Miscellany, Uncle Bazzit's Floorplans, Uncle Bazzit's Landscaping, and Uncle Bazzit's Furnishings.
- Evidence note: all five rows have direct VendorFull and ShopUpdate evidence from capture `20260613-184615`; Maria's Fashion reused existing exact shop hash `Fash`.
- A test DB backup was created before import under `tools-temp/db-backups/bazzits_workshop_before_import_*.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 99`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `104` to `99`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104 -> 99`.
- No runtime vendor behavior changed.

Prior Treepine Hut OT Clothes live capture import completed:

- Source capture: AOSharp capture `20260613-233535`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 1 `1887 Treepine Hut` vendor row, 1 vendor template, and 1 new shop inventory group with 16 inventory rows.
- Imported target: OT Clothes template `99490`, vendor id `123666433`, statel `0xC001075F`, coordinates X `199.189` Z `286.698`.
- Evidence note: VendorFull and ShopUpdate were both captured directly. Incidental already-covered Treepine captures were intentionally not imported.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\treepine_ot_clothes_before_import_20260613-233955.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 104`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `105` to `104`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105 -> 104`.
- No runtime vendor behavior changed.

Prior Omni Training Startup Shop live capture import completed:

- Source capture: AOSharp capture `20260613-231115`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 1 `950 Omni Training` vendor row, 1 vendor template, and 1 new shop inventory group with 7 inventory rows.
- Imported target: Startup Shop! template `100035`, vendor id `62259200`, statel `0xC00003B6`, coordinates X `60` Z `50`.
- Evidence note: VendorFull rows are emitted on playfield entry/dynel spawn for this target; ShopUpdate rows are emitted when the terminal is opened.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\omni_training_startup_before_import_20260613-232146.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 105`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `106` to `105`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106 -> 105`.
- No runtime vendor behavior changed.

Prior Tower Shop + BS Signup live capture import completed:

- Source capture: AOSharp capture `20260613-223554`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 18 vendor rows across `4704 Tower Shop (dungeon)` and `6007 BS Signup (dng)`, 18 vendor templates, and 18 new shop inventory groups with 2047 inventory rows.
- Imported targets: 14 Tower Shop terminals plus 4 BS Signup OFAB terminals.
- Excluded from this import: Clan City Buildings, Neutral City Buildings, and Leets-R-Us were VendorFull-only / not openable; remaining BS Signup profession-locked terminals require matching professions.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\tower_bs_signup_before_import_20260613-224634.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 106`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `124` to `106`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124 -> 106`.
- No runtime vendor behavior changed.

Prior Holes in the Wall live capture import completed:

- Source capture: AOSharp capture `20260613-221619`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 3 vendor rows across `791 Holes in the Wall` and `4565 Hardware Dimenion - Superior`, 2 vendor templates, and 1 new shop inventory group with 87 inventory rows.
- Imported targets: Holes in the Wall Containers, Holes in the Wall Superior Weapons, and one high-confidence exact-template inferred Hardware Dimension - Superior Superior Weapons statel.
- Reuse note: Holes in the Wall Containers exactly reused existing shop hash `Cont`; Holes in the Wall Superior Weapons created new shop hash `FZT5`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\holes_in_wall_before_import_20260613-222653.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 124`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `127` to `124`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127 -> 124`.
- No runtime vendor behavior changed.

Prior Broken Shores + Lush Fields live capture import completed:

- Source capture: AOSharp capture `20260613-215211`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 2 vendor rows across `665 Broken Shores` and `695 Lush Fields`, 2 vendor templates, and 2 new shop inventory groups with 190 inventory rows.
- Imported targets: Broken Shores OT Advanced Trade Skills and Lush Fields Basic Startup Equipment.
- Reuse note: Lush Fields Basic Startup Equipment did not reuse Newland Basic Startup Equipment because the captured treatment kit row is QL 8 instead of QL 4.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\broken_shores_lush_fields_before_import_20260613-221147.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 127`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `129` to `127`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129 -> 127`.
- No runtime vendor behavior changed.

Prior Clan Basic Startup Equipment import completed:

- Source capture: AOSharp capture `20260613-211234`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 4 `540 Old Athen` vendor rows, 1 vendor template, and 1 new shop inventory group with 9 inventory rows.
- Imported targets: four Old Athen Clan Basic Startup Equipment statels for template `99569`.
- Deduplication: all four startup terminals share one deduplicated shop inventory group, `VZMO`.
- Correlation note: `0xC000021C` had captured inventory but no direct VendorFull; it was accepted as Captured by template `99569` and exact inventory match with the three VendorFull-confirmed startup terminals.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\clan_basic_startup_before_import_20260613-212759.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 129`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `133` to `129`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133 -> 129`.
- No runtime vendor behavior changed.

Prior Newland + Omni startup import completed:

- Source capture: AOSharp capture `20260613-185338`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 9 vendor rows across `565 Newland Desert` and `710 Omni-1 Trade`, 6 vendor templates, and 6 new shop inventory groups with 232 inventory rows.
- Imported targets: Newland Basic Armor, Newland Basic Startup Equipment, Newland Basic Nano Clusters, Food, Drinks, and four OT Basic Startup Equipment statels.
- Deduplication: the four OT Basic Startup Equipment vendors share one deduplicated shop inventory group.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\newland_startup_before_import_20260613-204052.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 133`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `142` to `133`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142 -> 133`.
- No runtime vendor behavior changed.

Prior Arete ICC implant/cluster import completed:

- Source capture: AOSharp capture `20260613-172753`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 5 `6553 Arete Landing` vendor rows, 5 vendor templates, and 5 new shop inventory groups with 573 inventory rows.
- Imported core targets: ICC Basic Implants, ICC Faded Clusters, ICC Bright Clusters, ICC Shiny Clusters, and ICC Pharmacy.
- Incidental nearby capture evidence was intentionally excluded from this import.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\arete_icc_before_import_20260613-174753.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 142`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `147` to `142`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147 -> 142`.
- No runtime vendor behavior changed.

Prior Neutral ICC implant/cluster import completed:

- Source capture: AOSharp capture `20260613-170220`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 24 vendor rows across `2064 neut_basic_implants_shop` and `2073 neut_advanced_implants_shop`, 12 vendor templates, and 12 new shop inventory groups with 1876 inventory rows.
- Mapping basis: `2064` rows were captured directly; `2073` rows used high-confidence exact-template reuse from the captured `2064` template evidence, matching the existing ICC pharmacy reuse pattern across these interiors.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\neutral_icc_implants_before_import_20260613-171134.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 147`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `171` to `147`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171 -> 147`.
- No runtime vendor behavior changed.

Prior overnight exact-template inferred vendor import completed:

- Source evidence: existing captured and operator-approved inferred `vendortemplate` rows with exact `TemplateId` matches and existing `shopinventorytemplates` hashes.
- Source SQL promotion added the validated staged vendor inserts to `vendors.sql` only.
- Coverage added: 31 vendor rows across Parnassos, Varmint Woods, Andromeda, Broken Shores, and Treepine Hut.
- Reused existing vendortemplate hashes: `NBBBPWA`, `CAWFVZL`, `CAXKPAK`, `CAKVRD3`, `CA4ANR3`, `CAIYRLU`, `SPPJAN4`, `CSFKCVG`, `CSSD5SY`, `CSXKWKP`, `CSZKPVY`, `CSAUZMP`, `CS5JCOM`, `OSLC3UI`, `OSRA2ZZ`, `OSGQXEO`, `OSCP3HJ`, `OSXOL6H`, `OSQC5XR`, `CBGXGWQ`, `CASMUGY`, `CS3Q3IF`, `OBIUAFT`, `OAX2G2O`, `OST6OJS`, `OAE5BNV`, `OAW76SU`, `NBCQ762`, `CBIGA24`, and `OAL6IVC`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\overnight_exact_template_before_import_20260613-051359.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 171`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `202` to `171`.
- Current coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202 -> 171`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Clan Advanced General Shop import completed:

- Source capture: AOSharp capture `20260613-034740`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 16 `1181 ord_smarket_clan_advanced` vendor rows, 16 vendor templates, and 11 new shop inventory groups with 505 inventory rows.
- Reused shop hashes: `Cont`, `IVM2`, `IYD4`, `JTYS`, and `LJI7`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\cellao_codex_clean_clan_advanced_20260613-035810.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 202`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `218` to `202`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218 -> 202`.
- Spot checks passed for `ClanAdvancedWeapons`, `ClanAdvancedDevices`, and `AdvancedRangedWeaponComponents`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior spec_smarket specialty import (inferred) completed:

- Source inference: operator-approved reuse of Neutral Basic/Specialty captures `20260613-012810` and `20260613-014033`.
- Source SQL promotion added the validated inferred staged inserts to `vendortemplate.sql` and `vendors.sql`; `shopinventorytemplates.sql` was unchanged because all inventories reused existing shop hashes.
- Coverage added: 16 vendor rows across `1189 spec_smarket_clan_advanced`, `1190 spec_smarket_clan_sup`, `1191 spec_smarket_omni_advanced`, and `1192 spec_smarket_omni_sup`, plus 12 vendor templates.
- Reused shop hashes: `I3E4`, `7ATH`, `7X7Q`, `PX4X`, `FBQ3`, and `FLEW`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\spec_smarket_before_import_20260613-033215.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 218`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `234` to `218`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234 -> 218`.
- Spot checks passed for `ClanComputers`, `OTComputers`, and `ClanSpecialistCommerce`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Neutral Basic General/Specialty Shop import completed:

- Source captures: AOSharp captures `20260613-012810` and `20260613-014033`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 6 `1193 spec_smarket_neut_basic` vendor rows, 6 vendor templates, and 6 new shop inventory groups with 64 inventory rows.
- Specialist Commerce required Trader access and was captured in the second AOSharp session.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\neutral_basic_before_import_20260613-014923.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 234`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `240` to `234`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240 -> 234`.
- Spot checks passed for `NeutralBasicComputers`, `NeutralBasicSpecialistCommerce`, and `NeutralBasicSuperiorCars`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Omni Basic Implant Terminals import completed:

- Source capture: AOSharp capture `20260613-005616`.
- Source SQL promotion added the validated staged inserts to `vendortemplate.sql` and `vendors.sql`; `shopinventorytemplates.sql` was unchanged because all implant inventories reused existing shop hashes.
- Coverage added: 13 `1183 ord_smarket_omni_basic` implant vendor rows and 13 vendor templates, with existing implant shop hashes `5BUX`, `5M5F`, `6MQN`, `6YPW`, `7LZ3`, `A32J`, `JWHR`, `KV75`, `KVVT`, `O3KI`, `RNWW`, `RO4Q`, and `SBQ6` reused.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\omni_basic_implants_before_import_20260613-011140.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 240`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `253` to `240`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253 -> 240`.
- Spot checks passed for `BasicOmniTekAdventurerImplants`, `BasicOmniTekMetaPhysicistImplants`, and `BasicOmniTekKeeperImplants`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Omni Advanced General Shop import completed:

- Source capture: AOSharp capture `20260613-002828`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 23 `1184 ord_smarket_omni_advanced` vendor rows, 16 vendor templates, and 15 new shop inventory groups with 760 inventory rows, while reusing existing shop hash `LJI7`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\omni_advanced_before_import_20260613-004623.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 253`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `276` to `253`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276 -> 253`.
- Spot checks passed for `OTAdvancedArmor`, `OTAdvancedWeapons`, and `AdvancedImplants`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Clan Superior General Shop import completed:

- Source capture: AOSharp capture `20260612-232439`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 19 `1182 ord_smarket_clan_sup` vendor rows, 19 vendor templates, and 14 new shop inventory groups with 594 inventory rows, while reusing existing shop hashes `LJI7`, `CHHQ`, `OHOO`, `JYPE`, and `Cont`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\clan_superior_before_import_20260613-000803.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 276`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `295` to `276`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295 -> 276`.
- Spot checks passed for `ClanSuperiorArmor`, `ClanSuperiorWeapons`, and `ClanSuperiorContainers`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Clan Basic General Shop import completed:

- Source capture: AOSharp capture `20260612-225855`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 29 `1180 ord_smarket_clan_basic` vendor rows, 29 vendor templates, and 25 new shop inventory groups with 1575 inventory rows, while reusing existing shop hashes `G4XZ`, `HYDQ`, `LJI7`, and `R5R7`.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\clan_basic_before_import_20260612-231024.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 295`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `324` to `295`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324 -> 295`.
- Spot checks passed for `ClanBasicArmor`, `ClanBasicWeapons`, `BasicClanAdventurerImplants`, and `BasicImplants`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior Omni Superior General Shop import completed:

- Source capture: AOSharp capture `20260612-044234`.
- Source SQL promotion added the validated v2 staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 27 `1185 ord_smarket_omni_sup` vendor rows, 20 vendor templates, and 19 new shop inventory groups, with existing map shop hash `LJI7` reused.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\omni_superior_v2_before_import_20260612-220448.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 324`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `351` to `324`.
- Current live-capture coverage chain: `404 -> 381 -> 351 -> 324`.
- Spot checks passed for `OTSuperiorArmor`, `OTSuperiorWeapons`, and `SuperiorImplants`.
- Template `155225` remains excluded as a non-shop statel template.
- No runtime vendor behavior changed.

Prior coverage exclusion for non-shop `Refreshing Drink` statels completed:

- Excluded template: `155225`.
- Exclusion reason: `NonShopStatelTemplate`.
- Evidence: AOSharp captures `20260612-012644` and `20260612-044234` emitted VendorFullUpdate rows but no ShopUpdate inventory rows for these identities, and live operator verification found the Superior instances were not reachable/openable.
- Implementation: current-client verification keeps excluded rows in `statel-vendor-coverage.csv` with `CoverageExcluded` and `ExclusionReason`, but excludes them from coverage metrics, missing-vendor reports, `vendor-scan-targets.csv`, capture targeting, and import planning.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, `StatelVendorIssues = 351`, and `StatelVendorExclusions = 30`.
- Total actionable uncovered statel vendors dropped from `381` to `351`.
- No SQL, vendor mappings, imports, or runtime vendor behavior changed.

Prior Omni Basic General Shop import completed:

- Source capture: AOSharp capture `20260612-012644`.
- Source SQL promotion added the validated staged inserts to `shopinventorytemplates.sql`, `vendortemplate.sql`, and `vendors.sql`.
- Coverage added: 23 `1183 ord_smarket_omni_basic` vendor rows, 16 vendor templates, and 16 shop inventory groups with 690 inventory rows.
- A test DB backup was created before import: `C:\Users\Mike\Documents\AORebirth\tools-temp\db-backups\omni_basic_before_staged_import_20260612-032350.sql`.
- Verification showed `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 381`.
- Total uncovered statel vendors dropped from `404` to `381`.
- `1183 ord_smarket_omni_basic` uncovered count dropped from `39` to `16`.
- Spot checks passed for `OTBasicArmor`, `OTBasicWeapons`, and `BasicImplants`.
- No runtime vendor behavior changed.

Prior `600 Varmint Woods` vendor coverage expansion completed:

- Commit `e197b9f` added the 1 approved source SQL mapping.
- A targeted import inserted only that row into `cellao_codex_clean.vendors`.
- Query-back confirmed `39321612 | 600 | 93063 | AdvOA`.
- A `vendors` table backup was created before import: `C:\Users\Mike\Documents\Cellao-Clean\tools-temp\db-backups\vendors_before_600_varmint_woods_20260610_052107.sql`.
- Total uncovered statel vendors dropped from `571` to `570`.
- `600 Varmint Woods` uncovered count dropped from `3` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 570`.
- Rejected candidates `39321600`/`99479` and `39321601`/`99482` remain uncovered because no matching `vendortemplate.ItemTemplate` evidence exists.
- No runtime vendor behavior changed.

Prior `2070 neut_advanced_weapons_shop` vendor coverage expansion completed:

- Commit `9c41ed9` added the 1 approved source SQL mapping.
- A targeted import inserted only that row into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import: `C:\Users\Mike\Documents\Cellao-Clean\tools-temp\db-backups\vendors_before_2070_neut_advanced_weapons_shop_20260610_040826.sql`.
- Total uncovered statel vendors dropped from `572` to `571`.
- `2070 neut_advanced_weapons_shop` uncovered count dropped from `5` to `4`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 571`.
- Rejected candidates `135659521`/`297466`, `135659522`/`297470`, `135659523`/`99572`, and `135659524`/`99573` remain uncovered because no matching `vendortemplate` evidence exists.
- No runtime vendor behavior changed.

Prior `2060 neut_basic_weapon_shop` vendor coverage expansion completed:

- Commit `83fc74f` added the 1 approved source SQL mapping.
- A targeted import inserted only that row into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `573` to `572`.
- `2060 neut_basic_weapon_shop` uncovered count dropped from `5` to `4`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 572`.
- No runtime vendor behavior changed.

Prior `4565 Hardware Dimension - Superior` vendor coverage expansion completed:

- Commit `1810408` added the 2 approved source SQL mappings.
- A targeted import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `575` to `573`.
- `4565 Hardware Dimension - Superior` uncovered count dropped from `5` to `3`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 573`.
- No runtime vendor behavior changed.

Prior `4564 Hardware Dimension - Advanced` vendor coverage expansion completed:

- Commit `aa62dcd` added the 2 approved source SQL mappings.
- A targeted import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `577` to `575`.
- `4564 Hardware Dimension - Advanced` uncovered count dropped from `4` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 575`.
- No runtime vendor behavior changed.

Prior `6553 Arete Landing` vendor coverage expansion completed:

- Commit `389e8b3` added the 2 approved source SQL mappings.
- A targeted import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `579` to `577`.
- `6553 Arete Landing` uncovered count dropped from `8` to `6`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 577`.
- No runtime vendor behavior changed.

Prior `4563 Hardware Dimension - Basic` vendor coverage expansion completed:

- Commit `0ded4a9` added the 2 approved source SQL mappings.
- A targeted import inserted only those 2 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `581` to `579`.
- `4563 Hardware Dimension - Basic` uncovered count dropped from `4` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 579`.
- No runtime vendor behavior changed.

Prior `4569 Dimensional Shift - Superior` vendor coverage expansion completed:

- Commit `abee0ce` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `584` to `581`.
- `4569 Dimensional Shift - Superior` uncovered count dropped from `5` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 581`.
- No runtime vendor behavior changed.

Prior `4568 Dimensional Shift - Advanced` vendor coverage expansion completed:

- Commit `5e5303b` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `587` to `584`.
- `4568 Dimensional Shift - Advanced` uncovered count dropped from `5` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 584`.
- No runtime vendor behavior changed.

Prior `4567 Dimensional Shift - Basic` vendor coverage expansion completed:

- Commit `7c10b5a` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `590` to `587`.
- `4567 Dimensional Shift - Basic` uncovered count dropped from `5` to `2`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 587`.
- No runtime vendor behavior changed.

Prior `2096 4holes Fashion` vendor coverage expansion completed:

- Commit `0522ffb` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `593` to `590`.
- `2096 4holes Fashion` uncovered count dropped from `7` to `4`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 590`.
- No runtime vendor behavior changed.

Prior `565 Newland Desert` vendor coverage expansion completed:

- Commit `2bb7ad5` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `596` to `593`.
- `565 Newland Desert` uncovered count dropped from `9` to `6`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 593`.
- No runtime vendor behavior changed.

Prior `2073 neut_advanced_implants_shop` vendor coverage expansion completed:

- Commit `a79b5ec` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `599` to `596`.
- `2073 neut_advanced_implants_shop` uncovered count dropped from `15` to `12`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 596`.
- No runtime vendor behavior changed.

Prior `2064 neut_basic_implants_shop` vendor coverage expansion completed:

- Commit `ed869d5` added the 3 approved source SQL mappings.
- A targeted import inserted only those 3 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `602` to `599`.
- `2064 neut_basic_implants_shop` uncovered count dropped from `15` to `12`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 599`.
- No runtime vendor behavior changed.

Prior `1181 ord_smarket_clan_advanced` vendor coverage expansion completed:

- Commit `fbcc1a4` added the 4 approved source SQL mappings.
- A targeted import inserted only those 4 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `606` to `602`.
- `1181 ord_smarket_clan_advanced` uncovered count dropped from `30` to `26`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, `ShopInventoryIssues = 0`, and `StatelVendorIssues = 602`.
- No runtime vendor behavior changed.

Prior `1180 ord_smarket_clan_basic` vendor coverage expansion completed:

- Commit `b6a6410` added the 4 approved source SQL mappings.
- A targeted import inserted only those 4 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `610` to `606`.
- `1180 ord_smarket_clan_basic` uncovered count dropped from `43` to `39`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `655 Andromeda` vendor coverage expansion completed:

- Commit `9217459` added the 16 approved source SQL mappings.
- A targeted import inserted only those 16 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `626` to `610`.
- `655 Andromeda` uncovered count dropped from `17` to `1`.
- Template `151987` remains unknown and was not mapped.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `1182 ord_smarket_clan_sup` vendor coverage expansion completed:

- Commit `d7556bb` added the 17 approved source SQL mappings.
- The 17 approved rows are present in `cellao_codex_clean.vendors`.
- The latest import run did not insert duplicates because all 17 approved IDs already existed.
- A `vendors` table backup was created before the verification/import attempt.
- Total uncovered statel vendors dropped from `643` to `626`.
- `1182 ord_smarket_clan_sup` uncovered count dropped from `44` to `27`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `500 Parnassos` vendor coverage expansion completed:

- Commit `d47f12e` added the 25 approved source SQL mappings.
- A targeted import inserted only those 25 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `668` to `643`.
- `500 Parnassos` uncovered count dropped from `140` to `115`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `1185 ord_smarket_omni_sup` vendor coverage expansion completed:

- Commit `e755c25` added the 21 approved source SQL mappings.
- A targeted import inserted only those 21 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `689` to `668`.
- `1185 ord_smarket_omni_sup` uncovered count dropped from `68` to `47`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `1184 ord_smarket_omni_advanced` vendor coverage expansion completed:

- Commit `aa8da43` added the 21 approved source SQL mappings.
- A targeted import inserted only those 21 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `710` to `689`.
- `1184 ord_smarket_omni_advanced` uncovered count dropped from `68` to `47`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Prior `1183 ord_smarket_omni_basic` vendor coverage expansion completed:

- Commit `6dfb390` added the 20 approved source SQL mappings.
- A targeted import inserted only those 20 rows into `cellao_codex_clean.vendors`.
- A `vendors` table backup was created before import.
- Total uncovered statel vendors dropped from `730` to `710`.
- `1183 ord_smarket_omni_basic` uncovered count dropped from `77` to `57`.
- `DataFileIssues = 0`, `VendorDbIssues = 0`, and `ShopInventoryIssues = 0`.
- No runtime vendor behavior changed.

Smoke harness cleanup passed after stale assertions were aligned with current repaired behavior:

- `Run-CombatSmokeTests.ps1 -SkipBuild` passes.
- `Run-CorpseCreditTraceAssertions.ps1` passes.
- `Run-InventoryContainerRegressionAssertions.ps1` passes.
- Stale assertions for cash stat serialization, NPC/shop cash mutation, login-time debug enemy spawning, and corpse credit feedback were cleaned up.
- No gameplay behavior was changed by the harness cleanup.

Inventory Move Live Verification result: PASS. The item moved correctly before relog and remained in the correct slot after relog.

Equip Item Live Verification result: PASS. The item equipped correctly before relog, no duplicate remained in inventory, and the item remained equipped in the correct equipment slot after relog.

Unequip Item Live Verification result: PASS. The item moved from equipment slot to inventory correctly, the equipment slot became empty, no duplicate remained equipped, and after relog the item remained in inventory while the equipment slot stayed empty.

Corpse Item Loot Live Verification result: PASS. Non-credit corpse item appeared in inventory correctly, the corpse no longer offered the looted item, no duplicate item appeared, cash did not change from item loot, and the item remained in inventory after relog.

Corpse Credit Loot Live Verification result: PASS. One correct corpse credit message displayed, cash increased by exactly the awarded amount, no inventory item was created from credit loot, increased cash value persisted after relog, and no duplicate corpse credit feedback was observed.

Player Trade Item Live Verification result: PASS. Item left player A inventory correctly, appeared in player B inventory correctly, no duplicate item existed, cash remained unchanged, and after relog the item remained only with player B.

Player Trade Credits Live Verification result: PASS. Player A cash decreased by the expected amount, player B cash increased by the expected amount, no inventory items moved, appeared, or disappeared, cash values persisted after relog, and no duplicate cash behavior was observed.

Player Trade Cancel/Decline Live Verification result: PASS. Trade panes closed correctly, the offered item remained with the original player, cash remained unchanged, no duplicate item or cash behavior occurred, and state persisted correctly after relog.

Vendor Buy Live Verification result: PASS. Purchased item appeared in inventory correctly, cash decreased by the exact purchase price, no duplicate item appeared, and after relog the purchased item and reduced cash value both persisted.

Vendor Sell Live Verification result: PASS. Sold item left inventory correctly, cash increased by the exact sale price, no duplicate item appeared, and after relog the sold item remained absent and increased cash value persisted.

Vendor Close/Cancel Live Verification result: PASS. Pending vendor transaction state closed without accepting, cash stayed unchanged, items remained with their original owner/location, no duplicate item appeared, and the same item/cash state persisted after relog.

Live Persistence Verification complete. Inventory move, equip item, unequip item, corpse item loot, corpse credit loot, player trade item, player trade credits, player trade cancel/decline, vendor buy, vendor sell, and vendor close/cancel all matched expected client-visible behavior and survived relog.

Player-to-player trade verification passed after temporary `TRADE_*` trace logging was added in commit `4b68d4e`. Verification showed:

- Credit-only trade behaved as expected.
- Item-only trade behaved as expected.
- Mixed item-plus-credit trade behaved as expected.
- Cancel/decline trade behaved as expected.
- No player trade display or commit defect was reproduced.
- Temporary `TRADE_*` logging remains available for future trade investigation.

Prior corpse credit repairs were pushed to `origin/master` in commits `343a31d` and `e953c76` after verification showed:

- `CorpseFullUpdate` cash stat id remains at offset `203`.
- Corpse cash value is patched at offset `207`.
- The old hardcoded `111` cash value is not preserved.
- Delayed corpse credit award mutates cash once and sends the normal changed-stat packet.
- Manual server `ChatText` corpse credit feedback is suppressed so the client displays one corrected message.
- Cliff Malle playtest displayed one `You received 3 credits from the corpse.` message.

Prior ICC/Fair Trade vendor stock repairs were pushed to `origin/master` in commit `cffc5da` after verification showed:

- vendor DB issues: 0
- shop inventory item-cache issues: 0
- tradeskill room captured rows: 3,101
- tradeskill vendor rows: 38

# Vendor Coverage Deferred Backlog

Status: COMPLETE (LIVE COVERAGE).

Vendor coverage campaign complete for all practical live-accessible vendors. Remaining vendors require setup-specific access and are deferred.

Final state:

- Current uncovered count: 26.
- Covered: all practical live-accessible vendors reached during the campaign.
- Deferred: access-restricted, setup-specific, profession-locked, sided, special-location, or current-client divergence vendors.
- No SQL was generated for this freeze.
- No capture was run for this freeze.
- Existing mappings were not modified.

| Category | Playfield | Name | VendorId | TemplateId | Reason blocked | Required setup |
| --- | ---: | --- | ---: | ---: | --- | --- |
| Clan-only vendors | 665 | Broken Shores | 43581441 | 99522 | Clan-side shop access friction; not practical from current Omni-focused sweep. | Leveled/access-capable Clan character. |
| Clan-only vendors | 952 | Clan Training | 62390272 | 100034 | Clan starter/training access requires a Clan character in the correct area. | Clan character with access to Clan Training. |
| Clan-only vendors | 1426 | Clan Registration dng | 93454336 | 25885 | Clan registration interior; outside current Omni/non-swap scope. | Clan character and registration interior access. |
| Clan-only vendors | 1426 | Clan Registration dng | 93454337 | 81799 | Clan registration interior; outside current Omni/non-swap scope. | Clan character and registration interior access. |
| Clan-only vendors | 7012 | Freelancers Inc. HQ - Old Athen | 459538432 | 284692 | Old Athen/Clan-side Freelancers access requires separate Clan setup. | Clan character able to reach Old Athen Freelancers HQ. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674752 | 266562 | OFAB terminal is profession-locked. | Adventurer character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674753 | 266563 | OFAB terminal is profession-locked. | Agent character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674754 | 266569 | OFAB terminal is profession-locked. | Bureaucrat character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674755 | 266564 | OFAB terminal is profession-locked. | Doctor character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674756 | 266565 | OFAB terminal is profession-locked. | Enforcer character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674757 | 266566 | OFAB terminal is profession-locked. | Engineer character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674758 | 266567 | OFAB terminal is profession-locked. | Fixer character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674759 | 266568 | OFAB terminal is profession-locked. | Keeper character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674760 | 266570 | OFAB terminal is profession-locked. | Martial Artist character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674762 | 266572 | OFAB terminal is profession-locked. | Nano-Technician character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674763 | 266574 | OFAB terminal is profession-locked. | Shade character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674764 | 266573 | OFAB terminal is profession-locked. | Soldier character with BS Signup access. |
| BS Signup profession-locked | 6007 | BS Signup (dng) | 393674765 | 266575 | OFAB terminal is profession-locked. | Trader character with BS Signup access. |
| Tower Shop sided/org-dependent | 4704 | Tower Shop (dungeon) | 308281349 | 249724 | Terminal did not open during live attempts. | Unknown; revisit only with explicit tower-shop access investigation. |
| Tower Shop sided/org-dependent | 4704 | Tower Shop (dungeon) | 308281366 | 295890 | Sided city-building terminal; not openable from current character side. | Clan-side or appropriate city-building access setup. |
| Tower Shop sided/org-dependent | 4704 | Tower Shop (dungeon) | 308281368 | 295892 | Sided city-building terminal; not openable from current character side. | Neutral-side or appropriate city-building access setup. |
| ICC Holodeck / Arete divergence | 6131 | ICC Holodeck Alien Training | 401801216 | 287476 | Current-client Arete/Holodeck access diverges from legacy AO Rebirth playfield assumptions. | Separate current-client source-data investigation, not normal vendor capture. |
| Unicorn / registration / special terminals | 1427 | Omni registration dng | 93519872 | 81799 | Special registration interior; not part of practical shop sweep. | Registration-interior access investigation. |
| Unicorn / registration / special terminals | 1428 | Neutral organisation dng | 93585408 | 81799 | Special registration interior; access unknown. | Neutral organisation registration access investigation. |
| Unicorn / registration / special terminals | 4364 | Unicorn Outpost | 285999104 | 256457 | Special outpost terminal; access/route not confirmed during campaign. | Unicorn Outpost access and terminal capture plan. |
| Unicorn / registration / special terminals | 4364 | Unicorn Outpost | 285999105 | 287037 | Special outpost terminal; access/route not confirmed during campaign. | Unicorn Outpost access and terminal capture plan. |

# Next Milestone

Move to the next AO Rebirth system. Do not continue vendor capture/import work unless Mike intentionally reopens the deferred access backlog with the required character, profession, side, or special-location setup. Keep NPC movement out of scope unless explicitly selected later.
