# PF1931 Temple Nano Gameplay Completion - 2026-07-30

## Scope and repository boundary

- Requested baseline SHA: `c5fcf773bfe64ef8801f2b8db546281aae45bd23`.
- Actual synchronized starting SHA: `02d4a42cb80df5812cf2ca17234cfc82557df05f`.
- The intervening `02d4a42c` Arete loot import was preserved and was not
  modified by this pass.
- Existing combat, lifecycle, loot, Gulard `205584`, and Re-Animator `205604`
  ownership were not reworked.

## Shared nano-runtime result

The shared `NanoEventRuntimeService` now:

- requires an explicit caster and target for captured NPC nano-data execution;
- requires an explicit `Landed` or `Resisted` decision whenever nano data has
  a defend skill, and returns fail-closed for `Unresolved`;
- preflights the complete OnUse function list before applying any state;
- records every flat and percentage modifier contribution against the actual
  function target;
- refreshes by subtracting the prior recorded contribution before recording
  the replacement contribution;
- reverses only its own recorded deltas, so expiry never restores a stale
  snapshot over unrelated stat changes;
- removes modifier state on strain overwrite, expiry, recipient death,
  caster death, encounter reset, NPC despawn, and runtime disposal; and
- leaves periodic functions and unregistered gameplay functions fail-closed
  before partial application.

`CharacterActionMessageHandler.SetNanoDuration` now refreshes an existing
same-strain nano through `ActiveNanoRuntimeService`, resetting the expiry
timer instead of leaving the original timer active.

## Completed nano inventory

| Domain | Nano | Result |
|---|---:|---|
| Gartua the Doorkeeper | `205590` | Complete. Existing captured self target and schedule are retained. Finish is followed by captured refresh removal when applicable, `SetNanoDuration 3500cs`, data-defined `+35` health, flat modifiers `1 +350`, `360 +30`, `276 +80`, and `279/280/278/316/311/317/281/282 +30`. The heal emits the captured `HealthDamage` state packet. Refresh replaces rather than duplicates all modifiers; expiry/death/reset/disposal subtract the recorded deltas. |
| Reverend Gulard | `205584` | Preserved complete baseline: instant self nano-data heal. |
| The Re-Animator | `205604` | Preserved complete baseline: captured reanimated-add lifecycle. |

## Remaining named nano inventory

| Domain / nano | Final disposition and exact blocker |
|---|---|
| Defender `205389` | Fail-closed. Target, damage data, and both landed/resisted packet outcomes are known, but the repository and mapped client routines contain no authoritative server-side attack-skill versus Nano Resist resolution formula. |
| Defender `205561` | Fail-closed. The same resist blocker applies; the AreaCast child also lacks a categorical all-recipient capture contract. |
| Defender `209924` | Fail-closed and unscheduled. Capture `20260721-033006` shows an external actor casting it onto Defender, not a Defender-owned cast. Nano data has duration/strain but no executable OnUse payload; the external source owner and schedule are absent. |
| Yatila `205600` | Fail-closed. Fighting target, six-stat payload, duration, and refresh packet order are proven; authoritative attack-versus-resist resolution is not. |
| Yatila `205594` | Fail-closed. Fighting target, duration, two Energy hits, and modifier are proven; authoritative attack-versus-resist resolution is not. |
| Yatila / Re-Animator / Nematet `205592` | Fail-closed. Nano data proves damage plus RestrictAction 4, but no completed captured effect chain proves the action-restriction packet/behavior, and authoritative resist resolution is absent. Re-Animator also has no proven schedule for this nano. |
| Betany `205383` | Fail-closed. Fighting target and Fire damage packet are proven; authoritative resist resolution is absent. |
| Curator `205565` / child `205556` | Fail-closed. Captures prove child duration, Fire damage, expiry, and refresh removal for the observed player. They do not prove categorical AreaCast recipients or authoritative resist resolution; generic Stun action ownership is also absent. |
| Nematet `205395` / child `205378` | Fail-closed. Captures prove child CastNano, duration, Fire damage, and expiry for the observed player. Authoritative resist resolution and generic Stun action ownership are absent. |
| Nematet `205563` / child `205555` | Fail-closed. Captures prove direct Fire damage after the parent finish for the observed player. Categorical AreaCast recipients and authoritative resist resolution are absent. |
| Uklesh `204830` | Fail-closed and unscheduled. Both captures prove it is an on-hit proc immediately associated with ordinary attacks, not a timer cast. The shared weapon/on-hit trigger and authoritative attack-versus-resist result are absent; no actor-identity mapping or guessed proc cadence was added. |
| Murial `70294` | Fail-closed. Nano data proves the exact 16 modifiers, duration `1440000cs`, strain 2, and no defend check. Captures prove both ally and self targets from the same Murial generation, but do not prove the categorical missing-buff selector or safe cadence. Modifier reversal is no longer a blocker. |

## Cultist nano-family inventory

| Active domain | Final disposition and exact blocker |
|---|---|
| Cultist MD26074 | Disabled. No exact-name Cultist cast; the shared MonsterData appears under named actors with incompatible nano families. |
| Cultist MD26082 | Explicit no nano: no active-domain cast evidence. |
| Cultist MD26103 `49744/100198/157742` | Disabled. Exact-name casts and payloads exist, but generation/level selector, chain order, safe cadence, and categorical AreaCast recipients are not proven. |
| Cultist MD26135 `301424` | Disabled. One observation does not prove target, schedule, or refresh policy. |
| Cultist MD26137 `81829/82033` family | Disabled. Only Caska the Faithful, not an exact-name Cultist, owns the observed family. |
| Cultist MD26147 `205379/301406/301424` | Disabled. Multiple generation-local families exist without an authoritative generation selector; `205379` also requires the unresolved attack-versus-resist contract. |
| Cultist MD26149 `205580` | Disabled. The ally heal payload is exact, but ally selection and cadence are not proven. |
| Eternal Sentinel MD41690 | Explicit no nano: no active-domain nano chain. |
| Deathless Legionnaire MD42981 | Explicit no nano: no active-domain nano chain. |

No blocked row installs a nano timer, modifier, stun, periodic worker, or
active-nano entry.

## Shared hostile-mechanics re-audit

The 2026-07-31 backlog re-run established one additional generic wire contract:

- `FinishNanoCasting.Parameter1=1` means landed. In capture
  `20260721-052115`, Cultist nano `205379` from caster `7984B535` to target
  `7730012F` is followed immediately by the exact `HealthDamage` effect when
  the finish result is `1`.
- `FinishNanoCasting.Parameter1=3` means resisted/countered. The same caster,
  target, and nano produce no damage, duration, buff, or child-cast effect
  after result `3`. Defender nano `205389` independently has the same result-3
  no-effect shape in capture `20260721-033006`.
- `NanoEventRuntimeService.TryResolveFinishNanoCastingParameter` now owns this
  shared mapping: `NotRequired/Landed -> 1`, `Resisted -> 3`, and
  `Unresolved -> no packet result`. `ExecuteCapturedOnUseEvents` continues to
  apply no gameplay event on `Resisted` and refuses `Unresolved` defend-bearing
  nanos before partial application.

The following requested mechanics remain unimplemented because the available
authoritative sources do not define them:

- Attack skill versus Nano Resist: nano records define the weighted attack and
  defend inputs, but no mapped client routine or existing server service
  defines the server-owned random resolution formula. The legacy
  `PlayerController` contains only a TODO and always emits result `1`.
- Hostile `AreaCast`: nano records define child nano and radius, and captures
  prove the observed player effect. They do not prove categorical membership
  for players, pets, charmed actors, teamed actors, hostile NPCs, or gas/PvP
  filtering. The existing player/Mongo `AreaCastNano` path is not evidence for
  a hostile NPC Temple recipient contract.
- Stun/`RestrictAction`: nano records define the duration and action code, but
  mapped client routines and captures do not define the authoritative
  server-side action lock, interruption, expiry packet, or cleanup behavior.
- Uklesh on-hit proc: captures prove that `204830` occurs immediately before a
  landed ordinary attack effect, can occur from either weapon slot and on
  critical hits, and does not occur after misses. It is not present on every
  landed hit, and neither nano/item data nor captures provide the selection
  probability, so no rate is inferred.
- Shared scheduling and target selection: existing named schedules remain
  capture-owned. Murial `70294` proves both self and allied targets but not the
  categorical missing-buff selector or cadence. Cultist generations prove
  incompatible nano families without an authoritative level/generation
  selector, chain policy, or safe cadence.

Accordingly, the completed inventory remains `205584`, `205590`, and `205604`.
All other named and Cultist rows above remain independently fail-closed; the
new finish-result mapping does not promote any visual-only cast to gameplay.

## Validation

- `DungeonNamedEncounterCompletionTests`: PASS, `11/11`.
- `DungeonNamedLifecycleCompletionTests`: PASS, `20/20`.
- The lifecycle suite reconfirmed ordinary combat inventory `489/489` and
  named combat domains `19/19`.
- Debug build, full requested regression filters, restart, diff check, commit,
  push, and final synchronization are recorded in the final task response.
