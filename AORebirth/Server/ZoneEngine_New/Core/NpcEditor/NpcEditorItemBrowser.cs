namespace ZoneEngine_New.Core.NpcEditor;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

using AORebirth.Enums;

using SmokeLounge.AOtomation.Messaging.GameData;

using ZoneEngine_New.Core.GameData;
using ZoneEngine_New.Core.Inventory;

internal sealed record NpcEditorItemRow(int Id, string Name, int Quality, string ItemClass,
    string? Hash, int[] TemplateIds, int MinQuality, int MaxQuality);
internal sealed record NpcEditorItemPage(NpcEditorItemRow[] Rows, bool HasMore, int NextOffset);
internal sealed record NpcEditorItemStat(int Id, string Name, int Value);
internal sealed record NpcEditorItemDetails(int Id, string Name, int Quality, string ItemClass,
    string? Description, NpcEditorItemStat[] Stats);

internal sealed class NpcEditorItemBrowser(IItemTemplateCatalog items, IGameData data)
{
    const string Nano = "Nano";
    const int MaxPageSize = 100;

    // Catalog records are immutable after load. The index keeps references to those records.
    readonly Lazy<ItemTemplate[]> _orderedItems = new(() => items.Templates
        .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToArray());

    public NpcEditorItemPage Search(string? query, string? category, int? minQl, int? maxQl,
        int offset, int limit, bool families, CancellationToken cancellationToken)
    {
        string term = query?.Trim() ?? string.Empty;
        if (term.Length > 128 || offset < 0 || offset > int.MaxValue - MaxPageSize || limit is < 1 or > MaxPageSize
            || minQl < 1 || maxQl < 1 || minQl > maxQl)
            throw new NpcEditorRuntimeException(400, "Enter a search up to 128 characters, a positive QL range, and a valid page size.");

        var filter = new ItemFilter(term, ParseCategory(category), minQl ?? 1, maxQl ?? int.MaxValue);
        IEnumerable<Match> matches = families ? FindFamilies(filter, cancellationToken) : FindItems(filter, cancellationToken);
        Match[] page = matches.Skip(offset).Take(limit + 1).ToArray();
        NpcEditorItemRow[] rows = page.Take(limit).Select(Describe).ToArray();
        return new(rows, page.Length > limit, offset + rows.Length);
    }

    public NpcEditorItemDetails Get(int id)
    {
        if (!items.TryGet(id, out ItemTemplate item))
            throw new NpcEditorRuntimeException(404, "No item with that AO template ID exists in this catalog.");

        return new(item.Id, item.Name, item.Quality, Category(item), item.Description,
            item.Stats.OrderBy(entry => (int)entry.Key)
                .Select(entry => new NpcEditorItemStat((int)entry.Key, entry.Key.ToString(), entry.Value)).ToArray());
    }

    IEnumerable<Match> FindItems(ItemFilter filter, CancellationToken cancellationToken)
    {
        if (filter.TemplateId is int id)
        {
            if (items.TryGet(id, out ItemTemplate item) && filter.Matches(item))
                yield return new(item, null, item.Quality, item.Quality);
            yield break;
        }

        foreach (ItemTemplate item in _orderedItems.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (filter.Matches(item))
                yield return new(item, null, item.Quality, item.Quality);
        }
    }

    IEnumerable<Match> FindFamilies(ItemFilter filter, CancellationToken cancellationToken)
    {
        // ItemFamilies is a snapshot of the current loaded hash catalog. A reload can replace it.
        foreach (HashInstance family in data.ItemFamilies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ItemTemplate? preview = null;
            int min = int.MaxValue;
            int max = 0;
            bool hashMatches = filter.Term.Length > 0 && family.Hash.Contains(filter.Term, StringComparison.OrdinalIgnoreCase);
            foreach (int id in family.TemplateIds)
            {
                if (!items.TryGet(id, out ItemTemplate item))
                {
                    // A family that references unavailable templates cannot be saved by the editor.
                    preview = null;
                    break;
                }

                min = Math.Min(min, item.Quality);
                max = Math.Max(max, item.Quality);
                if (preview == null && filter.Matches(item, hashMatches))
                {
                    preview = item;
                }
            }

            if (preview != null)
                yield return new(preview, family, min, max);
        }
    }

    static NpcEditorItemRow Describe(Match match)
        => new(match.Item.Id, match.Item.Name, match.Item.Quality, Category(match.Item), match.Family?.Hash,
            match.Family == null ? [match.Item.Id] : (int[])match.Family.TemplateIds.Clone(), match.MinQl, match.MaxQl);

    static string Category(ItemTemplate item)
        => item.DynelType == (int)IdentityType.NanoProgram ? Nano
            : ((ItemClass)item.Stats.GetValueOrDefault(CharacterStat.ItemClass)).ToString();

    static string? ParseCategory(string? category)
    {
        string value = category?.Trim() ?? string.Empty;
        if (value.Length == 0)
            return null;
        if (string.Equals(value, Nano, StringComparison.OrdinalIgnoreCase))
            return Nano;
        return Enum.GetNames<ItemClass>().FirstOrDefault(name => string.Equals(value, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new NpcEditorRuntimeException(400, "Unknown item category.");
    }

    readonly record struct Match(ItemTemplate Item, HashInstance? Family, int MinQl, int MaxQl);

    sealed record ItemFilter(string Term, string? CategoryName, int MinQuality, int MaxQuality)
    {
        public int? TemplateId { get; } = int.TryParse(Term, NumberStyles.None, CultureInfo.InvariantCulture, out int id) ? id : null;

        public bool Matches(ItemTemplate item, bool hashMatches = false)
            => item.Quality >= MinQuality && item.Quality <= MaxQuality
                && (CategoryName == null || Category(item) == CategoryName)
                && (hashMatches || Term.Length == 0 || (TemplateId is int id
                    ? item.Id == id : item.Name.Contains(Term, StringComparison.OrdinalIgnoreCase)));
    }
}
