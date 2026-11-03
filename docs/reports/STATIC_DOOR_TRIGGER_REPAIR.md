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

Development validation is in progress. Automated sessions and loopback TCP tests
are server evidence only. Actual AO client animation, Crypt traversal and live
multiplayer gameplay remain UNVERIFIED until Mike tests them.

The canonical item catalog initially failed current-master deserialization on
the historical enum name Unknown3. Current Operator.cs explicitly requires
re-extraction after its rename to OnFightingTarget. A fresh items-only export is
required; no runtime alias or test suppression is used.

Production has not changed. Linux release work remains gated by exact-input
Windows acceptance under AGENTS.md and BUILD_RELEASE_CONTRACT.md.
