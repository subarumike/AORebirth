using System;
using AORebirth.Core.Items;
using AORebirth.Database.Dao;

class Probe
{
    static void Main()
    {
        ItemLoader.CacheAllItems(@"C:\Users\nermi\source\repos\AORebirth\AORebirth\Datafiles\items.dat");
        foreach (int id in new[] { 99566, 99634, 300439, 300440 })
        {
            if (!ItemLoader.ItemList.ContainsKey(id))
            {
                Console.WriteLine("item " + id + " MISSING from items.dat");
                continue;
            }

            var t = ItemLoader.ItemList[id];
            string name = "?";
            try
            {
                var n = ItemNamesDao.Instance.Get(id);
                if (n != null) name = n.Name;
            }
            catch (Exception ex)
            {
                name = "nameErr:" + ex.Message;
            }

            Console.WriteLine(
                "item " + id + " name=" + name
                + " events=" + (t.Events == null ? 0 : t.Events.Count)
                + " stats=" + (t.Stats == null ? 0 : t.Stats.Count));
            if (t.Events != null)
            {
                foreach (var ev in t.Events)
                {
                    Console.WriteLine("  EventType=" + ev.EventType + " funcs=" + (ev.Functions == null ? 0 : ev.Functions.Count));
                    if (ev.Functions == null) continue;
                    foreach (var f in ev.Functions)
                    {
                        Console.WriteLine("    FT=" + f.FunctionType);
                    }
                }
            }
        }
    }
}
