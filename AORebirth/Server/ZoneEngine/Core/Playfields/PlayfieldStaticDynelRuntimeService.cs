namespace ZoneEngine.Core.Playfields
{
    using AORebirth.Core.Entities;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal sealed class PlayfieldStaticDynelRuntimeService
    {
        internal IEntity CreateStaticDynel(Identity playfieldIdentity, PlayfieldStaticDynelDefinition staticDynel)
        {
            StaticDynel sdy = new StaticDynel(playfieldIdentity, staticDynel.Identity, staticDynel.Template);

            foreach (GameTuple<CharacterStat, uint> stat in staticDynel.Stats)
            {
                if (sdy.Stats.ContainsKey((int)stat.Value1))
                {
                    sdy.Stats[(int)stat.Value1] = (int)stat.Value2;
                    continue;
                }

                sdy.Stats.Add((int)stat.Value1, (int)stat.Value2);
            }

            sdy.Coordinate = staticDynel.Coordinate;
            sdy.Heading = staticDynel.Heading;
            return sdy;
        }
    }
}
