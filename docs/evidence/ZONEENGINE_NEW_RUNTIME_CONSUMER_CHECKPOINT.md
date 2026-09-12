# ZoneEngine_New remaining-consumer checkpoint

Starting source: `44fa42fce316d98c1d5f6326d92caae909398289`.
Branch: `codex/zoneengine-final-runtime-consumers`.
Worktree: `C:\Users\Mike\Documents\AORebirth-zoneengine-final-delmus-reconciliation`.
Fetched origin/master and merge base: `6e90dda030774726aa2060acb9edb756ea1f635c`.
Starting origin/codex/zoneengine-final-delmus-reconciliation: the same 44fa42fc checkpoint.
The completed Delmus 77-file reconciliation was not reopened. No master switch or
production operation is authorized by this partial checkpoint's results.

## Binding reconstruction

`Tools\export_accepted_ordinary_bindings.cmd --write` evaluates the existing compiled
`OrdinaryEnemyCatalog`, its three source providers, each source's level-definition
resolver, and `OrdinaryEnemyRuntimeService.ResolveCombatContractForSpawn`.
It does not invoke the engine entry point, read an external database, enable debug
population, or fabricate bindings from raw placements, names or visual families.
`--check` repeats that evaluation and requires byte-identical output.

The result is exactly **489 bindings: 322 Subway and 167 Temple**, covering 26 and
10 respective profiles. Each placement has its own row, original source identity,
template/construction mode, accepted level definition and complete atomic variants,
visual/behavior/combat/loot/corpse mappings and provenance. Repeated definitions
are content-addressed inside the same JSON; references are not omitted evidence.
The ledger is `ZONEENGINE_NEW_ORDINARY_BINDING_CONSUMERS.json`.

Important boundary: the exported combat contracts are explicitly **before runtime
preparation**. Legacy `CapturedEnemyCombatRuntime.Prepare` may resolve a canonical
profile, equip an exact weapon, or retain a quarantined passive actor. Its boolean
return is not a final combat-ready assertion. A New adapter must preserve that
distinction; neither catalog acceptance nor an export makes an actor combat-ready.
This checkpoint does not add a blanket `GenericNpc` population consumer.

| Domain | Accepted | Connected before | Connected after | Partial | Unresolved consumers |
| --- | ---: | ---: | ---: | ---: | ---: |
| Subway | 322 | 0 | 0 | 0 | 322 |
| Temple | 167 | 0 | 0 | 0 | 167 |

These are counts within the exact 489-binding set, not total visible NPCs, the
separate 22 static social adapters, or the dynamic Buckethead factory. No accepted
binding has been relabelled missing data merely because its consumer is absent.

## N05 Mongo implementation

```text
AREA_REOPENED=Nano specialization cast planning and explicit NPC combat capability
BLOCKER_REQUIRING_CHANGE=N05 parent heal and 100194 area taunt had no New consumer
WHY_EXISTING_IMPLEMENTATION_WAS_INSUFFICIENT=The generic planner rejects the area graph; specialization plans previously had no atomic initial resource hit
```

`MongoNanoSpecialization` accepts only the actual 100198 -> 100194 graph. Parent
cost, action requirements, cast/recharge timing, 20-second duration and zero NCU
come from the catalog. Initial +12 healing and mana cost commit together through
the existing nano repository. Restore and expiry never replay the heal.

`Events.Perform` executes that OnUse function once despite the catalog's tick
metadata. No periodic heal, invented 287046 effect, or generic NPC nano AI is added.
The unregistered Legacy HeadText function does not justify inventing another packet.
The exact child's three skill branches all execute a **one-point engage hit** and
forced taunt target; their 2000/3000/4000 arguments are not HP damage.

Recipient selection uses the accepted 20-meter **2D** boundary. Only an exact,
current, living, non-vendor `NpcCharacter` whose runtime adapter explicitly grants
`AcceptsPlayerCombatNanos` is eligible. Names, MonsterData and mob templates do not
grant the capability. The existing generated-mission combat owner grants it;
find-person NPCs and ordinary social/vendor actors do not. No PvP behavior is added.
Future accepted ordinary consumers must explicitly supply this same capability.

The postcommit action uses existing `ApplyDamage`/`OnDeath` and `StartFighting`
authorities, not a new death/reward/mission persistence path. Cast completion is
one-shot, rechecks exact recipient registration and caster ownership, and cannot
taunt a replacement actor. Actual mission NPC damage retains its existing durable
mission transaction; the parent heal/cost does not depend on an area target existing.

Source authorities inspected: `PlayerController.CastNano`, `Events.Perform`,
`areacastnano`, `tauntnpc`, `castnano.ApplyInstantNano`, `hit`,
`PlayfieldDynelRegistry.FindCharactersInRange`, the actual packaged 100198/100194
catalog audit, New `NanoService`, `NpcCharacter`, `GeneratedMissionNpcCharacter`,
`MissionNpcCombatRuntime` and `Character.ApplyDamage`/`OnDeath`.

## Persisted morph decision

```text
MORPH_CURRENT_PERSISTENCE_MODEL=Character snapshots store base stats; charactersactivenanos stores nano ID/strain/instance/duration/UTC expiry; visual overlays are transient Bonus contributions
MORPH_EXPECTED_SUPPORTED_MODEL=Restore accepted active effects after base/inventory/equipment load, without overwriting an unknown original appearance or double-applying modifiers
STARTING_FAILURE=An active recognized morph with nonzero saved MonsterData was rejected by MorphNanoSpecialization.TryPrepare
AFFECTED_STORAGE=Existing character stats and charactersactivenanos; no historical original-appearance field exists in the inspected model
AFFECTED_PACKETS=Initial character publication, Appearance, morph SpellList and removal stat publication
AFFECTED_RECONNECT_PATH=PlayerHydrator.Apply -> Player.Rebase -> NanoService.AttachPlayer -> initial publication
```

Mike explicitly approved: **Preserve saved appearance and allow login**. The
compatibility policy treats saved appearance as an opaque baseline, not as proof
of the lost pre-morph form. A supported active effect overlays that baseline;
cancel, expiry and detach remove only its own contributions. New casts may also
overlay that preserved baseline when their catalog requirements allow it. The
270542/288546/281569 ToUse requirement for MonsterData zero is unchanged: login
compatibility is not permission to bypass casting requirements. Sparrow 82835
allows a new overlay on the preserved nonzero baseline. No normalization to zero, template inference,
schema extension, data migration or production row census is authorized or needed.

The existing unversioned scalar/active-nano format remains unchanged. Existing
duration-only/zero-instance compatibility stays in `NanoService.AttachPlayer`;
invalid, duplicate, unknown and overflowing rows still fail closed. No new format
version is claimed. Base stats, inventory/equipment and derived state load before
active-nano restoration; morph restoration sends no premature cast/visual packet.
The initial character publication and ordinary reconnect refresh remain the owners
of client presentation. Base-only snapshots exclude the transient overlay.

Policy: accepted current active rows persist logout/reconnect and same-actor zone
transfer; cancellation/expiry clear only the owned effect; logout detaches the
process-local overlay without deleting its durable row. Death continues to cancel
pending casting through the existing interrupt authority; this compatibility change
does not invent a new global death-removal rule. Unknown original appearance remains
unproven and preserved, not silently reconstructed. Wider unsupported graphs and
equipment ownership remain N02, not falsely closed by this compatibility choice.

Inspected authorities: `MorphNanoSpecialization`, `MorphNanoTests`,
`CharacterMotor.HasFlightAuthority`, `PlayerHydrator`, `CharacterSnapshotService`,
`SpawnService`, `MySqlActiveNanoRepository`, and Legacy morph removal call sites.

Fifteen additional compatibility test cases cover all four recognized morphs,
fresh actor/session/service restoration without process-local history, base-only
snapshot reconstruction, cancellation and nonzero removal publication, expiry,
duration-only record normalization, no expired-effect resurrection, rebase,
current death policy and unchanged cast requirements. The existing Sparrow zone
refresh case now also verifies the opaque baseline survives transfer and removal.

## Exact remaining blocker matrix

| Class | Starting | Implemented/resolved here | Nonblocking unproven excluded | Remaining supported | Master blocker |
| --- | ---: | ---: | ---: | ---: | --- |
| Subway bindings | 322 | 0 | 0 | 322 | Yes |
| Temple bindings | 167 | 0 | 0 | 167 | Yes |
| Dialogue routes | 27 | 0 | 0 | 27 | Yes |
| Nano review units | 5 | 1 (N05 implementation) | 0 | 4 (N02/N07/N08/N10) | Yes |
| Persisted morph | 1 conditional class | 1 approved preserved-baseline implementation | Not reclassified | 0 in the four recognized morphs | No, subject to candidate acceptance |

The authoritative 27-route inventory remains
`ZONEENGINE_NEW_DIALOGUE_GAP_CLOSURE.md`; no route is reported connected, obsolete
or unproven by this checkpoint. N05's NPC capability integration does not close
the separately missing Subway/Temple world consumers.

### Concrete outstanding runtime dependencies

| Blocker | Playfield/domain | Accepted contract | Missing runtime capability and why not closed by this checkpoint |
| --- | --- | --- | --- |
| 489 ordinary bindings | PF127/PF1931 | Per-binding level, appearance, combat, loot, corpse, movement and respawn contracts in the generated ledger | New lacks an ordinary actor factory and capability-specific combat/movement/corpse owner. Legacy `Prepare` can accept a quarantined actor; exporting it or reusing the narrower generated-mission combat policy cannot certify these contracts. |
| 27 dialogue routes | Exact routes in `ZONEENGINE_NEW_DIALOGUE_GAP_CLOSURE.md` | Existing Legacy trigger, eligibility, action and actor mappings | No corresponding complete New actor/action-owner wiring was added here. Registering dialogue text alone would not connect those routes. |
| N02 | Morph/equipment | Legacy recognized MonsterShape/CanFly graphs and owned equipment removal | Four-nano compatibility does not supply wider graph planning or an equipment-owned overlay lifecycle. |
| N07 | Pets/shells | Existing shell grant/use and process-local pet summon, command and zone restore | New still lacks the pet world/command owner and a shell-item plus mana plan in the existing transaction. Durable restart pets are not required or inferred. |
| N08 | Executable modifiers/flags | Existing PercentageModifier contribution and bounded SetFlag behavior | Generic planning still lacks corresponding contribution ownership/reversal for supported graphs. No arbitrary operator or default has been substituted. |
| N10 | Player Hit against accepted combat NPC | Source-backed signed resource changes and existing death/corpse authority | New generic effects still target durable players. The explicit N05 target capability alone does not implement signed Hit planning or bridge NPC resource/death ownership. |

```text
FORENSIC_NPC_CENSUS_COMPLETE=NO
ALL_CURRENTLY_ACCEPTED_REQUIRED_BINDINGS_CONNECTED=NO
UNPROVEN_NPC_AI_ENABLED=NO
UNPROVEN_NPC_NANO_AI_ENABLED=NO
UNPROVEN_RUNTIME_FALLBACKS_ADDED=NO
ZONEENGINE_NEW_MASTER_READY=NO
MASTER_SWITCH_PERFORMED=NO
DEFAULT_ZONEENGINE_ON_CANDIDATE=ZoneEngine_New
LEGACY_ROLLBACK_AVAILABLE=YES
PRODUCTION_DATABASE_MODIFIED=NO
PRODUCTION_MIGRATIONS_APPLIED=NO
PRODUCTION_SERVICES_CHANGED=NO
PRODUCTION_DEPLOYMENT_PERFORMED=NO
LIVE_CLIENT_TEST_PERFORMED=NO
PUBLIC_NETWORK_EXPOSURE_CHANGED=NO
```

## Validation

Current NewEngine suite: PASS, **479 tests**, zero failures/skips, including 12
Mongo cases, one mission-capability regression and 15 additional N03 cases.
The first N03 run exposed four test setup errors: fresh casts of unmorphed-only
nanos were incorrectly expected to accept nonzero MonsterData. Tests now separately
exercise saved-row restoration, actual allowed Sparrow casting and unchanged
catalog refusal. No runtime requirement was weakened to make a test pass.
The exporter reconstructed 489 and its final `--check` passes. Precommit Windows
acceptance passes: all 12 mandatory stages, Legacy and NewEngine builds, 479/479
NewEngine tests, 1129/1129 AOtomation tests, packet coverage, DAO architecture,
generated combat, Subway/Temple, mission reproducibility, LFS, offline WebCore,
source inventory and playfield-package validation. The Release NewEngine build
and real disposable database lifecycle also pass, including explicit migration,
negative/current schema checks, rollback, atomic inventory/nano/mission writes,
runtime start/stop/restart and cleanup with no container/network residue.
`git diff --check` passes. Logs are under `build-verify/runtime-consumers-*`.

These are precommit results. Exact committed Windows acceptance and identical-SHA
Linux acceptance must be reported separately after commit; no historical
44fa42fc receipt approves this changed candidate. Master readiness remains NO
regardless of acceptance success while the concrete consumer blockers above exist.

Execution corrections: one regex search used an unescaped opening parenthesis;
it was corrected with literal `rg -n -F -e` patterns. Several targeted reads used
incorrect paths; actual paths were then resolved through `rg --files` or a literal
class-name lookup. These failed reads/searches changed no repository state. A
large generated combat JSON was inadvertently read by line; subsequent JSON
inspection uses selected parsed fields. None of these errors is a project blocker.
