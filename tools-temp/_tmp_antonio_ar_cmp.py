from pathlib import Path

raw = bytes.fromhex(Path(r"tools-temp/_tmp_antonio_ar_054034.hex").read_text().strip())
print("len", len(raw))
print("head", raw[:48].hex().upper())
print("tail", raw[-48:].hex().upper())
print("has5FA0E000", b"\x5F\xA0\xE0\x00" in raw)
print("has556A8FC0", b"\x55\x6A\x8F\xC0" in raw)
print("has7996C028", b"\x79\x96\xC0\x28" in raw)

# scan plausible expiry-like values near end
for i in range(max(0, len(raw) - 120), len(raw) - 4):
    v = (raw[i] << 24) | (raw[i + 1] << 16) | (raw[i + 2] << 8) | raw[i + 3]
    if 0x5F000000 <= (v & 0xFFFFFFFF) <= 0x61000000:
        print("cand@%d %08X" % (i, v & 0xFFFFFFFF))

# Compare body after first 2-byte size to old tip body start
old = (
    "D446000A0001052900000DC17996C028465A40610000C3507996C02801000007E20000DAC35569CDBF"
)
old_b = bytes.fromhex(old)
print("old first16", old_b[:16].hex().upper())
print("new first16", raw[:16].hex().upper())
# size field interpretation
print("be size", (raw[0] << 8) | raw[1], "le size", raw[0] | (raw[1] << 8), "packet_len", len(raw))
print("old be size", (old_b[0] << 8) | old_b[1])

# Find text region overlap
idx = raw.find(b"Assemble a BO-18")
print("assemble@", idx)
idx2 = raw.find(b"\xDA\xC3")
print("mission type@", idx2, "id", raw[idx2 + 2 : idx2 + 6].hex().upper() if idx2 >= 0 else None)
