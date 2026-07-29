using System;
using System.IO;
using System.Linq;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
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
        StatelData sd = pf.Statels.First(s => s.TemplateId == 297303);
        Console.WriteLine("id=" + sd.Identity + " events=" + sd.Events.Count);
        foreach (Event ev in sd.Events)
        {
            Console.WriteLine(" eventType=" + ev.EventType + " funcs=" + ev.Functions.Count);
            foreach (Function f in ev.Functions)
            {
                Console.WriteLine("  fn=" + f.FunctionType + " args=" + f.Arguments.Values.Count);
                for (int i = 0; i < f.Arguments.Values.Count; i++)
                {
                    Console.WriteLine("   [" + i + "]=" + f.Arguments.Values[i]);
                }
            }
        }
    }
}
