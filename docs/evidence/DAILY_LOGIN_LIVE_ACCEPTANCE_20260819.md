# Daily Login Same-Session Live Acceptance

Date: 2026-08-19 UTC

## Result

`DAILY_LOGIN_PRODUCTION_ACCEPTED=NO`

Exact candidate `96846ed450a28fde82807355b1335037138f1b93` was pushed, deployed, and tested through the normal real-client path. The reward grant persisted exactly once, but the reward was not visible in the uninterrupted active client session. Production was rolled back to `reconnect-fe6617b3`.

## Repository and pre-deployment gate

- Candidate source SHA: `96846ed450a28fde82807355b1335037138f1b93`
- Pre-push branch: `master`, clean, `0` behind and `1` ahead of `origin/master`
- Candidate scope: Daily Login runtime repair, focused contract test, and repair evidence only
- `git diff --check`: PASS
- Reused immediately preceding mandatory integration gate: PASS, `11/11`
- AOtomation suite: PASS, `1040/1040`
- Client patch/login-key/crash/RoomSpace/package self-tests: PASS
- Push: `9933f7dc..96846ed4 master -> master`
- Post-push local and `origin/master`: exact SHA match, divergence `0/0`

Relevant commands:

```text
git rev-parse HEAD
git status --short --branch
git diff --check
git show --check --stat --oneline --decorate --no-renames 96846ed450a28fde82807355b1335037138f1b93
git diff-tree --no-commit-id --name-status -r 96846ed450a28fde82807355b1335037138f1b93
git push origin master
git fetch origin master
```

## Linux artifact and deployment

The initial framework-dependent artifact passed local publish/smoke validation but failed remote staging validation because production intentionally has no system .NET runtime. The failure occurred before service stop or symlink promotion. Production remained on `reconnect-fe6617b3`.

The same exact source SHA was republished with the governed self-contained mode:

```text
cmd /d /c LinuxBuild\publish-zoneengine.cmd linux-x64 true
```

Artifact identity:

| Artifact | SHA-256 |
| --- | --- |
| `ZoneEngine` | `a143bbf7c92884ebe1ea08d389da628c16abe6708dd35247e19bd96c9417b75c` |
| `ZoneEngine.dll` | `a7447621bc0ed6f43d44ec7a08183a275b017e78e321cacdc8618cdc2aee528f` |
| Self-contained archive | `3b89a620617859b3b9def12df02401281342ba6075af916e7251d179fb050f3d` |
| Rejected framework-dependent archive | `0c35632f546d79a368c3f5547d53be58afa12d8b8d998bb496d554b512e087cf` |

Governed promotion command on production:

```text
bash upgrade-live-service.sh <verified-self-contained-publish-dir> dailylogin-sync-96846ed4
```

Deployment results:

- Pre-promotion online-character guard: PASS, `0`
- Immutable staging: PASS
- Startup validation: PASS
- Database preflight: PASS
- Rollback-release validation: PASS
- Atomic promotion: PASS
- Production release: `/opt/ao-rebirth/zoneengine/releases/dailylogin-sync-96846ed4`
- Service state: active/running
- Candidate service PID: `4003536`
- Candidate start: `2026-08-19T00:37:43Z`
- Rollback target ready: `/opt/ao-rebirth/zoneengine/releases/reconnect-fe6617b3`

## Safe claim state and baseline

No production ledger or inventory state was reset or edited for acceptance.

- Account: `subarumike`
- Character: `39`
- Pre-claim state: `nextDay=1`, `claimedToday=false`, `ClaimedCount=0`, `Taken=[]`
- Legitimate reward: day 1, one random Phasefront Phantom
- Existing Banshee `270996` was intentionally installed by the player and was therefore expected to leave inventory.
- Client baseline: full expected inventory PASS, movement PASS, sit/stand PASS, attack PASS
- Database baseline: 12 regular item rows and one instanced item row

## Claim evidence

- Claim token: `c202608190053333a49746b`
- Claim UTC: `2026-08-19T00:54:13.5925177Z`
- Selected reward: item `288809`, `Phasefront Phantom - Candy Cane Flyer`
- Quantity: `1`
- Quality: `1`
- Ledger after claim: `ClaimedCount=1`, `Taken=[1]`, `LastClaimUtc=2026-08-19`
- Database after claim: exactly one item `288809`, standard inventory page `104`, placement `75`
- Duplicate count: one row, total quantity one
- Client same-session result: reward was not visible before client closure

The active runtime logged reward resolution, item creation, and successful grant. The exact deployed source removed the synthetic `OverflowWindow` sender and retained the authoritative standard-inventory refresh call. No inventory-update failure was logged. The available journal did not independently decode the outbound packet bytes, so on-wire delivery of the standard inventory refresh remains unproven.

## Acceptance matrix

| Check | Result | Evidence |
| --- | --- | --- |
| Daily Login opens | PASS | Real client opened normal Daily Login flow |
| Claim accepted | PASS | Server result and grant log |
| Reward granted exactly once | PASS | One item row and one ledger transition |
| Reward persisted | PASS | Item `288809` persisted on page `104`, placement `75` |
| Reward visible without relog | FAIL | Player reported it was not visible in the uninterrupted session |
| Full existing inventory remains visible | NOT TESTED | Test stopped at first material failure |
| No synthetic overflow sequence | PASS | Exact deployed source and contract test contain no Daily Login overflow sender |
| Movement after claim | NOT TESTED | Test stopped at first material failure |
| Sit/stand after claim | NOT TESTED | Test stopped at first material failure |
| Attack after claim | NOT TESTED | Test stopped at first material failure |
| Continued gameplay | NOT TESTED | Test stopped at first material failure |
| Normal logout | NOT TESTED | Client was closed for rollback after failure |
| Cold-login inventory | PASS | Verified after rollback; full expected inventory present |
| Cold-login reward persistence | PASS | Phantom visible after rollback login |
| No duplicate reward | PASS | Exactly one item row and one ledger claim |

## Rollback

The client was closed before rollback. The online-character guard was confirmed clear, and production was atomically returned to:

`/opt/ao-rebirth/zoneengine/releases/reconnect-fe6617b3`

Post-rollback state:

- Service: active/running
- PID: `4017598`
- Start: `2026-08-19T00:57:15Z`
- Persisted Phantom visible: PASS
- Complete expected inventory: PASS
- Movement: PASS
- Sit/stand: PASS
- Attack: PASS

## Preserved evidence

Root-only incident snapshot:

`/root/aorebirth-recovery/20260819T005558Z-dailylogin-sync-96846ed4-failed-final`

The snapshot contains deployed release identity and hashes, service state, ZoneEngine journal, Daily Login claim files, post-claim character-39 inventory rows, and a SHA-256 manifest.

## Remaining evidence boundary

Persistence, exactly-once behavior, and removal of the synthetic overflow sender are proven. Same-session reward visibility remains failed. The first unresolved boundary is whether the authoritative standard-inventory refresh was transmitted and accepted on wire after the persisted grant. No further production edits were made during this task.
