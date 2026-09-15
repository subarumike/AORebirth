using System.Linq;
using AORebirth.Enums;
using ZoneEngine_New.Core.Data;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;

namespace ZoneEngine_New.Tests;

internal static class HitProjectionTestExtensions
{
    // These tests verify the imported Hit decoder and HealthDamage projection only.
    // Generic player item use remains fail-closed; durable effects use the DAO owner.
    internal static bool ExecuteHitProjection(this ItemTemplate template, Character target,
        IInventoryRepository inventory, IItemBuilder items, Character? source = null)
        => ItemUseFunctions.TryExecute(template.Id, target, source, template.SpellList[EventType.OnUse].Single(), inventory, items);
}
