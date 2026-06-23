# Current Task

## Active Task

Owned private-city initialization parity against capture `20260623-021643`.

## Scope

- Compare AORebirth owned-city entry sequence against `docs/generated/private_city_owned_entry_capture_20260623_021643.md`.
- Implement only capture-backed owned-entry initialization plumbing.
- Do not implement `CityAdvantages`.
- Do not implement `OrgClient`.
- Do not implement ownership management or city purchase state.

## Capture-Proven Behavior

- Public/source playfield: `655`.
- Owned private city playfield: `1196034`.
- Organization stat: `Clan=1970177`.
- Organization rank/stat: `ClanLevel=1`.
- `OrgInfoPacket` was sent on owned private-city init.
- Owned private-city `PlayfieldAnarchyF` advertised:
  - `CityController:9C182E`
  - `Terminal:5751538B`
  - `Door:108D96ED`
- Private-city ready block included `PlayfieldAllTowers` and `PlayfieldAllCities`.

## Implementation

- Resolve Montroyal private-city entry destination from the character organization city id when available.
- Preserve captured fallback behavior for prior Montroyal private-city capture evidence.
- Add captured owned Montroyal `PlayfieldAnarchyF` generator payload identities.
- Send private-city organization info and `SocialStatus`, `Clan`, and `ClanLevel` stats before `FullCharacter`.
- Keep towers/cities empty for captured Montroyal private-city instances.

## Validation Plan

- `cmd /d /c tools\build_aorebirth_debug.cmd`
- `cmd /d /c restart-engines.cmd`
- `cmd /d /c git diff --check`
- Live smoke if available:
  - enter owned private city from Montroyal/ICC area
  - confirm private PF resolves to owned city
  - confirm guest-key terminal, CityController, and shuttle door identities are correct
  - confirm zone init does not stall
  - confirm no `CityAdvantages` or `OrgClient` implementation was added

## Validation Result

- `cmd /d /c stop-engines.cmd`: PASS.
- `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- `cmd /d /c restart-engines.cmd`: PASS.
- `cmd /d /c git diff --check`: PASS.
- Live smoke: Pending.
