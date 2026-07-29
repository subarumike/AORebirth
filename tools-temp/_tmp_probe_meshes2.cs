using System;
using System.Linq;
using AORebirth.Core.Items;
using AORebirth.Enums;
class Program {
  static void Main(string[] args) {
    ItemLoader.CacheAllItems(args[0]);
    int[] want = {302739,291831,40630,226473,245098,245102,245103,245106};
    foreach (int w in want) {
      Console.WriteLine("==== search "+w);
      int c=0;
      foreach (var kv in ItemLoader.ItemList) {
        foreach (var ev in kv.Value.Events)
          foreach (var f in ev.Functions) {
            bool hit=false;
            foreach (var a in f.Arguments.Values) {
              try { if (Convert.ToInt32(a.ToObject())==w) hit=true; } catch {}
            }
            if (!hit) continue;
            if (f.FunctionType==(int)FunctionType.BackMesh || f.FunctionType==(int)FunctionType.Texture || f.FunctionType==(int)FunctionType.HeadMesh || f.FunctionType==(int)FunctionType.Shouldermesh || f.FunctionType==(int)FunctionType.ChangeBodyMesh) {
              Console.WriteLine("  item="+kv.Key+" "+(FunctionType)f.FunctionType+" "+string.Join(",",f.Arguments.Values.Select(v=>v.ToString())));
              c++; if(c>=6) break;
            }
          }
        if(c>=6) break;
      }
      if(c==0) Console.WriteLine("  (no hits)");
    }
  }
}
