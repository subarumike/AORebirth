# Rex B18C Robot Progress Smoke Result

Generated: 2026-06-17

## Summary

The local DB state and engine restart portions of the smoke were completed. The live client portion was not exercised during the watch window: after the gated restart, no LoginEngine or ZoneEngine client connection appeared in the logs, so Rex rendering, mission-window behavior, robot rendering, and B18C kill progress could not be verified.

Engines were restored to default gate-off behavior after the timeout.

Follow-up manual login after the default restore exposed a load-screen hang. ZoneEngine logged repeated playfield `6553` heartbeat `IndexOutOfRangeException` failures in `StatNanoDelta`, `StatMaxNanoEnergy`, and `StatLife`. The root cause was the five robot rows having only captured SCFU-visible stats; the existing heartbeat and SimpleCharFullUpdate paths also require safe actor-baseline stats.

The staged robot SQL patch was updated and reapplied locally with the same five captured robot spawns plus a separated runtime actor-baseline scaffold block. Playfield `6553` remains exactly Rex plus five robots, and each robot now has `27` stat rows. Engines were restarted gate-off after the repair; no fresh heartbeat exceptions appeared before the next client retry.

## Per-Kill Feedback Follow-Up

After live testing showed the B18C mission-window preview did not visually update after robot kills, capture `20260614-194454` was reinspected. The capture evidence shows per-kill feedback after B18C robot deaths as feedback packets, not as a decoded per-kill `QuestFullUpdate`:

- Kills `1/5` through `4/5` have captured encoded `FormatFeedbackMessage` payloads for the remaining-count text and captured `FeedbackMessage CategoryId=110 MessageId=249817907`.
- Kill `5/5` has the captured generic `FeedbackMessage CategoryId=110 MessageId=249817907`, followed by captured `Quest Delete` for `Mission:5514B18C` and a next `QuestFullUpdate`.
- Quest Delete and B18D handoff packets remain disabled because their live gameplay semantics are intentionally out of scope for this pass.

The gated B18C progress tracker now sends only the captured per-kill feedback after matched `Malfunctioning Cleaning Robot` deaths:

- `1/5` through `4/5`: captured `FormatFeedbackMessage` plus captured generic `FeedbackMessage`.
- `5/5`: captured generic `FeedbackMessage` only.

No mission completion, Quest Delete, B18D offer, rewards, inventory, XP/credits, DB writes, character mutation, or Cargo Box behavior was added.

Manual smoke after this first feedback pass showed:

- ZoneEngine counted all five robot kills.
- ZoneEngine logged `ARETE_REX_B18C_PROGRESS feedback sent` for progress `1/5` through `5/5`.
- The client visibly rendered the first progress feedback only.

The captured packet headers use server-sender framing for the feedback messages. The B18C feedback sender was updated to use the normal `ZoneClient.SendCompressed(message)` server sender instead of the character-sender overload used by the old reward feedback helper.

The engines were rebuilt and restarted with all three gates enabled:

| Engine | PID | Result |
| --- | ---: | --- |
| `ChatEngine` | `7664` | started after server-sender feedback update |
| `LoginEngine` | `34132` | started after server-sender feedback update |
| `ZoneEngine` | `33520` | started after server-sender feedback update |

Expected next smoke:

- Start B18C from Rex so `Mission:5514B18C` appears in the mission window.
- Kill the five spawned `Malfunctioning Cleaning Robot` NPCs.
- Watch for `ARETE_REX_B18C_PROGRESS feedback sent ... sender=server` in ZoneEngine logs after each counted kill.
- Observe the client feedback text after each kill.

## Mission-Window Handoff Follow-Up

Manual smoke with server-sender feedback proved the server counted all five robots and sent feedback for `1/5` through `5/5`, but the client mission window still did not advance because the captured end-of-B18C packet sequence was still disabled.

The capture evidence at `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/events.log:5919-5926` shows the final B18C cluster:

- `CharacterAction` action `59` targeting `Mission:5514B18C`, with `Parameter1=56003` and `Parameter2=1427419532`.
- `Quest` action `Delete` for `Mission:5514B18C`.
- next `QuestFullUpdate` for `Mission:5514B18D`.

The gated B18C progress tracker now sends that one-time mission-window handoff after matched progress reaches `5/5`. This is still evidence-backed packet handoff only:

- No rewards.
- No inventory mutation.
- No XP/credit implementation.
- No DB writes.
- No persistence.
- No Cargo Box behavior.
- No B18D objective behavior.
- No B18E behavior.
- No interpretation of action `59` or `Quest Delete` gameplay semantics.

Expected next smoke:

- Start B18C from Rex.
- Kill all five `Malfunctioning Cleaning Robot` NPCs.
- Confirm ZoneEngine logs `Arete Rex B18C completion handoff sent`.
- Confirm the client mission window no longer stays stale on B18C and shows `Open the Cargo Box` / `Mission:5514B18D`.

Validation after adding the handoff:

- Focused ZoneEngine build passed with package restore disabled.
- Arete validation harness passed 131 cases.
- `git diff --check` passed with line-ending warnings only.

## Files Inspected

- `AI_START_HERE.md`
- `AGENTS.md`
- `docs/ai/AI_START.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `docs/project/KNOWN_DECISIONS.md`
- `docs/project/ARCHITECTURE.md`
- `docs/ai/WORKFLOW.md`
- `start-engines.ps1`
- `stop-engines.ps1`
- `AORebirth/Config/Config.xml`
- `AORebirth/Built/Debug/ZoneEngineLog.txt`
- `AORebirth/Built/Debug/LoginEngineLog.txt`
- `AORebirth/Built/Debug/ChatEngineLog.txt`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/AreteRexDialogueRouter.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/RexQuestPreviewEmitter.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/RexB18CObjectiveProgressTracker.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/SafeQuestFullUpdateSender.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/CharacterActionMessage.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/CharacterActionType.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/QuestFullUpdateMessage.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3MessageType.cs`
- `tools-temp/external/aosharp/AOSharp.Common/SmokeLounge/AOtomation/Messaging/Messages/N3Messages/QuestMessage.cs`
- `tools-temp/external/aosharp/AOSharp.Common/SmokeLounge/AOtomation/Messaging/Messages/N3Messages/QuestAction.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs`
- `AORebirth/Libraries/Source/AORebirth.Core/NPCHandler/NonPlayerCharacterHandler.cs`
- `AORebirth/Libraries/Source/AORebirth.Stats/Stat.cs`
- `AORebirth/Libraries/Source/AORebirth.Stats/Stats.cs`
- `AORebirth/Libraries/Source/AORebirth.Stats/SpecialStats/StatHealDelta.cs`
- `AORebirth/Libraries/Source/AORebirth.Stats/SpecialStats/StatHealInterval.cs`
- `AORebirth/Libraries/Source/AORebirth.Stats/SpecialStats/StatLife.cs`
- `AORebirth/Libraries/Source/AORebirth.Stats/SpecialStats/StatMaxNanoEnergy.cs`
- `AORebirth/Libraries/Source/AORebirth.Stats/SpecialStats/StatNanoDelta.cs`
- `AORebirth/Libraries/Source/AORebirth.Stats/SpecialStats/StatNanoInterval.cs`
- `AORebirth/Libraries/Source/AORebirth.Stats/SpecialStats/StatTitleLevel.cs`
- `AORebirth/Libraries/Source/AORebirth.Database/Entities/DBMobSpawnStat.cs`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260614-194454/events.log`
- `tools-temp/sql-staging/arete_malfunctioning_cleaning_robot_mobspawns.sql`

## Files Changed

- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/RexB18CObjectiveProgressTracker.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/SafeQuestFullUpdateSender.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/QuestAction.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/QuestMessage.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/SmokeLounge.AOtomation.Messaging.csproj`
- `docs/generated/rex_b18c_robot_progress_smoke_result.md`
- `docs/generated/arete_malfunctioning_cleaning_robot_spawn_result.md`
- `docs/ai/CURRENT_TASK.md`
- `docs/project/PROJECT_STATE.md`
- `tools-temp/sql-staging/arete_malfunctioning_cleaning_robot_mobspawns.sql`

No schema files, quest content, rewards, inventory, XP/credits, DB persistence, B18D objective behavior, B18E behavior, Cargo Box behavior, or validation tooling were changed in this smoke phase.

## DB Verification

Pre-smoke DB verification passed:

| Check | Result |
| --- | ---: |
| total `mobspawns` rows in playfield `6553` | `6` |
| `Malfunctioning Cleaning Robot` rows in playfield `6553` | `5` |
| unrelated rows in playfield `6553` | `0` |
| captured stat rows per robot | `11` |
| heartbeat-safe total stat rows per robot after repair | `27` |
| each robot has level `1` | yes |
| each robot has life/current health `12/12` | yes |
| each robot has `monsterData=297023` | yes |

Current verified rows:

| Id | Name | X | Y | Z |
| ---: | --- | ---: | ---: | ---: |
| `2016273768` | `Rex Larsson` | `3624.59912` | `51.745` | `787.74652` |
| `2027138231` | `Malfunctioning Cleaning Robot` | `3608.66138` | `51.745` | `795.9552` |
| `2027138245` | `Malfunctioning Cleaning Robot` | `3598.61523` | `51.745` | `774.02472` |
| `2027138246` | `Malfunctioning Cleaning Robot` | `3606.31909` | `51.745` | `801.37567` |
| `2027138249` | `Malfunctioning Cleaning Robot` | `3617.60181` | `51.745` | `783.97473` |
| `2027138259` | `Malfunctioning Cleaning Robot` | `3607.9126` | `51.745` | `796.26025` |

## Gated Engine Restart

Engines were stopped, then restarted with these environment gates set in the launch environment:

```powershell
$env:AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING = '1'
$env:AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW = '1'
$env:AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS = '1'
```

Gated restart result:

| Engine | PID | Result |
| --- | ---: | --- |
| `ChatEngine` | `5088` | started |
| `LoginEngine` | `35280` | started |
| `ZoneEngine` | `2000` | started |

Startup logs showed:

- `LoginEngine` listening on `0.0.0.0:7500`.
- `ZoneEngine` listening on `0.0.0.0:7501`.
- `ChatEngine` listening on `0.0.0.0:6996` and `0.0.0.0:7012`.

## Live Smoke Observation

A 12-minute ZoneEngine log watch was started after the gated restart, looking for:

- `ARETE_REX_DIALOGUE`
- `QuestFullUpdate`
- `ARETE_REX_B18C_PROGRESS`
- `Malfunctioning Cleaning Robot`
- `targetName=`
- `progress=5/5`

Result:

- No fresh Rex dialogue markers were observed.
- No fresh B18C QuestFullUpdate preview markers were observed.
- No fresh robot kill/progress markers were observed.
- No `ARETE_REX_B18C_PROGRESS progress=5/5` marker was observed.

Follow-up log inspection showed no new LoginEngine or ZoneEngine client connection after the gated restart. The only post-restart lines were engine startup/listening lines.

## Requested Smoke Checks

| Check | Result |
| --- | --- |
| Rex rendered | not verified; no client-side zone session observed after gated restart |
| Rex talked | not verified; no fresh `ARETE_REX_DIALOGUE` marker observed |
| B18C appeared in mission window | not verified; no fresh QuestFullUpdate preview marker observed |
| Robots rendered near captured positions | not verified; no client-side zone session observed after gated restart |
| Progress reached `1/5` through `5/5` | no; no fresh `ARETE_REX_B18C_PROGRESS` markers observed |
| In-memory objective complete | no; no `progress=5/5 complete=true` marker observed |

## Forbidden Behavior Check

Because the live client smoke did not exercise Rex or combat, no smoke-time quest completion behavior occurred.

No new implementation was added for:

- Quest Delete.
- completion packets.
- B18D offer.
- rewards.
- inventory changes.
- XP/credits.
- DB writes.
- character stat mutation.

The existing gated preview/progress code remains the only relevant runtime path, and it was not triggered during this watch window.

## Gate-Off Restore

After the watch timed out, the three gate variables were removed from the shell environment and engines were restarted with default behavior:

```powershell
Remove-Item Env:\AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING -ErrorAction SilentlyContinue
Remove-Item Env:\AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW -ErrorAction SilentlyContinue
Remove-Item Env:\AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS -ErrorAction SilentlyContinue
```

Default restart result:

| Engine | PID | Result |
| --- | ---: | --- |
| `ChatEngine` | `35152` | started |
| `LoginEngine` | `10824` | started |
| `ZoneEngine` | `35244` | started |

Default startup logs showed all three engines listening again.

## Load-Screen Hang Follow-Up

Manual login after the default restore hit the Arete load path and hung. ZoneEngine logged repeated heartbeat failures for playfield `6553`:

- `StatNanoDelta.get_GetBaseValue()`
- `StatMaxNanoEnergy.get_GetBaseValue()`
- `StatLife.get_GetBaseValue()`

Those derived stats index breed/profession/title-level tables and read movement, stamina, psychic, body development, nano pool, and current nano. `SimpleCharFullUpdate` also reads side, fatness, breed, sex, race, current nano, visual profession, NPC family, and LOS height.

Repair applied:

- Stopped engines.
- Updated `tools-temp/sql-staging/arete_malfunctioning_cleaning_robot_mobspawns.sql`.
- Reapplied the SQL to `cellao_codex_clean`.
- Verified playfield `6553` still has exactly `6` `mobspawns`.
- Verified exactly `5` `Malfunctioning Cleaning Robot` rows remain.
- Verified each robot now has `27` stat rows.
- Restarted engines gate-off.

The added robot scaffold stats are runtime safety data only. They do not implement quest completion, rewards, progress packets, B18D, B18E, Cargo Box, or any mission-state behavior.

## Validation

Completed:

- DB verification query against `cellao_codex_clean`.
- Gated engine stop/start.
- Gated ZoneEngine marker watch.
- Post-timeout client-connection log inspection.
- Gate-off engine stop/start.
- Load-screen hang log diagnosis.
- Heartbeat-safe robot SQL reapply.
- Post-repair DB count verification.
- Post-repair gate-off engine restart.
- Focused ZoneEngine build with package restore disabled after the per-kill feedback code change.
- Arete validation harness with `-SkipBuild`, passing 131 cases.
- Gated engine restart after the per-kill feedback code change.
- Focused ZoneEngine build with package restore disabled after the B18C completion handoff code change.
- Arete validation harness with `-SkipBuild`, passing 131 cases after the handoff code change.
- `git diff --check`.
- Temporary-index `git diff --cached --check` covering untracked smoke/spawn report and SQL patch files.

The initial focused build attempt hung inside the legacy NuGet restore target. The successful build used `/p:RestorePackages=false`; packages were already present in the workspace.

## Remaining Blocker

The live smoke needs to be rerun while a client actually logs in after the gated restart. The next run should start a fresh log watch, then confirm the client reaches Arete, talks to Rex, receives B18C, sees the robots, and kills all five robots so the server-side progress log can prove `1/5` through `5/5`.

## Next Implementation Step

Rerun the same smoke with the client active. Do not implement quest completion, rewards, B18D, Cargo Box, or progress refresh packets until the gated server-side B18C kill progress reaches `5/5` against the runtime robot rows.
