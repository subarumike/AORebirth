# Current Task

## Active

Set the Rubi-Ka new-character start location to the supplied Arete position:
PF 6553, X 3607.6, Y 52.4, Z 785.7.

## Scope

- Preserve the supplied fractional coordinates in the persisted new-character
  record instead of rounding them to integers.
- Do not change database schemas or existing characters.
- Build, validate, restart engines, commit, and push only this spawn correction.
- Ensure the approved Debug build actually rebuilds LoginEngine so the corrected
  creation path reaches the active server binary.

## Delivery acceptance

- Complete AOtomation suite: PASS (989/989).
- Debug build including LoginEngine: PASS.
- Engine restart and exact-port ownership: PASS (Chat 6996/7012, Login 7500, Zone 7501).
