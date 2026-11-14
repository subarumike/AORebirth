namespace ZoneEngine_New.Core.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Config/Gameplay.json: tunable gameplay rules, loaded once at startup. Resolved beside the GameData tree first
    /// (a deployment's own Config folder), else beside the process, where the repo copy is placed by the build.
    /// </summary>
    public sealed class GameplaySettings
    {
        public const int CurrentVersion = 1;

        public const string ConfigFileName = "Gameplay.json";

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public int SchemaVersion { get; init; } = CurrentVersion;

        /// <summary>Seconds a successful /stuck locks the Stability skill, and so how long until the next one.</summary>
        public int StuckCooldownSeconds { get; init; } = 300;

        /// <summary>Where /stuck sends a player.</summary>
        public GameplayLocation? StuckLocation { get; init; }

        /// <summary>
        /// Playfields (tutorial-island style zones) where /stuck sends the player to their respawn point (save point,
        /// else the new-character start) instead of <see cref="StuckLocation"/>.
        /// </summary>
        public IReadOnlyList<int> StuckToRespawnPlayfields { get; init; } = Array.Empty<int>();

        /// <summary>
        /// A reload takes rounds from a stack of the weapon's ammo. Off, clips still run dry and reload (with the same
        /// pause), but fill without any ammo in the inventory.
        /// </summary>
        public bool RequireAmmo { get; init; } = true;

        /// <summary>
        /// Seconds NPCs leave a player alone after the client has finished loading a zone (or logging in); they never
        /// aggro on or attack one still loading. 0 ends the grace as soon as the client is in play.
        /// </summary>
        public double ZoneInGraceSeconds { get; init; } = 1;

        /// <summary>XP rules.</summary>
        public GameplayXp Xp { get; init; } = new();

        /// <summary>How district-spawned NPCs wander around their spawn point.</summary>
        public GameplayWander Wander { get; init; } = new();

        /// <summary>Rules for a player fighting a player.</summary>
        public GameplayPvp Pvp { get; init; } = new();

        /// <summary>Nearby NPCs joining a fight one of their own is drawn into.</summary>
        public GameplaySocialAggro SocialAggro { get; init; } = new();

        /// <summary>Unprovoked (proximity) aggro limits.</summary>
        public GameplayProximityAggro ProximityAggro { get; init; } = new();

        /// <summary>How much of a fight a player's pet starts falls back on the player.</summary>
        public GameplayPets Pets { get; init; } = new();

        /// <summary>Where the shared Rubi-Ka clock stood at its epoch.</summary>
        public GameplayWorldClock WorldClock { get; init; } = new();

        /// <summary>How often NPCs cast their uploaded nanos, and what they cast in a fight.</summary>
        public GameplayNpcNanos NpcNanos { get; init; } = new();

        /// <summary>Where a shop's stock list comes from.</summary>
        public GameplayShops Shops { get; init; } = new();

        /// <summary>Organization leave, kick and side-change lockouts.</summary>
        public GameplayOrg Org { get; init; } = new();

        /// <summary>Corpse loot rolls.</summary>
        public GameplayLoot Loot { get; init; } = new();

        /// <summary>Inventory rules: how many items the overflow window keeps.</summary>
        public GameplayInventory Inventory { get; init; } = new();

        /// <summary>Seasonal population switches, read once at startup.</summary>
        public GameplayEvents Events { get; init; } = new();

        /// <summary>IP Reset Points (RP) granted on reaching level milestones; the starting pool is in NewCharacter.json.</summary>
        public GameplayResetPoints IpResetPoints { get; init; } = new();

        public static string ResolvePath(string gameDataRoot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(gameDataRoot);
            string? parent = Directory.GetParent(Path.GetFullPath(gameDataRoot))?.FullName;
            if (!string.IsNullOrEmpty(parent))
            {
                string besideGameData = Path.Combine(parent, "Config", ConfigFileName);
                if (File.Exists(besideGameData))
                    return besideGameData;
            }

            return Path.Combine(AppContext.BaseDirectory, "Config", ConfigFileName);
        }

        public static GameplaySettings Load(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            if (!File.Exists(path))
                throw new FileNotFoundException("Gameplay config was not found.", path);

            GameplaySettings? settings;
            try
            {
                settings = JsonSerializer.Deserialize<GameplaySettings>(File.ReadAllText(path), JsonOptions);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("Gameplay config is invalid: " + path, exception);
            }

            if (settings == null)
                throw new InvalidDataException("Gameplay config is empty: " + path);

            settings.Validate(path);
            return settings;
        }

        void Validate(string path)
        {
            if (SchemaVersion != CurrentVersion)
                throw new InvalidDataException(path + ": schemaVersion must be " + CurrentVersion + " but was " + SchemaVersion + ".");
            if (StuckCooldownSeconds < 0)
                throw new InvalidDataException(path + ": stuckCooldownSeconds must not be negative.");
            if (StuckLocation is not GameplayLocation stuck || !stuck.IsValid)
                throw new InvalidDataException(path + ": stuckLocation needs a positive playfield and finite x, y, z.");
            if (StuckToRespawnPlayfields == null)
                throw new InvalidDataException(path + ": stuckToRespawnPlayfields must be a list (or omitted).");
            foreach (int playfield in StuckToRespawnPlayfields)
            {
                if (playfield <= 0)
                    throw new InvalidDataException(path + ": stuckToRespawnPlayfields entries must be positive playfield ids.");
            }
            if (!double.IsFinite(ZoneInGraceSeconds) || ZoneInGraceSeconds < 0)
                throw new InvalidDataException(path + ": zoneInGraceSeconds must be a finite number, 0 or more.");
            if (Xp == null || !double.IsFinite(Xp.KillPercentPerLevelDifference) || Xp.KillPercentPerLevelDifference < 0)
                throw new InvalidDataException(path + ": xp.killPercentPerLevelDifference must be a finite, non-negative number.");
            if (!double.IsFinite(Xp.GlobalModifier) || Xp.GlobalModifier < 0)
                throw new InvalidDataException(path + ": xp.globalModifier must be a finite, non-negative multiplier.");
            if (!double.IsFinite(Xp.ShadowlandsModifier) || Xp.ShadowlandsModifier < 0)
                throw new InvalidDataException(path + ": xp.shadowlandsModifier must be a finite, non-negative multiplier.");
            if (!double.IsFinite(Xp.SideBonusPercent) || Xp.SideBonusPercent < 0 || Xp.SideBonusPercent > short.MaxValue / 100d)
                throw new InvalidDataException(path + ": xp.sideBonusPercent must fit the non-negative, hundredths-of-percent InfoPacket field.");
            if (Xp.Caps == null || !Xp.Caps.IsValid)
                throw new InvalidDataException(path + ": xp.caps killXp, quest and research must be finite, non-negative percents.");
            if (Xp.NoKillXpNpcFamilies == null)
                throw new InvalidDataException(path + ": xp.noKillXpNpcFamilies must be a list of NPCFamily values (it may be empty).");
            if (Xp.DeathXpLossGraceLevel < 0)
                throw new InvalidDataException(path + ": xp.deathXpLossGraceLevel must be 0 (every level can lose XP) or more.");
            if (Xp.DefaultXpPoolRecoveryPercent is < 0 or > 100)
                throw new InvalidDataException(path + ": xp.defaultXpPoolRecoveryPercent must be from 0 to 100.");
            if (Xp.TradeskillXpPerQl < 0)
                throw new InvalidDataException(path + ": xp.tradeskillXpPerQl must be 0 or more.");
            if (Xp.TeamMemberPercent == null || Array.Exists(Xp.TeamMemberPercent, x => !double.IsFinite(x) || x < 0))
                throw new InvalidDataException(path + ": xp.teamMemberPercent must be a list of finite, non-negative percents (it may be empty).");
            // A wanderer more than the leash range from its spawn point would be sent home mid-stroll.
            if (Wander == null || !float.IsFinite(Wander.Radius) || Wander.Radius < 0
                || Wander.Radius >= Ai.NpcAiRules.MaxLeashRange)
                throw new InvalidDataException(path + ": wander.radius must be 0 (off) up to below the "
                    + Ai.NpcAiRules.MaxLeashRange + " m leash range.");
            if (!double.IsFinite(Wander.MinPauseSeconds) || !double.IsFinite(Wander.MaxPauseSeconds)
                || Wander.MinPauseSeconds < 0 || Wander.MaxPauseSeconds < Wander.MinPauseSeconds)
                throw new InvalidDataException(path + ": wander pause seconds must be non-negative with min <= max.");
            if (Pvp == null || !double.IsFinite(Pvp.DamagePercent) || Pvp.DamagePercent < 0)
                throw new InvalidDataException(path + ": pvp.damagePercent must be a finite, non-negative number.");
            if (!double.IsFinite(Pvp.CritChancePercent) || Pvp.CritChancePercent < 0)
                throw new InvalidDataException(path + ": pvp.critChancePercent must be a finite, non-negative number.");
            if (!double.IsFinite(Pvp.MaxHitPercent) || Pvp.MaxHitPercent <= 0)
                throw new InvalidDataException(path + ": pvp.maxHitPercent must be a positive number (100 for no cap).");
            if (Pvp.AttackerFlagNano < 0)
                throw new InvalidDataException(path + ": pvp.attackerFlagNano must be a nano id, or 0 for none.");
            if (ProximityAggro == null || !float.IsFinite(ProximityAggro.MaxWalkFactor) || ProximityAggro.MaxWalkFactor < 0)
                throw new InvalidDataException(path + ": proximityAggro.maxWalkFactor must be a finite number, 0 or more.");
            if (Loot == null || Loot.QualityVariancePercent is < 0 or > 100)
                throw new InvalidDataException(path + ": loot.qualityVariancePercent must be 0 to 100.");
            if (SocialAggro == null
                || !float.IsFinite(SocialAggro.DefaultRadius) || SocialAggro.DefaultRadius < 0
                || !float.IsFinite(SocialAggro.NpcVsNpcDefaultRadius) || SocialAggro.NpcVsNpcDefaultRadius < 0
                || !float.IsFinite(SocialAggro.GuardDefaultRadius) || SocialAggro.GuardDefaultRadius < 0
                || !float.IsFinite(SocialAggro.MaxRadius) || SocialAggro.MaxRadius < 0
                || SocialAggro.DefaultRadius >= Ai.NpcAiRules.MaxLeashRange
                || SocialAggro.NpcVsNpcDefaultRadius >= Ai.NpcAiRules.MaxLeashRange
                || SocialAggro.GuardDefaultRadius >= Ai.NpcAiRules.MaxLeashRange
                || SocialAggro.MaxRadius >= Ai.NpcAiRules.MaxLeashRange)
                throw new InvalidDataException(path + ": socialAggro.defaultRadius, npcVsNpcDefaultRadius, guardDefaultRadius and maxRadius must be 0 up to below the "
                    + Ai.NpcAiRules.MaxLeashRange + " m leash range.");
            if (!float.IsFinite(SocialAggro.ThreatOnlyRadiusPercent) || SocialAggro.ThreatOnlyRadiusPercent < 0
                || SocialAggro.ThreatOnlyRadiusPercent > 100)
                throw new InvalidDataException(path + ": socialAggro.threatOnlyRadiusPercent must be 0 to 100.");
            if (SocialAggro.MaxHelpers < 0)
                throw new InvalidDataException(path + ": socialAggro.maxHelpers must be 0 (no limit) or more.");
            if (Pets == null || !float.IsFinite(Pets.OwnerThreatSharePercent) || Pets.OwnerThreatSharePercent < 0)
                throw new InvalidDataException(path + ": pets.ownerThreatSharePercent must be a finite, non-negative percent.");
            if (Org == null || Org.LeaveLockoutSeconds < 0 || Org.KickLockoutSeconds < 0 || Org.SideChangeLockoutSeconds < 0
                    || Org.AnarchistLeaderKickLockoutSeconds < 0 || Org.DisbandLockoutSeconds < 0)
                    throw new InvalidDataException(path + ": org lockout seconds must be 0 or more.");
                if (Org.TaxTickSeconds <= 0 || Org.TaxPeriodSeconds <= 0)
                    throw new InvalidDataException(path + ": org taxTickSeconds and taxPeriodSeconds must be above 0.");
                if (Org.VoteCooldownSeconds < 0)
                    throw new InvalidDataException(path + ": org voteCooldownSeconds must be 0 or more.");
            if (WorldClock == null || !WorldClock.TryGetEpoch(out _) || WorldClock.DayAtEpoch < 0
                || WorldClock.TimeOfDayAtEpochSeconds < 0 || WorldClock.TimeOfDayAtEpochSeconds >= Network.GameClock.SecondsPerDay)
                throw new InvalidDataException(path + ": worldClock needs an ISO 8601 epochUtc, dayAtEpoch 0 or more, and "
                    + "timeOfDayAtEpochSeconds from 0 up to below the " + Network.GameClock.SecondsPerDay + " s day.");
            if (NpcNanos == null || !NpcNanos.IsValid)
                throw new InvalidDataException(path + ": npcNanos needs finite, non-negative intervals with min <= max, "
                    + "idleCheckSeconds and supportRange above 0, and healBelowPercent from 0 to 100.");
            if (Inventory == null || Inventory.OverflowCapacity < 1
                || Inventory.OverflowCapacity > ZoneEngine_New.Core.Inventory.PlayerInventory.OverflowCapacity)
                throw new InvalidDataException(path + ": inventory.overflowCapacity must be from 1 to "
                    + ZoneEngine_New.Core.Inventory.PlayerInventory.OverflowCapacity + " (the overflow window's slots).");
            if (Inventory.MaxCredits < 1)
                throw new InvalidDataException(path + ": inventory.maxCredits must be from 1 to " + int.MaxValue + ".");
            if (Events == null || Events.Halloween == null)
                throw new InvalidDataException(path + ": events and events.halloween must be objects.");
        }
    }

    /// <summary>
    /// IP Reset Points granted when a character reaches a title level (<see cref="TitleLevels"/>) or an alien level
    /// (<see cref="AlienLevels"/>). Each milestone pays once, on the way up; a level set lower takes nothing back.
    /// </summary>
    public sealed class GameplayResetPoints
    {
        /// <summary>Title level → reset points granted on reaching it.</summary>
        public Dictionary<int, int> TitleLevels { get; init; } = new();

        /// <summary>Alien level → reset points granted on reaching it.</summary>
        public Dictionary<int, int> AlienLevels { get; init; } = new();

        /// <summary>Points for every milestone in (<paramref name="from"/>, <paramref name="to"/>].</summary>
        public static int Between(Dictionary<int, int> milestones, int from, int to)
        {
            int points = 0;
            foreach (KeyValuePair<int, int> milestone in milestones)
            {
                if (milestone.Key > from && milestone.Key <= to)
                    points += milestone.Value;
            }

            return points;
        }
    }

    public sealed class GameplayEvents
    {
        public GameplayHalloween Halloween { get; init; } = new();
    }

    public sealed class GameplayHalloween
    {
        public bool Enabled { get; init; }
    }

    /// <summary>
    /// The npcNanos section of Gameplay.json: the pace of an NPC's own nano casting (<see cref="Ai.NpcNanoCaster"/>).
    /// In a fight an NPC casts hostile nanos and pet summons first, then heals; buffs wait for the fight to end unless
    /// <see cref="BuffInCombat"/> allows them. Each cast is followed by a random wait in the matching interval, and a
    /// look that found nothing to cast waits <see cref="IdleCheckSeconds"/>.
    /// </summary>
    public sealed class GameplayNpcNanos
    {
        internal static readonly GameplayNpcNanos Default = new();

        /// <summary>Wait after a hostile nano, heal or summon before the next look.</summary>
        public double MinCastIntervalSeconds { get; init; } = 10;

        public double MaxCastIntervalSeconds { get; init; } = 20;

        /// <summary>Wait after a buff before the next look: idle NPCs buffing themselves and their neighbours.</summary>
        public double MinBuffIntervalSeconds { get; init; } = 20;

        public double MaxBuffIntervalSeconds { get; init; } = 40;

        /// <summary>Wait after a look that found nothing to cast.</summary>
        public double IdleCheckSeconds { get; init; } = 3;

        /// <summary>Buffs may also be cast during a fight, after the hostile nanos and heals.</summary>
        public bool BuffInCombat { get; init; }

        /// <summary>Friendly nanos reach NPCs this close, in meters, when the nano has no AttackRange.</summary>
        public double SupportRange { get; init; } = 20;

        /// <summary>Heals only go on characters under this health percent (a pet heals its own all the way to full).</summary>
        public int HealBelowPercent { get; init; } = 75;

        internal bool IsValid
            => IsInterval(MinCastIntervalSeconds, MaxCastIntervalSeconds)
                && IsInterval(MinBuffIntervalSeconds, MaxBuffIntervalSeconds)
                && double.IsFinite(IdleCheckSeconds) && IdleCheckSeconds > 0
                && double.IsFinite(SupportRange) && SupportRange > 0
                && HealBelowPercent is >= 0 and <= 100;

        static bool IsInterval(double min, double max)
            => double.IsFinite(min) && double.IsFinite(max) && min >= 0 && max >= min;
    }

    /// <summary>
    /// The inventory section of Gameplay.json. An item given or returned to a player whose inventory is full goes to the
    /// overflow window, which is saved with the character. It holds at most <see cref="OverflowCapacity"/> items: one
    /// more removes the oldest. From overflow a player can only delete an item or move it into the inventory.
    /// </summary>
    public sealed class GameplayInventory
    {
        /// <summary>Retail's overflow window: 20 slots (its InventoryUpdate, capture 2026-10-18T20:05:29Z).</summary>
        public const int DefaultOverflowCapacity = 20;

        /// <summary>Most items the overflow window keeps; a new one past this deletes the oldest.</summary>
        public int OverflowCapacity { get; init; } = DefaultOverflowCapacity;

        /// <summary>
        /// Most credits a character may hold; nothing takes one past it. Above 1,000,000,000 the client's credits
        /// window stops updating (it ignores larger values), but the server keeps the real amount.
        /// </summary>
        public int MaxCredits { get; init; } = ZoneEngine_New.Core.Characters.CashLimit.Default;
    }

    /// <summary>
    /// The worldClock section of Gameplay.json: the one Rubi-Ka clock every player is sent (GameTime). The client runs
    /// a 27-hour day at 15 game seconds per real second from what it is sent, so these only fix where that clock stood
    /// at <see cref="EpochUtc"/>; the server time the same message carries is real UTC seconds.
    /// </summary>
    public sealed class GameplayWorldClock
    {
        /// <summary>When the clock read <see cref="DayAtEpoch"/> and <see cref="TimeOfDayAtEpochSeconds"/>, ISO 8601 UTC.</summary>
        public string EpochUtc { get; init; } = "2026-01-01T00:00:00Z";

        /// <summary>Day number at the epoch (GameTime_t::GetCurrentDay).</summary>
        public int DayAtEpoch { get; init; } = 185408;

        /// <summary>Time of day at the epoch in game seconds of the 97200 s day (30024 is 08:20:24, dawn).</summary>
        public int TimeOfDayAtEpochSeconds { get; init; } = 30024;

        public bool TryGetEpoch(out DateTime epochUtc)
            => DateTime.TryParse(EpochUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out epochUtc);
    }

    /// <summary>
    /// The pets section of Gameplay.json. A pet that fights for a player is not a free shield: the NPCs it fights also
    /// hold its owner responsible, so an owner whose pet dies, is dismissed or is somewhere the NPC cannot reach is the
    /// NPC's next target instead of the NPC walking home unharmed.
    /// </summary>
    public sealed class GameplayPets
    {
        /// <summary>
        /// Percent of every threat a player's pet earns (damage, taunts, hostile nanos) that its owner earns as well. The
        /// pet keeps the full amount, so it still holds the NPC in ordinary play; the owner builds up behind it. 0 leaves
        /// the owner with only the minimum entry from <see cref="OwnerAlwaysOnHateList"/>.
        /// </summary>
        public float OwnerThreatSharePercent { get; init; } = 50f;

        /// <summary>
        /// The owner goes on the NPC's hate list the moment their pet first draws its threat, at least at the proximity
        /// minimum, even with a 0 share. Without this an NPC whose pet target is gone has nobody left and resets.
        /// </summary>
        public bool OwnerAlwaysOnHateList { get; init; } = true;

        /// <summary>
        /// The owner of the pet an NPC is fighting is engageable out to the leash range, like the pet itself, instead
        /// of the shorter range a bystander must be within. An owner standing back behind their pet is in the fight.
        /// </summary>
        public bool OwnerSharesPetEngageRange { get; init; } = true;
    }

    /// <summary>Organization lockouts and tax timing, in seconds. Lockouts count down online; tax counts personal /played time.</summary>
    public sealed class GameplayOrg
    {
        /// <summary>Lockout after a member leaves (/org leave).</summary>
        public long LeaveLockoutSeconds { get; init; } = 21600;

        /// <summary>Lockout after a member is kicked.</summary>
        public long KickLockoutSeconds { get; init; } = 21600;

        /// <summary>Lockout after a member leaves the organization through a side change.</summary>
        public long SideChangeLockoutSeconds { get; init; } = 21600;

        /// <summary>Lockout after an Anarchism leader is kicked by the member quorum.</summary>
        public long AnarchistLeaderKickLockoutSeconds { get; init; } = 43200;

        /// <summary>Lockout after an organization is disbanded (applied to every former member, as after leave).</summary>
        public long DisbandLockoutSeconds { get; init; } = 43200;

        /// <summary>Personal /played seconds between two organization tax deductions for each non-leader member.</summary>
        public long TaxTickSeconds { get; init; } = 7200;

        /// <summary>Real-time seconds of a tax warning or suspension period before the next check.</summary>
        public long TaxPeriodSeconds { get; init; } = 1800;

        /// <summary>Real-time seconds an organization must wait after a vote ends or is stopped before starting another (retail 30 min).</summary>
        public int VoteCooldownSeconds { get; init; } = 1800;
    }

    /// <summary>
    /// The socialAggro section of Gameplay.json. When an NPC gains a new attacker (hit, or proximity aggro), nearby NPCs
    /// on the same Side join the fight against that attacker. How far each helper comes is its own spawn point's
    /// AssistanceRadius (GameData Playfields/{id}/Spawns.json); 0 there means it never assists. Pets, non-attackable,
    /// evading and pacified NPCs never join.
    /// </summary>
    /// <summary>Unprovoked (proximity) aggro limits.</summary>
    public sealed class GameplayProximityAggro
    {
        /// <summary>
        /// An NPC only aggroes a player it can walk to within this many times its aggro range: one under a bridge sees
        /// the player overhead, but the way up is far longer than that, so it stays put. 0 turns the check off. Fights
        /// already started (attacks, calls for help) chase as before.
        /// </summary>
        public float MaxWalkFactor { get; init; } = 2f;
    }

    public sealed class GameplaySocialAggro
    {
        /// <summary>Off: no NPC answers a call for help.</summary>
        public bool Enabled { get; init; } = true;

        /// <summary>
        /// How far a call for help carries from an attacked NPC whose spawn point has none (AssistanceRadius 0) or that
        /// has no spawn point (summoned, GM spawn). 0 keeps those from calling for help.
        /// </summary>
        public float DefaultRadius { get; init; }

        /// <summary>Cap on a spawn point's AssistanceRadius, in meters edge to edge (the data reaches 200).</summary>
        public float MaxRadius { get; init; } = 30f;

        /// <summary>
        /// Call radius against an NPC attacker (not a player's pet) for an NPC whose spawn point has none: NPC-versus-NPC
        /// fights (a hated family, a guard defending) pull in the victim's kind even where the spawn data gives no radius.
        /// 0 falls back to defaultRadius.
        /// </summary>
        public float NpcVsNpcDefaultRadius { get; init; }

        /// <summary>
        /// Call radius between guards of one side, whatever the attacker (a player of their own side included), when
        /// the attacked guard's spawn point gives none. Guard spawns mostly carry AssistanceRadius 0, and a player
        /// attacker otherwise falls to <see cref="DefaultRadius"/>. 0 falls back to the other defaults.
        /// </summary>
        public float GuardDefaultRadius { get; init; }

        /// <summary>
        /// How far the call of an attacked NPC with <paramref name="assistanceRadius"/> from its spawn point reaches;
        /// <paramref name="npcAttacker"/> when the enemy is an NPC that is not a player's pet, <paramref name="guards"/>
        /// when a guard is answering a guard.
        /// </summary>
        public float RadiusFor(int assistanceRadius, bool npcAttacker = false, bool guards = false, bool threatOnly = false)
        {
            float fallback = guards && GuardDefaultRadius > 0 ? GuardDefaultRadius
                : npcAttacker && NpcVsNpcDefaultRadius > 0 ? NpcVsNpcDefaultRadius
                : DefaultRadius;
            float radius = Math.Min(assistanceRadius > 0 ? assistanceRadius : fallback, MaxRadius);
            // Guards answering a guard are one force: a taunt on one still brings the rest at the full radius.
            return threatOnly && !guards ? radius * ThreatOnlyRadiusPercent / 100f : radius;
        }

        /// <summary>
        /// Percent of the call radius when the attacker's first threat was not damage (a taunt, a debuff, a hostile
        /// nano that did no damage, the NPC's own proximity aggro on a player). The attacker's first damage afterwards calls again at the full radius. Guards
        /// answering a guard always come from the full radius. 100 leaves it as it is.
        /// </summary>
        public float ThreatOnlyRadiusPercent { get; init; } = 100f;

        /// <summary>The farthest any helper can come: how far a call for help has to look.</summary>
        public float SearchRadius => Math.Max(Math.Max(Math.Max(DefaultRadius, NpcVsNpcDefaultRadius), GuardDefaultRadius), MaxRadius);

        /// <summary>
        /// Helpers must share the NPC's Clan (5), its kind of creature, as well as its Side. Guards answering a guard are
        /// one force whatever their kind, so this does not hold them back.
        /// </summary>
        public bool SameClanOnly { get; init; } = true;

        /// <summary>Helpers must have line of sight to the NPC under attack.</summary>
        public bool RequireLineOfSight { get; init; } = true;

        /// <summary>Most NPCs one call brings in, nearest first. 0 means no limit.</summary>
        public int MaxHelpers { get; init; }

        /// <summary>A helper calls for help in turn. Off keeps one pull from spreading across a whole area.</summary>
        public bool Chain { get; init; }
    }

    /// <summary>
    /// The pvp section of Gameplay.json: a player's attack on a player, its own included (self damage counts). 100 leaves
    /// a value as it is.
    /// </summary>
    public sealed class GameplayPvp
    {
        /// <summary>Percent of its rolled amount that a player's damage to a player deals.</summary>
        public double DamagePercent { get; init; } = 100;

        /// <summary>Percent of a player's critical chance (CriticalIncrease) that applies against a player.</summary>
        public double CritChancePercent { get; init; } = 100;

        /// <summary>
        /// Most of the target's max health one player-on-player hit can take, in percent, after
        /// <see cref="DamagePercent"/>. 100 is no cap.
        /// </summary>
        public double MaxHitPercent { get; init; } = 100;

        /// <summary>
        /// The nano a player gets for starting a fight with, or aiming a hostile action at, a player (214879, the PvP
        /// flag). Landed only while the player does not already have it running. 0 turns it off.
        /// </summary>
        public int AttackerFlagNano { get; init; }

        static bool IsPvp(Entities.Character? source, Entities.Character target)
            => source is Entities.Player && target is Entities.Player;

        /// <summary>
        /// <paramref name="damage"/> as <paramref name="source"/> deals it to <paramref name="target"/>: in PvP scaled by
        /// <see cref="DamagePercent"/> (rounded half up, never below 1 while there was any). The hit cap is
        /// <see cref="CapHit"/>, applied to what gets past the target's absorb layers.
        /// </summary>
        public int ScaleDamage(Entities.Character? source, Entities.Character target, int damage)
        {
            if (damage <= 0 || !IsPvp(source, target))
                return damage;
            if (DamagePercent == 0)
                return 0;

            return DamagePercent == 100
                ? damage
                : (int)Math.Clamp(Math.Round(damage * DamagePercent / 100, MidpointRounding.AwayFromZero), 1, int.MaxValue);
        }

        /// <summary>
        /// <paramref name="damage"/> held to <see cref="MaxHitPercent"/> of the target's max health, in PvP. Applied to what
        /// reaches health after the absorb layers, never to the layers: a layer takes the full hit, so a low max health
        /// does not shrink what a shield has to take (retail moved the cap behind the absorbs for that reason).
        /// </summary>
        public int CapHit(Entities.Character? source, Entities.Character target, int damage)
        {
            if (damage <= 0 || MaxHitPercent >= 100 || !IsPvp(source, target))
                return damage;

            int maxHealth = target.Stats.GetOrZero(SmokeLounge.AOtomation.Messaging.GameData.CharacterStat.MaxHealth);
            if (maxHealth <= 0)
                return damage;

            int cap = (int)Math.Max(1, Math.Round(maxHealth * MaxHitPercent / 100, MidpointRounding.AwayFromZero));
            return Math.Min(damage, cap);
        }

        /// <summary>The percent chance to crit, <paramref name="critChance"/>, scaled in PvP.</summary>
        public double ScaleCritChance(Entities.Character source, Entities.Character target, double critChance)
            => IsPvp(source, target) ? critChance * CritChancePercent / 100 : critChance;
    }

    /// <summary>The wander section of Gameplay.json.</summary>
    public sealed class GameplayWander
    {
        /// <summary>Meters around the district spawn point an NPC is scattered and wanders within. 0 turns wandering off.</summary>
        public float Radius { get; init; } = 50f;

        /// <summary>Shortest time an NPC stands still between strolls.</summary>
        public double MinPauseSeconds { get; init; } = 5;

        /// <summary>Longest time an NPC stands still between strolls.</summary>
        public double MaxPauseSeconds { get; init; } = 20;
    }

    /// <summary>The xp section of Gameplay.json.</summary>
    public sealed class GameplayXp
    {
        /// <summary>
        /// Multiplier on every regular XP grant (kill, quest, tradeskill, daily and mission rewards), before the side bonus
        /// and the per-grant caps. 1.0 leaves XP unchanged. Alien XP is not affected.
        /// </summary>
        public double GlobalModifier { get; init; } = 1.0;

        /// <summary>
        /// Extra multiplier, on top of <see cref="GlobalModifier"/>, for regular XP earned on a Shadowlands playfield
        /// (ExpansionPlayfield 1, <see cref="Entities.PlayfieldEntryFlags"/>). 1.0 leaves it unchanged.
        /// </summary>
        public double ShadowlandsModifier { get; init; } = 1.0;

        /// <summary>The regular XP multiplier on a playfield that is (or is not) in the Shadowlands.</summary>
        public double RateMultiplier(bool shadowlands) => GlobalModifier * (shadowlands ? ShadowlandsModifier : 1.0);

        /// <summary>Flat Side XP bonus for every player's regular kill and quest XP. 0 disables it.</summary>
        public double SideBonusPercent { get; init; }

        /// <summary>
        /// Percent of the victim's KillAward added per level the victim is above the recipient (removed per level below).
        /// </summary>
        public double KillPercentPerLevelDifference { get; init; } = 2.5;

        /// <summary>
        /// Least alien XP each recipient gets from an alien that is not grey to them (grey: more than the recipient's
        /// Xp.json XPKillRange below them, as for regular XP), solo or as a team member's cut. The solo formula reaches
        /// 0 at about 1.5x the alien's level, well inside the levels the client still shows green.
        /// </summary>
        public int AlienMinimumXp { get; init; } = 150;

        /// <summary>
        /// NPCFamily (455) values whose kills give no XP at all, not even the grey 1 XP. Family 0 is the guards and
        /// civilians (Newland Militia Guard and some 78 other templates). An NPC with no NPCFamily stat is not family 0.
        /// </summary>
        public int[] NoKillXpNpcFamilies { get; init; } = [0];

        /// <summary>
        /// The death XP loss grace level: characters below it lose no XP when they die (live's insurance terminal says
        /// you are "always" saved there). 0 turns the grace off.
        /// </summary>
        public int DeathXpLossGraceLevel { get; init; } = 20;

        /// <summary>
        /// Percent of a kill's XP (before the side bonus) that also comes back from the death pool (XP lost on deaths since
        /// the last insurance save), at least 1 XP and at most what the pool holds. Live: 50.
        /// </summary>
        public int DefaultXpPoolRecoveryPercent { get; init; } = 50;

        /// <summary>A successful tradeskill combine gives XP (<see cref="TradeskillXpPerQl"/> per result QL). Off by default.</summary>
        public bool TradeskillGrantsXp { get; init; }

        /// <summary>XP per QL of a combine's result while <see cref="TradeskillGrantsXp"/> is on.</summary>
        public int TradeskillXpPerQl { get; init; } = 5;

        /// <summary>Per-grant caps, in percent of the current level's XP bar.</summary>
        public GameplayXpCaps Caps { get; init; } = new();

        /// <summary>
        /// Percent of the team's kill share (regular and alien XP) each nearby member gets, by how many share it: entry 0
        /// for one, entry 1 for two, and so on; a bigger team uses the last entry. 1 100%, 2 82.5% (165% in all),
        /// 3 75% (225%), 4 70% (280%), 5 65% (330%), 6 62.5% (375%).
        /// NOTE: this table is NOT correct. It is a stand-in until live's team XP rule is known. Empty splits the share
        /// evenly (100% / members).
        /// </summary>
        public double[] TeamMemberPercent { get; init; } = [100, 82.5, 75, 70, 65, 62.5];

        /// <summary>One member's percent of the team share when <paramref name="members"/> share it.</summary>
        public double MemberPercent(int members)
        {
            if (members <= 1)
                return TeamMemberPercent.Length > 0 ? TeamMemberPercent[0] : 100;
            if (TeamMemberPercent.Length == 0)
                return 100.0 / members;
            return TeamMemberPercent[Math.Min(members, TeamMemberPercent.Length) - 1];
        }

        public bool GivesNoKillXp(int npcFamily) => Array.IndexOf(NoKillXpNpcFamilies, npcFamily) >= 0;
    }

    /// <summary>
    /// The xp.caps section of Gameplay.json: the most one grant may give, in percent of the current level's XP bar
    /// (NextLevelXp; the alien level's bar for alien XP), never below 1.
    /// </summary>
    public sealed class GameplayXpCaps
    {
        public static GameplayXpCaps Default { get; } = new();

        /// <summary>Kill XP, and alien XP.</summary>
        public double KillXp { get; init; } = 10;

        /// <summary>Quest XP rewards.</summary>
        public double Quest { get; init; } = 20;

        /// <summary>
        /// The personal research share of one grant, in percent of the XP the selected research tier needs in all (not of
        /// the level bar). Taken before the XP caps; the rest of the share stays XP.
        /// </summary>
        public double Research { get; init; } = 10;

        internal bool IsValid => IsPercent(KillXp) && IsPercent(Quest) && IsPercent(Research);

        /// <summary><paramref name="percent"/> of <paramref name="levelBar"/>, rounded down, at least 1.</summary>
        public static long Of(double percent, long levelBar)
            => (long)Math.Clamp(Math.Floor(levelBar * percent / 100), 1, int.MaxValue);

        static bool IsPercent(double value) => double.IsFinite(value) && value >= 0;
    }

    /// <summary>A playfield and position in Gameplay.json.</summary>
    /// <summary>The shops section of Gameplay.json.</summary>
    /// <summary>Corpse loot rolls.</summary>
    public sealed class GameplayLoot
    {
        /// <summary>
        /// Every corpse drop rolls its QL within this percent either side of where it is centred: an item table row on
        /// the NPC's level, an equipment spawn (Lootgiver, Boss Modifier) on its own QL. An item table row with its own
        /// levelMod uses that instead. 0 drops at exactly that QL.
        /// </summary>
        public int QualityVariancePercent { get; init; } = 25;
    }

    public sealed class GameplayShops
    {
        /// <summary>
        /// A static vending machine with a database vendor row (IShopDao) takes that row's stock instead of its own
        /// record's list (items.dat ShopInventory). On while the database vendors are the vetted set; off makes every
        /// shop's own list the source and the database rows are ignored.
        /// </summary>
        public bool PreferDatabaseStock { get; init; } = true;
    }

    public sealed class GameplayLocation
    {
        public int Playfield { get; init; }

        public float X { get; init; }

        public float Y { get; init; }

        public float Z { get; init; }

        public bool IsValid => Playfield > 0 && float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);
    }
}
