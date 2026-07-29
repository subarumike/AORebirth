# -*- coding: utf-8 -*-
import csv
import collections
import os

path = r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260718-210135\shop-updates.csv"
out = r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Playfields\CapturedThrakGardenVendorContentProvider.cs"

vendors = [
    ("12F446F2", "FuriousFists", "Craig-Or of the Furious Fists", False, 0x79758F3F, 0x12F446F2),
    ("12F446F1", "Preservation", "Craig-Or of Preservation", False, 0x79758F3E, 0x12F446F1),
    ("12F446EE", "FlamingBarrels", "Craig-Or of Flaming Barrels", False, 0x79758F3B, 0x12F446EE),
    ("12F446EF", "GearAndAmmo", "Craig-Or of Gear & Ammo", False, 0x79758F3C, 0x12F446EF),
    ("12F446F0", "Protection", "Craig-Or of Protection", False, 0x79758F3D, 0x12F446F0),
    ("12F446F3", "SonLen", "Son-Len, Official of Power", True, 0x79758F40, 0x12F446F3),
]

by = collections.defaultdict(list)
with open(path, newline="", encoding="utf-8") as f:
    for row in csv.DictReader(f):
        hexid = row["TerminalIdentity"].split(":")[1].rstrip(")")
        by[hexid].append(
            (int(row["Slot"]), int(row["LowId"]), int(row["HighId"]), int(row["Quality"]))
        )

lines = []
a = lines.append
a("// Capture-backed Thrak Omni garden vendors — PF 4677.")
a("// AOSharpLiveCapture 20260718-210135 (shop-updates.csv + Use->ShopUpdate pairing).")
a("")
a("namespace ZoneEngine.Core.Playfields")
a("{")
a("    using System;")
a("    using System.Collections.ObjectModel;")
a("")
a("    internal static class CapturedThrakGardenVendorContentProvider")
a("    {")
a("        internal const int ThrakOmniGardenPlayfieldId = 4677;")
a("")
a("        // Known working vendor template with OnTrade/Shophash (same as Subway container merchant).")
a("        internal const int VendorTemplateId = 99634;")
a("")
a('        private const string Evidence = "AOSharpLiveCapture/20260718-210135";')
a("")
a("        private static readonly ReadOnlyCollection<CapturedThrakGardenVendorDefinition> CapturedDefinitions =")
a("            Array.AsReadOnly(")
a("                new[]")
a("                {")
for hexid, method, name, gated, npc, vm in vendors:
    a("                    Create(")
    a("                        unchecked((int)0x%08X)," % npc)
    a("                        unchecked((int)0x%08X)," % vm)
    a('                        "%s",' % name)
    a("                        %s," % ("true" if gated else "false"))
    a("                        %sStock())," % method)
a("                });")
a("")
a("        internal static ReadOnlyCollection<CapturedThrakGardenVendorDefinition> Definitions")
a("        {")
a("            get { return CapturedDefinitions; }")
a("        }")
a("")
a("        private static CapturedThrakGardenVendorDefinition Create(")
a("            int sourceNpcInstance,")
a("            int sourceVendorInstance,")
a("            string displayName,")
a("            bool requiresCompletedGardenKeyQuest,")
a("            CapturedThrakGardenVendorStockDefinition[] stock)")
a("        {")
a("            return new CapturedThrakGardenVendorDefinition(")
a("                sourceNpcInstance,")
a("                sourceVendorInstance,")
a("                displayName,")
a("                VendorTemplateId,")
a("                requiresCompletedGardenKeyQuest,")
a("                stock,")
a("                Evidence);")
a("        }")
a("")
for hexid, method, name, gated, npc, vm in vendors:
    rows = sorted(by[hexid], key=lambda x: x[0])
    a("        private static CapturedThrakGardenVendorStockDefinition[] %sStock()" % method)
    a("        {")
    a("            return new[]")
    a("            {")
    for i, (slot, low, high, ql) in enumerate(rows):
        comma = "," if i < len(rows) - 1 else ""
        a(
            "                new CapturedThrakGardenVendorStockDefinition(%d, %d, %d, %d)%s"
            % (slot, low, high, ql, comma)
        )
    a("            };")
    a("        }")
    a("")
a("    }")
a("")
a("    internal sealed class CapturedThrakGardenVendorDefinition")
a("    {")
a("        internal CapturedThrakGardenVendorDefinition(")
a("            int sourceNpcInstance,")
a("            int sourceVendorInstance,")
a("            string displayName,")
a("            int vendorTemplateId,")
a("            bool requiresCompletedGardenKeyQuest,")
a("            CapturedThrakGardenVendorStockDefinition[] stock,")
a("            string evidence)")
a("        {")
a("            this.SourceNpcInstance = sourceNpcInstance;")
a("            this.SourceVendorInstance = sourceVendorInstance;")
a("            this.DisplayName = displayName;")
a("            this.VendorTemplateId = vendorTemplateId;")
a("            this.RequiresCompletedGardenKeyQuest = requiresCompletedGardenKeyQuest;")
a("            this.Stock = Array.AsReadOnly(stock ?? new CapturedThrakGardenVendorStockDefinition[0]);")
a("            this.Evidence = evidence ?? string.Empty;")
a("        }")
a("")
a("        internal int SourceNpcInstance { get; private set; }")
a("        internal int SourceVendorInstance { get; private set; }")
a("        internal string DisplayName { get; private set; }")
a("        internal int VendorTemplateId { get; private set; }")
a("        internal bool RequiresCompletedGardenKeyQuest { get; private set; }")
a("        internal ReadOnlyCollection<CapturedThrakGardenVendorStockDefinition> Stock { get; private set; }")
a("        internal string Evidence { get; private set; }")
a("        internal bool HasCapturedStock { get { return this.Stock != null && this.Stock.Count > 0; } }")
a("    }")
a("")
a("    internal sealed class CapturedThrakGardenVendorStockDefinition")
a("    {")
a("        internal CapturedThrakGardenVendorStockDefinition(int slot, int lowId, int highId, int quality)")
a("        {")
a("            this.Slot = slot;")
a("            this.LowId = lowId;")
a("            this.HighId = highId;")
a("            this.Quality = quality;")
a("        }")
a("")
a("        internal int Slot { get; private set; }")
a("        internal int LowId { get; private set; }")
a("        internal int HighId { get; private set; }")
a("        internal int Quality { get; private set; }")
a("    }")
a("}")
a("")

text = "\n".join(lines)
with open(out, "w", encoding="utf-8", newline="\n") as w:
    w.write(text)
print("wrote", out, "bytes", os.path.getsize(out))
