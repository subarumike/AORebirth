# AO client crash corpus, normalized

This document normalizes the crash evidence supplied for the AO `version.dll`
compatibility investigation.  It is an evidence catalog, not a claim that every
record has the same producer or can be recovered safely.

All module offsets in this document are **actual PE image RVAs**.  AO's text
report prints a logical offset that is normally `actual RVA - 0x1000`; both are
shown where that distinction matters.  `K`, `I`, and `U` mean known from the
record, inferred from surrounding evidence, and unresolved.

## Corpus boundary and counting

| Set | Count | Meaning |
|---|---:|---|
| Exception blocks supplied in the discussion | 43 | 38 `C0000005`, four native `E06D7363`, one injected AOSharp/.NET `E0434352`; some blocks/stacks are truncated |
| Explicit additional dump-only event | 1 | NVIDIA fault originally identified only as absolute address `0x6648314F` |
| Canonical discussion corpus | 44 | The 43 supplied reports plus that requested dump-only event |
| Additional non-matching dump records | 7 | Real exception records in the 19-dump evidence set, but not exact matches to a canonical row |
| Raw exception records available | 51 | Canonical rows plus the seven auxiliary records |

The AOSharp event remains in the catalog for provenance but is excluded from
official-client native conclusions.  The canonical discussion corpus therefore
contains 43 official-client records and one injected managed record.

The 43 pasted reports collapse to 20 pasted families.  The additional
NVIDIA-driver event is family F21.  A family can contain the explicitly
enumerated exact-site variants with compatible state/stacks; it does not assume
a shared upstream cause across families.

In the canonical matrix, the family ID is an explicit foreign key into the
family/causality matrix. Unless an event row states an override, it inherits
that family's likely producer, proven consumer, guard classification, and
confidence. This avoids repeating an inference as though each duplicate report
independently proved it.

## Environment fingerprints

### Proxy builds observed in dumps

| ID | Available identity | Notes |
|---|---|---|
| P-A | hash unknown; image size `0x28000` | Early proxy artifact |
| P-B | SHA-256 prefix `A81328EE`; image size `0x29000` | Seen with C and D dumps |
| P-C | SHA-256 prefix `654D0797`; image size `0x29000` | Exact old NVIDIA guards present |
| P-D | SHA-256 prefix `BBB20071`; image size `0x2C000` | Later GUI/renderer guard artifact |
| P-E | SHA-256 prefix `F1131FCC`; image size `0x2C000` | Loaded for the F21 dump-only event |

These identifiers prove what a dump loaded; they do not prove that every
individual guard installed successfully.  Successful source coverage and
runtime activation are separate facts.

### NVIDIA drivers observed

| ID | File version | Repository directory suffix | SHA-256 prefix | Relevant actual RVAs |
|---|---|---|---|---|
| NV-A | `32.0.15.9186` | `nv_dispig...f4c7` | `E50395CF` | `+0x172776C`, `+0x173A009`, `+0x170C490`, auxiliary `+0x170C4C6` |
| NV-B | `32.0.16.1074` | `nv_dispi...b26` | `77001EAE` | `+0x154314F` |

The current exact NVIDIA exception predicates in source recognize NV-A by PE
identity, fault bytes, RVA, register state, and access address.  They do not
recognize NV-B.

### Client fingerprints and state inspected on 2026-07-15

| Variant | Graphics path | Key N3 SHA-256 prefix | Current proxy state at inspection |
|---|---|---|---|
| C | new graphics | `E242F485` | P-E present |
| D | old graphics | `8C019EFD` | `version.dll` absent; stale install marker remains |

The two variants share most AO gameplay code but use different graphics paths.
They are kept separate wherever hook sites, module layouts, or renderer chains
differ.

## Canonical 44-event matrix

Unknown timestamps, threads, module manifests, and proxy builds are written as
`U`; they are not filled by guesswork.  “Main/render” is inferred only where the
available stack is the GUI/Display frame loop.  “Worker” is inferred only from
the ACE/Connection/ResourceManager thread chain.  The `state` column records the
distinct register/access fingerprint; the available raw blocks remain the
primary evidence, and some pasted stacks are truncated.

| ID | Family | Source/time (CDT) | Client | Normalized exception and state | Thread / stable stack | Proxy / driver | Current-source guard and repeat |
|---|---|---|---|---|---|---|---|
| E01 | F17 | attachment; time U | C/new | `E06D7363`, `KERNELBASE+0x1659A4` | main I; MSVCR100 -> N3 -> Gamecode | U / U | diagnostic only; unique pasted |
| E02 | F04 | attachment; time U | D/old | read `0x00042267`, `randy+0x25118` | render I; randy -> GUI | U / U | unresolved; unique pasted |
| E03 | F01 | attachment; time U | D/old | read `0x42880000`, `Utils+0x82F1` | render I; Utils -> GUI | U / U | exact VEH available in source; F01 repeat |
| E04 | F11 | attachment; time U | D context I | EIP/target `0x8`; likely execute AV | stack unusable | U / U | no generic guard; F11 variant |
| E05 | F01 | attachment; time U | D/old | read `0`, `Utils+0x82F1` | render I; Utils -> GUI | U / U | exact VEH available; F01 repeat |
| E06 | F01 | attachment; time U | D/old | read `0`, `Utils+0x82F1` | render I; Utils -> GUI | U / U | exact VEH available; F01 repeat |
| E07 | F01 | discussion; time U | D/old | read `0`, `Utils+0x82F1` | render I; Utils -> GUI | U / U | exact VEH available; F01 repeat |
| E08 | F02 | discussion; time U | D/old | read `0x202`, `randy+0x6C3A1`; low EAX | render I; randy chain | U / U | exact byte-color VEH available; F02 variant |
| E09 | F02 | discussion; time U | D/old | read `0x100`, `randy+0x6C51D`; low ESI | render I; randy chain | U / U | exact dword-color VEH available; F02 variant |
| E10 | F05 | discussion; time U | D/live context I | read `0x8`, NV-A `+0x172776C`; EAX=0 | thread U; NVIDIA top only | U / NV-A | exact-driver containment available; F05 repeat |
| E11 | F20 | discussion; time U | D + AOSharp | `E0434352`, CLR -> GUI | managed/injected | U / U | excluded from official-client mitigation |
| E12 | F11 | discussion; time U | U | EIP/target `0`; likely execute AV | no trustworthy mapped caller | U / U | no generic guard; F11 variant |
| E13 | F08 | discussion; time U | D/live context I | EIP/target `5`; likely execute AV | thread U; NVIDIA frames below invalid target | U / NV-A | only initial AO dispatch can be preflighted; unique |
| E14 | F01 | discussion; time U | D/old | read `0x423C0000`, `Utils+0x82F1` | render I; Utils -> GUI | U / U | exact VEH available; F01 repeat |
| E15 | F19 | discussion; time U | D/live | `E06D7363`, N3/Vehicle | main I; MSVCR100 -> N3 -> Vehicle | U / U | diagnostic only; pasted Vehicle record |
| E16 | F16 | discussion; time U | D/old | read `0x40000000`, `N3+0x15040` | main I; VERSION -> Vehicle -> N3 | loaded, build U / U | no safe catch; RoomSpace causality must be A/B tested |
| E17 | F16 | repeat discussion; time U | D/old | same read and `N3+0x15040` | same stable chain | loaded, build U / U | exact duplicate |
| E18 | F03 | discussion; time U | D/old | read `0x144`, `randy+0x21A94`; EAX low | render I; randy -> GUI | U / U | exact whole-call unwind available; unique pasted |
| E19 | F05 | Subway discussion; time U | D/live context I | read `0x8`, NV-A `+0x172776C` | thread U; NVIDIA top only | U / NV-A | exact-driver containment available; repeat |
| E20 | F05 | Subway discussion; time U | D/live context I | same family/signature variant | thread U; NVIDIA top only | U / NV-A | exact duplicate |
| E21 | F01 | discussion; time U | D/old | read `0`, `Utils+0x82F1` | render I; Utils -> GUI | U / U | exact duplicate; sixth F01 report |
| E22 | F12 | new-client discussion; time U | C/new | EIP/target `0x41C80000`; reporter says write | render; GUI -> Display | U / U | narrow outer helper wrapper exists; cleanup unproven |
| E23 | F04 | `2026-07-13 01:40:25` | D/old | read `0x84B7A0F4`, `randy+0x2511A`; observed stack/argument `0x0A` | render; randy -> GUI -> Display | P-A / NV-A | exact state lookup skip available; dump PID 14448 |
| E24 | F13 | `2026-07-13 21:20:05` | C/new | write `0x26ED3000`, `BinaryStream+0x1B1D`; caller output pointer | main I; BinaryStream -> Gamecode -> N3 -> Interfaces | P-B / NV-A | dump PID 29984 proves Gamecode fixed-array loop overrun; whole-object reject contract unresolved |
| E25 | F14 | interleaved with E24; time/PID U | C/new | read `0`, `ntdll+0x431E9`; `EAX=0x26E90214` inside E24 overwrite range | worker; heap/MSVCR -> Interfaces -> Connection -> ACE | U / U | strong conditional secondary-victim link to F13; never catch allocator fault |
| E26 | F13 | discussion; time U | C/new | write `0x2525E000`, `BinaryStream+0x1B1D` | same deserialization chain | U / U | same caller-output signature |
| E27 | F13 | discussion; time U | C/new | write `0x1E101000`, `BinaryStream+0x1B1D` | same deserialization chain | U / U | same caller-output signature |
| E28 | F13 | `2026-07-13 22:14:24` | C/new | write `0x25634000`, `BinaryStream+0x1B1D` | same deserialization chain | P-B / NV-A | dump PID 24200; repeat caller-output signature |
| E29 | F15 | interleaved with E30; time/PID U | C/new | read `0`, `ResourceManager+0x3D84`; request `0x25B287B0` inside E30 overwrite range | worker; ResourceManager -> ACE | U / U | strong conditional secondary-victim link to F13; notifier skip unsafe |
| E30 | F13 | `2026-07-13 22:30:05` | C/new | write `0x25B2A000`, `BinaryStream+0x1B1D` | same deserialization chain | P-B / NV-A | dump PID 33032; fifth caller-output signature |
| E31 | F11 | discussion; time U | D/old | EIP/target `2`; likely execute; GUI state present | GUI stack; later unwind truncated | P-B I / NV-A I | no generic guard; possible link to A02 is unproven |
| E32 | F18 | live discussion; time U | D/live | `E06D7363`, N3/Gamecode | main I; MSVCR100 -> N3 -> Gamecode | U / U | diagnostic only; repeat family |
| E33 | F18 | `2026-07-14 22:53:11` | D/live | same throw chain | same stable chain | P-B / NV-A | dump PID 19308; repeat |
| E34 | F06 | live discussion; time U | D/old | read `0x8`, NV-A `+0x173A009`; ESI=0 | render; NVIDIA -> DDRAW/D3DIM -> randy | U / NV-A | exact-driver containment available; unique |
| E35 | F05 | live discussion; time U | D/old I | read `0x8`, NV-A `+0x172776C` | thread U; NVIDIA top only | U / NV-A | same family; no register-matching dump |
| E36 | F07 | `2026-07-14 23:33:35` | D/old | read `0x14`, NV-A `+0x170C490`; EAX=4 | render; NVIDIA -> DDRAW/D3DIM -> randy/GUI | P-C / NV-A | exact GUI-batch cleanup path; dump PID 6900 |
| E37 | F02 | `2026-07-14 23:37:05` | D/old | read `0x100`, `randy+0x6C476`; low ECX, power-of-two EDI | render; randy chain | P-C / NV-A | exact missing-sample branch available; dump PID 19692 |
| E38 | F05 | `2026-07-14 23:52:34` | D/old | read `0x8`, NV-A `+0x172776C` | thread U; NVIDIA top only | P-C / NV-A | dump PID 7520; repeat |
| E39 | F05 | `2026-07-15 00:01:18` | D/old | same family/signature variant | thread U; NVIDIA top only | P-C / NV-A | dump PID 15872; sixth pasted F05 |
| E40 | F10 | discussion; time U | D/old | read `0x8`, `GUI+0x4ED00` | render; broken frame; caller `GUI+0x4F0BE` reported | U / U | observed low-key path can be prevented upstream; fault itself unresolved |
| E41 | F09 | `2026-07-15 00:52:33` | D/old | write `0xABF0`, `GUI+0x150F22`; ECX=`0x1C`, EDX=0 | render; GUI -> VERSION -> Display | P-D / NV-A | exact null-DynamicVB cleanup path; dump PID 21008 |
| E42 | F11 | `2026-07-15 01:05:58` | D/old | true execute AV at EIP/target `0`; AO text says write | thread U; DDRAW-related stack words, invalid frame | P-D / NV-A | dump PID 23240 proves reporter mislabel; no generic guard |
| E43 | F12 | latest discussion; time U | D/old I | EIP/target `0x420C70A4`; likely execute | invalid EBP; mixed N3/Gamecode/data words | U; later D inspection found proxy absent / NV U | no proven hook or recovery boundary |
| E44 | F21 | `2026-07-15 01:57:02` | D/old | read `0x0A0A0000`, NV-B `+0x154314F`; ECX/access same | render; NVIDIA -> DDRAW/D3DIM -> proxy -> randy -> Display -> GUI | P-E / NV-B | current exact NV-A filter correctly passes through; dump PID 2740 |

## Deduplicated family and causality matrix

| Family | Canonical n | Actual signature and key state | Earliest evidenced boundary / final consumer | Current-source availability | Assessment |
|---|---:|---|---|---|---|
| F01 rectangle | 6 | read AV at `Utils+0x82F1`; values 0, float 47, float 68 | GUI supplies Rect/Point inputs / Utils consumes | exact two-instruction VEH | bad pointer or float in point/rect slot; high confidence at consumer, producer unknown |
| F02 color/sample | 3 | `randy+0x6C3A1/+0x6C476/+0x6C51D`; low 0x202/0x100 | randy color/sample selection / randy byte or dword read | exact low-pointer fallbacks | missing/invalid sample pointer; high confidence for exact cases only |
| F03 GUI batch object | 1 | read `0x144` at `randy+0x21A94` | GUI hands resource object / randy first dereference | exact function unwind | low invalid resource; high confidence |
| F04 primitive/material | 2 | wild read at `randy+0x25118/+0x2511A` | randy state-table lookup / randy state consumer | `+0x2511A` exact skip; `+0x25118` unresolved | invalid state entry/index likely; medium confidence |
| F05 NVIDIA null object | 6 | NV-A `+0x172776C`, EAX=0/read8 | AO DrawIndexed submission / NVIDIA compatibility code | exact NV-A post-fault containment plus AO preflight | driver consumes null internal object; upstream producer not identified |
| F06 NVIDIA submission | 1 | NV-A `+0x173A009`, ESI=0/read8 | same AO DrawIndexed choke / NVIDIA | exact NV-A post-fault containment | same class, distinct driver phase |
| F07 deferred flush | 1 | NV-A `+0x170C490`, EAX=4/read14 | GUI whole-batch/DynamicVB path / NVIDIA deferred lock/flush | exact batch cleanup | null/invalid deferred object; cleanup is conditional, not proven driver recovery |
| F08 corrupt callback | 1 | execute target 5 with NVIDIA frames | unknown indirect dispatch / invalid target | initial DrawIndexed target preflight only | control-flow corruption; no safe resume if already in driver |
| F09 null VB copy | 1 | write ABF0 at `GUI+0x150F22`, `rep movsd`, null-base arithmetic | DynamicVB acquisition / GUI copy | exact whole-batch cleanup | null DynamicVB is proven root condition |
| F10 GUI tree/object | 1 | read8 at `GUI+0x4ED00`, broken frame | low key observed upstream / GUI lookup | low-key sentinel at `GUI+0x4F2EF` | one producer case prevented; direct fault not characterized |
| F11 tiny target | 4 | execute-like EIP 0/2/8; dump proves EIP 0 access type 8 | unknown indirect caller / CPU instruction fetch | no family-wide guard | invalid control flow; origin unresolved |
| F12 data as code | 2 | EIP float 25.0 or 35.1100006 | C event through GUI helper; D event unknown / CPU fetch | C outer helper containment only | float/slot confusion likely; common callsite not proven |
| F13 Gamecode deserialization | 5 | caller-supplied float destination faults at `BinaryStream+0x1B1D` | count read and fixed-array loop in Gamecode / float extractor initializes output | none | count is checked against 30 only after the loop; PID 29984 proves runaway 12-byte-stride destination; reject/consume contract unresolved |
| F14 heap | 1 | null read `ntdll+0x431E9`; allocator value lies inside paired F13 overwrite interval | Gamecode fixed-array overflow conditional on paired PID / allocator | none | strong paired-address secondary-victim evidence; not a hook point |
| F15 ResourceManager | 1 | null sentinel read `ResourceManager+0x3D84`; request lies inside paired F13 overwrite interval | Gamecode fixed-array overflow conditional on paired PID / notifier | none | strong paired-address secondary-victim evidence; upstream repair first |
| F16 N3 login | 2 | read float 2.0 at `N3+0x15040`, VERSION/Vehicle path | unknown; source audit places it 0x14 before a RoomSpace-patched call / N3 | none | proxy involvement unresolved; RoomSpace A/B required |
| F17 C native throw | 1 | E06D, N3/Gamecode | AO native precondition / MSVC exception machinery | diagnostic dump only | exception type/message unavailable |
| F18 D native throw | 2 | repeat E06D Gamecode chain | AO native precondition / MSVC exception machinery | diagnostic dump only | same limitation |
| F19 Vehicle throw | 1 | E06D Vehicle/N3 chain | vehicle initialization / MSVC exception machinery | diagnostic dump only | type/message and safe failure return unknown |
| F20 AOSharp | 1 | E0434352 CLR -> GUI | injected managed code / CLR | excluded | third-party managed exception |
| F21 new NVIDIA | 1 | NV-B `+0x154314F`, `mov eax,[ecx]`, ECX=`0x0A0A0000` | legacy AO DrawIndexed boundary / NV-B virtual dispatch | evidence only; not caught | NV-A and NV-B fail downstream of the same submission boundary; producer and cleanup remain unknown |

Canonical family counts are F01=6, F02=3, F03=1, F04=2, F05=6,
F06=1, F07=1, F08=1, F09=1, F10=1, F11=4, F12=2, F13=5,
F14=1, F15=1, F16=2, F17=1, F18=2, F19=1, F20=1, and F21=1.

## Latest `0x420C70A4` stack classification

Against the E44/PID2740 D-client module map, the requested E43 words would
resolve as follows. E43 has no own module manifest, and EBP `0x454CE000` is
unreadable, so these inferred mappings must not be presented as a recovered
ordered call stack.

| Address | Resolution | Section/type | Classification |
|---|---|---|---|
| `0x6E6E7046` | `N3+0x7046` | executable `.text` | plausible post-call return word |
| `0x6E73C630` | `N3+0x5C630` | writable `.data` | object/global data, not a return address |
| `0x6E6ECFC8` | `N3+0xCFC8` | executable `.text` | plausible post-call return word |
| `0x6DD39F14` | `Gamecode+0x89F14` | executable `.text` | plausible return word |
| `0x6DDF7C74` | `Gamecode+0x147C74` | mapped code address | not immediately after a call; false-unwind/data candidate |
| `0x6E6E5372` | `N3+0x5372` | executable `.text` | immediately after an indirect `call [eax+0x28]`; plausible return word |
| `0x6EB38CB8` | `dinput+0x8CB8` | executable `.text` | plausible return word, ordering unproven |
| `0x6E6F96D8` | `N3+0x196D8` | executable `.text` | plausible return word |

The target `0x420C70A4` is not attributable to a loaded image in the borrowed
module map and decodes as approximately `35.11f`.
Other register/stack values also decode as ordinary geometry-scale floats.  The
correct conclusion is invalid indirect execution with coordinate-like data, not
that this exact coordinate is a missing world object.  No dump or valid frame
exists for this event. A later inspection found the D proxy absent, but runtime
proxy state at E43 is unknown; neither its producer nor a safe recovery boundary
is proven.

## Reporter correction: execute AVs mislabeled as writes

E42's pasted report says “Attempted write to 00000000,” but its matching dump
records `ExceptionInformation[0] == 8`, the Windows value for execute.  EIP and
the reported target are both zero.  The EIP/target 0, 2, 5, 8,
`0x41C80000`, and `0x420C70A4` records are therefore classified as likely
invalid indirect execution/control-flow faults by analogy. E42 is proven; the
others remain inferred until matching dumps establish access type. A text label
of “write” is not sufficient evidence for a data-store recovery.

## Evidence-source manifest

The immutable source pointer for E01-E06 is:

`C:\Users\Mike\.codex\attachments\227f5984-6906-4063-b2cd-5bcf2e1a117c\pasted-text.txt`

E07-E43 were supplied as discussion exception blocks. They are not yet
independent repository artifacts; E31 and E37 are visibly truncated. The
causal registers and stable resolved frames are normalized in the canonical and
family matrices above. A strict export of every raw register/stack word remains
gap C04 in `UNRESOLVED_EVIDENCE_GAPS.md`; missing words are not reconstructed.

The dump root is
`C:\Users\Mike\AppData\Local\AORoomSpaceFix\Dumps`. The following manifest
maps all 19 dumps used in the investigation:

| Canonical/aux ID | Dump file |
|---|---|
| E23 | `AO-20260713-014025-550-pid14448-exC0000005-at5C6A511A.dmp` |
| E24 | `AO-20260713-212005-217-pid29984-exC0000005-at6AA21B1D.dmp` |
| E28 | `AO-20260713-221424-700-pid24200-exC0000005-at6D121B1D.dmp` |
| E30 | `AO-20260713-223005-070-pid33032-exC0000005-at6BB51B1D.dmp` |
| A01 | `AO-20260713-223054-988-pid34884-exC0000005-at6C541B1D.dmp` |
| A02 | `AO-20260714-004546-465-pid21464-exC0000005-at04E503A8.dmp` |
| A03 | `AO-20260714-225236-585-pid16524-exE06D7363-at774859A4.dmp` |
| E33 | `AO-20260714-225311-578-pid19308-exE06D7363-at774859A4.dmp` |
| A04 | `AO-20260714-230829-308-pid5060-exC0000005-at6541776C.dmp` |
| A05 | `AO-20260714-232031-103-pid7692-exC0000005-at6541776C.dmp` |
| A06 | `AO-20260714-232100-780-pid2056-exC0000005-at5CBEC476.dmp` |
| E36 | `AO-20260714-233334-915-pid6900-exC0000005-at653FC490.dmp` |
| E37 | `AO-20260714-233705-026-pid19692-exC0000005-at5CFDC476.dmp` |
| E38 | `AO-20260714-235234-600-pid7520-exC0000005-at6541776C.dmp` |
| E39 | `AO-20260715-000118-292-pid15872-exC0000005-at5D6A776C.dmp` |
| E41 | `AO-20260715-005233-878-pid21008-exC0000005-at608D0F22.dmp` |
| E42 | `AO-20260715-010558-772-pid23240-exC0000005-at00000000.dmp` |
| A07 | `AO-20260715-010733-939-pid22284-exC0000005-at5D68C4C6.dmp` |
| E44 | `AO-20260715-015702-347-pid2740-exC0000005-at6648314F.dmp` |

The associated runtime log is
`C:\Users\Mike\AppData\Local\AORoomSpaceFix\AORoomSpaceFix.log`. It supplied
the initial E44 address/build record; the dump supplied the exact exception,
driver identity, registers, modules, and chain.

For all AO text reports, the displayed `0001:logical` offset normalizes to
actual PE image RVA `logical+0x1000`. Dump-backed rows use the PE section/image
mapping directly. A frame is excluded once EBP/stack readability fails; words
after that point are data candidates, not silently promoted to frames.

## Auxiliary dump records

These seven records are retained as evidence but are not silently added to the
mandated 44-event discussion matrix.

| ID | Time (CDT) / PID | Normalized exception | Environment | Relationship |
|---|---|---|---|---|
| A01 | `2026-07-13 22:30:54` / 34884 | write `0x22AB0000`, `BinaryStream+0x1B1D` | C, P-B, NV-A | sixth raw F13 recurrence |
| A02 | `2026-07-14 00:45:46` / 21464 | execute AV at `0x04E503A8`, outside loaded images | D, P-B, NV-A | possible precursor to E31, not proven same exception |
| A03 | `2026-07-14 22:52:37` / 16524 | E06D Vehicle/N3 | D, P-B, NV-A | additional raw F19 record |
| A04 | `2026-07-14 23:08:29` / 5060 | read8 NV-A `+0x172776C` | D, P-C | additional F05 recurrence |
| A05 | `2026-07-14 23:20:31` / 7692 | read8 NV-A `+0x172776C` | D, P-C | additional F05 recurrence |
| A06 | `2026-07-14 23:21:00` / 2056 | read100 `randy+0x6C476` | D, P-C, NV-A | additional F02 recurrence |
| A07 | `2026-07-15 01:07:34` / 22284 | write `0x3C`, NV-A `+0x170C4C6` | D, P-D | distinct driver-neighborhood signature |

Across all 51 raw exception records, F01 occurs six times, F02 four, F05
eight, F13 six, and F19 two; A02 and A07 remain their own exact signatures.
Timestamp proximity does not prove that A02 was a precursor to E31 or that each
auxiliary dump caused a separate dialog.

## Float-shaped invalid addresses

Several reported “pointers” are exact IEEE-754 single-precision values:

| Bits | Float |
|---|---:|
| `0x40000000` | 2.0 |
| `0x41C80000` | 25.0 |
| `0x420C70A4` | approximately 35.11 |
| `0x423C0000` | 47.0 |
| `0x42880000` | 68.0 |

This is evidence for argument, ABI, stack-slot, or object-layout confusion in at
least some records.  It is not proof of which component introduced the
confusion, and it does not justify a heuristic that treats every float-looking
address as recoverable.

## Confidence and non-conclusions

- High confidence: exact exception site/access/state in matching dumps; F09's
  null DynamicVB condition; F13's repeated boundary store; F21's complete
  NV-B-to-AO draw chain; E42's execute-access correction.
- Medium confidence: main/render versus worker classification from stable
  stacks; float/slot confusion as a class; the AO DrawIndexed boundary as the
  common upstream choke for F05/F06/F21.
- Low or unresolved: producer of the D `0x420C70A4` target; causality between
  F13 and F14/F15; native exception types/messages; high-pointer randy object
  layouts; driver state after an intercepted NVIDIA AV.
- The data does not show millions of independent bad objects.  It shows a much
  smaller set of repeated consumer sites receiving bad state.
- A top frame in NVIDIA, ntdll, KERNELBASE, or the CLR identifies the final
  consumer/exception machinery; it does not by itself identify the producer.
