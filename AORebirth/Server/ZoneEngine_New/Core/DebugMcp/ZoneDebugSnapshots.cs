namespace ZoneEngine_New.Core.DebugMcp
{
    using System;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Vector3 = AORebirth.Core.Vector.Vector3;

    using ZoneEngine_New.Core.Entities;

    public sealed record DynelView(
        string Name,
        string RuntimeType,
        string IdentityType,
        int Instance,
        double X,
        double Y,
        double Z,
        double HeadingDegrees);

    public sealed record PlayerView(
        int CharacterId,
        string Name,
        string FirstName,
        string LastName,
        int? PlayfieldId,
        string ConnectionPhase,
        double X,
        double Y,
        double Z,
        double HeadingDegrees,
        string TargetType,
        int TargetInstance,
        string FightingTargetType,
        int FightingTargetInstance,
        int Level,
        int Health,
        int MaxHealth,
        int Nano,
        int MaxNano,
        int ProfessionId,
        string? Profession,
        int BreedId,
        string? Breed,
        int ActiveNanoCount,
        bool PersistenceQuarantined);

    public static class ZoneDebugSnapshots
    {
        public const int MaxDynels = 50;
        public const int MaxInventorySlots = 100;
        public const int MaxStats = 32;
        public const int MaxPlayfields = 100;
        public const int MaxPlayers = 100;
        public const int MaxMissions = 20;

        public static PlayerView ProjectPlayer(Player player)
        {
            ArgumentNullException.ThrowIfNull(player);
            CopyPose(player, out double x, out double y, out double z, out double heading);
            int professionId = player.Stats.GetOrZero(CharacterStat.Profession);
            int breedId = player.Stats.GetOrZero(CharacterStat.Breed);
            return new PlayerView(
                player.Identity.Instance,
                DisplayName(player),
                player.FirstName ?? string.Empty,
                player.LastName ?? string.Empty,
                player.Playfield?.Identity.Instance,
                player.ConnectionPhase.ToString(),
                x,
                y,
                z,
                heading,
                player.Target.Type.ToString(),
                player.Target.Instance,
                player.FightingTarget.Type.ToString(),
                player.FightingTarget.Instance,
                player.Stats.GetOrZero(CharacterStat.Level),
                player.Stats.GetOrZero(CharacterStat.Health),
                player.Stats.GetOrZero(CharacterStat.MaxHealth),
                player.Stats.GetOrZero(CharacterStat.CurrentNano),
                player.Stats.GetOrZero(CharacterStat.MaxNanoEnergy),
                professionId,
                EnumName<Profession>(professionId),
                breedId,
                EnumName<Breed>(breedId),
                player.Buffs.Count,
                player.IsPersistenceQuarantined);
        }

        public static DynelView ProjectDynel(Dynel dynel)
        {
            ArgumentNullException.ThrowIfNull(dynel);
            CopyPose(dynel, out double x, out double y, out double z, out double heading);
            return new DynelView(
                DynelName(dynel),
                dynel.GetType().Name,
                dynel.Identity.Type.ToString(),
                dynel.Identity.Instance,
                x,
                y,
                z,
                heading);
        }

        public static string DisplayName(Player player)
        {
            string full = ((player.FirstName ?? string.Empty) + " " + (player.LastName ?? string.Empty)).Trim();
            if (full.Length > 0)
                return full;
            if (!string.IsNullOrWhiteSpace(player.Name))
                return player.Name;
            return "player-" + player.Identity.Instance.ToString(CultureInfo.InvariantCulture);
        }

        public static string DynelName(Dynel dynel)
        {
            if (dynel is Player player)
                return DisplayName(player);
            if (dynel is Character character && !string.IsNullOrWhiteSpace(character.Name))
                return character.Name;
            return dynel.GetType().Name;
        }

        public static string? EnumName<TEnum>(int value)
            where TEnum : struct, Enum
        {
            if (!Enum.IsDefined(typeof(TEnum), value))
                return null;
            return Enum.GetName(typeof(TEnum), value);
        }

        static void CopyPose(Dynel dynel, out double x, out double y, out double z, out double heading)
        {
            Vector3 position = dynel.Position;
            x = position.x;
            y = position.y;
            z = position.z;
            heading = dynel.Rotation.yaw * (180.0 / Math.PI);
        }
    }
}
