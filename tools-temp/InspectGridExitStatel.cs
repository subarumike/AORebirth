using System;
using System.Linq;

using AORebirth.Core.Playfields;

using Utility;

internal static class InspectGridExitStatel
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: InspectGridExitStatel <playfields.dat> <hex instance>");
            return 1;
        }

        int instance = unchecked((int)Convert.ToUInt32(args[1], 16));
        PlayfieldData grid = MessagePackZip.UncompressData<PlayfieldData>(args[0])
            .First(pf => pf.PlayfieldId == 152);
        var statel = grid.Statels.First(x => x.Identity.Instance == instance);

        Console.WriteLine(
            "{0}:{1:X8} template={2} pos=({3:F3},{4:F3},{5:F3}) heading=({6:F6},{7:F6},{8:F6},{9:F6}) events={10}",
            statel.Identity.Type,
            unchecked((uint)statel.Identity.Instance),
            statel.TemplateId,
            statel.X,
            statel.Y,
            statel.Z,
            statel.HeadingX,
            statel.HeadingY,
            statel.HeadingZ,
            statel.HeadingW,
            statel.Events.Count);

        foreach (var eventData in statel.Events)
        {
            Console.WriteLine("event={0} functions={1}", eventData.EventType, eventData.Functions.Count);
            foreach (var function in eventData.Functions)
            {
                Console.Write("  function={0} args=[", function.FunctionType);
                for (int i = 0; i < function.Arguments.Values.Count; i++)
                {
                    if (i > 0)
                    {
                        Console.Write(",");
                    }

                    Console.Write(function.Arguments.Values[i]);
                }

                Console.WriteLine("] requirements={0}", function.Requirements.Count);
            }
        }

        return 0;
    }
}
