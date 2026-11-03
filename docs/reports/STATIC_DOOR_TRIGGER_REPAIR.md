# Static door and switch repair

## Scope and evidence

The repair implements AreaTrigger (53149), RemoteAreaTrigger (53159), static
object action context and authored object scheduling in ZoneEngine_New. It uses
the completed native AreaTrigger/RemoteAreaTrigger investigation and the finalized
PF665 capture. PF665 establishes door packet serialization and replication;
it does not establish Crypt of Home switch semantics.

PROVEN native behavior: event/radius and XYZ/event/radius arguments, signed
coordinates, radius <= 0 becoming 1, strict three-dimensional float distance,
same-playfield SimpleItem candidates, ordered signal handling and consumable
conditions. The owner, invoking session, source, supplied target and selected
execution target remain distinct. State transitions and state-entry actions run
on separate object runs. Retained effects use authored centisecond intervals;
54001 timers count odd pulses independently. Repeated activations retain separate
action instances. Unload disposes all pending work. No SQL is added to either
movement or simulation.

Trigger-bearing OnUse, OnTargetInVicinity and trigger-only OnEnter use the same
executor. Existing crossing detection retains full object identity and event
type; objects of different types sharing an instance cannot capture each other's
events. Existing recognized teleport portals keep their existing transport path.

## Compatibility assumptions

INFERRED server compatibility, explicitly recorded in editable private
ObjectStateMachines.json:

- Signal 128 unlocks; 64 locks; 136 unlocks and opens; 80 closes and locks.
- Signal 256 disables controls; 4 enables them. Signals are not Flags values.
- Reviewed state-machine bindings start in state 1, after the state-0 initializer.
- The paired entrance machine opts into consuming at most one queued signal per
  object run. Native coalescing remains the default. No distinct-player rule is
  imposed.
- An admitted static-owned RemoteAreaTrigger world action may finish on its
  original playfield after its invoking player leaves. Player effects and
  feedback remain fenced to the original session.

These policies are the smallest functioning server compatibility implementation,
not recovered original server mappings. Runtime C# contains no Crypt coordinates
or door/control identity table. Original placement blobs supply the skeleton's
state-machine binding; a shared item template does not globally acquire it.

Unsupported remote actor-chain selectors 14/23, character-gated condition
operators 1/2 and sleeping/animation Flag3C graphs fail closed. The reviewed Crypt
program does not require these branches. Future resource imports must establish
their behavior before enabling them.

## Door authority and publication

Placed doors share their effective Flags with existing door collision/occlusion
ownership. Trigger policy publishes one final DoorStatusUpdate (0x4C7D403B) per
state change, preserving unrelated flags. Existing ordinary use, automatic
opening and lockpicking remain on their current infrastructure. Initial entry
and reconnect replay current static door state; dynamically spawned doors use
their existing spawn companion mechanism for the hold-open latch.

PROVEN native/capture distinction: the status close-inhibit byte sets a latch;
a false byte does not clear it. An explicit locked/closed update overrides that
latch. The repair retains it through relocking rather than inventing a reset
packet. Physical collision geometry is not replaced.

## Validation and release state

PROVEN development validation: ZoneEngine_New and the focused test project compile;
60 focused cases pass, zero fail or skip. They cover the actual five Crypt door
chains, paired/simultaneous activations, expiry/reuse, control resets, authored
timers, criteria and selector semantics, ordinary doors, lockpicking, unload,
session fencing, state replay and two real loopback TCP connections. Actual
PF1404, PF4604 and PF4873 data additionally cover delayed use, vicinity/entry
dispatch and reset behavior. Generic crossing tests exercise the existing
movement-trigger dispatcher with an identity-type decoy. The full reconnect
publication call site and mixed-portal exit predicate were source-reviewed;
their complete authenticated/geometry paths were not newly exercised.

Automated sessions and loopback TCP tests are server evidence only. Actual AO
client animation, Crypt traversal and live multiplayer gameplay remain
UNVERIFIED until Mike tests them. The finalized PF665 capture supplied four exact
door status bodies used by serializer regressions; it does not verify Crypt.

The canonical item catalog initially failed current-master deserialization on
the historical enum name Unknown3. Current Operator.cs explicitly requires
re-extraction after its rename to OnFightingTarget. A fresh items-only export and
current reader validation succeeded with 131,807 records. Its complete measured
provenance remains in ignored private release evidence. No runtime alias or test
suppression is used. Tests selected an ignored staged
view; canonical item adoption remains governed separately.

The generated private ObjectStateMachines.json has one machine, four states and
six compatibility policies. Its exact staged identity remains private.
The existing private catalog adoption mechanism is extended for its schema,
preserving staged-byte hashes and actual filename-specific user approval.
Implementation data and native snapshots remain private.
Three focused content-adoption tests pass, including malformed/unclassified
content rejection and preservation of existing catalog grouping rules. The first
new fixture used an invalid empty baseline; seeding a complete synthetic baseline
corrected the fixture without changing production validation.

Tool-only exporter/reader child commands succeeded, but the feature helper could
not attest them because they emit no product compiler-hook observations. Those
helper receipts remain FAIL/nondeployable; native success is recorded separately.
The focused Zone test helper completed successfully. An absolute-project reader
invocation was rejected before execution; the supported correction used the
owning checkout and an explicit relative Tools project through the same helper.
An initial Python module-style test invocation could not resolve the test module;
the corrected form executes the tracked test script directly. Neither rejected
invocation changed product source or canonical content.

## Changed files

- Runtime: AreaTriggerContext.cs, AreaTriggerService.cs,
  StaticObjectStateMachineCatalog.cs, DynelEventSpells.cs, TriggerVolumeCatalog.cs,
  PlayfieldWorldSimulation.cs, DoorOcclusion.cs, Door.cs, StaticDynel.cs,
  Playfield.cs, SpawnService.cs, ItemSpells.cs, ItemTemplate.cs, ItemUseFunctions.cs.
- Exporter: StateMachineExporter.cs, ExtractionOptions.cs, Program.cs.
- Validation: TriggerTestWorld.cs, AreaTriggerTests.cs,
  AreaTriggerSchedulerTests.cs, CryptDoorIntegrationTests.cs, DoorStateTests.cs,
  DoorNetworkIntegrationTests.cs, ZoneEngine_New.Tests.csproj and the existing
  test_windows_release_contract.py.
- Governed catalog admission: windows_release_contract.py. Task/status notes and
  this report record scope, results and limitations.

Production has not changed. Linux release work remains gated by exact-input
Windows acceptance under AGENTS.md and BUILD_RELEASE_CONTRACT.md.
