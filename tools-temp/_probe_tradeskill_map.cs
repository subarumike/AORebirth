using System;
using System.Linq;
using AORebirth.Database.Dao;
using Utility.Config;

class Probe
{
    static void Main()
    {
        var cfg = ConfigReadWrite.Instance.CurrentConfig;
        Console.WriteLine("mysql=" + cfg.MysqlConnection);
        var all = TradeSkillDao.Instance.GetAll().ToList();
        Console.WriteLine("total=" + all.Count);
        foreach (var r in all.Take(3))
        {
            Console.WriteLine(
                "ID1=" + r.ID1 + " ID2=" + r.ID2 + " ResultIDS=" + r.ResultIDS
                + " IsImplant=" + r.IsImplant + " Ql=" + r.QLRangePercent);
        }

        var implant = all.FirstOrDefault(x => x.ID1 == 101310 && x.ID2 == 101214);
        Console.WriteLine(
            implant == null
                ? "MISSING 101310/101214"
                : ("FOUND Result=" + implant.ResultIDS + " SkillPct=" + implant.SkillPercent));

        int zeros = all.Count(x => x.ID1 == 0 && x.ID2 == 0);
        int implants = all.Count(x => x.IsImplant > 0);
        Console.WriteLine("zeros=" + zeros + " implants=" + implants);
    }
}
