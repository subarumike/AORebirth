namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
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
    /// Selects capture-backed five-offer type cohorts, then clones independently compatible captured
    /// offer shells. Mutable location, quality, identity and reward fields are applied before structured
    /// text is regenerated, while a materially unchanged captured combination keeps its exact text.
    /// </summary>
    internal static class MissionRollService
    {
        // Mission-terminal identity type as observed on the wire. The repo enum value
        // (IdentityType.MissionTerminal = 0x0000DCA1) does not match live captures, so compare raw.
        private const int MissionTerminalIdentityTypeRaw = 0x0000DAC1;

        private const int MissionIdentityTypeRaw = 0x0000DAC3;

        private const int ClientClockBaseSeconds = 1201445827;

        private const int MissionOfferLifetimeSeconds = 48 * 60 * 60;

        private static readonly object InitLock = new object();

        private static volatile bool initialized;

        private static SerializerResolver serializerResolver;

        private static ISerializer questAlternativeSerializer;

        private static byte[] templateBody;

        private static byte[][] libraryBodies;

        private static CapturedOfferReference[] capturedOfferReferences;

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
            int terminalPlayfieldId,
            float terminalX,
            float terminalZ,
            MissionLocationSide characterSide,
            int clientClockNowSeconds)
        {
            EnsureInitialized();

            int missionQuality;
            if (request == null
                || !MissionLevelTable.TryGetMissionQuality(characterLevel, request.LevelSlider, out missionQuality))
            {
                throw new ArgumentOutOfRangeException(
                    "request",
                    "Mission roll contains an unsupported difficulty detent.");
            }

            MissionSliderProfile sliders;
            string sliderError;
            if (!MissionSliderProfile.TryCreate(request, out sliders, out sliderError))
            {
                throw new ArgumentOutOfRangeException("request", sliderError);
            }

            var rng = new Random(unchecked(Environment.TickCount * 397) ^ character.Instance ^ missionQuality);
            return BuildRollResponseCore(
                request,
                character,
                characterLevel,
                terminalPlayfieldId,
                terminalX,
                terminalZ,
                characterSide,
                missionQuality,
                sliders,
                rng,
                unchecked((int)(uint)Environment.TickCount),
                clientClockNowSeconds,
                NextQuestInstance);
        }

        internal static QuestAlternativeMessage BuildRollResponseDeterministic(
            QuestAlternativeMessage request,
            Identity character,
            int characterLevel,
            int terminalPlayfieldId,
            float terminalX,
            float terminalZ,
            MissionLocationSide characterSide,
            int seed,
            int responseNonce,
            int firstQuestInstance,
            int clientClockNowSeconds)
        {
            EnsureInitialized();

            int missionQuality;
            if (request == null
                || !MissionLevelTable.TryGetMissionQuality(characterLevel, request.LevelSlider, out missionQuality))
            {
                throw new ArgumentOutOfRangeException(
                    "request",
                    "Mission roll contains an unsupported difficulty detent.");
            }

            MissionSliderProfile sliders;
            string sliderError;
            if (!MissionSliderProfile.TryCreate(request, out sliders, out sliderError))
            {
                throw new ArgumentOutOfRangeException("request", sliderError);
            }

            int nextQuestInstance = firstQuestInstance;
            return BuildRollResponseCore(
                request,
                character,
                characterLevel,
                terminalPlayfieldId,
                terminalX,
                terminalZ,
                characterSide,
                missionQuality,
                sliders,
                new Random(seed),
                responseNonce,
                clientClockNowSeconds,
                delegate
                {
                    int allocated = nextQuestInstance;
                    nextQuestInstance++;
                    return allocated;
                });
        }

        private static QuestAlternativeMessage BuildRollResponseCore(
            QuestAlternativeMessage request,
            Identity character,
            int characterLevel,
            int terminalPlayfieldId,
            float terminalX,
            float terminalZ,
            MissionLocationSide characterSide,
            int missionQuality,
            MissionSliderProfile sliders,
            Random rng,
            int responseNonce,
            int clientClockNowSeconds,
            Func<int> questIdAllocator)
        {
            int effectiveCharacterLevel = MissionLevelTable.ClampCharacterLevel(characterLevel);
            MissionLocationSide citySide;
            MissionLocationSide poolSide = MissionLocationPool.TryGetCityAffiliation(
                                                   terminalPlayfieldId,
                                                   out citySide)
                                               ? citySide
                                               : characterSide;

            int capturedResponseHeaderIndex =
                (int)((uint)responseNonce % (uint)CapturedRollCount);
            QuestAlternativeMessage response = DecodeCapturedRoll(capturedResponseHeaderIndex);
            Identity terminal = request.MissionTerminalIdentity;
            response.Identity = character;
            response.MissionTerminalIdentity = terminal;

            // QuestAlternative is direction-sensitive. In client requests these
            // fields carry the visible slider values. Official server responses
            // instead carry an opaque response envelope whose bytes vary between
            // captured rolls. Echoing request sliders here produced a structurally
            // valid packet that the live client silently rejected. Preserve one
            // complete captured response envelope atomically and retarget only
            // identities and generated offers below.

            MissionRollType[] typeMix = MissionRollEvidenceCatalog.SelectTypeMix(
                effectiveCharacterLevel,
                request.LevelSlider,
                missionQuality,
                sliders,
                rng);
            var offers = new QuestInfo[typeMix.Length];
            var usedSpotIndexes = new int[offers.Length];
            for (int i = 0; i < usedSpotIndexes.Length; i++)
            {
                usedSpotIndexes[i] = -1;
            }

            int[] allowedSpotIndexes = BuildAllowedSpotIndexes(
                poolSide,
                effectiveCharacterLevel,
                terminalPlayfieldId,
                terminalX,
                terminalZ);
            bool preferDistinctPlayfields = effectiveCharacterLevel > 40;

            MissionDiagnostics.Log(
                "ROLL-SIDE terminalPf={0} poolSide={1} charSide={2} charLvl={3} termXZ=({4:F0},{5:F0}) sliderEvidence={6}",
                terminalPlayfieldId,
                poolSide,
                characterSide,
                effectiveCharacterLevel,
                terminalX,
                terminalZ,
                sliders.EvidenceProfile);

            for (int i = 0; i < offers.Length; i++)
            {
                MissionRollType type = typeMix[i];
                Identity capturedTerminal;
                QuestInfo offer = PickCompatibleCapturedOffer(type, sliders, rng, out capturedTerminal);
                if (offer == null)
                {
                    throw new InvalidOperationException(
                        "No compatible captured mission template exists for "
                        + MissionTypeCatalog.TypeName(type)
                        + ".");
                }

                MissionOfferDescriptor descriptor;
                string compatibilityError;
                if (!MissionOfferCompatibility.TryDescribeCaptured(
                        offer,
                        capturedTerminal,
                        out descriptor,
                        out compatibilityError)
                    || !MissionOfferCompatibility.IsCompatibleWithSliders(descriptor, sliders))
                {
                    throw new InvalidOperationException(
                        "Captured mission template failed compatibility validation: "
                        + (compatibilityError ?? MissionTypeCatalog.TypeName(type)));
                }

                MissionOfferTextBuilder.Snapshot originalText = MissionOfferTextBuilder.Capture(offer);
                ApplyPoolLocation(
                    offer,
                    rng,
                    usedSpotIndexes,
                    i,
                    allowedSpotIndexes,
                    preferDistinctPlayfields);

                offer.QuestIdentity = new Identity
                                      {
                                          Type = (IdentityType)MissionIdentityTypeRaw,
                                          Instance = questIdAllocator()
                                      };
                offer.Unknown5 = RetargetTerminal(offer.Unknown5, capturedTerminal, terminal);
                offer.Unknown14 = RetargetTerminal(offer.Unknown14, capturedTerminal, terminal);
                offer.Unknown23 = RetargetTerminal(offer.Unknown23, capturedTerminal, terminal);
                if (offer.QuestActions != null
                    && offer.QuestActions.Length > 0
                    && offer.QuestActions[0] != null)
                {
                    offer.QuestActions[0].Unknown1 = RetargetTerminal(
                        offer.QuestActions[0].Unknown1,
                        capturedTerminal,
                        terminal);
                }
                offer.Quality = missionQuality;
                // Official five-offer pulls carry one common deadline exactly
                // 48 hours after the roll. This server's GameTime packet anchors
                // that clock to 2008, so wall-clock Unix seconds are invalid here.
                offer.QuestActions[0].UnknownHash15 = checked(
                    clientClockNowSeconds + MissionOfferLifetimeSeconds);

                int markerPf = 0;
                float markerX = 0;
                float markerZ = 0;
                if (offer.QuestActions != null
                    && offer.QuestActions.Length > 0
                    && offer.QuestActions[0] != null)
                {
                    markerPf = offer.QuestActions[0].Playfield.Instance;
                    markerX = offer.QuestActions[0].X;
                    markerZ = offer.QuestActions[0].Z;
                }

                MissionRewardEvidenceModel.Apply(
                    offer,
                    type,
                    effectiveCharacterLevel,
                    request.LevelSlider,
                    missionQuality,
                    sliders,
                    markerPf,
                    rng);
                ApplyMaliReward(offer, missionQuality, rng, type);
                MissionOfferTextBuilder.Apply(offer, descriptor, originalText);

                MissionOfferDescriptor generatedDescriptor;
                if (!MissionOfferCompatibility.TryValidateGenerated(
                        offer,
                        descriptor,
                        sliders,
                        terminal,
                        out generatedDescriptor,
                        out compatibilityError)
                    || generatedDescriptor.Type != type)
                {
                    throw new InvalidOperationException(
                        "Generated mission offer failed compatibility validation: "
                        + (compatibilityError ?? MissionTypeCatalog.TypeName(type)));
                }

                offers[i] = offer;
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
                    offer.ItemRewards != null && offer.ItemRewards.Length > 0
                        ? offer.ItemRewards[0].LowId
                        : 0,
                    offer.ItemRewards != null && offer.ItemRewards.Length > 0
                        ? offer.ItemRewards[0].Quality
                        : 0);
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
                return (byte[])templateBody.Clone();
            }
        }

        internal static int CapturedRollCount
        {
            get
            {
                EnsureInitialized();
                return libraryBodies.Length;
            }
        }

        internal static byte[] CapturedRollBody(int index)
        {
            EnsureInitialized();
            if (index < 0 || index >= libraryBodies.Length)
            {
                throw new ArgumentOutOfRangeException("index");
            }

            return (byte[])libraryBodies[index].Clone();
        }

        internal static QuestAlternativeMessage DecodeCapturedRoll(int index)
        {
            return DecodeRollBody(CapturedRollBody(index));
        }

        private static QuestInfo PickCompatibleCapturedOffer(
            MissionRollType type,
            MissionSliderProfile sliders,
            Random rng,
            out Identity capturedTerminal)
        {
            capturedTerminal = new Identity();
            var candidates = new List<CapturedOfferReference>();
            for (int i = 0; i < capturedOfferReferences.Length; i++)
            {
                CapturedOfferReference candidate = capturedOfferReferences[i];
                if (candidate.Type == type)
                {
                    candidates.Add(candidate);
                }
            }

            while (candidates.Count > 0)
            {
                int selectedIndex = rng.Next(candidates.Count);
                CapturedOfferReference selected = candidates[selectedIndex];
                candidates.RemoveAt(selectedIndex);

                QuestAlternativeMessage roll = DecodeRollBody(libraryBodies[selected.BodyIndex]);
                if (roll.QuestInfos == null
                    || selected.OfferIndex < 0
                    || selected.OfferIndex >= roll.QuestInfos.Length)
                {
                    continue;
                }

                QuestInfo offer = roll.QuestInfos[selected.OfferIndex];
                MissionOfferDescriptor descriptor;
                string error;
                if (!MissionOfferCompatibility.TryDescribeCaptured(
                        offer,
                        roll.MissionTerminalIdentity,
                        out descriptor,
                        out error)
                    || descriptor.Type != type
                    || !MissionOfferCompatibility.IsCompatibleWithSliders(descriptor, sliders))
                {
                    continue;
                }

                capturedTerminal = roll.MissionTerminalIdentity;
                return offer;
            }

            return null;
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
                bool samePlayfield =
                    terminalPlayfieldId != 0 && spot.Playfield == terminalPlayfieldId;
                if (!samePlayfield
                    && !MissionLocationPool.IsSpotAllowedForTerminal(spot.Playfield, poolSide))
                {
                    continue;
                }

                // A usable terminal can sit in a neutral hub whose outdoor marker playfield has
                // a sided classification (ICC PF 655 is the live example). A same-playfield
                // marker cannot cross-route the character and is the safest low-level result,
                // so marker affiliation must not discard it before same-zone selection.
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
                throw new InvalidOperationException(
                    "No QL-aware mission item reward is available for QL "
                    + missionQuality
                    + ".");
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

        /// <summary>
        /// Legacy completion fallback retained for <see cref="MissionCompleteService"/>.
        /// Generated roll rewards do not use this approximation.
        /// </summary>
        internal static int BaseCashForMissionQl(int missionQuality)
        {
            int ql = missionQuality > 0 ? missionQuality : 1;
            return Math.Max(25, ql * ql * 2);
        }

        /// <summary>
        /// Legacy completion fallback retained for <see cref="MissionCompleteService"/>.
        /// Generated roll rewards do not use this approximation.
        /// </summary>
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

        internal static int ResolveClientClockNowSeconds(
            DateTime lastGameTimeSyncUtc,
            DateTime utcNow)
        {
            double secondsSinceSync = (utcNow - lastGameTimeSyncUtc).TotalSeconds;
            if (secondsSinceSync < 0)
            {
                secondsSinceSync = 0;
            }

            return checked(ClientClockBaseSeconds + (int)secondsSinceSync);
        }

        internal static int ResolveClientExpirySeconds(
            DateTime lastGameTimeSyncUtc,
            DateTime utcNow,
            DateTime expiryUtc)
        {
            double remainingSeconds = (expiryUtc - utcNow).TotalSeconds;
            if (remainingSeconds <= 0)
            {
                return 0;
            }

            return checked(
                ResolveClientClockNowSeconds(lastGameTimeSyncUtc, utcNow)
                + (int)Math.Ceiling(remainingSeconds));
        }

        internal static string IntToFixedBinaryString(int value)
        {
            return new string(
                new[]
                {
                    (char)(byte)(value >> 24),
                    (char)(byte)(value >> 16),
                    (char)(byte)(value >> 8),
                    (char)(byte)value
                });
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
            if (initialized)
            {
                return;
            }

            lock (InitLock)
            {
                if (initialized)
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
                var compatibleOffers = new List<CapturedOfferReference>();
                for (int bodyIndex = 0; bodyIndex < libraryBodies.Length; bodyIndex++)
                {
                    var roll = (QuestAlternativeMessage)Deserialize(libraryBodies[bodyIndex]);
                    RestoreStringTerminators(roll);
                    if (roll.QuestInfos == null)
                    {
                        continue;
                    }

                    for (int offerIndex = 0; offerIndex < roll.QuestInfos.Length; offerIndex++)
                    {
                        MissionOfferDescriptor descriptor;
                        string error;
                        if (!MissionOfferCompatibility.TryDescribeCaptured(
                                roll.QuestInfos[offerIndex],
                                roll.MissionTerminalIdentity,
                                out descriptor,
                                out error))
                        {
                            MissionDiagnostics.Log(
                                "ROLL-TEMPLATE-REJECT body={0} offer={1} reason={2}",
                                bodyIndex,
                                offerIndex,
                                error ?? string.Empty);
                            continue;
                        }

                        compatibleOffers.Add(
                            new CapturedOfferReference(bodyIndex, offerIndex, descriptor.Type));
                    }
                }

                capturedOfferReferences = compatibleOffers.ToArray();
                initialized = true;
                MissionDiagnostics.Log(
                    "ROLL-LIBRARY loaded={0} compatibleOffers={1} fallbackTemplateBytes={2}",
                    libraryBodies.Length,
                    capturedOfferReferences.Length,
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

        private sealed class CapturedOfferReference
        {
            internal readonly int BodyIndex;
            internal readonly int OfferIndex;
            internal readonly MissionRollType Type;

            internal CapturedOfferReference(int bodyIndex, int offerIndex, MissionRollType type)
            {
                BodyIndex = bodyIndex;
                OfferIndex = offerIndex;
                Type = type;
            }
        }
    }
}
