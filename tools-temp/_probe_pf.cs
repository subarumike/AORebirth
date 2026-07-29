using System;
using System.IO;
using ZoneEngine.Core.Playfields;

class P
{
    static void Main()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "playfields.dat");
        try
        {
            PlayfieldLoader.CacheAllPlayfieldData(path);
        }
        catch (Exception ex)
        {
            Console.WriteLine("cache_exception=" + ex.GetType().Name + ":" + ex.Message);
        }

        Console.WriteLine("count=" + PlayfieldLoader.PFData.Count);
        Console.WriteLine("has7001=" + PlayfieldLoader.PFData.ContainsKey(7001));
        Console.WriteLine("has655=" + PlayfieldLoader.PFData.ContainsKey(655));
        Console.WriteLine("has6131=" + PlayfieldLoader.PFData.ContainsKey(6131));
        if (PlayfieldLoader.PFData.ContainsKey(7001))
        {
            var pf = PlayfieldLoader.PFData[7001];
            Console.WriteLine("name=" + pf.Name + " statels=" + pf.Statels.Count);
        }
        else
        {
            // print nearby ids
            int[] sample = new int[0];
            System.Collections.Generic.List<int> near = new System.Collections.Generic.List<int>();
            foreach (int id in PlayfieldLoader.PFData.Keys)
            {
                if (id >= 6900 && id <= 7100)
                {
                    near.Add(id);
                }
            }
            near.Sort();
            Console.WriteLine("near7000=" + string.Join(",", near.ToArray()));
        }
    }
}
