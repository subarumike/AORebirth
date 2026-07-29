using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;

namespace TmpCheck
{
    class Program
    {
        static void Main()
        {
            string hex = File.ReadAllText(@"C:\Users\nermi\source\repos\AORebirth\tools-temp\_tmp_stats.hex").Trim();
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber);
            }

            Console.WriteLine("raw len=" + raw.Length);
            TryUnpack("padded", raw);

            int end = raw.Length;
            while (end > 0 && raw[end - 1] == 0)
            {
                end--;
            }

            byte[] trimmed = new byte[end];
            Array.Copy(raw, trimmed, end);
            Console.WriteLine("trimmed len=" + trimmed.Length);
            TryUnpack("trimmed", trimmed);
        }

        static void TryUnpack(string label, byte[] data)
        {
            try
            {
                List<GameTuple<CharacterStat, uint>> stats =
                    MessagePackZip.DeserializeData<GameTuple<CharacterStat, uint>>(data);
                Console.WriteLine(label + " OK count=" + stats.Count);
                foreach (GameTuple<CharacterStat, uint> s in stats)
                {
                    Console.WriteLine("  " + s.Value1 + "(" + (int)s.Value1 + ")=" + s.Value2);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(label + " FAIL: " + ex.GetType().Name + " " + ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("  inner: " + ex.InnerException.Message);
                }
            }
        }
    }
}
