// Compile: csc /r:ZoneEngine.exe /r:SmokeLounge... too heavy.
// Instead dump with correct playfield marker.
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;

class P
{
    static void Main()
    {
        string text = File.ReadAllText(@"AORebirth\Server\ZoneEngine\Core\Missions\MissionRollCaptureTemplate.cs");
        var ms = Regex.Matches(text, "\"([0-9A-Fa-f]{32,})\"");
        var sb = new System.Text.StringBuilder();
        foreach (Match m in ms) sb.Append(m.Groups[1].Value);
        string h = sb.ToString();
        byte[] data = new byte[h.Length / 2];
        for (int i = 0; i < data.Length; i++)
            data[i] = byte.Parse(h.Substring(i * 2, 2), NumberStyles.HexNumber);

        string needle = "00009C50";
        for (int i = 0; i < h.Length - 8; i += 2)
        {
            if (h.Substring(i, 8) != needle) continue;
            int off = i / 2;
            int inst = (data[off + 4] << 24) | (data[off + 5] << 16) | (data[off + 6] << 8) | data[off + 7];
            int u18 = (data[off + 8] << 24) | (data[off + 9] << 16) | (data[off + 10] << 8) | data[off + 11];
            int u19 = (data[off + 12] << 24) | (data[off + 13] << 16) | (data[off + 14] << 8) | data[off + 15];
            float x = BitConverter.ToSingle(new byte[] { data[off + 19], data[off + 18], data[off + 17], data[off + 16] }, 0);
            float y = BitConverter.ToSingle(new byte[] { data[off + 23], data[off + 22], data[off + 21], data[off + 20] }, 0);
            float z = BitConverter.ToSingle(new byte[] { data[off + 27], data[off + 26], data[off + 25], data[off + 24] }, 0);
            Console.WriteLine("off={0} pf={1} ent={2}/{3} xyz=({4:F2},{5:F2},{6:F2})", off, inst, u18, u19, x, y, z);
        }

        // icons
        int[] icons = { 0x2C41, 0x2C42, 0x2C47, 0x2C49, 0x2C4E };
        foreach (int icon in icons)
        {
            string hx = icon.ToString("X8");
            for (int i = 0; i < h.Length - 8; i += 2)
            {
                if (h.Substring(i, 8) == hx)
                    Console.WriteLine("icon {0} off {1}", icon, i / 2);
            }
        }
    }
}
