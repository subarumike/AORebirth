namespace ZoneEngine.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Items;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Statels;
    using AORebirth.Core.Vector;
    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

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

            return PlayfieldLoader.PFData[playfieldIdentity.Instance].Statels;
        }

        internal bool TryResolveVendorStatels(Identity playfieldIdentity, out StatelData[] vendorStatels)
        {
            PlayfieldData playfieldData;
            if (!PlayfieldLoader.PFData.TryGetValue(playfieldIdentity.Instance, out playfieldData))
            {
                vendorStatels = null;
                return false;
            }

            vendorStatels =
                playfieldData.Statels.Where(x => x.Identity.Type == IdentityType.VendingMachine).ToArray();
            return true;
        }

        internal IEnumerable<PlayfieldStaticDynelDefinition> ResolveStaticDynels(Identity playfieldIdentity)
        {
            IEnumerable<DBStaticDynel> dynels =
                StaticDynelDao.Instance.GetWhere(new { Playfield = playfieldIdentity.Instance });
            foreach (DBStaticDynel staticDynel in dynels)
            {
                List<GameTuple<CharacterStat, uint>> stats =
                    MessagePackZip.DeserializeData<GameTuple<CharacterStat, uint>>(staticDynel.stats.ToArray());

                if (!stats.Any(x => x.Value1 == (CharacterStat)StatIds.acgitemtemplateid))
                {
                    continue;
                }

                int templateId =
                    (int)stats.First(x => x.Value1 == (CharacterStat)StatIds.acgitemtemplateid).Value2;
                yield return new PlayfieldStaticDynelDefinition(
                    new Identity { Type = (IdentityType)staticDynel.Type, Instance = staticDynel.Instance },
                    ItemLoader.ItemList[templateId],
                    stats,
                    new Coordinate(staticDynel.X, staticDynel.Y, staticDynel.Z),
                    new Quaternion
                    {
                        X = staticDynel.HeadingX,
                        Y = staticDynel.HeadingY,
                        Z = staticDynel.HeadingZ,
                        W = staticDynel.HeadingW
                    });
            }
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
