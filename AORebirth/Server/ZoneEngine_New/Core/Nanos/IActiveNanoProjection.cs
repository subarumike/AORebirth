namespace ZoneEngine_New.Core.Nanos
{
    using System.Collections.Generic;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using ZoneEngine_New.Core.Entities;

    /// <summary>
    /// Pure, fixed-key base-stat projection from the prospective durable active inventory.
    /// The nano transaction persists these values with its active rows, before projecting memory.
    /// SendProjection owns any capture-specific stat wire fields; no DAO calls in a specialization.
    /// </summary>
    public interface IActiveNanoProjection
    {
        IReadOnlyDictionary<CharacterStat, int> Project(IReadOnlyList<ActiveNanoRecord> active);
        void SendProjection(Player player);
    }
}
