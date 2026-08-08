// Quick check: does JavaScriptSerializer preserve DayReward.amount?
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

class Program
{
    class DayReward
    {
        public int ItemId { get; set; }
        public int itemId { get { return ItemId; } set { ItemId = value; } }
        public int Amount { get; set; }
        public int amount { get { return Amount; } set { Amount = value; } }
        public string QualityMode { get; set; }
        public string qualityMode { get { return QualityMode; } set { QualityMode = value; } }
    }
    class RewardsConfig
    {
        public Dictionary<string, DayReward> Days { get; set; }
        public Dictionary<string, DayReward> days { get { return Days; } set { Days = value; } }
    }

    static void Main()
    {
        string path = @"C:\xampp\htdocs\uwg.daily.icc-rk\rewards.json";
        var s = new JavaScriptSerializer();
        var cfg = s.Deserialize<RewardsConfig>(File.ReadAllText(path));
        foreach (string d in new[] { "3", "4", "17", "26" })
        {
            DayReward r = cfg.Days[d];
            Console.WriteLine("day " + d + " itemId=" + r.ItemId + " Amount=" + r.Amount + " mode=" + r.QualityMode);
        }
    }
}
