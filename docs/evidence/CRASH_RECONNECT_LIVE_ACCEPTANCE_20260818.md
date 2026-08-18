# Crash reconnect live acceptance

Date: 2026-08-18

Repository: `C:\Users\Mike\Documents\AORebirth`

Accepted source SHA:

```text
fe6617b3bcd1d3806eddd4dbbb91e9c6680ef499
```

Windows authoritative head after the later client-patch sync:

```text
5d0a84960df961e504f8761da46521d9968b8cd8
```

The later commits after `fe6617b3` touched only the AORebirth client patch:

```text
e6500df4 Add bounded DailyLogin client routing
5d0a8496 Embed manifest in client patch self-test
```

They did not touch ZoneEngine, AORebirth.Core runtime, or Linux ZoneEngine
packaging, so the accepted Linux ZoneEngine deployment remains reconciled with
the Windows-authoritative server source.

## Root cause

Crash reconnect could previously reuse a pooled character while an old pending
logout timer still had authority over the same character. That allowed a zombie
session where inventory/equipment/action state could be missing until a later
zone/death/relogin path rebuilt state.

## Source fix

The accepted source fix:

- claims reconnect/logout-timer ownership before pooled-character reuse;
- prevents an old pending logout timer from retaining normal authority over a
  successfully reclaimed session;
- reloads inventory from the database on true reconnect;
- rejects incomplete/untrusted inventory hydration before `CharInPlay`;
- falls back/fails closed rather than accepting unsafe pooled state.

Source contract:

```text
UNTRUSTED_INVENTORY_CAN_REACH_CHARINPLAY=NO
```

## Windows validation before deployment

```text
tools\run_aotomation_messaging_tests.cmd: PASS
focused reconnect contract: PASS
tools\build_aorebirth_debug.cmd: PASS
tools\run_mandatory_integration_gate.cmd: PASS
Tools\scan_secrets.cmd: PASS
git diff --check: PASS
```

## Linux candidate

```text
LinuxBuild\publish-zoneengine.cmd linux-x64 true: PASS
Stage 8 offline ZoneEngine smoke: PASS
```

## Production target

```text
host: mail
ssh target: root@2.24.96.30
ZoneEngine service: ao-rebirth-zoneengine.service
LoginEngine service: ao-rebirth-loginengine.service
ChatEngine service: ao-rebirth-chatengine.service
AccountBroker service: ao-rebirth-accountbroker.service
```

## Deployment scope

ZoneEngine only.

The live defect was in ZoneEngine session/inventory rehydration. LoginEngine and
ChatEngine remained active and were not redeployed.

## Previous release and rollback

Previous ZoneEngine release:

```text
/opt/ao-rebirth/zoneengine/releases/deploy-perms-245da9aa
```

Rollback status:

```text
ROLLBACK_READY=YES
```

## Candidate release

New immutable release:

```text
/opt/ao-rebirth/zoneengine/releases/reconnect-fe6617b3
```

Candidate preflight:

```text
ZONEENGINE_VALIDATION_OK mode=startup provider=MySql bindPolicy=Public address=0.0.0.0 listeners=0 assets=ok
ZONEENGINE_DATABASE_OK provider=MySql database=aorebirth_chatengine_stage6 requiredTables=34 visibleTables=39 onlineCharacters=0 listeners=0
```

Local and remote candidate hashes matched for:

- `ZoneEngine`
- `ZoneEngine.dll`
- `AORebirth.Core.dll`
- `SmokeLounge.AOtomation.Messaging.dll`

## Production promotion

Promoted active ZoneEngine symlink:

```text
/opt/ao-rebirth/zoneengine/current -> /opt/ao-rebirth/zoneengine/releases/reconnect-fe6617b3
```

Post-promotion service state:

```text
ZoneEngine: active
LoginEngine: active
ChatEngine: active
AccountBroker: active
ZoneEngine PID: 3255281
ZoneEngine restart count: 0
TCP 7501: 0.0.0.0:7501
TCP 7500: 0.0.0.0:7500
```

External connectivity:

```text
TCP 7500: OPEN
TCP 7501: OPEN
TCP 33067: CLOSED externally
TCP 6996: CLOSED externally
```

## Live official-client acceptance

Test character used: `Nanotechnica`, character ID `39`.

Live client results reported:

| Scenario | Inventory | Equipment | Sit/Stand | Attack | Single Owner |
| --- | --- | --- | --- | --- | --- |
| Initial login | PASS | PASS | PASS | PASS | PASS |
| Fast reconnect <30s | PASS | PASS | PASS | PASS | PASS |
| After old timer deadline | PASS | PASS | PASS | PASS | PASS |
| Normal logout/relog | PASS | PASS | PASS | PASS | PASS |
| Reconnect >30s | PASS | PASS | PASS | PASS | PASS |
| Fast reconnect 3/3 | PASS | PASS | PASS | PASS | PASS |

Primary acceptance:

```text
CRASH_RECONNECT_UNDER_30S=PASS
STALE_TIMER_AFTER_RECONNECT=NO_EFFECT
CRASH_RECONNECT_AFTER_30S=PASS
FAST_RECONNECT_REPEAT=3/3 PASS
DIRECT_DB_MUTATION_USED=NO
```

Server-side findings during acceptance:

- ZoneEngine remained on PID `3255281`;
- ZoneEngine restart count stayed `0`;
- logs showed repeated `Reconnected to Character 39`;
- filtered reconnect/timer/inventory/fatal scans showed no errors;
- final `ONLINE_COUNT=0`;
- final `Nanotechnica Online=0`;
- final `Nanotechnica Playfield=6553`;
- no manual inventory or online-state repair was used.

## Credential quarantine

The unrelated quarantined helper under
`C:\Users\Mike\Documents\AORebirth_quarantine\legacy-chat-acceptance-20260818-010617`
contains credential-shaped logic and fields. The credential value was not
printed. Exact live/reusable status could not be proven because the referenced
source file was not present.

Precautionary status:

```text
CREDENTIAL_ROTATION_REQUIRED=YES
```

## Final production state

Production accepted and left on:

```text
/opt/ao-rebirth/zoneengine/releases/reconnect-fe6617b3
```
