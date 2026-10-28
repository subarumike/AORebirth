# Mission traps: recovery consolidation and implementation

Date: 2026-10-28. Branch: `codex/mission-traps-recovery`.
Inspected and branched from freshly fetched public master
`9a18a15a2d760aab896c708f532e31aa4078bb39`.

**Status: implemented and verified for development, with five independently
reproduced public-source regression exceptions. Generated trap profiles remain
disabled by default. No master merge, deployment, client operation, running
engine change, player database access or schema migration was performed.**

## Existing research reused

The completed original-client investigations were read together with their
machine-readable resources, native exports and retained packet relationships:

| Actual completed report | Evidence reused |
| --- | --- |
| `Docs/AO_MISSION_DUNGEON_INTERACTIVES_RECOVERY_001.md` | Trap/Mine interaction mechanics, common object loading, resource programs, container/parent relationships and the warning about the lockable authorization list |
| `Docs/AO_MISSION_DUNGEON_INTERACTIVES_RECOVERY_002.md` | Lockpicking/disarming, ActionIIR dispatch, CA190, tool definition, thirteen exact trigger deliveries and subtype state transitions |
| `Docs/AO_MISSION_DUNGEON_INTERACTIVES_RECOVERY_003.md` | Smoke Bomb effect sequences, signed AMS modifiers, Taunt versus damage, lifecycle distinctions and Mine collision callbacks |

The requested Recovery002/003 filenames were stale aliases; the actual files
above contain the completed investigations. Their corresponding
`Docs/generated/mission_dungeon_interactives_recovery_001/002/003` evidence was
used. The prior deep-recovery common/TrapItem wire schema was reused with the
later corrections, rather than treating its older labels as current authority.

Private consolidation retains 38 machine-readable artifacts, all three reports,
six complete original resource records/programs, thirteen exact event109 joins,
four Smoke Bomb sequences and sixty selected original packets. All sixty packet
lengths/hashes and nine historical source-CSV hashes were verified. Original
identities, native addresses, allocations and source locators remain preserved
in those private artifacts; no packet corpus or protected resource data is
published with this branch.

No Ghidra investigation or capture campaign was repeated. No new capture was
requested. The previous October28 review's failure to incorporate these
completed findings is superseded by this consolidated assessment.

### Important correction preserved

**PROVEN:** the scaled list in ChestFullUpdate's Lockable portion is an
authorization list, not a trap-child list. The earlier deep-recovery
`trap_identities` label is superseded by Recovery001's explicit warning and
Recovery002's consumer: `A0BEF -> 859A1 -> Lockable+0x98`, read by `85890`.
The pending-trap collection at `+0x1A4` is distinct. Historical Chest ordinal481
has an empty wire authorization list while Trap482 names it as its parent.

Generated trap IDs are therefore **not** inserted into `Chest.UnknownArray`.
The existing chest spawn bytes, authorization list and loot behavior remain
unchanged. Supported association uses `TrapItemFullUpdate.Parent`.

## All 221 latest packet records

The complete supplied October27/28 corpus contains 25 sessions, including the
five sessions whose finalization metadata is incomplete. Their surviving raw
records were included, rather than discarded because of stale zero counters.

| Packet family | Selected | Completely decoded | Remaining byte-grammar failures | Observed parent/transform form |
| --- | ---: | ---: | ---: | --- |
| MineFullUpdate, `0x215B5678` | 173 | 173 | 0 | Parentless; explicit position/quaternion |
| TrapItemFullUpdate, `0x59313928` | 48 | 48 | 0 | Parented: 45 containers and 3 doors; no absolute transform on wire |
| Total | 221 | 221 | 0 | Exact source bytes retained |

**PROVEN decoder cause:** current AOtomation had enum keys but no concrete
contracts for these two families. Its subtype lookup returned no concrete
message and deserialization returned null. The previous adapter incorrectly
grouped these unsupported N3 records under `NON_N3`. This is independent of the
separate ChestFullUpdate overflow errors; this change does not claim those
chest errors were repaired.

The existing mission analyzer now has a bounded `--traps` mode. It preserves
the complete packet identity, pass-on byte, parent, conditional transforms,
playfield, neutral identities, header, stats, opaque data, raw74 and flags,
alongside source session, ordinal, sequence, context and hashes.

The recovered TrapItem tail is exactly 25 bytes. Every Mine record additionally
has eight bytes: marker1 and a neutral integer matching that record's raw74.
Those eight bytes are preserved as `MineMarker` and `UnknownMineValue`.
**Their native consumer semantics remain UNVERIFIED.** Successful structural
decoding does not turn that value into damage, difficulty, a timer or a trigger.

All 25 source CSV hashes matched before and after replay. Offline replay
completed in approximately1.17 seconds. Canonical product serializer tests also
consumed and byte-for-byte reserialized all221 records without alteration.

The analyzer's five Quest scalar accesses now accept the recorded assembly's
existing neutral property names while retaining neutral output labels. Missing
properties fail explicitly; no absent scalar value or meaning is invented.

The thirteen historical event109 controls and all four Smoke Bomb cast/CA107/
CA98 sequences use pass-on zero. Runtime emission preserves that observed
envelope instead of inheriting N3's default1. Optional reveal, detection and
disarmed notifications explicitly use the same AORebirth envelope policy;
no unobserved original packet fixture is claimed for those notifications.

## Consolidated mechanics matrix

`PROVEN` below refers to the stated native/resource/packet scope. Automated
server verification is separate from original-client acceptance.

| Mechanic | Recovered evidence and distinction | Implemented status | Genuine limit |
| --- | --- | --- | --- |
| Object publication and parentage | Common FullUpdate reader `Gamecode+A1661`; TrapItem reader `+A28CA`; exact parent identities | Canonical Trap/Mine messages; generated ordinary TrapItem children retain their exact chest parent | Neutral identity78/80 generation binding is unknown |
| Detection inputs | Perception136 and TrapDifficulty289 | Captured stats retained; optional named AORebirth comparison uses resource289 or an explicit override | Original server threshold, probability, schedule and radius unknown |
| Detection notification | CharacterAction190 resolves its second identity as SimpleItem; notification is not visibility | Optional per-connection CA190 notification | No claim that notification alone reveals/disarms a trap; unused scalar choices are our policy |
| Remembered detection | TrapFlags bit0; independent of enabled bit1 | Preserved separately from reveal/disable state | Not interpreted as a visibility bit |
| Reveal112 | Ordinary Trap `+996C1` assigns reveal mesh/enables visibility; Mine `+85CE6` also remembers detection | Explicit reveal transition; saved original revealer replayed after reconstruction | No retained original112 delivery fixture or complete rendered-idempotence proof; replay is AORebirth publication policy |
| Disarming skill/tool | DisarmTrap135, current enum `TrapDisarm`; original tool95576 | Optional exact-target route requires hydrated owned inventory, CanFlags.DisarmTraps and configured skill mode | No observed successful original95576 attempt; its stored BreakingEntry10 is not a character threshold |
| Disarm108 | Ordinary Trap parent cleanup differs from Mine's flag0x40 behavior | Ordinary trap108 publication after a durable configured success | Not automatically destruction or the TrapDisarmed state; Mine gameplay remains disabled |
| TrapDisarmed | Separate received enabled-state clearing | Canonical message; persisted disarmed state and reconstruction companion | No inferred universal original success sequence or tool consumption |
| Trigger109 | ActionIIR key `0x2049527C`, reader `+9F042`, activation `+9F098`;13 exact deliveries | Exact target trap/event/actor message; validated parent-open activation | Native109 alone does not establish one-shot consumption or original server chance |
| Smoke Bomb effects | Four exact controls:3 Shroud83957,1 Cornea83955 | Passive resource-defined AMS modifiers -88/-118 through existing buff/expiry machinery | No inferred selector threshold from the QL2/220 observations |
| Effect source | Cast source51008; CA98 carries trap instance, client uses its50000 context alias | Correct in-memory caster and Cast ->109 ->victim107 ->98 sequence | Existing active-nano storage loses all caster identities on hydration; see below |
| Taunt CF7D | Authored149/193 is TauntNPC, not damage | Deliberately inactive | Original trap hate/source behavior not established |
| Lizard CF47 | Resource program found, terminal identity/activation bridge incomplete | Disabled | No speculative NPC summon |
| Mine collision / SpellList16 | Recovered collision callbacks and OnEnter list execution | Packet preservation only | No server activation radius/cadence/effect policy implemented |
| Mine140 / destruction | Malfunction, flag state, AttackCount and destruction are distinct | Preserved in evidence; incomplete gameplay disabled | No invented failure penalty, rearm, disappearance or destruction rule |

The four Smoke Bomb controls are all parented: three to chests and one to a
door. All thirteen retained109 joins are parented: eleven chests and two doors.
The first runtime slice deliberately uses the three observed chest-opening
controls; it does not manufacture a parentless Smoke Bomb collision behavior.

## Implemented runtime slice

- New mission trap entities attach to existing generated chests. They inherit
  their validated parent's world location for locality/range checks and have
  zero server collision radius. No new floor/wall placement or navigation
  system is introduced, and doors/objectives/traversal gain no new blocker.
- The optional trap policy is frozen with a newly accepted mission. Existing
  missions without that policy are not retrofitted. Random selection happens
  once; a durable seal records even an empty population.
- Existing mission DAO transactions lock the owner/quest and validate exact
  assignment, active state, expiry and frozen JSON. Existing mission flags
  store the seal, identities, parents, profile and state. There is no schema
  migration, alternative inventory ownership or chest-loot persistence.
- State changes compare the stored previous state under the existing lock.
  A second stale projection cannot win. Triggered, detected, revealed,
  disarmed, disabled and destroyed states are not conflated.
- Successful closed-to-open chest use invokes the trap after the existing
  contents response. No close, denied open or failed ownership check triggers
  it. The chest loot selector, rarity, QL, RK exclusions and item rows are not
  changed.
- Locality publishes the exact chest parent before its trap, including a
  child-first candidate order on reconnect. Saved reveal/disarmed companions
  follow the child spawn. Missing/stale parents fail closed.
- Partial materialization retracts only its own exact registered objects and
  callbacks through existing locality/registry cleanup. An unrelated identity
  collision is not overwritten. The durable population remains reconstructable.
- Trap persistence errors do not escape an already successful chest opening.
  An uncertain projection retires without publishing or retrying an effect.
- Environmental application reuses existing passive Skill buff admission,
  stat rebasing, duration and expiry. Unsupported programs fail closed. The
  unresolved Taunt program is not reinterpreted as damage or executed through
  a fake player caster.

### Detection and disarming

Both optional modes default to `Disabled`. The implemented opt-in mode is
`AORebirthSkillAtLeastDifficulty`, with resource `TrapDifficulty` or an explicit
`ConfiguredDifficulty` override. These are **AORebirth decisions**, not original
server formulas. Range and difficulty choices are editable.

Detection feedback is per connection and never automatically reveals the trap.
Reveal is a separate transition. Disarming requires the exact supplied trap
target and actual held inventory tool; no unproven parent-target substitution
is added. Acknowledgement follows the durable transition. No tool consumption,
failed-attempt damage or successful original-client tool workflow is claimed.

## Editable policy and disabled defaults

The existing `DungeonGeneration.json` mechanism gains optional `Traps` content:
`Enabled`, `ChestChance`, `MaximumCount`, weighted profiles with explicit mission
QL bands/nano IDs, captured spawn fields and interaction settings. Runtime C#
contains mechanics, not hard-coded Smoke Bomb IDs or individual content rows.

The public default is disabled with no profiles. A private, disabled development
fragment preserves the exact low Smoke Bomb control. Its proposed25% chest
chance, maximum8, fixed effect, QL band and ranges are explicitly authored
AORebirth policy, not retail probabilities or scaling. Repeating activation is
unsupported; the first slice requires explicit one-shot consumption policy.

**Unknown generation binding remains disabled:** captured identity78 has
type54002 and differing instances across original objects. Its reusable role
and rebinding are not established. No parent/player/trap substitution is
invented. The private fragment retains exact fields for review but is not an
accepted release profile or a claim of new-client compatibility. Canonical
private GameData was not changed or replaced by the public disabled example.

## Verification

All product checks used the existing `run-feature-validation` operation on the
isolated feature branch. Receipts remain `FEATURE_VALIDATION`, `DEPLOYABLE=NO`,
`WINDOWS_USER_ACCEPTED=NO`. These are not deployment or live-client acceptance.

| Verification | Result |
| --- | --- |
| Existing offline analyzer compilation | PASS |
| Latest173 Mine /48 TrapItem decode, complete consumption |221/221;0 remaining byte-grammar failures |
| Actual canonical serializer read/write round trips |221/221 byte exact |
| Historical event109 target/actor separation |13 retained deliveries verified |
| Original resource hashes / Smoke Bomb sequence preservation |6 allocations and4 sequences verified |
| New trap packet/resource/runtime/persistence tests |49 passed;0 failed;0 skipped |
| Selected existing chest/door/mission/visibility cohort |218 passed;5 independently reproduced baseline failures;0 skipped |
| Combined unique cases after fixture correction |267 passed;5 baseline failures;272 total |
| Reviewed production-selector loot regression |10,000 normal chest populations across QL1-250 passed; no policy edits |
| Original frozen cave / five-floor reconstruction |PASS using the retained original input and actual generated layouts |
| Live AO client, running servers and player databases |Not operated |

Coverage includes parent identity and ordering, no physical obstruction,
detection versus reveal, resource difficulty, supported modifier/expiry behavior,
disarm without consumption, stale/distant/disconnected actors, multiplayer state
races, sealed empty populations, allocation-free reconstruction, changed/expired
runs, transaction rollback, uncertain commits and partial registration cleanup.

Tests were bounded by per-test limits/45-second hang collection and180/240-second
process-tree deadlines. Runs completed without reaching the process deadline.
The temporary independent baseline checkout was removed only after preserving
and hashing its noncapability audit outputs. No profiles, credentials, live
capabilities or acceptance state were copied as validation evidence.

The initial new-test failures were resolved in test setup: BOM-aware private
JSON reading and establishing derived player vitals before checking that the
AMS debuff does not damage health. No production change was made to satisfy
those assertions. Missing existing review/frozen-cave fixture settings were
supplied from retained originals; no fixture or catalog was regenerated.

### Five proven public baseline exceptions

Exact unchanged public source
`9a18a15a2d760aab896c708f532e31aa4078bb39`, tree
`750d195dcaa4d684941c3bbfe8bec63a38d3593a`, independently ran41 destination
tests:36 passed,5 failed,0 skipped. Tracked source remained clean.

- `UnsupportedQualityFailsBeforeAllocatingAnyOfferIdentity(34/36/200)`:
  expected `NotSupportedException` was not thrown, line299.
- `OriginQualityPoolsRemainDistinctAndFallbackUsesOnlyTheExactRequestedQuality`:
  existing `Assert.IsFalse` failure, line322.
- `UnsupportedQualityHandlerDoesNotPublishChargeOrSendOffers`:
  expected0, actual1, line478.

Names and signatures match the feature run. No destination behavior or stale
expectation was changed. Raw mixed-cohort and baseline receipts correctly
record FAIL; they were not rewritten to PASS. Overall assessment is
`PASS_WITH_PROVEN_PUBLIC_BASELINE_EXCEPTIONS`, with zero new trap failures.

## Remaining limits

1. Original server detection/disarm scheduling, formulas, chance, penalties,
   consumption, rearming and universal sequencing remain unknown.
2. Mine collision/list16 gameplay, malfunction140 policy, Lizard spawning and
   trap-specific Taunt behavior remain disabled.
3. Mine's eight-byte extension meaning and neutral identity78/80 generation
   binding remain unresolved. Byte preservation is proven; semantic meaning
   and new-client acceptance are not.
4. Existing active-nano persistence stores no caster identity:
   `Character.SnapshotActiveNanosForPersistence`, `ActiveNanoRecord`,
   `PersistedActiveNanoData`, and existing SQL omit it; hydration substitutes
   the player at `PlayerHydrator.cs:102`. Debuff resource/duration survive the
   existing path, but trap caster attribution does not survive hydration.
   This inherited limitation was not concealed or expanded into a schema fix.
5. A durable trap claim and environmental effect are not one cross-system
   transaction. A crash after claim can consume the trap without delivering
   the effect. Unknown outcomes are not retried; exactly-once delivery is not
   claimed.
6. Reveal112 reconstruction is a documented publication policy. Rendered
   idempotence, original disarm-tool success and new mission-client bindings
   have not been verified by operating the AO client.

## Changed files and Git disposition

Protocol changes: named Trap identity and CA190 enum; three canonical message
models; existing AOtomation project and active shared compile inventory.

Runtime changes: `Core/Entities/MissionTrap.cs`, the narrow `Chest.AfterOpen`
hook, environmental methods in `Core/Nanos/NanoRuntime.cs`, exact trap item-use
dispatch, parent visibility ordering, existing playfield manager/build/disposal
hooks, `MissionTrapPolicy.cs`, `MissionTrapPopulation.cs`, frozen parameters,
generation policy and `QuestService.Traps.cs`.

Tooling: existing mission analyzer project/entry point, neutral Quest scalar
compatibility, `TrapPacketReplay.cs` and `RawTrapFullUpdateDecoder.cs`.

Verification: four `MissionTrap*Tests.cs` files and existing test-project
includes. Documentation/content: this report, `docs/ai/CURRENT_TASK.md` and the
public disabled `DungeonGeneration.json` entry.

Private evidence remains under ignored `mission-traps-recovery-20261028`:
research consolidation/manifests, historical fixtures, decoder receipts,
disabled content fragment, baseline proof, TRX files and command logs. Its
contents are excluded from the feature commit. Final Git status and pushed
feature commit are reported with the task completion; master is not merged.

### Exact changed paths (pre-commit inventory)

- AORebirth/GameData/DungeonGeneration.json
- AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/IdentityType.cs
- AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/CharacterActionType.cs
- AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/MineFullUpdateMessage.cs
- AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/TrapDisarmedMessage.cs
- AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/Messages/N3Messages/TrapItemFullUpdateMessage.cs
- AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/SmokeLounge.AOtomation.Messaging.csproj
- AORebirth/Server/ZoneEngine_New.Tests/MissionTrapPacketTests.cs
- AORebirth/Server/ZoneEngine_New.Tests/MissionTrapPersistenceTests.cs
- AORebirth/Server/ZoneEngine_New.Tests/MissionTrapResourceTests.cs
- AORebirth/Server/ZoneEngine_New.Tests/MissionTrapRuntimeTests.cs
- AORebirth/Server/ZoneEngine_New.Tests/ZoneEngine_New.Tests.csproj
- AORebirth/Server/ZoneEngine_New/Core/Entities/Chest.cs
- AORebirth/Server/ZoneEngine_New/Core/Entities/MissionTrap.cs
- AORebirth/Server/ZoneEngine_New/Core/MessageHandlers/GenericCmdMessageHandler.cs
- AORebirth/Server/ZoneEngine_New/Core/Nanos/NanoRuntime.cs
- AORebirth/Server/ZoneEngine_New/Core/Playfield/Locality/LocalityVisibility.cs
- AORebirth/Server/ZoneEngine_New/Core/Playfield/PlayfieldManager.cs
- AORebirth/Server/ZoneEngine_New/Core/Playfield/QuestDungeonPlayfield.cs
- AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/DungeonLayoutGenerator.cs
- AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/MissionTrapPolicy.cs
- AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/MissionTrapPopulation.cs
- AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/QuestDungeonParameters.cs
- AORebirth/Server/ZoneEngine_New/Core/Quests/Dungeons/QuestDungeonService.cs
- AORebirth/Server/ZoneEngine_New/Core/Quests/QuestService.Traps.cs
- SharedBuild/source-inventory/SmokeLounge.AOtomation.Messaging.CompileItems.props
- docs/ai/CURRENT_TASK.md
- docs/reports/MISSION_TRAPS_RECOVERY_AND_IMPLEMENTATION.md
- tools-temp/AOSharpCaptureProtocol/RawTrapFullUpdateDecoder.cs
- tools-temp/AOSharpMissionCaptureAnalyzer/AOSharpMissionCaptureAnalyzer.csproj
- tools-temp/AOSharpMissionCaptureAnalyzer/AcgLayoutExtractor.cs
- tools-temp/AOSharpMissionCaptureAnalyzer/Program.cs
- tools-temp/AOSharpMissionCaptureAnalyzer/TrapPacketReplay.cs

All 33 paths belong to this task. The index contains no private evidence, private
content profile, credentials, database export, captured packet bytes or Linux
infrastructure. The feature working tree is expected clean after the recorded
commit/push; final commit identity is supplied in the completion message.
