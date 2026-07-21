# Quest System Audit and Implementation Status

Date: 2026-07-17
Primary quest capture: `20260717-223626`
TOTW gateway capture: `20260717-232249`

## Outcome

AORebirth now has one persistent, character-scoped mission foundation in the ZoneEngine runtime. The existing Rex/Marcus B18C through B194 handoff path uses it, and Windcaller Karrec is the first additional capture-backed quest implemented through the same repository, transition service, reward ledger, content bootstrap, and session reload entry points.

This is not a general production-ready AO mission system. The implemented scope is the currently proven Arete flow and the bounded Karrec/TOTW entrance flow described below.

## Implemented

### Persistent ownership and lifecycle

- Mission ownership is keyed by stable `CharacterId + QuestId`; no production mission mutation can omit character identity.
- MySQL tables persist mission lifecycle, objective progress, deduplicated observations, character flags, account flags, and reward stages.
- Lifecycle timestamps cover offered, accepted, completed, failed, and abandoned states. Records also carry created/updated timestamps and optimistic versions.
- Account access is keyed to the normalized account username from the character record. The MySQL repository verifies that the supplied account owns the character before an account-scoped transaction can write a flag.
- Login, reconnect, zoning, and ZoneEngine-restart reload entry points reconstruct one character's durable snapshot. They deliberately do not invent general client-journal replay packets.
- The retained older mission-state and objective-playback scaffolding is character-scoped and is not the production owner used by the dialogue router.

### Transition and reward safety

- Current-mission completion plus next-mission activation runs in one repository transaction for B18C to B18D, B18D to B18E, B18E to B18F, and B18F to B194.
- Durable observation keys suppress repeated kill, object-use, talk, and item-delivery callbacks.
- Numeric character rewards update the `stats` rows and reward ledger in one MySQL transaction.
- External inventory rewards use a durable claim/lease stage. Marcus item `296780` records a pre-grant inventory baseline, persists the inventory grant, and only then completes the reward stage; retries do not treat a pre-existing copy as the quest reward.
- Client packet projections use durable per-character flags. Network send and projection-flag persistence cannot be one physical transaction, so a process stop between those two operations may repeat a harmless client projection, but it cannot repeat mission rewards.

### Arete Rex and Marcus migration

- B18C Cleaning Robot progress is persistent and isolated per player. Final-observation retries can complete the mission and activate B18D after repository/service reconstruction.
- B18C per-kill client feedback remains disabled and is covered by an executable fail-closed policy test.
- B18D accepts only the existing exact Cargo Box interaction, completes persistently, and activates B18E through the atomic handoff.
- B18E persists the Rex-return observation, awards `290` XP and `1040` credits once, removes B18E from the client projection, and activates the existing B18F handoff state.
- B18F persists Marcus completion, grants item `296780` through the shared inventory service once, removes B18F from the client projection, and activates the existing B194 preview/handoff state.
- B18F and B194 remain bounded handoff definitions with no invented objectives.

### Windcaller Karrec and TOTW access

- Source playfield is `655`. The capture-local player identity is `SimpleChar:7944C065` (`2034548837`).
- Captured NPC identities are Windcaller Karrec `SimpleChar:796360BB`, Annoying Dude `SimpleChar:796360BD`, and Maddy Cardile `SimpleChar:796360BC`.
- Playfield `655` now materializes all three as passive code-backed capture NPCs. Their exact captured SCFU appearances are preserved; Karrec is stationary, Annoying Dude replays the complete `16`-segment walking cycle, and Maddy Cardile replays the complete `19`-segment walking cycle. Runtime identity registration, spawn cleanup, and dialogue registration share the same bounded content definitions.
- Mission `Mission:55579381` is accepted through the captured Karrec dialogue path. No speculative mission-window accept/decline/abandon handler was added.
- Annoying Dude grants Bronto Burger `297042`; Maddy grants Maddy's Credit Card `297043` through persisted inventory writes.
- Completion requires exactly two distinct offered slots containing one burger and one card. Unknown or extra offerings fail closed. The selected slots and durable trade-pending state are used for crash recovery, so unrelated duplicate copies elsewhere in inventory do not decide whether the offered copies were consumed.
- Completion records both delivery observations, completes the mission, awards one full Rubi-Ka level of direct XP through the shared XP/level-up path, applies the two-token Daily Mission XP Reward once per reached mission-token level tier, and only then writes account flag `totw-wall-access`. Level 25 receives `21500` XP and `+4` tokens; level 60 receives `203500` XP and `+6` tokens while retaining prior XP-bar progress.
- Clan tokens use stat `62`, Omni tokens use stat `75`, and Neutral characters receive neither tokens nor token feedback. Capture-local personal-research feedback and PerkUpdate values are not replayed because research belongs to an expansion system outside AORebirth's implemented scope.
- The level/XP/token decision is durably snapshotted before either offering is consumed. Retry therefore cannot recalculate a higher token tier after the XP level-up, switch token counters after a faction change, or turn a Neutral completion into a later sided reward. Unsupported XP levels fail before inventory mutation.
- Completion sends the captured post-trade dialogue, direct-XP projection in the excluded research slot, side-token pulses/feedback, action `59`, mission deletion, and SocialStatus reset in ordered durable retryable projection stages. No follow-on mission is invented. The direct-XP placement is an implementation decision because official live diverted the XP component into research.
- The pre-consumption snapshot, live-stat hydration, state/wire split, and fail-stopping order are implementation- and Debug-build-validated; a private-client completion/reconnect smoke remains required before calling those paths live-proven.
- Gateway `Terminal:C004028F` is accepted only in playfield `655`, only when that terminal exists in the loaded playfield data, and only for an account with `totw-wall-access`. Eligible use sends the captured acknowledgement and PF `647` transfer with payload landing `(1814, 29, 2699)` and captured heading/envelope fields.
- Because the denial packet was not captured, denial uses the existing generic denied acknowledgement and does not invent retail-specific feedback.

## Repairs made during the audit

- Replaced quest-only and process-local authoritative ownership with character-scoped persistence.
- Removed the B18C/B18D crash window that could leave a completed objective stuck in an active mission.
- Made all Arete handoffs durable, atomic at the repository boundary, and retry-safe.
- Prevented extra Karrec trade items and stale trade-session selections from satisfying the quest.
- Bound trade completion to the Karrec identity that actually opened the trade, while retaining the exact captured identity contract.
- Replaced global item-presence recovery with exact persisted offered-slot recovery.
- Delayed the account gateway flag until both the scaled token reward and direct full-level XP reward are durable.
- Added a pre-consumption reward snapshot plus amount-bearing reward provenance, preventing retry-time level/side drift and preserving a durable zero-token Neutral decision.
- Made completion projection sequential and fail-stopping so a failed earlier wire stage cannot be skipped while later stages are marked complete.
- Removed unsafe fixed `PerkUpdate` replay.
- Added server-side verification that the requested TOTW terminal exists in the current playfield data.
- Strengthened Marcus item idempotency so a pre-existing `296780` does not suppress the captured reward.
- Added MySQL verification that an account-scoped mission transaction owns the character.

## Not implemented or still unresolved

- Personal research is an unimplemented expansion system and is explicitly outside this quest runtime. Capture-local `5000`/`45000` diversion messages, absolute `UnsavedXP=170716`, and player-specific PerkUpdate values remain evidence only; they are not persisted as Karrec rewards or projected to the client.
- Pre-correction `side-tokens-2` ledger rows prove only that the old fixed `+2` was applied; they do not retain completion level, side, or the missing tier delta. They are not automatically backfilled from mutable current character state. Any such legacy under-reward or interrupted completion needs targeted repair backed by completion-time evidence.
- Durable XP and tokens are exactly-once, but completion packets are intentionally at-least-once because projection flags are written after a successful send. A process failure in the send/flag gap, or login normalization after the XP commit but before mission reconciliation, can replay the final unacknowledged level-up projection without duplicating the persisted reward.
- Banked alien-level follow-up remains coupled to successful XP projection, matching the existing ordinary level-up order. If an earlier completion dialogue/network stage fails, that follow-up is delayed until completion projection retries or login reconciliation runs.
- No general mission-journal reconstruction occurs on login/reconnect/zoning. Durable state reloads, while captured quest packets are emitted only by their proven dialogue, objective, and completion triggers.
- No team missions, shared kill credit, random missions, repeatable missions, timed missions, escort missions, branching quests, or broad mission-window client actions were added.
- The capture does not prove abandon, failure, repeat acceptance, alternate completion, or team-sharing behavior for Karrec, so those routes remain unavailable.
- The capture does not identify database template or `mobspawns` IDs for this trio. They are therefore materialized from code-backed capture definitions with free runtime identities rather than speculative database rows; the dialogue router binds those identities by the shared exact names/content contract.
- The gateway capture proves one nearby successful use but not the official maximum interaction distance. The implementation verifies exact identity, loaded playfield object, playfield, and account access without inventing a retail distance threshold.
- Live schema creation and private-client restart persistence remain unconfirmed. An approved ZoneEngine restart attempt did not open port `7501` within 60 seconds after startup database connection; ChatEngine and LoginEngine were healthy. No destructive database operation was performed.
- A private-client smoke is still required for Karrec dialogue, item handout/trade, reward display, restart recovery, account-shared wall access, denied wall use, and the PF647 landing.

## Evidence and validation

- `20260717-223626`: `3940/3940` raw records, zero raw write/projection/callback errors; bounded fixture and evidence report checked in. Temporary-copy validation decoded `288/288` SCFU rows with zero failures or incomplete rows.
- `20260717-232249`: `220/220` raw records, zero errors; exact gateway Use, acknowledgement, N3Teleport envelope, PF647 payload, and post-init landing recovered.
- `20260721-023942`: second Karrec completion, with level 60 confirmed by the user, captures three sequential stat-`75` projections (`4005/4007/4009`) and absolute `UnsavedXP=170716`. Item `285612`, the repository stat contract, and the mission-token ratio resolve the projections as three `+2` Omni-token pulses; research/PerkUpdate values remain player-specific and must not be hardcoded. A bounded fixture is checked in at `AORebirth/Server/ZoneEngine/Content/Captured/Quests/windcaller_karrec_completion_20260721_023942.json`.
- Persistent mission foundation tests: `12/12` PASS.
- Quest runtime persistence and duplicate-reward tests: `9/9` PASS.
- Daily Mission XP Reward scaling/source/provenance tests: `9/9` PASS.
- Checked-in bootstrap/runtime-definition tests: `5/5` PASS.
- Karrec eligibility and gateway identity rules: `3/3` PASS.
- Capture-backed Karrec NPC population/appearance/patrol/dialogue-link/lifecycle tests: `7/7` PASS; complete Windcaller-focused suite: `11/11` PASS.
- Full approved legacy wrapper: `428/452` PASS with the same `24` known unrelated/harness failures; every affected Karrec/reward suite passes when run through its focused approved filter.
- AORebirth Debug build, including ZoneEngine: PASS.

The capture evidence remains bounded in `Content/Captured/Quests`; the original raw captures were not modified or copied into source control.
