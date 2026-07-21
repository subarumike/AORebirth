namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Items;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Statels;
    using AORebirth.Core.Vector;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Missions;

    using Quaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;

    #endregion

    internal sealed class PlayfieldContentDataProvider
    {
        private readonly Func<Identity, bool> isPrivateCityPlayfieldCandidate;

        internal PlayfieldContentDataProvider(Func<Identity, bool> isPrivateCityPlayfieldCandidate)
        {
            if (isPrivateCityPlayfieldCandidate == null)
            {
                throw new ArgumentNullException("isPrivateCityPlayfieldCandidate");
            }

            this.isPrivateCityPlayfieldCandidate = isPrivateCityPlayfieldCandidate;
        }

        internal List<StatelData> ResolveStatels(Identity playfieldIdentity)
        {
            PlayfieldData playfieldData;
            if (PlayfieldLoader.PFData.TryGetValue(playfieldIdentity.Instance, out playfieldData))
            {
                return playfieldData.Statels;
            }

            if (this.isPrivateCityPlayfieldCandidate(playfieldIdentity))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Dynamic private city instance created without PFData statels instance={0} evidence=live_capture_20260622-101935",
                        playfieldIdentity));
                return new List<StatelData>();
            }

            // RK mission interiors (e.g. 1413198 / 0x15904E) are dynamic high-band ids with no PFData —
            // same empty-statel pattern as private cities. Capture 20260718-062936.
            if (MissionInstanceService.IsMissionInstancePlayfield(playfieldIdentity.Instance))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Dynamic mission instance created without PFData statels instance={0} evidence=live_capture_20260718-062936",
                        playfieldIdentity.Instance));
                return new List<StatelData>();
            }

            // Capture-backed ICC Holodeck (7001) — may be absent from older playfields.dat builds.
            if (playfieldIdentity.Instance == 7001)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    "HoloDeck PF 7001 created without PFData statels evidence=20260719-155043");
                return new List<StatelData>();
            }

            return PlayfieldLoader.PFData[playfieldIdentity.Instance].Statels;
        }

        internal bool TryResolveVendorStatels(
            Identity playfieldIdentity,
            IEnumerable<StatelData> statels,
            out StatelData[] vendorStatels)
        {
            if (!PlayfieldLoader.PFData.ContainsKey(playfieldIdentity.Instance))
            {
                vendorStatels = null;
                return false;
            }

            vendorStatels = this.ResolveVendorStatels(statels);
            return true;
        }

        internal StatelData[] ResolveCollisionStatels(IEnumerable<StatelData> statels)
        {
            if (statels == null)
            {
                return new StatelData[0];
            }

            return statels.Where(HandlesCollisionEvent).ToArray();
        }

        internal IEnumerable<PlayfieldStaticDynelDefinition> ResolveStaticDynels(Identity playfieldIdentity)
        {
            List<PlayfieldStaticDynelDefinition> fromDb = new List<PlayfieldStaticDynelDefinition>();
            IEnumerable<DBStaticDynel> dynels =
                StaticDynelDao.Instance.GetWhere(new { Playfield = playfieldIdentity.Instance });
            int dbCount = 0;
            int skippedNoTemplateStat = 0;
            int skippedMissingItem = 0;
            int yielded = 0;
            foreach (DBStaticDynel staticDynel in dynels)
            {
                dbCount++;
                List<GameTuple<CharacterStat, uint>> stats =
                    MessagePackZip.DeserializeData<GameTuple<CharacterStat, uint>>(staticDynel.stats.ToArray());

                if (!stats.Any(x => x.Value1 == (CharacterStat)StatIds.acgitemtemplateid))
                {
                    skippedNoTemplateStat++;
                    continue;
                }

                int templateId =
                    (int)stats.First(x => x.Value1 == (CharacterStat)StatIds.acgitemtemplateid).Value2;
                ItemTemplate template;
                if (!ItemLoader.ItemList.TryGetValue(templateId, out template))
                {
                    skippedMissingItem++;
                    continue;
                }

                yielded++;
                PlayfieldStaticDynelDefinition definition = new PlayfieldStaticDynelDefinition(
                    new Identity { Type = (IdentityType)staticDynel.Type, Instance = staticDynel.Instance },
                    template,
                    stats,
                    new Coordinate(staticDynel.X, staticDynel.Y, staticDynel.Z),
                    new Quaternion
                    {
                        X = staticDynel.HeadingX,
                        Y = staticDynel.HeadingY,
                        Z = staticDynel.HeadingZ,
                        W = staticDynel.HeadingW
                    });
                fromDb.Add(definition);
                yield return definition;
            }

            foreach (PlayfieldStaticDynelDefinition captureProp in
                AreteLandingQuestPropDefinitions.ResolveMissingProps(playfieldIdentity, fromDb))
            {
                yielded++;
                yield return captureProp;
            }

            LogUtil.Debug(
                DebugInfoDetail.Database,
                "ResolveStaticDynels pf=" + playfieldIdentity.Instance
                + " db=" + dbCount
                + " yielded=" + yielded
                + " skipNoTplStat=" + skippedNoTemplateStat
                + " skipMissingItem=" + skippedMissingItem);
        }

        private StatelData[] ResolveVendorStatels(IEnumerable<StatelData> statels)
        {
            if (statels == null)
            {
                return new StatelData[0];
            }

            return statels.Where(x => x.Identity.Type == IdentityType.VendingMachine).ToArray();
        }

        private static bool HandlesCollisionEvent(StatelData statel)
        {
            return statel != null
                   && statel.Events.Any(
                       x =>
                           x.EventType == EventType.OnCollide
                           || x.EventType == EventType.OnEnter
                           || x.EventType == EventType.OnTargetInVicinity);
        }
    }

    internal sealed class PlayfieldStaticDynelDefinition
    {
        internal PlayfieldStaticDynelDefinition(
            Identity identity,
            ItemTemplate template,
            List<GameTuple<CharacterStat, uint>> stats,
            Coordinate coordinate,
            Quaternion heading)
        {
            this.Identity = identity;
            this.Template = template;
            this.Stats = stats;
            this.Coordinate = coordinate;
            this.Heading = heading;
        }

        internal Identity Identity { get; private set; }

        internal ItemTemplate Template { get; private set; }

        internal List<GameTuple<CharacterStat, uint>> Stats { get; private set; }

        internal Coordinate Coordinate { get; private set; }

        internal Quaternion Heading { get; private set; }
    }
}
