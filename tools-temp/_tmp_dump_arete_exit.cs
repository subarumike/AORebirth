using System;
using System.IO;
using System.Linq;
using AORebirth.Core.Playfields;
using AORebirth.Core.Statels;
using Utility;

class P
{
    static void Main()
    {
        Directory.SetCurrentDirectory(@"C:\Users\nermi\source\repos\AORebirth\AORebirth\Built\Debug");
        var list = MessagePackZip.UncompressData<PlayfieldData>("playfields.dat");
        PlayfieldData pf = list.First(x => x.PlayfieldId == 6553);
        Console.WriteLine("statels=" + pf.Statels.Count);
        foreach (StatelData sd in pf.Statels.OrderBy(s => Math.Abs(s.X - 3365.3f) + Math.Abs(s.Z - 838.0f)))
        {
            double d = Math.Abs(sd.X - 3365.3f) + Math.Abs(sd.Z - 838.0f);
            if (d > 50)
            {
                continue;
            }

            Console.WriteLine(
                "id=" + sd.Identity
                + " tpl=" + sd.TemplateId
                + " pos=(" + sd.X + "," + sd.Y + "," + sd.Z + ")"
                + " heading=(" + sd.HeadingX + "," + sd.HeadingY + "," + sd.HeadingZ + "," + sd.HeadingW + ")"
                + " events=" + sd.Events.Count
                + " d=" + d.ToString("0.0"));
        }

        Console.WriteLine("--- template 297303 ---");
        int n = 0;
        foreach (StatelData sd in pf.Statels.Where(s => s.TemplateId == 297303))
        {
            n++;
            Console.WriteLine("id=" + sd.Identity + " pos=(" + sd.X + "," + sd.Y + "," + sd.Z + ")"
                + " heading=(" + sd.HeadingX + "," + sd.HeadingY + "," + sd.HeadingZ + "," + sd.HeadingW + ")");
        }
        Console.WriteLine("count297303=" + n);

        Console.WriteLine("--- instance 574187C3 ---");
        foreach (StatelData sd in pf.Statels.Where(s => s.Identity.Instance == unchecked((int)0x574187C3)))
        {
            Console.WriteLine("FOUND id=" + sd.Identity + " tpl=" + sd.TemplateId);
        }
    }
}
