# Current Task

## Active

Offer each newly created Rubi-Ka character a durable, one-time starting-area
choice between Arete and ICC Shuttleport after first login.

## Scope

- Keep the official character-creation `Shadowlands` selector unchanged for
  future Shadowlands work.
- Seed the choice only for new Rubi-Ka characters; existing and Shadowlands
  characters remain unaffected.
- Present the choice through the standard AO KnuBot dialogue window using the
  dedicated ICC Shuttleport Commander speaker instead of Marcus Stone.
- Persist the selection in the existing `missionflags` table before applying it.
- Arete keeps the character in PF 6553. ICC Shuttleport transfers to PF 4582 at
  X 939.0, Y 20.3, Z 732.0.
- Closing the window without choosing leaves the selection pending and offers it
  again on the next login.
- Do not change database schemas.

## Delivery acceptance

- Complete AOtomation suite: PASS (993/993).
- Debug build including LoginEngine: PASS.
- Engine restart and exact-port ownership: PASS (Chat 6996/7012, Login 7500, Zone 7501).
