# Generate C# snippets for shape 1441792 from capture 20260728-093557
payload_hex = (
    "0000C79F00D79A93000000020003001E001E00400000014464646400000012002B001D0402"
    "0033001A0203000E001901030012001C0503000100180403000600190203000500180103"
    "0006001B01010025001C0700001700180702000500180300000D00190702000D00190300"
    "000A001A0702000A001A03000025001B06030025001B0503000500170403FFFFFFFFFFFFFFFF"
)
data = bytes.fromhex(payload_hex)
lines = []
lines.append("                case 1441792:")
lines.append("                    // Fog gold ACG D79A93 — capture 20260728-093557.")
lines.append("                    return new byte[]")
lines.append("                    {")
row = []
for i, b in enumerate(data):
    row.append("0x%02X" % b)
    if len(row) == 8:
        lines.append("                       " + ", ".join(row) + ",")
        row = []
if row:
    lines.append("                       " + ", ".join(row) + ",")
lines.append("                    };")
open(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_payload_1441792.csfrag", "w", encoding="utf-8").write(
    "\n".join(lines) + "\n"
)
print("payload bytes", len(data))
print("doors frag exists", open(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_doors_1441792.csfrag", encoding="utf-8").readline().strip())
print("chests frag exists", open(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_chests_1441792.csfrag", encoding="utf-8").readline().strip())
