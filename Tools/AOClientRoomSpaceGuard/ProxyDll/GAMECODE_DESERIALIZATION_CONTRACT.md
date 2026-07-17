# Gamecode deserialization contract

Status: static contract proven for the observed `N3::AddNetworkMessage ->
n3InfoItemRemote_t::Construct -> SimpleCharFullUpdateIIR_t` path. It is not a
universal contract for unknown external callers of exported Construct.

## Function ABI

| Property | C/new | D/old |
|---|---|---|
| Gamecode function | `+0x7A41E..+0x7AAEE` | `+0x7916D..+0x79815` |
| convention | x86 member/thiscall; object in `ECX`; one stack stream argument; `ret 4` | same |
| count location | object `+0x19C` | same |
| waypoint destination | object `+0x1A0`, 30 x 12 bytes | same |
| success | `EAX=0` | `AL/EAX=0` |
| failure | `EAX=1` | `AL/EAX=1` |

C derives its result from `BinaryStream::good()` at `+0x7AAD1..+0x7AADB`.
D compares stream state `+0x28` at `+0x79802..+0x79809`. Neither has a native
oversized-count error path.

## Observed owner and cleanup path

### C/new

`N3.dll` identity: SHA-256
`E242F4855DE93094161B619047CD838B6A3261BB53A5EB17065F60EDA5239168`,
timestamp `0x647A0D46`, image `0x67000`, checksum `0x71AD7`.

```text
n3Engine_t::AddNetworkMessage +0x6E08
  call n3InfoItemRemote_t::Construct +0x6E4A
    Construct +0xB622
      virtual deserialize call +0xB732 (return +0xB735)
      stream bad -> reject partial object
      stream good and result != 0 at +0xB788
        destroy/release partial object +0xB78D..+0xB793
        return NULL
  NULL branch +0x6E58..+0x6E5C
  destroy temporary BinaryStream +0x6E80..+0x6E83
  return without parsing remaining bytes
```

### D/old

`N3.dll` identity: SHA-256
`8C019EFD72D547879A06585B69147AB1546B9617A2FCE090E5863791AEC8B0BB`,
timestamp `0x647A073C`, image `0x62000`, checksum `0x6BEC4`.

```text
n3Engine_t::AddNetworkMessage +0x62FD
  call n3InfoItemRemote_t::Construct +0x633F
    Construct +0x9B08
      virtual deserialize call +0x9C15 (return +0x9C18)
      stream good at +0x9C5A and result != 0 at +0x9C5E
        destroy/release partial object +0x9C62..+0x9C68
        return NULL
  NULL branch +0x634D..+0x6351
  destroy temporary BinaryStream +0x6375..+0x6378
  return
```

This proves that returning failure before the first waypoint float is read
causes the partial `SimpleCharFullUpdateIIR_t` to be destroyed, the enclosing
temporary stream to be destroyed, and all unread message bytes to be discarded.
The next decode does not begin at the unread waypoint payload.

The C/new outer interface is `N3InterfaceModule_t::ServerN3Message(char*,int)`
at `Interfaces+0x7F98` in Interfaces SHA-256
`A75DBE4CB5293778468AA3283BC4EF93EFC9573A0CD1C32314176E692C3EC414`.
Its call to AddNetworkMessage is at `+0x7FAD`. This further establishes that the
owned unit being abandoned is the supplied network buffer, not a shared
long-lived stream cursor.

The partial-object destructor at C `Gamecode+0x7A332..+0x7A41D` cleans the
decoded prefix fields it owns. The optional tail allocations at `+0x308` and
`+0x320` have not occurred at the proposed pre-loop rejection point. This is
why rejecting before the first waypoint is materially safer than catching the
later fault or rejecting after tail parsing.

## Strategy decision

| Strategy | Verdict | Reason |
|---|---|---|
| A — reject and consume nothing | valid only as the local mechanism under D | the proven owner destroys the entire temporary stream; unsafe for an unknown caller that continues parsing |
| B — reject but consume all entries | rejected | an untrusted count such as `0x5A000000` creates unbounded work and more short-read/fault exposure |
| C — keep 30 and consume remainder | rejected | same unbounded consumption plus unproven semantic truncation |
| D — abort enclosing resource/message | selected | matches the observed native failure path and prevents partial publication |

The selected behavior is therefore:

```text
signed count <= 0  preserve native empty/zero-fill behavior
signed count 1..30 preserve native decode behavior
signed count > 30  return native failure value 1 before reading waypoint X
                    Construct destroys the partial object
                    AddNetworkMessage destroys the temporary stream
                    no partial object is published
```

## Stream and object postconditions

For the proven caller:

- no waypoint destination is touched for an oversized positive count;
- the stream remains positioned immediately after the count inside the
  temporary message stream;
- that stream is destroyed rather than reused or advanced to another object;
- the partial object is destroyed exactly once by Construct;
- Construct returns null and AddNetworkMessage does not publish the object;
- no tail list/container allocation has occurred;
- no wait or ResourceManager notification is skipped by the hook itself;
- success is never returned for an oversized object.

## Contract limits

Only one direct intra-N3 call to Construct was found in each supported binary:
C `+0x6E4A` and D `+0x633F`. Construct is exported, so an external ordinal
caller, dynamic lookup, or other indirect caller cannot be ruled out by direct
xrefs alone.

Any future mitigation must either:

1. prove all Construct callers have the same discard contract; or
2. gate the consume-nothing rejection on the exact deserializer return address
   from the proven Construct path (C `N3+0xB735`, D `N3+0x9C18`) and pass
   unknown callers through unchanged.

The second option is the minimum narrowly scoped, compatibility-conservative
design. It deliberately leaves unknown callers on native behavior rather than
assuming their stream contract. Runtime stack provenance, module identity,
patch installation, and rollback still require offline tests before production
use.
