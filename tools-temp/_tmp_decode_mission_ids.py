# Decode all Mission (DAC3) identities inside Veronica / Insignia / Updated QFU hex constants.
veronica = "00D7000A0001032300000DB0765A690A465A40610000C350765A690A01000007E20000DAC35556893A0000000F000000000000000000000002596F752061677265656420746F2066696E6420696E666F726D6174692E2E2E"
insignia_start = "0134000A0001023700000DB6765A690A465A40610000C350765A690A01000007E20000DAC355563C160000000F"
updated_start = "0137000A0001031100000DB6765A690A465A40610000C350765A690A01000007E20000DAC355563C170000000F"

def find_missions(name, hexstr):
    data = bytes.fromhex(hexstr)
    print(name, "len", len(data))
    i = 0
    while i + 8 <= len(data):
        if data[i:i+2] == b"\x00\x00" and data[i+2:i+4] == b"\xDA\xC3":
            inst = int.from_bytes(data[i+4:i+8], "big")
            print(f"  offset {i}: Mission:{inst:08X}")
            i += 8
            continue
        # also DAC3 without leading 0000 if packed differently
        if data[i:i+2] == b"\xDA\xC3":
            inst = int.from_bytes(data[i+2:i+6], "big")
            print(f"  offset {i}: DAC3:{inst:08X}")
            i += 6
            continue
        i += 1

# load full hex from packet sender file
import re
path = r"AORebirth\Server\ZoneEngine\Core\Thrak\Quests\ThrakGardenKeyPacketSender.cs"
text = open(path, encoding="utf-8").read()
for const in ("VeronicaQuestFullUpdateHex", "InsigniaQuestFullUpdateHex", "VeronicaUpdatedQuestFullUpdateHex"):
    m = re.search(const + r'\s*=\s*\n?\s*"([0-9A-Fa-f]+)"', text)
    if not m:
        print("missing", const)
        continue
    find_missions(const, m.group(1))
