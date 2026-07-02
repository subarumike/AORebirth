# PacketSequencingCoordinator Checkpoint

Date: 2026-07-02

## Scope

This checkpoint records the first packet sequencing ownership boundary. It documents architecture progress only. It does not document gameplay behavior changes.

## Sequencing Ownership Moved

- `PacketSequencingCoordinator` is owned by `ZoneClient` for session-local packet sequencing.
- `PacketSequencingCoordinator` is exposed through `PlayfieldRuntimeSystems` for playfield-local packet pair sequencing.
- Session ready/full-character initialization now routes through `PacketSequencingCoordinator`:
  - ready-block phase entry
  - private-city ready block begin trace
  - login SCFU broadcast trace/send
  - inventory/weapon preparation point
  - private-city pre-FullCharacter ready block
  - FullCharacter boundary trace/phase/send
  - private-city post-FullCharacter ready block
  - ready block end trace
- Same-playfield visibility initialization now routes through `PacketSequencingCoordinator`:
  - joiner-ready trace
  - CharInPlay phase entry
  - joining-player visibility broadcast
  - existing-player snapshot send
- SCFU -> CharInPlay pair ordering now routes through `PacketSequencingCoordinator` for:
  - existing-player snapshot packets sent to the joining client
  - joining-player broadcast packets sent to the playfield
- Private-city ready/init sequencing now routes through `PacketSequencingCoordinator`:
  - OrgInfoPacket before org stat initialization when org info exists
  - SocialStatus, org id, org rank, and repeated SocialStatus order before FullCharacter
  - PlayfieldAllTowers before PlayfieldAllCities in the ready block
- Cross-playfield zoning start now routes through `PacketSequencingCoordinator` for:
  - zoning phase entry before the teleport packet send

## Intentionally Excluded Runtime Mechanics

`PacketSequencingCoordinator` does not own packet construction, packet serialization, transport, socket code, gameplay systems, or runtime handoff mechanics.

The following remain intentionally outside the coordinator:

- message object construction
- `SendCompressed`
- packet serializers
- network streams
- destination playfield lookup/creation
- current playfield despawn broadcast
- coordinate and heading mutation
- client detach and dynel disposal
- `ZoneRedirectionMessage` construction/send
- same-playfield local teleport path
- private-city packet payload construction
- SCFU/CharInPlay packet construction
- FullCharacter packet construction/send

## Guardrails

`PlayfieldLifecycleTraceTests` now assert:

- session ready/full-character/visibility sequencing is routed through `PacketSequencingCoordinator`
- SCFU -> CharInPlay visibility pair sequencing is routed through `PacketSequencingCoordinator`
- private-city org/stat sequencing is routed through `PacketSequencingCoordinator`
- private-city towers -> cities ready-block sequencing is routed through `PacketSequencingCoordinator`
- zoning phase entry happens before the teleport packet send through `PacketSequencingCoordinator`
- teleport destination lookup, playfield creation, despawn broadcast, coordinate mutation, client detach/dispose, and redirect mechanics stay in `Playfield`
- same-playfield local teleport stays outside `PacketSequencingCoordinator`
- packet construction, serialization, transport/socket code, gameplay systems, database import, capture tooling, inventory, org commands, and GenericCmd routing stay outside `PacketSequencingCoordinator`

## Validation Status

Latest validation for this checkpoint:

- `cmd /d /c git diff --check`
- focused `PlayfieldLifecycleTraceTests`

No production or project files were changed by this final checkpoint, so the documented build/restart workflow was not required for the checkpoint commit.

## Remaining Packet Sequencing Work

The next packet sequencing work should only proceed where packet order can be moved without moving payload construction or runtime ownership.

Candidate future slices:

- death/respawn packet ordering guardrail before any extraction
- login ready-block sub-sequence guardrails around non-private-city static dynel/vendor emission
- redirect/zoning completion sequencing only after a stronger handoff model exists

## Recommended Next Major System

Recommended next major system after this epic: a packet construction/payload boundary, not another runtime ownership extraction.

Reason:

- `PacketSequencingCoordinator` now owns several high-risk ordering decisions.
- Packet construction still lives in runtime handlers and coordinators.
- The next risk is payload drift mixed into runtime classes, especially FullCharacter, visibility packets, private-city packets, and teleport/redirection packets.

The next system should keep sequencing separate from construction and avoid moving transport/socket ownership.
