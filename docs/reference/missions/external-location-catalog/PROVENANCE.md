# Supplied mission-location catalog

SOURCE_ROLE: AUTHORITATIVE_EXTERNAL_GAME_CODE_EXTRACT

AUTHORITATIVE_FOR:
- Complete mission-location ID catalog
- Exact location ID values
- Exact associated display names

ORIGIN: Supplied by another project; reportedly extracted directly from AO game code.

AOREBIRTH_LOCAL_GHIDRA_EXTRACTION: NO
AOREBIRTH_INDEPENDENT_REPRODUCTION: NO
SOURCE_INDEPENDENTLY_REPRODUCED: NO

Supplied path: `C:/Users/Mike/Downloads/ACGEntrances.json`.
Retained file: `ACGEntrances.json`, copied byte-for-byte, 36,270 bytes.
SHA-256: `da64734fd544d93c3ccfb2ae56ad4248c18a101b86fed7e0deadc8f315d6c1c8`.

The supplied file has one trailing comma before the closing object brace.
The offline reader removes only that terminal comma in memory. The retained
source remains unchanged. The catalog has 370 exact display names and 2,235
distinct unsigned 32-bit IDs; no ID is assigned to multiple names. Preserve
display-name case, punctuation and spelling. The source does not supply
coordinates, packet offsets, identity type tags, selection rules or weights.

The authority classification is supplied by Mike. Mike subsequently identified
the source as `never-knows-best/aosharp.utils`, directory `Neko`:
https://gitlab.com/never-knows-best/aosharp.utils/-/tree/fdc5017a1abd77ed6c85dfee19e3e6459aba0c11/Neko

Inspected revision: `fdc5017a1abd77ed6c85dfee19e3e6459aba0c11` (master at inspection).
The repository catalog matches the supplied file when line-ending differences
are ignored. The retained supplied-file hash above remains the byte authority.
`Neko/KeyWarper.cs` loads the values as unsigned 32-bit integers and uses each
as the instance of `IdentityType.ACGEntrance`, casting to signed int without
changing the bits. This confirms the consumer's identity semantics; it is not
a coordinate decoder.

For names with multiple entries, Neko queues all candidate IDs, tries the
mission key on each entrance, advances on rejection/interval, and caches the
entrance when accepted. It does not determine the selected ID from offer
coordinates. The code consumes this catalog; an extraction script, function
addresses and extraction logs were not found in this inspected consumer.
This is not an AORebirth or Codex extraction, and the existing local Ghidra
work does not independently establish the extraction provenance.

This reference is used only by offline reconciliation. It is not runtime data.
