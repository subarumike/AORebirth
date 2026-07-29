using System;
using AORebirth.Core.Items;

class Probe
{
    static void Main()
    {
        ItemLoader.CacheAllItems(@"C:\Users\nermi\source\repos\AORebirth\AORebirth\Datafiles\items.dat");
        int[] ids = { 144767, 144768, 144769, 144770, 150273, 150274, 144799, 144800, 144801, 144802 };
        foreach (int id in ids)
        {
            var t = ItemLoader.ItemList[id];
            Console.WriteLine(id + " QL=" + t.Quality);
        }

        PrintItem(130, 144767, 144770);
        PrintItem(1, 144767, 144770);
        PrintItem(130, 144770, 144768);
        PrintItem(1, 144770, 144768);
        PrintItem(255, 144770, 144768);
        PrintItem(130, 144770, 144767);
        PrintItem(130, 144767, 144769);
        PrintItem(130, 144769, 144768);
        PrintItem(130, 144800, 144799);
        PrintItem(130, 144801, 144802);
    }

    static void PrintItem(int ql, int low, int high)
    {
        try
        {
            var item = new Item(ql, low, high);
            Console.WriteLine("Item(" + ql + "," + low + "," + high + ") => QL=" + item.Quality);
        }
        catch (Exception e)
        {
            Console.WriteLine("Item(" + ql + "," + low + "," + high + ") FAIL " + e.Message);
        }
    }
}
