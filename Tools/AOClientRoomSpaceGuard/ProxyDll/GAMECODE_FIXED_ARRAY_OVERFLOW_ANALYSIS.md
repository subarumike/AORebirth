# Gamecode fixed-array overflow analysis

Status: **Outcome B — the defect and repair boundary are proven; production
implementation remains blocked.** No client code was patched by this
investigation.

## Finding

The recurring crash reported at `BinaryStream.dll+0x1B1D` is an out-of-bounds
write by the Gamecode deserializer for `SimpleCharFullUpdateIIR_t`. The affected
optional block is `HasWaypoints` (`0x00010000`). Gamecode reads a signed waypoint
count and writes that many three-float vectors into an inline array that has
space for exactly 30 vectors.

`BinaryStream::operator>>(float*)` is only where the invalid caller-provided
destination is first touched. BinaryStream capacity, growth, allocation,
alignment, and terminator behavior are not the cause of this family and are not
repair targets.

## Exact binary profiles

| Profile | Module identity | Deserializer | Count boundary | Native failure tail |
|---|---|---:|---:|---:|
| C/new | Gamecode SHA-256 `60E5C2073FD488EC01579CD23BA7C87E3881228815EC037954D5CE3DBF64B5B4`; timestamp `0x647A0E09`; image `0x311000`; checksum `0x2F101D` | `+0x7A41E..+0x7AAEE` | `+0x7A91D` | `+0x7AADE` |
| D/old | Gamecode SHA-256 `654969A6B65946CB161F0E60AED8589260FC5ECA1795488F66BB56F8FFF73726`; timestamp `0x647A08F0`; image `0x30B000`; checksum `0x2F290A` | `+0x7916D..+0x79815` | `+0x7964B` | `+0x79807` |

Both functions are x86 member functions: the destination object is passed in
`ECX`, the sole stack argument is the BinaryStream pointer, and the function
uses `ret 4`. The object is retained in `EBX`, the stream in `ESI`, and the
address of the count in `EDI` at the affected block. Both return `0` for
success and `1` for stream/deserialization failure.

The C profile vtable is at `Gamecode+0x165848`; its deserializer pointer is the
entry at `+0x165864`. Static type evidence includes the adjacent diagnostic
literal `Wrong client version of SimpleCharFullupdateIIR!`. Its factory at
`+0xD2D9` allocates `0x330` bytes and calls the constructor at `+0x79E19`. The
copy constructor is `+0x7A092`; the complete destructor is
`+0x7A332..+0x7A41D`, and scalar deleting destructor `+0x7AC16` invokes it.

## Exact unsafe loop

C/new:

```text
+7A90A  address object+0x19C
+7A913  read signed int32 count
+7A919  set loop/zero-fill index to zero
+7A91D  signed compare count with zero
+7A920  count <= 0 -> zero-fill path
+7A934  read X -> object+0x1A0 + index*12
+7A93F  read Y -> destination+4
+7A94E  read Z -> destination+8
+7A954  increment index
+7A95A  advance destination by 12
+7A95E  compare index with untrusted count
+7A960  loop while index < count
+7A962  compare completed index with 30
+7A965  count >= 30 -> normal continuation
```

D/old has the same instructions and layout at
`+0x79638..+0x79693`; its three float reads are at
`+0x79662/+0x7966D/+0x7967C`.

The comparison with 30 is **not** a late rejection. It merely decides whether
the remaining array entries must be zero-filled. A count greater than 30 has
already overwritten memory and then proceeds as normal if it survives. There
is no native oversized-count failure branch.

## Capacity proof and overwrite arithmetic

The count is at `object+0x19C`. The inline array starts at `object+0x1A0` and
contains 30 records of three 32-bit floats:

```text
capacity        30 records
record size     12 bytes
valid byte span object+0x1A0 .. object+0x307
first OOB byte  object+0x308
```

The C constructor zeroes exactly indices 29 through 0 at
`+0x79FEF..+0x7A00E`; a second constructor initialization loop at
`+0x7A05E..+0x7A07F` also processes exactly 30 records. The function beginning
at `+0x7A092` zeroes exactly 30 waypoint records at `+0x7A2C9..+0x7A2E4`.

| Declared count | New corrupt range | Directly overlapped fields |
|---:|---|---|
| 31 | `+0x308..+0x313` | `+0x308`, `+0x30C`, `+0x310` |
| 32 | adds `+0x314..+0x31F` | `+0x314`, padding `+0x315..+0x317`, `+0x318`, `+0x31C` |
| 33 | adds `+0x320..+0x32B` | `+0x320`, `+0x324`, `+0x328` |
| 34 | adds `+0x32C..+0x337` | `+0x32C..+0x32F`, then eight bytes beyond the allocation |
| N > 30 | `object+0x308 .. object+0x1A0+(12*N)-1` | progressively arbitrary following heap |

There is no count multiplication before the loop, so a huge positive count
does not first wrap an allocation calculation. It drives repeated 12-byte
writes until an unmapped page or another fault stops it.

Counts zero and below take a signed `jle`. The index was independently reset to
zero at `+0x7A919/+0x79647`, so negative values do not index before the array;
they zero-fill all 30 slots while leaving the negative count stored at
`object+0x19C`.

## Adjacent object fields

The table separates directly proven use from semantic names recovered through
the repository stat mapping. A semantic name is not claimed where the binary
does not establish one.

| Offset | Proven binary behavior | Meaning/status |
|---:|---|---|
| `+0x308` | optional pointer, initialized null, later assigned and traversed; gated by flag `0x40000000` | heap list/container pointer; domain and ownership unresolved |
| `+0x30C` | low 16-bit value consumed as stat `0x185` | `expansion` (stat 389) |
| `+0x310` | high 16-bit value consumed as stat `0x294` | `accountflags` (stat 660) |
| `+0x314` | signed byte, default 2, conditionally decoded; stat `0x29C` | `battlestationside` (stat 668) |
| `+0x315..+0x317` | no same-type access found | padding or unknown |
| `+0x318` | optional int32, default zero; stat `0xC4` | `petmaster` (stat 196) |
| `+0x31C` | unconditional int32; stat `0x296` | `mechdata` (stat 662) |
| `+0x320` | optional pointer to an allocated 16-byte dynamic integer container | domain and ownership unresolved |
| `+0x324/+0x328` | one two-dword aggregate, copied together | identity-like structure; exact role unresolved |
| `+0x32C` | uint16; stat `0x2A1` | `visualflags` (stat 673) |
| `+0x32E` | byte | semantic name unresolved |
| `+0x32F` | final stream byte normalized to boolean | semantic name unresolved |

Some fields are decoded again after the waypoint block, but that is not a
repair: count 31 leaves `+0x30C/+0x310` corrupted; count 32 may leave
`+0x314/+0x318` corrupted; count 33 may leave `+0x320` corrupted; and count 34
has already left the allocation. Conditional later writes cannot make an
arbitrary runaway write safe.

No adjacent vtable, reference count, allocator pointer, or ResourceManager
request field is proven inside `+0x308..+0x32F`. The object's vtable is at
offset zero and cannot be reached by this forward overwrite. The allocator and
ResourceManager values seen in paired reports are separate heap objects inside
the much larger absolute runaway spans, not named fields of this object.

## Serialized waypoint format

The repository wire serializer and the client disassembly agree on the field
widths and record structure in this block:

```text
HasWaypoints flag       0x00010000 in the enclosing update flags
waypoint owner          two serialized int32 values (Identity)
waypoint count          one serialized signed int32
each waypoint           X, Y, Z as three serialized IEEE-754 single values
per-entry bytes         12
entry branches          none
entry padding           none
entry terminator        none
```

The repository serializer writes the count followed immediately by exactly
three singles per waypoint. Its evidence decoder currently accepts counts
`0..4096`; that is decoder evidence, not proof that the stock client's fixed
array accepts more than 30. No server behavior was changed in this task.

The repository writer emits network byte order. The client BinaryStream scalar
extractor conditionally swaps according to stream endian state, but this
investigation did not independently reconstruct the inbound stream's endian
mode initialization. That remaining producer detail does not affect the
host-order signed count boundary, but it prevents claiming why the observed
huge count appeared.

The observed host-order counts `0x5A000000` and `0x0001CB95` are not credible
valid client waypoint counts. Their producer is unresolved. A wrong cursor,
wrong message version, byte-order mismatch, or earlier malformed field can
make unrelated four bytes appear as the count. The crash corpus does not prove
which producer occurred.

## Short reads and partial entries

Each integer or float extractor requests exactly four bytes. The memory-buffer
reader at C/new `BinaryStream+0x117D` copies and advances by four only when all
four bytes are available. A short read copies nothing, advances zero, and sets
stream error 3. The float extractor first zero-initializes its destination.

Gamecode does not check stream state after each waypoint scalar, so a truncated
record can cause later destinations in the in-capacity loop to be initialized
to zero while the cursor remains fixed. The function's final result is derived
from stream-good state, and N3 then rejects the partial object. The safe
oversize design reads no waypoint scalar at all and asks the proven N3 owner to
discard the temporary stream.

## Cross-crash correlation

| Family | Classification | Evidence |
|---|---|---|
| `BinaryStream+0x1B1D` | proven direct symptom | four dumps put the attempted address in the caller output register while Gamecode's index walks the waypoint destination |
| ntdll allocator event E25 | strongly supported, conditional | value `0x26E90214` lies inside E24's runaway range; independent PID/time/thread for E25 was not preserved |
| ResourceManager E29 | strongly supported, conditional | request `0x25B287B0` lies inside E30's runaway range and its sentinel is zero; independent PID/time/thread for E29 was not preserved |
| invalid EIP `0/2/5/8` | plausible mechanism, unsupported event link | a runaway write can corrupt code pointers, but no shared process/object/allocation evidence ties these events to this object |
| coordinate-like EIP | plausible mechanism, unsupported event link | waypoint float bits can overwrite pointer fields, but no matching dump/session connects E22 or E43 |
| renderer objects | unsupported | no renderer object is located in a proven waypoint overwrite span |
| NVIDIA faults | unsupported | dump-backed events are separate D/old sessions with no matching object/allocation identity |

The Gamecode repair must be tested before downstream allocator or
ResourceManager changes. It does not replace renderer virtualization: the
renderer, GUI, and NVIDIA families have independent proof and remain separate.

## Outcome

The affected type, both client implementations, fixed array, record format,
adjacent fields, failure result, and observed N3 message-abort contract are
statically proven. The production hook was **not** implemented because exact
integration/rollback tests and live C/D validation do not yet exist, and the
consume-nothing rule is proven only for the observed N3 Construct caller.

See `GAMECODE_DESERIALIZATION_CONTRACT.md`,
`GAMECODE_OVERFLOW_MITIGATION_PLAN.md`, and
`GAMECODE_OVERFLOW_VALIDATION.md` for the rejection contract, candidate hook,
and release gates.
