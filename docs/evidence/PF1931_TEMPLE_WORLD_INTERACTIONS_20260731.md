# PF1931 Temple world interactions (2026-07-31)

## Scope and corpus

This pass started from `a83d689a6172b8a232ffeb29bf98002e3dde4dc2` (the
requested `d1b1c43827c64c682ec476ae082f2b07453cea05` baseline is an ancestor).
It is limited to the Temple of Three Winds and its PF647 entrance boundary.
The established 43-door runtime, combat, lifecycle, collision, navigation, and
unrelated content were not redesigned.

The audit searched all 36 existing sessions associated with PF1931:

- 32 current raw sessions whose capture metadata identifies PF1931;
- the current boundary session `20260722-041602`, which starts in PF647 and
  records both the PF647-to-PF1931 entry and PF1931-to-PF647 exit;
- three legacy raw-hex sessions: `20260528-190456`, `20260528-191120`, and
  `20260528-192819`.

The boundary session was omitted by the old PF1931-only session projection
because its initial playfield is PF647. The raw corpus, not that projection, is
therefore the authoritative enumeration for boundary interactions.

## Authoritative resources

- client `18.8.62_EP1` `playfields.dat` statels, destination segment, and
  FunctionType `53082` (`TeleportProxy`) event arguments;
- official PF1931 dungeon geometry: 30 rooms and the EntryHall exterior edge
  `roomIndex=-1`, `doorIndex=4468`;
- mapped client statel/event and teleport packet layouts;
- raw `N3Teleport`, `PlayfieldAnarchyF`, `GenericCmd`, `DoorStatusUpdate`, and
  `ChestFullUpdate` rows from the sessions above;
- the existing exact-byte packet fixtures and generated PF1931 door evidence.

## Complete official interaction inventory

| Owner | Official object | Count | Contract |
|---|---|---:|---|
| PF1931 | Door statels | 44 | 43 internal doors use the completed shared dynamic-door runtime; `C024078B` is the one EntryHall exterior link. |
| PF1931 | Exterior geometry links | 1 | EntryHall `doorIndex=4468`, paired with statel `C024078B`. |
| PF1931 | Destination segments | 1 | Static authored arrival segment; it is not a separate Use target. The captured arrival point is selected by the PF647 proxy. |
| PF647 boundary | Door statel `C0080287` | 1 | Official OnEnter `TeleportProxy(51102,1931,0,C0080287)` for characters below level 61. |
| PF1931 | Portal/terminal/container/chest/room-trigger statels | 0 | None exist in the official statel inventory. |

All official PF1931 statels are Door identities with template `28485`; none of
the PF1931 statels owns a GenericCmd/Use, chest, terminal, portal, or additional
room-trigger event. The single PF647 source door uses template `41565`.

The raw PF1931 GenericCmd/Use traffic resolves to player-owned corpses,
inventory objects, characters, and containers. The inbound door Use row is for
an already-completed internal door. All 127-byte ChestFullUpdate rows are
inventory-container state, not 155-byte world-chest state. Consequently there
is no evidence or official identity for an additional Temple world container.
Standard GameTime, playfield city/tower, visibility, and character-entry packet
families are environment initialization, not interactable objects.

## Recovered zoning contract

### Entry

PF647 door `C0080287` fires its official OnEnter TeleportProxy through the
shared 0.5m statel contact cell: the last pre-contact movement sample is 0.638m
from the official statel and the triggering sample is 0.461m away. The transfer
uses the source character's live position and heading in the N3Teleport
envelope, categorical target `(51102,1931)`, the official source-door identity,
the server-owned runtime playfield identity, and the exact captured PF1931
landing `(172.989990234375, 24.011247634887695, 7.81494140625)`.
The PF1931 PlayfieldAnarchyF preserves categorical resource identity separately
from mutable server ownership. No captured runtime identity map is stored.

### Exit

The exact exit is present in boundary session `20260722-041602`. The character
lands 1.800m from `C024078B`, then crosses into the shared 0.5m statel contact
cell; the last two samples are 0.179m and 0.175m from the official statel before
the transfer. Entry contact is edge-triggered per character, so repeated
movement cannot enqueue duplicate transitions.

The exit N3Teleport preserves the character's live PF1931 position and heading,
uses categorical target `(51100,647)`, runtime target `(40016,647)`, official
portal `(100003,C0080287)`, and payload `00000001`. The exact landing is
`(1813.9990234375, 26.806131362915039, 2715.84521484375)`. The shared playfield
transfer owner clears statel contact state before removal and re-primes it on
arrival/re-entry; playfield disposal owns no independent worker or timer.

## Decoder correction

`tools-temp/AOSharpLiveCapture/decode_world_interactions.py` decodes the five
interaction-relevant packet families directly from raw hex and canonicalizes
the consecutive duplicate rows produced by the legacy dual logger. It recovers
the entry/exit packet fields from both current and legacy sessions with zero
decode errors. This prevents initial-playfield projections and duplicate legacy
rows from hiding otherwise usable interaction evidence.

## Remaining blockers

None for official PF1931 world interactables. The corpus contains no official
or captured Temple world chest, terminal, GenericCmd target, extra portal, or
room-trigger identity to implement. Those categories remain absent rather than
being synthesized from player inventory/corpse traffic.
