namespace ZoneEngine_New.Core.Entities
{
    using System;
    using System.Collections.Generic;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Logging;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Inventory;

    /// <summary>
    /// Online player character. Session is attached at login via SpawnService.
    /// </summary>
    public class Player : Character
    {
        public Player(Identity identity, IZoneLogger logger)
            : base(identity)
        {
            Logger = logger;
            Inventory = new PlayerInventory();
        }

        public override bool IsPlayer => true;

        public IZoneSession? Session { get; set; }

        public PlayerConnectionPhase ConnectionPhase { get; set; } = PlayerConnectionPhase.Online;

        /// <summary>UTC deadline while <see cref="PlayerConnectionPhase.LinkDead"/>; null when Online.</summary>
        public DateTime? LinkDeadUntilUtc { get; set; }

        public PlayerInventory Inventory { get; }

        /// <summary>Current look-at / selection target from the client.</summary>
        public Identity Target { get; set; } = Identity.None;

        internal IZoneLogger Logger { get; }

        public void EnterLinkDead(TimeSpan timeout)
        {
            ConnectionPhase = PlayerConnectionPhase.LinkDead;
            LinkDeadUntilUtc = DateTime.UtcNow + timeout;
            Session = null;
        }

        public void EnterOnline(IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(session);

            ConnectionPhase = PlayerConnectionPhase.Online;
            LinkDeadUntilUtc = null;
            Session = session;
            session.BindPlayer(this);
        }

        /// <summary>
        /// Builds a FullCharacter login packet from current player state.
        /// Structure follows ZoneEngine FullCharacterMessageHandler.Filler without capture/runtime special cases.
        /// </summary>
        public FullCharacterMessage BuildFullCharacterMessage()
        {
            StatCollection stats = Stats;

            var message = new FullCharacterMessage
            {
                Identity = Identity,
                MsgVersion = 26,
                InventorySlots = [.. Inventory.BuildInventorySlots()],
                UploadedNanoIds = [.. UploadedNanoIds],
                Unknown2 = [],
                Unknown3 = 1,
                Unknown4 = [],
                UnknownI2 = 1,
                Unknown5 = [],
                UnknownI3 = 1,
                Unknown6 = []
            };

            var statGroup1 = new List<GameTuple<int, uint>>
            {
                new() { Value1 = (int)CharacterStat.State, Value2 = (uint)stats.Get(CharacterStat.State, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.UnarmedTemplateInstance, Value2 = (uint)stats.Get(CharacterStat.UnarmedTemplateInstance, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.InvadersKilled, Value2 = (uint)stats.Get(CharacterStat.InvadersKilled, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.KilledByInvaders, Value2 = (uint)stats.Get(CharacterStat.KilledByInvaders, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.AccountFlags, Value2 = (uint)stats.Get(CharacterStat.AccountFlags, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.VP, Value2 = (uint)stats.Get(CharacterStat.VP, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.UnsavedXP, Value2 = (uint)stats.Get(CharacterStat.UnsavedXP, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.NanoFocusLevel, Value2 = (uint)stats.Get(CharacterStat.NanoFocusLevel, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.Specialization, Value2 = (uint)stats.Get(CharacterStat.Specialization, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.ShadowBreedTemplate, Value2 = (uint)stats.Get(CharacterStat.ShadowBreedTemplate, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.ShadowBreed, Value2 = (uint)stats.Get(CharacterStat.ShadowBreed, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.LastPerkResetTime, Value2 = (uint)stats.Get(CharacterStat.LastPerkResetTime, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.SocialStatus, Value2 = (uint)stats.Get(CharacterStat.SocialStatus, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.PlayerOptions, Value2 = (uint)stats.Get(CharacterStat.PlayerOptions, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.TempSaveTeamID, Value2 = (uint)stats.Get(CharacterStat.TempSaveTeamID, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.TempSavePlayfield, Value2 = (uint)stats.Get(CharacterStat.TempSavePlayfield, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.TempSaveX, Value2 = (uint)stats.Get(CharacterStat.TempSaveX, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.TempSaveY, Value2 = (uint)stats.Get(CharacterStat.TempSaveY, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.VisualFlags, Value2 = (uint)stats.Get(CharacterStat.VisualFlags, StatDetail.Base) },
                // PvP / commendation / mission-bit / research / battlestation stats not wired yet.
                // new() { Value1 = (int)CharacterStat.SavedXP, Value2 = (uint)stats.Get(CharacterStat.SavedXP, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Flags, Value2 = (uint)stats.Get(CharacterStat.Flags, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.Features, Value2 = (uint)stats.Get(CharacterStat.Features, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.ApartmentsAllowed, Value2 = (uint)stats.Get(CharacterStat.ApartmentsAllowed, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.ApartmentsOwned, Value2 = (uint)stats.Get(CharacterStat.ApartmentsOwned, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Scale, Value2 = (uint)stats.Get(CharacterStat.Scale, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.VisualProfession, Value2 = (uint)stats.Get(CharacterStat.VisualProfession, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.NanoAC, Value2 = (uint)stats.Get(CharacterStat.NanoAC, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.CurrentNano, Value2 = (uint)stats.Get(CharacterStat.CurrentNano, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.MaxNanoEnergy, Value2 = (uint)stats.Get(CharacterStat.MaxNanoEnergy, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.LastConcretePlayfieldInstance, Value2 = (uint)stats.Get(CharacterStat.LastConcretePlayfieldInstance, StatDetail.Base) },
                // Map / mission / auto-attack / research stats not wired yet.
                // new() { Value1 = (int)CharacterStat.MapOptions, Value2 = (uint)stats.Get(CharacterStat.MapOptions, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.MapAreaPart1, Value2 = (uint)stats.Get(CharacterStat.MapAreaPart1, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.MapAreaPart2, Value2 = (uint)stats.Get(CharacterStat.MapAreaPart2, StatDetail.Base) },
                // ActiveNanos-derived MapsC override not implemented yet.
                // new() { Value1 = (int)CharacterStat.MapAreaPart3, Value2 = (uint)stats.Get(CharacterStat.MapAreaPart3, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.MapAreaPart4, Value2 = (uint)stats.Get(CharacterStat.MapAreaPart4, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.MissionBits1, Value2 = (uint)stats.Get(CharacterStat.MissionBits1, StatDetail.Base) },
            };

            var statGroup2 = new List<GameTuple<int, uint>>
            {
                // new() { Value1 = (int)CharacterStat.VeteranPoints, Value2 = (uint)stats.Get(CharacterStat.VeteranPoints, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.MonthsPaid, Value2 = (uint)stats.Get(CharacterStat.MonthsPaid, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.PaidPoints, Value2 = (uint)stats.Get(CharacterStat.PaidPoints, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.AutoAttackFlags, Value2 = (uint)stats.Get(CharacterStat.AutoAttackFlags, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.XPKillRange, Value2 = (uint)stats.Get(CharacterStat.XPKillRange, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.InPlay, Value2 = (uint)stats.Get(CharacterStat.InPlay, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Health, Value2 = (uint)stats.Get(CharacterStat.Health, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.MaxHealth, Value2 = (uint)stats.Get(CharacterStat.MaxHealth, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Psychic, Value2 = (uint)stats.Get(CharacterStat.Psychic, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Sense, Value2 = (uint)stats.Get(CharacterStat.Sense, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Intelligence, Value2 = (uint)stats.Get(CharacterStat.Intelligence, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Stamina, Value2 = (uint)stats.Get(CharacterStat.Stamina, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Agility, Value2 = (uint)stats.Get(CharacterStat.Agility, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Strength, Value2 = (uint)stats.Get(CharacterStat.Strength, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Attitude, Value2 = (uint)stats.Get(CharacterStat.Attitude, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.AlignmentClanTokens, Value2 = (uint)stats.Get(CharacterStat.AlignmentClanTokens, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Cash, Value2 = (uint)stats.Get(CharacterStat.Cash, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Profession, Value2 = (uint)stats.Get(CharacterStat.Profession, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.AggDef, Value2 = (uint)stats.Get(CharacterStat.AggDef, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.Icon, Value2 = (uint)stats.Get(CharacterStat.Icon, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Mesh, Value2 = (uint)stats.Get(CharacterStat.Mesh, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.RunSpeed, Value2 = (uint)stats.Get(CharacterStat.RunSpeed, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.DeadTimer, Value2 = (uint)stats.Get(CharacterStat.DeadTimer, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Team, Value2 = (uint)stats.Get(CharacterStat.Team, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Breed, Value2 = (uint)stats.Get(CharacterStat.Breed, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Sex, Value2 = (uint)stats.Get(CharacterStat.Sex, StatDetail.Base) },
                // XP bar stats omitted from FullCharacter in legacy; login uses standalone StatMessage.
                // new() { Value1 = (int)CharacterStat.LastSaveXP, Value2 = (uint)stats.Get(CharacterStat.LastSaveXP, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.NextXP, Value2 = (uint)stats.Get(CharacterStat.NextXP, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.LastXP, Value2 = (uint)stats.Get(CharacterStat.LastXP, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Level, Value2 = (uint)stats.Get(CharacterStat.Level, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.XP, Value2 = (uint)stats.Get(CharacterStat.XP, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.IP, Value2 = (uint)stats.Get(CharacterStat.IP, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Mass, Value2 = (uint)stats.Get(CharacterStat.Mass, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.ItemType, Value2 = (uint)stats.Get(CharacterStat.ItemType, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.PreviousHealth, Value2 = (uint)stats.Get(CharacterStat.PreviousHealth, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.CurrentState, Value2 = (uint)stats.Get(CharacterStat.CurrentState, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Age, Value2 = (uint)stats.Get(CharacterStat.Age, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Side, Value2 = (uint)stats.Get(CharacterStat.Side, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.WaitState, Value2 = (uint)stats.Get(CharacterStat.WaitState, StatDetail.Base) },
                // Skill / AC / weapon / perk stats not wired yet.
                // new() { Value1 = (int)CharacterStat.DriveWater, Value2 = (uint)stats.Get(CharacterStat.DriveWater, StatDetail.Base) },
                // new() { Value1 = (int)CharacterStat.MeleeMultiple, Value2 = (uint)stats.Get(CharacterStat.MeleeMultiple, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.TitleLevel, Value2 = (uint)stats.Get(CharacterStat.TitleLevel, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.GmLevel, Value2 = (uint)stats.Get(CharacterStat.GmLevel, StatDetail.Base) },
                new() { Value1 = (int)CharacterStat.Expansion, Value2 = (uint)stats.Get(CharacterStat.Expansion, StatDetail.Base) },
                // Faction / alien / social / player-id stats not wired yet.
                // new() { Value1 = (int)CharacterStat.ClanRedeemed, Value2 = (uint)stats.Get(CharacterStat.ClanRedeemed, StatDetail.Base) },
            };

            var statGroup3 = new List<GameTuple<byte, byte>>
            {
                // new() { Value1 = (byte)CharacterStat.InsurancePercentage, Value2 = (byte)stats.Get(CharacterStat.InsurancePercentage, StatDetail.Base) },
                new() { Value1 = (byte)CharacterStat.ProfessionLevel, Value2 = (byte)stats.Get(CharacterStat.ProfessionLevel, StatDetail.Base) },
                // new() { Value1 = (byte)CharacterStat.PrevMovementMode, Value2 = (byte)stats.Get(CharacterStat.PrevMovementMode, StatDetail.Base) },
                new() { Value1 = (byte)CharacterStat.CurrentMovementMode, Value2 = (byte)stats.Get(CharacterStat.CurrentMovementMode, StatDetail.Base) },
                new() { Value1 = (byte)CharacterStat.Fatness, Value2 = (byte)stats.Get(CharacterStat.Fatness, StatDetail.Base) },
                new() { Value1 = (byte)CharacterStat.Race, Value2 = (byte)stats.Get(CharacterStat.Race, StatDetail.Base) },
                // new() { Value1 = (byte)CharacterStat.TeamSide, Value2 = (byte)stats.Get(CharacterStat.TeamSide, StatDetail.Base) },
                new() { Value1 = (byte)CharacterStat.BeltSlots, Value2 = (byte)stats.Get(CharacterStat.BeltSlots, StatDetail.Base) },
            };

            var statGroup4 = new List<GameTuple<byte, short>>
            {
                // Absorb / insurance / temp-skill stats not wired yet.
                // new() { Value1 = (byte)CharacterStat.AbsorbProjectileAC, Value2 = (short)stats.Get(CharacterStat.AbsorbProjectileAC, StatDetail.Base) },
                new() { Value1 = (byte)CharacterStat.CurrentNano, Value2 = (short)stats.Get(CharacterStat.CurrentNano, StatDetail.Base) },
                // new() { Value1 = (byte)CharacterStat.MaxNanoEnergy, Value2 = (short)stats.Get(CharacterStat.MaxNanoEnergy, StatDetail.Base) },
                // new() { Value1 = (byte)CharacterStat.MaxNCU, Value2 = (short)stats.Get(CharacterStat.MaxNCU, StatDetail.Base) },
                new() { Value1 = (byte)CharacterStat.MapFlags, Value2 = (short)stats.Get(CharacterStat.MapFlags, StatDetail.Base) },
                // new() { Value1 = (byte)CharacterStat.ChangeSideCount, Value2 = (short)stats.Get(CharacterStat.ChangeSideCount, StatDetail.Base) },
            };

            message.Stats1 = statGroup1.ToArray();
            message.Stats2 = statGroup2.ToArray();
            message.Stats3 = statGroup3.ToArray();
            message.Stats4 = statGroup4.ToArray();

            message.Unknown9 = 0;
            message.Unknown10 = 0;
            message.Unknown11 = [];
            message.Unknown12 = [];
            message.Unknown13 = [];

            // Team / raid conditional blocks not wired yet.
            // message.Unknown10 = ...
            // message.Unknown11 = ...
            // message.Unknown12 = ...
            // message.Unknown13 = ...

            return message;
        }
    }
}
