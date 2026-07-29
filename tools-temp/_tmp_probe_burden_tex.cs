using System;
using System.Linq;
using AORebirth.Core.Items;
using AORebirth.Enums;
class Program {
  static void Main(string[] args) {
    ItemLoader.CacheAllItems(args[0]);
    int[] want = {245106,245105,302715,301724,303244,303241,303243,303240,303245};
    foreach (int w in want) {
      Console.WriteLine("==== "+w);
      int c=0;
      foreach (var kv in ItemLoader.ItemList) {
        foreach (var ev in kv.Value.Events)
          foreach (var f in ev.Functions) {
            bool hit=false;
            foreach (var a in f.Arguments.Values) {
              try { if (Convert.ToInt32(a.ToObject())==w) hit=true; } catch {}
            }
            if (!hit) continue;
            if (f.FunctionType==(int)FunctionType.BackMesh || f.FunctionType==(int)FunctionType.Texture || f.FunctionType==(int)FunctionType.HeadMesh || f.FunctionType==(int)FunctionType.Shouldermesh || f.FunctionType==(int)FunctionType.ChangeBodyMesh || f.FunctionType==(int)FunctionType.StrTexture) {
              int icon=0; kv.Value.Stats.TryGetValue(79,out icon);
              Console.WriteLine("  item="+kv.Key+" icon="+icon+" "+(FunctionType)f.FunctionType+" "+string.Join(",",f.Arguments.Values.Select(v=>v.ToString())));
              c++; if(c>=8) break;
            }
          }
        if(c>=8) break;
      }
      if(c==0) Console.WriteLine("  (no function hits)");
      // also icon match
      foreach (var kv in ItemLoader.ItemList) {
        int icon=0; if(kv.Value.Stats.TryGetValue(79,out icon) && icon==w) { Console.WriteLine("  icon-of item="+kv.Key); break; }
      }
    }
  }
}
