# Corpse Reopen Capture Evidence Audit

Generated deterministically from the local repository evidence on 2026-07-12. This is an evidence-only artifact; it does not authorize a packet or runtime change.

## 1. Outcome

**Outcome B — no proven existing sequence found.**

No existing capture proves the corpse open → close → reopen contract.

## 2. Search scope

The audit enumerated local capture-session directories from capture marker files, then inspected corpse/loot events, parsed inventory traces, raw packet logs when present, repository documentation and source references, Git history, superseded implementations, and quarantine enforcement/tests. It excluded `.git`, build `obj`, and IDE metadata while retaining ignored local capture evidence.

## 3. Capture locations inspected

- `For Repo`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures`

## 4. Session accounting

- Total capture-session directories inspected: **264**
- Candidate sessions containing corpse or loot evidence: **111**
- Sessions containing a complete initial corpse-open request/response: **60**
- Sessions containing an observable local close-hook event: **8**
- Sessions containing possible same-corpse post-close traffic: **8**
- Proven same-corpse UI reopen sequences: **0**
- Classification totals: `{'INSUFFICIENT_EVIDENCE': 51, 'PARTIAL_OPEN_CLOSE': 8, 'PARTIAL_OPEN_ONLY': 52}`

The complete candidate inventory is in `corpse_reopen_candidate_sessions.json`; no weak candidate was silently discarded.

## 5. Best partial candidates

- `20260509-225115`: local `CloseContainer` invocation plus repeated same-corpse Use/InventoryUpdate traffic; actual loot-window reopening is not independently observed.
- `20260509-225210`: local `CloseContainer` invocation plus repeated same-corpse Use/InventoryUpdate traffic; actual loot-window reopening is not independently observed.
- `20260509-225300`: local `CloseContainer` invocation plus repeated same-corpse Use/InventoryUpdate traffic; actual loot-window reopening is not independently observed.
- `20260509-225700`: local `CloseContainer` invocation plus repeated same-corpse Use/InventoryUpdate traffic; actual loot-window reopening is not independently observed.
- `20260517-220409`: local `CloseContainer` invocation plus repeated same-corpse Use/InventoryUpdate traffic; actual loot-window reopening is not independently observed.
- `20260517-222543`: local `CloseContainer` invocation plus repeated same-corpse Use/InventoryUpdate traffic; actual loot-window reopening is not independently observed.
- `20260517-223438`: local `CloseContainer` invocation plus repeated same-corpse Use/InventoryUpdate traffic; actual loot-window reopening is not independently observed.
- `20260707-142139`: local `CloseContainer` invocation plus repeated same-corpse Use/InventoryUpdate traffic; actual loot-window reopening is not independently observed.

The strongest known partial is `20260707-142139`: `(Corpse:F0F001)` receives the initial `GenericCmd Action=Use` and `InventoryUpdate Handle=112`; the harness invokes `InventoryGUIModule.CloseContainer`; later same-corpse Use traffic receives another `InventoryUpdate Handle=112`. The harness declares reopen from inventory/access signals, not from verified UI window state. This matches a server refresh, but does not prove the client reopened the window.

Exact decoded evidence is in `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260707-142139/events.log` at lines 184, 190, 192, 204, 206, 208, 223, and 225. Corresponding raw frames are in `packets.hex.log` at lines 130-132, 136-137, and 146-147. The smoke predicate that makes this evidence partial rather than proven is in `tools-temp/AOSharpLiveCapture/CombatLootSmoke.cs` at lines 705-799.

`20260509-225115` is also partial: it records a local close hook and changes from Handle 121 to Handle 122, but the harness reports reopen before the later inventory response and subsequently fails remaining-item looting. It therefore does not validate the rejected fresh-handle hypothesis.

## 6. Initial-open packet sequence

The best partial sessions consistently show an outbound `GenericCmd` with `Action=Use`, the player as user, the corpse as target, and a client count, followed by an inbound `InventoryUpdate` for the corpse containing a handle and loot entries. That sequence proves initial access only.

## 7. Close sequence evidence

No raw outbound close packet is established. The only explicit close boundary in the promising sessions is local smoke-harness instrumentation stating that `InventoryGUIModule.CloseContainer` was called. Historical harness logic may accept an access/inventory signal or elapsed delay as close/reopen progress, so its PASS text is not independent UI evidence.

## 8. Reopen packet sequence evidence

Promising smoke sessions contain later outbound same-corpse `GenericCmd Action=Use` traffic and inbound same-corpse `InventoryUpdate` traffic. This proves a repeated request/server refresh after the local close call. It does not prove the loot UI reopened, and there is no captured network close/teardown transition that explains the client state needed for reopening.

## 9. Packet and action comparison

The partial evidence uses `GenericCmd Action=Use` for both initial and later access. It does not prove that this alone is the reopen contract. The `0x66` action was introduced without authoritative capture proof and is rejected. No alternative close or teardown action is proven.

## 10. Identity comparison

For the promising partials, the corpse type, corpse instance, target identity, player identity, and playfield remain the same across repeated access. Sessions involving other corpse identities are not treated as reopen proof. Static containers, backpacks, bank, trade, pets, corpse replacement, and refreshed snapshots without a close boundary are not promoted to proof.

## 11. Inventory-handle comparison

The evidence is inconsistent: `20260707-142139` uses Handle 112 before and after the local close hook, while `20260509-225115` changes from 121 to 122 and does not complete reliable remaining-item looting. Thus neither same-handle reuse nor fresh-handle allocation is proven as the missing reopen mechanism; the fresh-handle hypothesis remains rejected.

## 12. Counter and ordering comparison

Client `GenericCmd.Count` values increase across attempts, and captured frame sequence numbers preserve request/response ordering. The counters establish event order but do not identify a close transition. No server-generated counter has been demonstrated to be the client UI reopen key.

## 13. Loot and state comparison

Partial sessions show full inventory snapshots and, after item moves, reduced contents. They do not independently preserve or expose the loot-window open/closed state. Corpse presence is recorded in the smoke sequences, but a refreshed inventory snapshot must not be conflated with a visible reopened window.

## 14. Historical code findings

- `a4089f62` extracted corpse-access orchestration and its assumptions into a runtime service.
- `5a7077e1` removed an older alternating/access-action path in favor of the captured initial corpse-inventory open path.
- `dfb64a18` introduced the unproven `0x66` reopen supplement and altered runtime/tests/docs. The current task establishes that behavior as failed and rejected.
- The local fresh-handle experiment was never accepted as capture-backed proof and is not present as a retained fallback in these report changes.

Commit `dfb64a18` changed `Playfield.cs`, `PlayfieldCorpseAccessRuntimeService.cs`, `PlayfieldRuntimeSystems.cs`, `PlayfieldLifecycleTraceTests.cs`, `docs/ai/CURRENT_TASK.md`, and `docs/project/PROJECT_STATE.md`. Commit `5a7077e1` changed the same three runtime surfaces plus `PlayfieldLifecycleTraceTests.cs` to remove the older action-only alternation. No historical implementation inspected supplied a complete authoritative UI close/reopen capture.

Historical smoke PASS messages are not authoritative because their success predicate is a signal/inventory refresh rather than observed UI reopening.

## 15. Quarantine status

The supported-content provider filters 29 source instances through `RuntimeQuarantinedSourceInstances`. The ordinary-content provider filters all nine rows whose evidence capture is `20260710-202132`. Their orchestrators consume the filtered provider results. Tests assert both filter expressions. No current spawn path bypass was found in the inspected provider/orchestrator path.

Quarantined supported enemies: Discarded Pet (11), Disobedient Bot (2), Mugger (5), Violent Vagabond (11). Quarantined ordinary enemies: Looter (2), Stim Fiend (6), Deranged Shopper (1).

Supported source instances: `0x79557C09`, `0x79557C26`, `0x79557C31`, `0x79557C8B`, `0x79557CA7`, `0x79557CAB`, `0x79557CAD`, `0x7957E411`, `0x7957E4A5`, `0x7957E4B1`, `0x7957E4BC`, `0x79557C66`, `0x7957E40A`, `0x79557F14`, `0x7957E5C6`, `0x7957E5C7`, `0x7957E5C8`, `0x7957E5CA`, `0x79557CAC`, `0x7957405C`, `0x795743A7`, `0x795743A8`, `0x7957E02C`, `0x7957E02E`, `0x7957E123`, `0x7957E40E`, `0x7957E5BF`, `0x7957E5C4`, `0x7957E5C5`.

Ordinary source instances: `0x79557CB8`, `0x7957E5CD`, `0x79557F12`, `0x7957E128`, `0x7957E415`, `0x7957E5CF`, `0x7957E5D0`, `0x7957E5D1`, `0x79574527`.

Enforcement is at `CapturedSubwayContentProvider.cs:20-29,251-263` and `CapturedSubwayOrdinaryContentProvider.cs:3350-3354`; consumers are `CapturedSubwaySpawnOrchestrator.cs:65-68` and `CapturedSubwayOrdinarySpawnOrchestrator.cs:41-44`; regression assertions are in `PlayfieldLifecycleTraceTests.cs:1224-1226,1436-1438`.

## 16. Evidence conclusion

No existing capture proves the corpse open → close → reopen contract.

Existing evidence proves initial open, local invocation of a close API in smoke tests, and repeated same-corpse network inventory responses. It does not prove the actual client loot window closed and then visibly reopened, nor does it expose a definitive network close/teardown transition. Implementing another action, handle, counter, or state mutation would be inference.

## 17. Minimum targeted capture required

Use one ordinary enemy and no other container interaction:

1. Start the approved comprehensive capture immediately before killing the enemy.
2. Kill it and keep the same corpse identity visible.
3. Open the corpse and wait until its loot window is visibly open.
4. Leave at least one item or credit entry in the corpse.
5. Close the loot window and visibly confirm it is closed.
6. Wait briefly without interacting with another corpse, backpack, bank, static container, trade, pet, or summon.
7. Reopen that exact corpse and wait until the same loot window is visibly open again.
8. Move one remaining loot entry to prove the reopened window is functional.
9. Stop capture only after the transfer is acknowledged.

The capture must preserve full inbound/outbound raw traffic, timestamps, directions, frame sequence numbers, `GenericCmd.Count`, action values, player and corpse identities, inventory/container identities, inventory handles, loot contents, corpse lifecycle state, playfield, distance/position, and an explicit UI close/open marker or synchronized visual evidence. Do not start this capture as part of this audit.

## 18. Required unknowns

The future capture must resolve whether close is networked or implicit; whether inventory teardown occurs; whether reopen uses the initial action; whether a handle is reused, refreshed, or server-issued; whether the response is a full snapshot or delta; and which client state transition causes the loot window to become visible again.

## 19. Machine-readable artifacts

- `corpse_reopen_candidate_sessions.json` contains every discovered corpse/loot candidate and its fixed-result classification.
- `corpse_reopen_timelines.json` contains exact ordered events for every promising session, including source file and line references.
