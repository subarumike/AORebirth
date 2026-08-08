using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

class Program
{
    class DayReward
    {
        public int ItemId { get; set; }
        public int Amount { get; set; }
        public string QualityMode { get; set; }
    }

    class RewardsConfig
    {
        public bool FreeTestMode { get; set; }
        public Dictionary<string, DayReward> Days { get; set; }
    }

    static void Main()
    {
        string path = @"C:\xampp\htdocs\uwg.daily.icc-rk\rewards.json";
        var s = new JavaScriptSerializer();
        RewardsConfig cfg = s.Deserialize<RewardsConfig>(File.ReadAllText(path));
        foreach (string d in new[] { "3", "4", "17", "26" })
        {
            DayReward r = cfg.Days[d];
            Console.WriteLine("day " + d + " itemId=" + r.ItemId + " Amount=" + r.Amount + " mode=" + r.QualityMode);
        }
        Console.WriteLine("freeTest=" + cfg.FreeTestMode + " days=" + cfg.Days.Count);
    }
}
