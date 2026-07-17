# Gamecode overflow mitigation plan

Status: **design proven; implementation blocked**. This file is not permission
to patch the client until every release gate below passes.

## Selected boundary

Patch the complete five-byte signed count test immediately after count
extraction and before first waypoint setup:

```text
original bytes: 83 3F 00 7E 45
C/new site:      Gamecode+0x7A91D
D/old site:      Gamecode+0x7964B
replacement:     E9 rel32-to-profile-thunk
```

The candidate thunk is stateless and makes no calls.

| Signed count | C destination | D destination | Effect |
|---:|---:|---:|---|
| `<= 0` | `+0x7A967` | `+0x79695` | preserve native zero-fill behavior |
| `1..30` | `+0x7A922` | `+0x79650` | preserve native waypoint loop |
| `> 30` | native failure tail | native failure tail | reject enclosing object/message |

C rejection sets `ECX=1` and jumps to the native unwind at
`Gamecode+0x7AADE`, which returns `EAX=1` and restores the compiler FS chain.
D rejection executes `mov ecx,[ebp-0x0C]; mov eax,1; test eax,eax` and jumps
to `Gamecode+0x79807`. The native `setne al` then leaves the full register
exactly `EAX=1` while the tail restores FS and the saved frame.

Do not branch to C `+0x7A996` or D `+0x796C4`: those are normal continuation
sites and would leave unread waypoint payload while returning success.

## ABI and machine-state contract

At both count sites:

- stack is `EBP-0x6C` after the function's fixed frame and saved registers;
- the integer extractor used callee cleanup, so no caller stack adjustment is
  pending;
- compiler EH state remains `-1`; its first later transition is C `+0x7A9F4`
  or D `+0x79722`;
- no inline x87/SSE instruction occurs in Gamecode from function entry through
  the hook; imported callees must separately preserve ABI-visible x87 depth,
  control state, and MXCSR;
- the thunk must preserve `EBX`, `ESI`, `EDI`, `EBP`, `ESP`, FS exception
  registration, x87 control/state, and MXCSR;
- the rejection branch may change caller-volatile `EAX/ECX/EDX` and flags
  because it exits through the native failure return;
- accepted counts must reach native code with the register and stack state
  required by the overwritten comparison/branch.

The thunk must additionally compare `[EBP+4]` with the exact proven Construct
return address after validating that the address belongs to the exact N3
profile:

```text
C/new N3+0xB735
D/old N3+0x9C18
```

An oversized count from any other caller must follow the original unsafe code
until that caller's failure contract is independently proven. This is a narrow
mitigation, not a generic exception suppressor.

## Installation transaction

A future implementation should add a dedicated module such as
`src/gamecode_waypoint_guard.h/.cpp` and integrate it with the existing
one-time deferred installer. It must:

1. require the exact Gamecode and N3 SHA-256, PE timestamp, image size, and
   checksum listed in the contract;
2. require the exact five original hook bytes and exact native-tail bytes;
3. verify module ranges, executable sections, and every branch target;
4. allocate executable thunk memory within signed `rel32` reach;
5. suspend peer threads for the patch transaction;
6. refuse/retry if any thread IP is in the five-byte patch window or thunk
   publication window;
7. change protection, write all thunk bytes, publish the jump, restore
   protection, and flush the instruction cache;
8. roll back every completed step on failure and verify the original bytes;
9. never patch or query per frame;
10. install no behavior on an unknown or partial profile.

Direct relative jumps avoid an indirect CFG edge. The emitted code still needs
byte-level tests for both profiles.

## Policy and diagnostics

Required independent modes:

```text
disabled         install no Gamecode observer or mitigation
diagnostic-only  observe exact supported count boundary without changing flow
mitigation       reject only proven-caller counts >30
kill switch      force disabled before installation
```

The final names and configuration source must follow the proxy's existing
policy model; no new environment variable is invented in this design.

The proven control-flow thunk is stateless and performs no allocation, lock,
file I/O, module lookup, hashing, `VirtualQuery`, counter update, queue write,
or logging. Production diagnostics require a separate cold rejection recorder
that runs only for the oversized branch, preserves the complete ABI state, and
places a fixed-size event in a preallocated bounded queue. That recorder is not
yet designed or proven and is therefore an explicit implementation blocker. A
deferred worker may rate-limit and emit:

```text
timestamp, PID, TID, client profile
Gamecode and N3 identity
hook RVA, object, stream, signed count, capacity 30
stream position before rejection
caller return address and selected abort action
resource identity only if later proven
```

Valid objects are never logged. Queue overflow increments one drop counter.
This is required to avoid repeating the prior `version.dll` FPS regression.
The exact non-mutating stream-position field/read is itself a production gate;
if it cannot be proven and captured without hook-path calls or locks, the
mitigation must not be promoted with an incomplete required event.

## Why no implementation was made

The binary repair design and observed caller contract are strong enough to
specify exact emitted behavior, but the current source tree has not yet proven:

- transactional integration with the proxy's existing mixed patch lifecycle;
- exact rollback under every partial failure and thread-IP race;
- emitted C and D thunk bytes, branch reachability, and near-miss rejection;
- diagnostic queue behavior without hot-path cost;
- live C/new and D/old object-abort behavior;
- absence of retry storms or login/resource side effects;
- runtime proof that exact caller-return gating matches every reproduced
  waypoint-overflow event.

Implementing before those checks would violate the task's complete-proof gate.
The approved next step is isolated emitted-code and patch-transaction testing,
followed by a Mike-launched runtime validation pass. No BinaryStream change,
generic VEH/SEH catch, post-overflow guard, count clamp, or renderer/driver RVA
dependency is allowed.
