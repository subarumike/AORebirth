# Gamecode overflow validation

Status: validation specification. Static reconstruction passed; implementation,
build, and live runtime validation were not performed because the release gates
are not yet satisfied.

## Required offline harness

The harness must execute the exact emitted C and D thunks against synthetic x86
contexts and a guarded `0x330`-byte object. It must also model the observed N3
Construct/AddNetworkMessage return chain.

For each case record:

```text
selected binary profile and identities
hook-site bytes and native-tail bytes
initial/final registers, EFLAGS, ESP, EBP, FS chain
x87 control/status/depth and MXCSR
count, number of native scalar reads, final stream position
highest destination byte written
deserializer result, Construct result, object destruction count
temporary-stream destruction count and publication count
diagnostic counter/event count and drop count
```

## Count and malformed-stream matrix

| Case | Expected mitigation behavior | Required assertion |
|---|---|---|
| count 0 | native zero-fill | 30 records zeroed; success follows native stream state |
| count 1 | native decode | 12 input bytes; remaining 29 records zeroed |
| count 29 | native decode | 348 input bytes; record 30 zeroed |
| count 30 | native decode | 360 input bytes; last write is object `+0x307` |
| count 31 | reject before X of record 1 | no waypoint write; result 1; whole message discarded |
| far above 30 | same rejection | constant work independent of count |
| maximum signed positive `0x7FFFFFFF` | same rejection | no loop, multiply, allocation, or scalar read |
| encoded `0xFFFFFFFF` (-1) | preserve native nonpositive behavior | zero-fill from index zero; no pre-array write |
| encoded `0x80000000` | preserve native nonpositive behavior | zero-fill from index zero; no overflow |
| truncated count | native stream failure | partial object destroyed once; temporary stream destroyed once |
| malformed/short scalar before capacity | native stream failure | no write beyond `+0x307`; object not published |
| malformed scalar after serialized entry 30 with declared count 31 | reject at count | no scalar consumed; enclosing stream discarded |
| repeated rejects | one abort per message | no leak, double free, deadlock, or unbounded log |

Run every row against both exact client profiles. Unknown Gamecode, unknown N3,
one-byte hook mismatch, one-byte tail mismatch, wrong caller return, and branch
target outside the approved module must all install no behavioral mitigation.

## Patch-transaction tests

Inject failure after every transition:

```text
profile identification
thunk allocation
thunk protection change
thunk write
peer-thread suspension
hook protection change
hook write byte 1..5
instruction-cache flush
protection restoration
peer-thread resume
```

Every failed transaction must leave either the complete old state or complete
new state. Test a peer IP at every byte of the hook window and every published
thunk boundary. Verify idempotent install, double-install refusal, kill-switch
behavior, and clean process-detach policy.

## ABI tests

For accepted and rejected branches independently verify:

- exact native destination and no split instruction;
- `EBX/ESI/EDI/EBP` and `ESP` restoration;
- correct FS exception chain;
- no x87 depth/control/status change;
- no MXCSR change;
- C result exactly 0/1 as expected;
- D `setne al` path receives the intended ZF and returns exactly 1;
- accepted branches receive native-required flags/registers;
- wrong N3 return address follows the original path;
- no thunk call, allocation, lock, file I/O, or module query.

## Object and stream assertions

For count 31 and above under the proven caller:

```text
object+0x1A0..+0x32F unchanged by waypoint decoding
stream cursor remains immediately after the count
Construct destroys/releases partial object exactly once
Construct returns NULL
AddNetworkMessage destroys the temporary stream exactly once
remaining bytes are not interpreted as another object
no partial SimpleCharFullUpdateIIR_t is published
```

Guard `object+0x308`, `+0x320`, the allocation boundary at `+0x330`, and the
next allocation so the harness detects even one excess byte.

## Runtime matrix

Mike launches and exercises AO; Codex does not launch or attach to the client.
Each completed run must preserve the proxy log, module manifest, dump if any,
and exact policy/hash evidence.

Required configurations:

1. C/new with proxy absent.
2. C/new diagnostic-only.
3. C/new mitigation enabled.
4. D/old with proxy absent.
5. D/old diagnostic-only.
6. D/old mitigation enabled.
7. kill switch for both clients.
8. unknown/mutated profile fail-closed test offline only.

Required scenarios:

- normal login and character appearance updates;
- zoning and repeated resource/message activity;
- a controlled count-31 reproduction if a safe test source is available;
- repeated malformed-object rejection;
- concurrent ResourceManager activity;
- multi-client use;
- two-hour resource/zoning soak;
- FPS/frame-time A/B with no valid-object logging.

## Acceptance gates

Promotion requires all of the following:

- counts 0, 1, 29, and 30 remain byte-for-byte native-equivalent;
- every positive count above 30 from the exact supported Gamecode/N3 profile
  and proven Construct return address rejects before the first waypoint write;
- the stream is discarded through the proven owner, not left for another
  parse;
- no partial object publication, leak, double release, wait starvation,
  retry storm, deadlock, or new crash family;
- no allocator or ResourceManager corruption in the controlled reproduction;
- exact C and D identity and caller gates reject every near miss;
- disabled and diagnostic-only modes do not alter native parsing, result, or
  publication semantics;
- kill switch prevents installation;
- no meaningful FPS or frame-time regression (repeatable >5% is a blocker);
- compact diagnostics are rate-limited and show the selected message abort;
- renderer/NVIDIA defects remain tracked independently rather than declared
  fixed by this mitigation.

## Current results

| Validation layer | Result |
|---|---|
| C/new static deserializer/layout/caller reconstruction | PASS |
| D/old static deserializer/layout/caller reconstruction | PASS |
| serialized waypoint structure | PASS for owner/count/three fixed floats; malformed-count producer unresolved |
| whole-message discard for observed N3 caller | PASS |
| adjacent allocator/ResourceManager field hypothesis | DISPROVEN; they are separate conditional secondary victims |
| emitted thunk and transaction tests | NOT RUN; no implementation |
| proxy build | NOT RUN; docs-only outcome |
| client runtime | NOT RUN; Codex did not launch or attach |
| hardware/FPS/soak | NOT RUN |

Until the NOT RUN rows pass, the outcome remains B and production mitigation
must remain absent.
