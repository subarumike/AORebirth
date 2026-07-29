// Quick check: does ItemLoader have SL passage templates?
using System;
using AORebirth.Core.Items;

class Program {
  static void Main() {
    int n = ItemLoader.CacheAllItems("items.dat");
    Console.WriteLine("loaded=" + n);
    int[] ids = {244737,244730,244735,222955,223577,244738};
    foreach (int id in ids) {
      ItemTemplate t;
      bool ok = ItemLoader.ItemList.TryGetValue(id, out t);
      Console.WriteLine(id + " " + (ok ? "YES" : "MISSING"));
    }
  }
}
