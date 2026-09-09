namespace ZoneEngine_New.Core.Mobs;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AORebirth.Core.Textures;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using ZoneEngine.Core.Playfields;
using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Inventory;
using ZoneEngine_New.Core.Network;
using ZoneEngine_New.Core.Playfield;
using ZoneEngine_New.Core.Playfield.Locality;
using Source = ZoneEngine.Core.Playfields.CapturedBucketheadTechnodealerContentProvider;

/// <summary>
/// Exact BKTH summon, owned and expired by the source playfield tick. The nano
/// transaction is the sole cost authority; this service never writes player data.
/// </summary>
internal sealed class BucketheadSummonService(Playfield playfield, DynelRegistry registry,
    PlayfieldLocality locality, AcceptedNpcActivationService accepted, IItemBuilder items,
    IItemTemplateCatalog catalog, Func<long>? milliseconds = null)
{
    sealed record Summon(Player Owner, IZoneSession Session, BucketheadCharacter Npc, long ExpiresAt);
    readonly Dictionary<int, Summon> _summons = new();
    readonly Func<long> _milliseconds = milliseconds ?? (() => Environment.TickCount64);
    bool _stopped;
    internal int Count => _summons.Count;
    internal NpcCharacter? ForOwner(Player owner) => _summons.TryGetValue(owner.Identity.Instance, out var summon)
        && ReferenceEquals(summon.Owner, owner) ? summon.Npc : null;

    internal bool TryPrepare(Player owner, Func<bool> stillCurrent, out Action afterCommit)
    {
        afterCommit = null!;
        if (_stopped || !Current(owner, owner.Session) || !Finite(owner)
            || !catalog.TryGet(Source.VendorTemplateId, out var template)
            || Source.Stock.Count != 46
            || Source.Stock.Where((row, index) => row.Slot != index || row.Quality <= 0
                || !catalog.TryGet(row.LowId, out _) || !catalog.TryGet(row.HighId, out _)).Any()) return false;
        var session = owner.Session!;
        // Deferred allocation/registration runs once, only after a known successful
        // cost commit. Failed casts neither replace the old summon nor burn its lifetime.
        int published = 0;
        afterCommit = () =>
        {
            if (Interlocked.Exchange(ref published, 1) != 0 || _stopped
                || !stillCurrent() || !Current(owner, session) || !Finite(owner)) return;
            var npc = CreateNpc(owner, registry.AllocateNpcIdentity(), items);
            npc.Playfield = playfield;
            var shop = new VendingMachine(registry.AllocateVendingMachineIdentity(), template)
                { Playfield = playfield, Position = npc.Position, Rotation = npc.Rotation };
            shop.Stock.SetAcceptedSnapshot(Source.Stock.Select(row => new ZoneEngine_New.Core.Trade.ShopStockSlot(
                row.LowId, row.HighId, row.Quality)).ToArray());
            npc.AttachShop(shop);
            npc.Stats.Set(CharacterStat.Flags, Source.CharacterFlags); // Exact captured NPC flags, not a synthesized cart bit.
            if (!registry.TryRegister(npc)) throw new InvalidOperationException("Buckethead identity allocation collided.");
            try
            {
                accepted.Bind(npc, new AcceptedNpcBinding("summon:BKTH:" + owner.Identity.Instance + ":" + npc.Identity.Instance,
                    Source.Evidence + ";nano:300439;function:SpawnMonster2(BKTH,220,600)", string.Empty,
                    playfield.Identity.Instance, false, true));
                if (_summons.Remove(owner.Identity.Instance, out var old)) Remove(old);
                _summons.Add(owner.Identity.Instance, new(owner, session, npc, checked(_milliseconds() + 600_000)));
                locality.RegisterDynel(npc); // Existing visibility sends SCFU then the exact companion VMFU.
            }
            catch
            {
                if (_summons.TryGetValue(owner.Identity.Instance, out var added) && ReferenceEquals(added.Npc, npc))
                    _summons.Remove(owner.Identity.Instance);
                accepted.Detached(npc); locality.UnregisterDynel(npc); registry.UnregisterExact(npc);
                npc.Playfield = null;
                throw;
            }
        };
        return true;
    }

    internal void Tick()
    {
        foreach (var entry in _summons.ToArray())
        {
            var summon = entry.Value;
            if (_milliseconds() < summon.ExpiresAt && Current(summon.Owner, summon.Session)
                && !summon.Npc.IsDead && ReferenceEquals(summon.Npc.Playfield, playfield)
                && registry.TryGet(summon.Npc.Identity, out var current) && ReferenceEquals(current, summon.Npc)) continue;
            _summons.Remove(entry.Key); Remove(summon);
        }
    }

    internal void Shutdown()
    {
        _stopped = true;
        foreach (var summon in _summons.Values) Remove(summon);
        _summons.Clear();
    }

    void Remove(Summon summon)
    {
        // Exact-reference cleanup must never withdraw a replacement's shop/visibility.
        accepted.Detached(summon.Npc);
        locality.UnregisterDynel(summon.Npc); registry.UnregisterExact(summon.Npc);
        if (ReferenceEquals(summon.Npc.Playfield, playfield)) summon.Npc.Playfield = null;
    }

    bool Current(Player owner, IZoneSession? session) => !playfield.IsDisposed && !owner.IsDead
        && !owner.IsPersistenceQuarantined && session is { State: SessionState.InPlay }
        && ReferenceEquals(owner.Session, session) && ReferenceEquals(session.Player, owner)
        && ReferenceEquals(owner.Playfield, playfield)
        && registry.TryGet(owner.Identity, out var current) && ReferenceEquals(current, owner);

    static bool Finite(Player owner)
    {
        float x = owner.Rotation.xf, y = owner.Rotation.yf, z = owner.Rotation.zf, w = owner.Rotation.wf;
        double norm = (double)x * x + (double)y * y + (double)z * z + (double)w * w;
        // Check the actual float wire projection as well as the input doubles.
        // Do not normalize or invent a replacement heading for invalid input.
        return double.IsFinite(norm) && norm > 0
            && float.IsFinite((float)(owner.Position.x + 1f - 2f * y * y))
            && float.IsFinite((float)owner.Position.y)
            && float.IsFinite((float)(owner.Position.z + 2f * y * w));
    }

    static BucketheadCharacter CreateNpc(Player owner, Identity identity, IItemBuilder builder)
    {
        float y = owner.Rotation.yf, w = owner.Rotation.wf;
        var npc = new BucketheadCharacter(identity, builder)
        {
            Name = Source.DisplayName, SpawnSource = SpawnSource.AcceptedPlacement,
            Position = new(owner.Position.x + 1f - 2f * y * y, owner.Position.y, owner.Position.z + 2f * y * w),
            Rotation = new(owner.Rotation.xf, owner.Rotation.yf, owner.Rotation.zf, owner.Rotation.wf)
        };
        npc.Stats.Set(CharacterStat.Side, 0); npc.Stats.Set(CharacterStat.Fatness, 1);
        npc.Stats.Set(CharacterStat.Breed, (int)Breed.Monster); npc.Stats.Set(CharacterStat.Sex, (int)Gender.None); npc.Stats.Set(CharacterStat.Race, 1);
        npc.Stats.Set(CharacterStat.Flags, Source.CharacterFlags); npc.Stats.Set(CharacterStat.AccountFlags, 0);
        npc.Stats.Set(CharacterStat.Expansion, 0); npc.Stats.Set(CharacterStat.NPCFamily, 0);
        npc.Stats.Set(CharacterStat.MonsterData, Source.MonsterData); npc.Stats.Set(CharacterStat.Scale, Source.MonsterScale);
        npc.Stats.Set(CharacterStat.HeadMesh, 0); npc.Stats.Set(CharacterStat.VisualFlags, Source.VisualFlags);
        npc.Stats.Set(CharacterStat.CurrentMovementMode, 3); npc.Stats.Set(CharacterStat.PrevMovementMode, 3);
        npc.Stats.Set(CharacterStat.RunSpeed, Source.RunSpeed); npc.Stats.Set(CharacterStat.Level, Source.Level);
        npc.Stats.Set(CharacterStat.Health, Source.Health); npc.Stats.Set(CharacterStat.MaxHealth, Source.Health);
        for (int i = 0; i < 5; i++) npc.Textures.Add(new AOTextures(i, 0));
        npc.Motor.RefreshFromStats();
        return npc;
    }

    sealed class BucketheadCharacter(Identity identity, IItemBuilder builder) : NpcCharacter(identity, builder)
    {
        protected override bool UsesPassiveRegen => false;
        public override void Rebase() { }
        public override void RebaseWeapons() { }
        public override void StartFighting(Identity target, byte action) { }
        protected override void TickCombat(double deltaTime) { }
        public override IEnumerable<MessageBody> BuildSpawnCompanionMessages()
        {
            if (Shop is not { } shop || IsDead) yield break;
            // VendingMachineFullUpdateMessageHandler.BucketheadFiller, capture 20260723-114826.
            yield return new VendingMachineFullUpdateMessage
            {
                Identity = shop.Identity, Unknown = 0, Coordinates = null, Heading = null,
                NpcIdentity = Identity, TypeIdentifier = 0x0b, PlayfieldId = Playfield!.Identity.Instance,
                Unknown4 = 0xf424f, Unknown5 = 0, Unknown6 = 64,
                Stats = [Stat(CharacterStat.Flags, unchecked((uint)-2147338749)),
                    Stat(CharacterStat.StaticInstance, 99566), Stat(CharacterStat.ACGItemLevel, 1),
                    Stat(CharacterStat.ACGItemTemplateID, 99566), Stat(CharacterStat.ACGItemTemplateID2, 99566),
                    Stat(CharacterStat.MultipleCount, 1), Stat((CharacterStat)501, 2), Stat((CharacterStat)500, 0)],
                Unknown7 = string.Empty, Unknown8 = 2, Unknown9 = 50, Unknown10 = [], Unknown11 = 3
            };
        }
        static GameTuple<CharacterStat, uint> Stat(CharacterStat stat, uint value) => new() { Value1 = stat, Value2 = value };
    }
}
