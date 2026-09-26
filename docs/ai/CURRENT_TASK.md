# Current Task

Mail Terminal on `CursorNerko/Mail-sistem`.

Capture `Andromeda [PF 655] - 20260926-061753` fixed Return rules:

- FlagsBase **0x28** (open 0x29, TakeAll 0x2B)
- Return only COD **or** unique-already-owned; not already `Returned:`
- Success → `SendAccepted` EchoAction=7
- Retention 14d normal / 2d COD
- NoDrop + backpack/container still rejected on send

Next: restart ZoneEngine_New and live-test COD Return.

How-to docs only after Return works in-game.
