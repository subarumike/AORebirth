namespace ZoneEngine_New.Core.GameData;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class GameDataStore
{
    internal string[] EditorFamiliesFor(string hash) => _npcTemplates.EditorFamiliesFor(hash);
}

public sealed partial class NpcTemplateCatalog
{
    readonly Lazy<Dictionary<string, List<string>>> _familyParents;

    static Dictionary<string, List<string>> IndexFamilyParents(Dictionary<string, string[]> families)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach ((string parent, string[] children) in families)
        {
            foreach (string child in children)
            {
                if (string.IsNullOrEmpty(child))
                    continue;
                if (!index.TryGetValue(child, out List<string>? parents))
                    index.Add(child, parents = []);
                parents.Add(parent);
            }
        }
        return index;
    }

    internal string[] EditorFamiliesFor(string hash)
    {
        if (!_leaves.ContainsKey(hash) || !_familyParents.Value.TryGetValue(hash, out List<string>? parents))
            return [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(parents);
        while (pending.TryPop(out string? parent))
        {
            if (!seen.Add(parent) || !_familyParents.Value.TryGetValue(parent, out List<string>? ancestors))
                continue;
            foreach (string ancestor in ancestors)
                pending.Push(ancestor);
        }
        return seen.Order(StringComparer.Ordinal).ToArray();
    }
}
