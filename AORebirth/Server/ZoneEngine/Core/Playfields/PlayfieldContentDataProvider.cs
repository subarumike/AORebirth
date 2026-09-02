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
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Navigation;

    using Quaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;

    #endregion

    internal sealed class PlayfieldContentDataProvider
    {
        private const int LuxuryApartmentPlayfieldBase = 0x0019E000;

        private const int LuxuryApartmentMaxSlots = 0x400;

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

            // Capture 20260806-202421: luxury apartment instance 0x19E000 (no PFData row).
            if (IsLuxuryApartmentPlayfield(playfieldIdentity.Instance))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Luxury apartment instance created without PFData statels instance={0} evidence=20260806-202421",
                        playfieldIdentity.Instance));
                return new List<StatelData>();
            }

            // Capture 20260824-125154: Nascence Dungeon 1 dyn ACG PF 0x1F900B — no PFData row.
            if (NascenceDungeon1Rules.IsDungeonPlayfield(playfieldIdentity.Instance))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Nascence Dungeon 1 instance created without PFData statels instance={0} evidence=20260823-171238",
                        playfieldIdentity.Instance));
                return new List<StatelData>();
            }

            if (NascenceDungeon2Rules.IsDungeonPlayfield(playfieldIdentity.Instance))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Nascence Dungeon 2 instance created without PFData statels instance={0} evidence=20260823-182854",
                        playfieldIdentity.Instance));
                return new List<StatelData>();
            }

            if (NascenceDungeon3Rules.IsDungeonPlayfield(playfieldIdentity.Instance))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Nascence Dungeon 3 instance created without PFData statels instance={0} evidence=20260830-140240",
                        playfieldIdentity.Instance));
                return new List<StatelData>();
            }

            if (NascenceDungeon4Rules.IsDungeonPlayfield(playfieldIdentity.Instance))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Nascence Dungeon 4 instance created without PFData statels instance={0} evidence=20260830-143801",
                        playfieldIdentity.Instance));
                return new List<StatelData>();
            }

            return PlayfieldLoader.PFData[playfieldIdentity.Instance].Statels;
        }

        private static bool IsLuxuryApartmentPlayfield(int playfieldInstance)
        {
            return playfieldInstance >= LuxuryApartmentPlayfieldBase
                   && playfieldInstance < LuxuryApartmentPlayfieldBase + LuxuryApartmentMaxSlots;
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

            return statels
                .Where(
                    statel => HandlesCollisionEvent(statel)
                              || TempleWorldInteractionRules.IsExteriorLinkStatel(statel))
                .ToArray();
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
    internal static class TempleWorldInteractionRules
    {
        internal const int TemplePlayfieldId = 1931;

        internal const int TempleGatewayPlayfieldId = 647;

        internal const int TempleGatewayDoorInstance = unchecked((int)0xC0080287);

        internal const int TempleExteriorDoorInstance = unchecked((int)0xC024078B);

        internal const int TempleExteriorGeometryDoorIndex = 4468;

        internal const float CapturedEntryX = 172.989990234375f;

        internal const float CapturedEntryY = 24.011247634887695f;

        internal const float CapturedEntryZ = 7.81494140625f;

        internal const float CapturedExitX = 1813.9990234375f;

        internal const float CapturedExitY = 26.806131362915039f;

        internal const float CapturedExitZ = 2715.84521484375f;

        internal static bool IsExteriorLinkStatel(StatelData statel)
        {
            return statel != null
                   && statel.PlayfieldId == TemplePlayfieldId
                   && statel.Identity.Type == IdentityType.Door
                   && statel.Identity.Instance == TempleExteriorDoorInstance;
        }

        internal static bool IsExteriorLinkStatel(int playfieldId, StatelData statel)
        {
            return playfieldId == TemplePlayfieldId
                   && IsExteriorLinkStatel(statel);
        }

        internal static bool IsGatewayEntryStatel(StatelData statel)
        {
            return statel != null
                   && statel.PlayfieldId == TempleGatewayPlayfieldId
                   && statel.Identity.Type == IdentityType.Door
                   && statel.Identity.Instance == TempleGatewayDoorInstance;
        }

        internal static bool IsBoundaryLinkStatel(StatelData statel)
        {
            return IsExteriorLinkStatel(statel) || IsGatewayEntryStatel(statel);
        }

        internal static bool TryResolveProxyEntry(
            int sourcePlayfieldId,
            Identity sourceStatel,
            int destinationIdentityType,
            int destinationPlayfieldId,
            int destinationDoorIndex,
            int sourceDoorArgument,
            out Coordinate destination)
        {
            destination = null;
            if (sourcePlayfieldId != TempleGatewayPlayfieldId
                || sourceStatel.Type != IdentityType.Door
                || sourceStatel.Instance != TempleGatewayDoorInstance
                || destinationIdentityType != 51102
                || destinationPlayfieldId != TemplePlayfieldId
                || destinationDoorIndex != 0
                || sourceDoorArgument != TempleGatewayDoorInstance
                || !HasExactOfficialInventory())
            {
                return false;
            }

            destination = new Coordinate(CapturedEntryX, CapturedEntryY, CapturedEntryZ);
            return true;
        }

        internal static bool IsTempleProxyArrival(ICharacter character)
        {
            return character != null
                   && character.Playfield != null
                   && character.Playfield.Identity.Instance == TemplePlayfieldId
                   && character.Stats[StatIds.externalplayfieldinstance].Value
                       == TempleGatewayPlayfieldId
                   && unchecked((int)character.Stats[StatIds.externaldoorinstance].BaseValue)
                       == TempleGatewayDoorInstance;
        }

        internal static bool IsInBoundaryTriggerRange(StatelData statel, ICharacter character)
        {
            if (!IsBoundaryLinkStatel(statel) || character == null)
            {
                return false;
            }

            float dx = statel.X - character.RawCoordinates.X;
            float dz = statel.Z - character.RawCoordinates.Z;
            float horizontalDistance = (float)Math.Sqrt((dx * dx) + (dz * dz));
            float verticalDistance = Math.Abs(statel.Y - character.RawCoordinates.Y);
            return horizontalDistance <= TempleDoorProximityRuntime.TriggerRadius
                   && verticalDistance <= 6.0f;
        }

        internal static bool TryResolveProxyExit(
            int sourcePlayfieldId,
            int externalPlayfieldId,
            uint externalDoorInstance,
            out Coordinate destination)
        {
            destination = null;
            if (sourcePlayfieldId != TemplePlayfieldId
                || externalPlayfieldId != TempleGatewayPlayfieldId
                || unchecked((int)externalDoorInstance) != TempleGatewayDoorInstance
                || !HasExactOfficialInventory())
            {
                return false;
            }

            destination = new Coordinate(CapturedExitX, CapturedExitY, CapturedExitZ);
            return true;
        }

        internal static bool HasExactOfficialInventory()
        {
            PlayfieldData temple;
            PlayfieldData gateway;
            if (!PlayfieldLoader.PFData.TryGetValue(TemplePlayfieldId, out temple)
                || !PlayfieldLoader.PFData.TryGetValue(TempleGatewayPlayfieldId, out gateway))
            {
                return false;
            }

            StatelData[] templeDoors = temple.Statels
                .Where(statel => statel.Identity.Type == IdentityType.Door)
                .GroupBy(statel => statel.Identity)
                .Select(group => group.First())
                .ToArray();
            bool exactGateway = gateway.Statels.Any(
                statel => statel.Identity.Type == IdentityType.Door
                          && statel.Identity.Instance == TempleGatewayDoorInstance);
            bool exactExterior = templeDoors.Count(
                statel => statel.Identity.Instance == TempleExteriorDoorInstance) == 1;
            OfficialDungeonGeometryLoadResult geometry =
                Pf1931OfficialDungeonGeometryLoader.Current;
            return templeDoors.Length == 44
                   && exactExterior
                   && exactGateway
                   && temple.Destinations.Count == 1
                   && geometry.IsLoaded
                   && geometry.Geometry.ExteriorDoorConnectionCount == 1
                   && geometry.Geometry.HasExteriorDoorConnection(
                       "EntryHall",
                       TempleExteriorGeometryDoorIndex);
        }

    }
}
