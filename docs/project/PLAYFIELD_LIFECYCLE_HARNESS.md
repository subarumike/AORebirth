# Playfield Lifecycle Harness

This harness exists to guard packet-order-sensitive `Playfield` flows without changing runtime behavior.

`PlayfieldLifecycleTrace` is a no-op unless a test opens a `PlayfieldLifecycleCapture` scope. Production code records named lifecycle stages at existing packet send and scheduling boundaries; the default runtime path does not buffer, emit, or alter packets.

Current covered flows:

- Private city ready/init order around ready-block begin/end, org init, `FullCharacter`, `PlayfieldAllTowers`, `PlayfieldAllCities`, and towers/cities summary markers.
- Same-playfield visibility order around `CharInPlay`, joiner visibility broadcast, and existing-player snapshot.
- Cleaning robot death/corpse/despawn order around attacker stop fight, robot stop fight, death action `Parameter2=500`, corpse scheduling, despawn scheduling, and `CorpseFullUpdate`.

Use this harness before changing packet-order-sensitive `Playfield` behavior. Do not treat the trace labels as live protocol evidence; they are regression guards over current server behavior.
