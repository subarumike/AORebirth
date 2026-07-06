# Playfield Statel Transition Runtime Boundary

## Scope

This checkpoint introduces `PlayfieldStatelTransitionRuntimeService` as the named runtime owner for statel collision/contact and captured Montroyal private-city transition orchestration.

The service owns:

- statel contact tracking for `OnEnter` suppression
- post-zone collision grace state and expiration checks
- statel collision loop sequencing and event firing
- captured Montroyal private-city entry route checks
- captured Montroyal private-city exit route checks
- captured entry/exit location constants and transition logging

## Preserved Playfield Ownership

`Playfield` intentionally still owns behavior that must remain outside the transition service:

- teleport packet construction and emission
- playfield lookup and handoff mechanics
- destination identity construction for teleport handoff
- character position mutation performed by teleport handling
- private-city entry social-status packet construction and emission
- organization DB lookup for city-id resolution
- static dynel/vendor object construction

## Behavior

No gameplay behavior, packet payloads, packet ordering, collision timing, contact cleanup, transition routing, or private-city entry/exit behavior changed.

The service receives callbacks for teleport, movement stop, social-status packet emission, and organization/city-id resolution so the moved boundary remains orchestration-only.
