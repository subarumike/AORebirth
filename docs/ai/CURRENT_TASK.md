# Current Task

## Active Task

Captured private-city compatibility layer for CityController, guest-key terminal, city advantages, and grid exit entry.

## Scope

- Use only AOSharp live capture evidence from `20260622-092054`, `20260622-092724`, `20260622-093102`, and `20260622-093540`.
- Send private-city ready packets on normal city zone-in, including captured `PlayfieldAnarchyF` generator payload and non-empty `PlayfieldAllCities` payload.
- Handle `GenericCmd Use` for `CityController` and the captured guest-key terminal without falling through to generic statel handling.
- Create the captured temporary City Access Card visual/update packets only; do not persist inventory, city bank, org storage, or money semantics.
- Route `OrgClient BankAdd = 19` safely and respond to captured `OrgClient CityAdvantages = 31`.
- Add the minimal `InstancedPlayerCity` function path needed for the captured grid exit destination, resolving an org `CityId` first and using the captured playfield only for captured org fallback.

## Evidence

- `20260622-092054`: private city zone-in, PF `1067112`, city dynels, `PlayfieldAnarchyF`, and non-empty `PlayfieldAllCities`.
- `20260622-092724`: guest-key terminal `Terminal:574DF8BB`, City Access Card template `280642`, overflow slot `111`, and success ack.
- `20260622-093102`: `CityController:9CA00B` use ack and `OrgClient` command `31` followed by four QL 300 city advantages.
- `20260622-093540`: grid city exit uses `GridDestinationSelect` / `GridSelected`, then `N3Teleport` to PF `1067112`, org `1370122`, building identity `C79E:177A`.

## Validation Plan

- `git diff --check`
- repo-approved build
- `cmd /d /c restart-engines.cmd`
- User gameplay testing required: private city zone-in, guest-key terminal, CityController use, city advantages request, and grid exit entry.

## Validation Result

- `git diff --check`: PASS.
- First `tools\build_aorebirth_debug.cmd`: compile passed; final copy blocked by running engine DLL locks.
- `stop-engines.cmd`: PASS, used only to clear build locks.
- Second `tools\build_aorebirth_debug.cmd`: PASS.
- `SmokeLounge.AOtomation.Messaging.Tests.csproj` build: PASS.
- `restart-engines.cmd`: PASS.
- User gameplay testing still required.
