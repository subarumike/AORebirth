# Subway capture follow-ups: 2026-07-20

## Rest-of-subway capture

Capture `20260720-051714` is a whole-subway population/combat survey. Its
decoder status is incomplete, but the raw packet sink is preserved and the
decoded dossier contains the ordinary hostile names already represented by the
Subway catalog: Architect Striker, Bloodcreeper, Empty Shell, Filth Flea,
Fragmented Soul, Incomplete Rebuild, Infected Attendant, Infector, Lost
Thought, Melded Patterns, Molested Molecules, Neural Burnout, Premature
Pattern, Redundant Scan, Shadow, Slum Runner, Stim Fiend, Uncontrollable
Anger, and Workman Striker. It does not prove a new ordinary hostile
archetype that can be safely promoted.

The two additional names are not missing hostile profiles. The lifecycle rows
classify Bureaucrat Worker (`96056`) and Wrath Incarnation (`96195`) as
player-owned pets (`pet=True`), so they remain excluded from ordinary Subway
population generation.

## Vergil follow-up

Capture `20260720-053542` adds eight normal local-player hits for Vergil's
weapon roll (`22..25`) and one captured critical hit for `54`. The runtime
continues to use the captured weapon-owned damage/cadence rather than inventing
a separate critical formula.

## Abmouth pull/warp

Capture `20260720-053802` records Abmouth casting nano `286237` on the local
player at `10:38:39.092Z`. At `10:38:39.244Z` the client receives an
`N3Teleport` for the player and `SetPos` updates for both owned pets to
`(325.01, 73.61795, 101.01)`, the Abmouth engagement position. The runtime now
replays this once per Abmouth fight at the captured approximately `21.8-second`
combat point, teleporting the engaged player and repositioning that player's
living pets together.
