namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.IO;
    using System.Threading;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
    using SmokeLounge.AOtomation.Messaging.Serialization;

    using StreamReader = SmokeLounge.AOtomation.Messaging.Serialization.StreamReader;
    using StreamWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;

    #endregion

    /// <summary>
    /// Produces the QuestAlternative "roll" response a mission terminal sends back to the client.
    /// Starts from a whole captured 5-offer roll (library: capture 20260719-Rolling different mishes,
    /// fallback: 20260717-Mission terminal2) so icons stay paired with ShortInfo/Info. Variety across
    /// pulls comes from selecting different captured rolls. Safe fixed-size fields (QL, quest id, cash/XP,
    /// item reward ids, XYZ, terminal retarget) are mutated — never variable-length strings.
    /// </summary>
    internal static class MissionRollService
    {
        // Mission-terminal identity type as observed on the wire. The repo enum value
        // (IdentityType.MissionTerminal = 0x0000DCA1) does not match live captures, so compare raw.
        private const int MissionTerminalIdentityTypeRaw = 0x0000DAC1;

        private const int MissionIdentityTypeRaw = 0x0000DAC3;

        private static readonly object InitLock = new object();

        private static SerializerResolver serializerResolver;

        private static ISerializer questAlternativeSerializer;

        private static byte[] templateBody;

        private static byte[][] libraryBodies;

        private static int questInstanceSeed =
            Math.Max(0x55569000, unchecked((int)(DateTime.UtcNow.Ticks & 0x3fffffff)));

        /// <summary>
        /// Builds a fresh 5-offer roll for the requesting player/terminal.
        /// </summary>
        /// <param name="terminalPlayfieldId">
        /// Playfield of the rolling character (Omni Trade / Rome / Tir / Athens…). Used to keep
        /// Omni city rolls off Clan markers and Clan city rolls off Omni markers.
        /// </param>
        /// <param name="terminalX">Character X at the terminal (proxy for terminal world position).</param>
        /// <param name="terminalZ">Character Z at the terminal (proxy for terminal world position).</param>
        public static QuestAlternativeMessage BuildRollResponse(
            QuestAlternativeMessage request,
            Identity character,
            int characterLevel,
            int terminalPlayfieldId = 0,
            float terminalX = 0f,
            float terminalZ = 0f,
            MissionLocationSide characterSide = MissionLocationSide.Neutral)
        {
            EnsureInitialized();

            int missionQuality = MissionLevelTable.GetMissionQuality(characterLevel, request.LevelSlider);
            var rng = new Random(unchecked(Environment.TickCount * 397) ^ character.Instance ^ missionQuality);
            MissionLocationSide citySide;
            MissionLocationSide poolSide = MissionLocationPool.TryGetCityAffiliation(terminalPlayfieldId, out citySide)
                                              ? citySide
                                              : characterSide;

            // Whole captured roll — keeps Kill / Find / Repair / Broken-Machine texts matched to icons.
            QuestAlternativeMessage response = DecodeRollBody(PickLibraryBody(rng));
            Identity templateTerminal = response.MissionTerminalIdentity;
            Identity terminal = request.MissionTerminalIdentity;

            response.Identity = character;
            response.MissionTerminalIdentity = terminal;
            response.VersionId = request.VersionId;
            response.Unknown5 = request.Unknown5;
            response.LevelSlider = request.LevelSlider;
            response.GoodBadSlider = request.GoodBadSlider;
            response.OrderChaosSlider = request.OrderChaosSlider;
            response.OpenHiddenSlider = request.OpenHiddenSlider;
            response.PhysicalMysticalSlider = request.PhysicalMysticalSlider;
            response.HeadOnStealthSlider = request.HeadOnStealthSlider;
            response.MoneyExperienceSlider = request.MoneyExperienceSlider;
            response.Unknown4 = unchecked((int)(uint)Environment.TickCount);

            QuestInfo[] offers = response.QuestInfos;
            if (offers == null || offers.Length == 0)
            {
                MissionDiagnostics.Log("ROLL-EMPTY-LIBRARY fallback to single template");
                response = DecodeTemplate();
                offers = response.QuestInfos;
                templateTerminal = response.MissionTerminalIdentity;
                response.Identity = character;
                response.MissionTerminalIdentity = terminal;
            }

            var usedSpotIndexes = new int[offers.Length];
            for (int i = 0; i < usedSpotIndexes.Length; i++)
            {
                usedSpotIndexes[i] = -1;
            }

            MissionDiagnostics.Log(
                "ROLL-SIDE terminalPf={0} poolSide={1} charSide={2} charLvl={3} termXZ=({4:F0},{5:F0})",
                terminalPlayfieldId,
                poolSide,
                characterSide,
                characterLevel,
                terminalX,
                terminalZ);

            // Whole captured roll — icons stay paired with ShortInfo/Info (do NOT retype icons).
            // ApplyMissionType used to overwrite MissionIconId onto foreign text and made
            // Find Person show Kill/Find-Item descriptions ("clean out his stronghold").
            int[] allowedSpotIndexes = BuildAllowedSpotIndexes(
                poolSide,
                characterLevel,
                terminalPlayfieldId,
                terminalX,
                terminalZ);

            // Prefer distinct playfields at higher levels; low-level same-zone rolls allow multiple
            // XYZ markers inside one PF so all five offers stay near the terminal.
            bool preferDistinctPlayfields = characterLevel > 40;

            for (int i = 0; i < offers.Length; i++)
            {
                QuestInfo offer = offers[i];
                if (offer == null)
                {
                    continue;
                }

                MissionRollType type = MissionTypeCatalog.TypeFromIcon(offer.MissionIconId);

                // Assign a Rubi-Ka marker from the live roll capture pool.
                ApplyPoolLocation(offer, rng, usedSpotIndexes, i, allowedSpotIndexes, preferDistinctPlayfields);

                offer.QuestIdentity = new Identity
                                      {
                                          Type = (IdentityType)MissionIdentityTypeRaw,
                                          Instance = NextQuestInstance()
                                      };
                offer.Unknown5 = RetargetTerminal(offer.Unknown5, templateTerminal, terminal);
                offer.Unknown14 = RetargetTerminal(offer.Unknown14, templateTerminal, terminal);
                offer.Unknown23 = RetargetTerminal(offer.Unknown23, templateTerminal, terminal);
                offer.Quality = missionQuality;

                ApplyMoneyExperienceSlider(offer, request.MoneyExperienceSlider, missionQuality);
                ApplyMaliReward(offer, missionQuality, rng, type);

                int markerPf = 0;
                float markerX = 0;
                float markerZ = 0;
                if (offer.QuestActions != null && offer.QuestActions.Length > 0 && offer.QuestActions[0] != null)
                {
                    markerPf = offer.QuestActions[0].Playfield.Instance;
                    markerX = offer.QuestActions[0].X;
                    markerZ = offer.QuestActions[0].Z;
                }

                MissionDiagnostics.Log(
                    "ROLL-OFFER slot={0} type={1} icon={2} ql={3} quest={4:X8} pf={5} xz=({6:F0},{7:F0}) rewardLow={8} rewardQl={9}",
                    i,
                    MissionTypeCatalog.TypeName(type),
                    offer.MissionIconId,
                    offer.Quality,
                    offer.QuestIdentity.Instance,
                    markerPf,
                    markerX,
                    markerZ,
                    offer.ItemRewards != null && offer.ItemRewards.Length > 0 ? offer.ItemRewards[0].LowId : 0,
                    offer.ItemRewards != null && offer.ItemRewards.Length > 0 ? offer.ItemRewards[0].Quality : 0);
            }

            response.QuestInfos = offers;
            RestoreStringTerminators(response);
            return response;
        }

        /// <summary>
        /// Decodes the captured template into a live message and restores the string terminators that the
        /// stream reader trims on read, so that re-serialization reproduces the captured bytes exactly.
        /// </summary>
        internal static QuestAlternativeMessage DecodeTemplate()
        {
            EnsureInitialized();

            var message = (QuestAlternativeMessage)Deserialize(templateBody);
            RestoreStringTerminators(message);
            return message;
        }

        /// <summary>
        /// Serializes a QuestAlternative message to its N3 body bytes using the shared resolver.
        /// </summary>
        internal static byte[] SerializeBody(QuestAlternativeMessage message)
        {
            EnsureInitialized();

            using (var memoryStream = new MemoryStream())
            using (var writer = new StreamWriter(memoryStream))
            {
                questAlternativeSerializer.Serialize(
                    writer,
                    new SerializationContext(serializerResolver),
                    message);
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// The captured template N3 body (without the transport header).
        /// </summary>
        internal static byte[] TemplateBody
        {
            get
            {
                EnsureInitialized();
                return templateBody;
            }
        }

        private static QuestInfo CloneArchetype(QuestInfo[] archetypes, int index)
        {
            // DecodeTemplate gives a deep copy of all five; pick by index from a fresh decode so each offer
            // is an independent object graph (no shared QuestActions arrays across slots).
            QuestInfo[] fresh = DecodeTemplate().QuestInfos;
            int safe = index;
            if (fresh == null || fresh.Length == 0)
            {
                return archetypes != null && archetypes.Length > 0 ? archetypes[0] : null;
            }

            if (safe < 0)
            {
                safe = 0;
            }

            if (safe >= fresh.Length)
            {
                safe = fresh.Length - 1;
            }

            return fresh[safe];
        }

        private static byte[] PickLibraryBody(Random rng)
        {
            if (libraryBodies == null || libraryBodies.Length == 0)
            {
                return templateBody;
            }

            return libraryBodies[rng.Next(libraryBodies.Length)];
        }

        private static QuestAlternativeMessage DecodeRollBody(byte[] body)
        {
            EnsureInitialized();
            var message = (QuestAlternativeMessage)Deserialize(body);
            RestoreStringTerminators(message);
            return message;
        }

        private static void ApplyPoolLocation(
            QuestInfo offer,
            Random rng,
            int[] usedSpotIndexes,
            int slot,
            int[] allowedSpotIndexes,
            bool preferDistinctPlayfields)
        {
            if (offer == null || offer.QuestActions == null || offer.QuestActions.Length == 0
                || MissionLocationPool.Spots == null || MissionLocationPool.Spots.Length == 0)
            {
                return;
            }

            QuestActionList dst = offer.QuestActions[0];
            if (dst == null)
            {
                return;
            }

            int spotIndex = PickDistinctSpotIndex(
                rng,
                usedSpotIndexes,
                slot,
                allowedSpotIndexes,
                preferDistinctPlayfields);
            usedSpotIndexes[slot] = spotIndex;
            MissionLocationPool.Spot spot = MissionLocationPool.Spots[spotIndex];

            dst.Playfield = new Identity { Type = IdentityType.Playfield2, Instance = spot.Playfield };
            dst.Unknown18 = spot.EntranceLow;
            dst.Unknown19 = spot.EntranceHigh;
            dst.X = spot.X;
            dst.Y = spot.Y;
            dst.Z = spot.Z;
        }

        private static int PickDistinctSpotIndex(
            Random rng,
            int[] usedSpotIndexes,
            int slot,
            int[] allowedSpotIndexes,
            bool preferDistinctPlayfields)
        {
            int count = MissionLocationPool.Spots.Length;
            int[] allowed = allowedSpotIndexes;
            if (allowed == null || allowed.Length == 0)
            {
                allowed = new int[count];
                for (int i = 0; i < count; i++)
                {
                    allowed[i] = i;
                }
            }

            for (int attempt = 0; attempt < 48; attempt++)
            {
                int candidate = allowed[rng.Next(allowed.Length)];
                bool taken = false;
                int candidatePf = MissionLocationPool.Spots[candidate].Playfield;
                for (int i = 0; i < slot; i++)
                {
                    if (usedSpotIndexes[i] < 0)
                    {
                        continue;
                    }

                    if (usedSpotIndexes[i] == candidate)
                    {
                        taken = true;
                        break;
                    }

                    // High-level rolls: spread across playfields. Low-level: same zone OK, different XYZ.
                    if (preferDistinctPlayfields
                        && MissionLocationPool.Spots[usedSpotIndexes[i]].Playfield == candidatePf)
                    {
                        taken = true;
                        break;
                    }
                }

                if (!taken)
                {
                    return candidate;
                }
            }

            return allowed[rng.Next(allowed.Length)];
        }

        private static int[] BuildAllowedSpotIndexes(
            MissionLocationSide poolSide,
            int characterLevel,
            int terminalPlayfieldId,
            float terminalX,
            float terminalZ)
        {
            int count = MissionLocationPool.Spots.Length;
            var sideMatched = new System.Collections.Generic.List<int>(count);
            var sameZone = new System.Collections.Generic.List<int>(count);
            var nearCluster = new System.Collections.Generic.List<int>(count);
            var nearRing = new System.Collections.Generic.List<int>(count);
            var distanceMatched = new System.Collections.Generic.List<int>(count);

            double minDist = MinMissionDistanceMeters(characterLevel);
            double maxDist = MaxMissionDistanceMeters(characterLevel);

            for (int i = 0; i < count; i++)
            {
                MissionLocationPool.Spot spot = MissionLocationPool.Spots[i];
                if (!MissionLocationPool.IsSpotAllowedForTerminal(spot.Playfield, poolSide))
                {
                    continue;
                }

                sideMatched.Add(i);

                int nearRank = MissionLocationPool.NearClusterRank(terminalPlayfieldId, spot.Playfield);
                if (nearRank == 0)
                {
                    sameZone.Add(i);
                }
                else if (nearRank == 1)
                {
                    nearCluster.Add(i);
                }
                else if (nearRank == 2)
                {
                    nearRing.Add(i);
                }

                double dist = EstimateSpotDistance(spot, terminalPlayfieldId, terminalX, terminalZ);
                if (dist >= minDist && dist <= maxDist)
                {
                    distanceMatched.Add(i);
                }
            }

            // Low-level: stay in the terminal's zone (same PF), else city→near outdoor cluster.
            if (characterLevel <= 40)
            {
                if (sameZone.Count > 0)
                {
                    MissionDiagnostics.Log(
                        "ROLL-DIST lvl={0} mode=sameZone hits={1} termPf={2}",
                        characterLevel,
                        sameZone.Count,
                        terminalPlayfieldId);
                    return sameZone.ToArray();
                }

                if (nearCluster.Count > 0)
                {
                    MissionDiagnostics.Log(
                        "ROLL-DIST lvl={0} mode=nearCluster hits={1} termPf={2}",
                        characterLevel,
                        nearCluster.Count,
                        terminalPlayfieldId);
                    return nearCluster.ToArray();
                }

                if (nearRing.Count > 0)
                {
                    MissionDiagnostics.Log(
                        "ROLL-DIST lvl={0} mode=nearRing hits={1} termPf={2}",
                        characterLevel,
                        nearRing.Count,
                        terminalPlayfieldId);
                    return nearRing.ToArray();
                }
            }
            else if (characterLevel <= 80)
            {
                var mid = new System.Collections.Generic.List<int>(sameZone.Count + nearCluster.Count + nearRing.Count);
                mid.AddRange(sameZone);
                mid.AddRange(nearCluster);
                mid.AddRange(nearRing);
                if (mid.Count > 0)
                {
                    MissionDiagnostics.Log(
                        "ROLL-DIST lvl={0} mode=midNear hits={1} termPf={2}",
                        characterLevel,
                        mid.Count,
                        terminalPlayfieldId);
                    return mid.ToArray();
                }
            }

            if (distanceMatched.Count > 0)
            {
                MissionDiagnostics.Log(
                    "ROLL-DIST lvl={0} min={1:F0} max={2:F0} sideHits={3} distHits={4}",
                    characterLevel,
                    minDist,
                    maxDist,
                    sideMatched.Count,
                    distanceMatched.Count);
                return distanceMatched.ToArray();
            }

            if (sideMatched.Count > 0)
            {
                MissionDiagnostics.Log(
                    "ROLL-DIST-FALLBACK lvl={0} min={1:F0} max={2:F0} usingSideHits={3}",
                    characterLevel,
                    minDist,
                    maxDist,
                    sideMatched.Count);
                return sideMatched.ToArray();
            }

            var all = new int[count];
            for (int i = 0; i < count; i++)
            {
                all[i] = i;
            }

            return all;
        }

        /// <summary>
        /// Low-level characters stay near the terminal; higher levels push markers farther out.
        /// </summary>
        private static double MaxMissionDistanceMeters(int characterLevel)
        {
            int level = characterLevel < 1 ? 1 : characterLevel;
            return 900.0 + (level * 35.0);
        }

        private static double MinMissionDistanceMeters(int characterLevel)
        {
            // Low levels: no minimum — stay as close as possible.
            if (characterLevel <= 40)
            {
                return 0.0;
            }

            int level = characterLevel < 1 ? 1 : characterLevel;
            double min = (level * 12.0) - 200.0;
            return min < 400 ? 400 : min;
        }

        private static double EstimateSpotDistance(
            MissionLocationPool.Spot spot,
            int terminalPlayfieldId,
            float terminalX,
            float terminalZ)
        {
            if (spot == null)
            {
                return 99999.0;
            }

            if (terminalPlayfieldId != 0 && spot.Playfield == terminalPlayfieldId)
            {
                double dx = spot.X - terminalX;
                double dz = spot.Z - terminalZ;
                return Math.Sqrt((dx * dx) + (dz * dz));
            }

            return MissionLocationPool.ApproxTravelMeters(terminalPlayfieldId, spot.Playfield);
        }

        /// <summary>
        /// Retypes a captured offer shell by MissionIconId only.
        /// Do not rewrite ShortInfo / CharInfos / Info — those lengths are capture-aligned and mutating
        /// them has broken client roll parsing ("rolling mission not work at all").
        /// Invented kill/find names are applied at instance spawn instead.
        /// </summary>
        private static void ApplyMissionType(QuestInfo offer, MissionRollType type, Random rng)
        {
            offer.MissionIconId = MissionTypeCatalog.IconId(type, 0);
        }

        private static void ApplyMaliReward(QuestInfo offer, int missionQuality, Random rng, MissionRollType type)
        {
            QuestItemShort reward;
            string itemName;
            bool isNano;
            if (!MissionRewardCatalog.TryPickReward(missionQuality, rng, out reward, out itemName, out isNano))
            {
                MissionDiagnostics.Log(
                    "ROLL-REWARD-MISS ql={0} type={1} catalogItems={2} err={3}",
                    missionQuality,
                    MissionTypeCatalog.TypeName(type),
                    MissionRewardCatalog.ItemCount,
                    MissionRewardCatalog.LastLoadError ?? string.Empty);
                return;
            }

            // Keep the captured ItemRewards array length when possible — only overwrite the first slot's
            // ids/QL so the X3F1 count stays identical to the template for that archetype.
            if (offer.ItemRewards != null && offer.ItemRewards.Length > 0)
            {
                offer.ItemRewards[0].LowId = reward.LowId;
                offer.ItemRewards[0].HighId = reward.HighId;
                offer.ItemRewards[0].Quality = reward.Quality;
            }
            else
            {
                offer.ItemRewards = new[] { reward };
            }

            MissionDiagnostics.Log(
                "ROLL-REWARD ql={0} type={1} nano={2} low={3} high={4} rewardQl={5} name={6}",
                missionQuality,
                MissionTypeCatalog.TypeName(type),
                isNano,
                reward.LowId,
                reward.HighId,
                reward.Quality,
                itemName ?? string.Empty);
        }

        private static void ApplyMoneyExperienceSlider(QuestInfo offer, byte moneyExperienceSlider, int missionQuality)
        {
            int slider;
            if (moneyExperienceSlider <= 100)
            {
                slider = moneyExperienceSlider;
            }
            else
            {
                slider = 50 + (unchecked((sbyte)moneyExperienceSlider) / 2);
                if (slider < 0)
                {
                    slider = 0;
                }

                if (slider > 100)
                {
                    slider = 100;
                }
            }

            int ql = missionQuality > 0 ? missionQuality : 1;
            int baseCash = BaseCashForMissionQl(ql);
            int baseXp = BaseXpForMissionQl(ql);

            offer.CashReward = Math.Max(0, baseCash * (150 - slider) / 100);
            // Live capture 20260724-144103: cash-heavy slider can yield 0 XP on the description/finish line.
            offer.ExperienceReward = Math.Max(0, baseXp * (50 + slider) / 100);
            if (offer.CashReward <= 0 && offer.ExperienceReward <= 0)
            {
                offer.CashReward = Math.Max(1, baseCash / 2);
            }

            // Absolute safety vs any future template regression.
            if (offer.CashReward > 150000)
            {
                offer.CashReward = baseCash;
            }

            if (offer.ExperienceReward > 2500000)
            {
                offer.ExperienceReward = baseXp;
            }
        }

        /// <summary>Balanced (slider mid) cash for a mission QL.</summary>
        internal static int BaseCashForMissionQl(int missionQuality)
        {
            int ql = missionQuality > 0 ? missionQuality : 1;
            return Math.Max(25, ql * ql * 2);
        }

        /// <summary>Balanced (slider mid) XP for a mission QL.</summary>
        internal static int BaseXpForMissionQl(int missionQuality)
        {
            int ql = missionQuality > 0 ? missionQuality : 1;
            return Math.Max(50, ql * ql * 50);
        }

        private static int NextQuestInstance()
        {
            int instance = Interlocked.Increment(ref questInstanceSeed) & 0x7fffffff;
            return instance == 0 ? NextQuestInstance() : instance;
        }

        private static void RestoreStringTerminators(QuestAlternativeMessage message)
        {
            if (message.QuestInfos == null)
            {
                return;
            }

            foreach (QuestInfo info in message.QuestInfos)
            {
                if (info == null)
                {
                    continue;
                }

                if (info.Info != null && !info.Info.EndsWith("\0", StringComparison.Ordinal))
                {
                    info.Info += '\0';
                }
            }
        }

        private static Identity RetargetTerminal(Identity value, Identity from, Identity to)
        {
            if ((int)value.Type == MissionTerminalIdentityTypeRaw && value.Instance == from.Instance)
            {
                value.Type = to.Type;
                value.Instance = to.Instance;
            }

            return value;
        }

        private static void EnsureInitialized()
        {
            if (questAlternativeSerializer != null)
            {
                return;
            }

            lock (InitLock)
            {
                if (questAlternativeSerializer != null)
                {
                    return;
                }

                var builder = new SerializerResolverBuilder<MessageBody>();
                SerializerResolver resolver = builder.Build();
                ISerializer serializer = resolver.GetSerializer(typeof(QuestAlternativeMessage));

                byte[] full = HexToBytes(MissionRollCaptureTemplate.CapturedPacketHex);
                var body = new byte[full.Length - MissionRollCaptureTemplate.TransportHeaderLength];
                Array.Copy(full, MissionRollCaptureTemplate.TransportHeaderLength, body, 0, body.Length);

                serializerResolver = resolver;
                questAlternativeSerializer = serializer;
                templateBody = body;

                string[] hexRolls = MissionRollCaptureLibrary.CapturedRollBodiesHex;
                var loaded = new byte[hexRolls.Length][];
                for (int i = 0; i < hexRolls.Length; i++)
                {
                    loaded[i] = HexToBytes(hexRolls[i]);
                }

                libraryBodies = loaded;
                MissionDiagnostics.Log(
                    "ROLL-LIBRARY loaded={0} fallbackTemplateBytes={1}",
                    libraryBodies.Length,
                    templateBody.Length);
            }
        }

        private static QuestAlternativeMessage Deserialize(byte[] body)
        {
            using (var memoryStream = new MemoryStream(body))
            using (var reader = new StreamReader(memoryStream))
            {
                return (QuestAlternativeMessage)questAlternativeSerializer.Deserialize(
                    reader,
                    new SerializationContext(serializerResolver));
            }
        }

        private static byte[] HexToBytes(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(
                    hex.Substring(i * 2, 2),
                    System.Globalization.NumberStyles.HexNumber);
            }

            return bytes;
        }
    }
}
