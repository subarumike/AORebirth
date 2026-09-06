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

The authority classification is supplied by Mike. The supplying project's name,
revision, extraction script, function addresses and extraction logs were not
provided. This is not an AORebirth or Codex extraction, and the existing local
Ghidra work does not independently establish this catalog's provenance.

This reference is used only by offline reconciliation. It is not runtime data.
