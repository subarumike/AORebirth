using System;
using System.Linq;
using AORebirth.Core.Items;
using AORebirth.Enums;
class Program {
  static void Main(string[] args) {
    ItemLoader.CacheAllItems(args[0]);
    foreach (int id in new int[]{302730,223751,223752,223753}) {
      ItemTemplate t; if(!ItemLoader.ItemList.TryGetValue(id,out t)){Console.WriteLine(id+" MISSING");continue;}
      int icon=0; t.Stats.TryGetValue(79,out icon);
      Console.WriteLine("==== "+id+" icon="+icon);
      foreach (var ev in t.Events)
        foreach (var f in ev.Functions)
          if (f.FunctionType==(int)FunctionType.BackMesh || f.FunctionType==(int)FunctionType.Texture)
            Console.WriteLine("  "+(FunctionType)f.FunctionType+" "+string.Join(",", f.Arguments.Values.Select(v=>v.ToString())));
    }
    // items with Texture arg matching capture body tex 302714 / 303100 / 265541
    int[] want = {302714,303100,265541,245958,9612,302739,245069,268500};
    foreach (int w in want) {
      int c=0;
      foreach (var kv in ItemLoader.ItemList) {
        foreach (var ev in kv.Value.Events)
          foreach (var f in ev.Functions) {
            bool hit=false;
            foreach (var a in f.Arguments.Values) {
              try { if (Convert.ToInt32(a.ToObject())==w) hit=true; } catch {}
            }
            if (!hit) continue;
            if (f.FunctionType==(int)FunctionType.BackMesh || f.FunctionType==(int)FunctionType.Texture || f.FunctionType==(int)FunctionType.StrTexture) {
              Console.WriteLine("id="+kv.Key+" "+(FunctionType)f.FunctionType+" "+string.Join(",",f.Arguments.Values.Select(v=>v.ToString())));
              c++; if(c>8) break;
            }
          }
        if(c>8) break;
      }
      Console.WriteLine("searched "+w+" shown up to 8");
    }
  }
}
