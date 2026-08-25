namespace AORebirth.Core.Playfields
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    #endregion

    /// <summary>
    /// Normalized PF4582 placement evidence. These records do not define NPC behavior.
    /// </summary>
    internal sealed class IccShuttleportPlacementRecord
    {
        internal IccShuttleportPlacementRecord(
            int npcId,
            int templateHash,
            string templateTag,
            string sourceName,
            string bossMods,
            int spawnHash,
            float positionX,
            float positionY,
            float positionZ,
            float spawnRadius,
            float spawnAngle,
            float spawnAngleW,
            int minLevel,
            int maxLevel,
            int spawnChance,
            int extraData,
            int exFlags,
            int candidateRespawnTime,
            int[] spawnUnknowns,
            string spawnPointFlags,
            string sourceNameInterpretation,
            bool placementKnown,
            string runtimeProfile,
            bool behaviorProven,
            bool runtimeEligible,
            bool runtimeActive)
        {
            this.NpcId = npcId;
            this.TemplateHash = templateHash;
            this.TemplateTag = templateTag;
            this.SourceName = sourceName;
            this.BossMods = bossMods;
            this.SpawnHash = spawnHash;
            this.PositionX = positionX;
            this.PositionY = positionY;
            this.PositionZ = positionZ;
            this.SpawnRadius = spawnRadius;
            this.SpawnAngle = spawnAngle;
            this.SpawnAngleW = spawnAngleW;
            this.MinLevel = minLevel;
            this.MaxLevel = maxLevel;
            this.SpawnChance = spawnChance;
            this.ExtraData = extraData;
            this.ExFlags = exFlags;
            this.CandidateRespawnTime = candidateRespawnTime;
            this.SpawnUnknowns = spawnUnknowns;
            this.SpawnPointFlags = spawnPointFlags;
            this.SourceNameInterpretation = sourceNameInterpretation;
            this.PlacementKnown = placementKnown;
            this.RuntimeProfile = runtimeProfile;
            this.BehaviorProven = behaviorProven;
            this.RuntimeEligible = runtimeEligible;
            this.RuntimeActive = runtimeActive;
        }

        internal int NpcId { get; private set; }

        internal int TemplateHash { get; private set; }

        internal string TemplateTag { get; private set; }

        internal string SourceName { get; private set; }

        internal string BossMods { get; private set; }

        internal int SpawnHash { get; private set; }

        internal float PositionX { get; private set; }

        internal float PositionY { get; private set; }

        internal float PositionZ { get; private set; }

        internal float SpawnRadius { get; private set; }

        internal float SpawnAngle { get; private set; }

        internal float SpawnAngleW { get; private set; }

        internal int MinLevel { get; private set; }

        internal int MaxLevel { get; private set; }

        internal int SpawnChance { get; private set; }

        internal int ExtraData { get; private set; }

        internal int ExFlags { get; private set; }

        internal int CandidateRespawnTime { get; private set; }

        internal int[] SpawnUnknowns { get; private set; }

        internal string SpawnPointFlags { get; private set; }

        internal string SourceNameInterpretation { get; private set; }

        internal bool PlacementKnown { get; private set; }

        internal bool TemplateMapped
        {
            get { return !string.IsNullOrEmpty(this.RuntimeProfile); }
        }

        internal string RuntimeProfile { get; private set; }

        internal bool BehaviorProven { get; private set; }

        internal bool RuntimeEligible { get; private set; }

        internal bool RuntimeActive { get; private set; }

        internal string FlagInterpretation
        {
            get { return "Unresolved"; }
        }

        internal string CandidateRespawnInterpretation
        {
            get { return "Unresolved"; }
        }
    }

    internal static partial class IccShuttleportPlacementCatalog
    {
        private static readonly IccShuttleportPlacementRecord[] Placements = CreatePlacements();

        private static readonly Dictionary<int, IccShuttleportPlacementRecord> PlacementsByNpcId =
            BuildPlacementIndex();

        static IccShuttleportPlacementCatalog()
        {
            ValidateOrThrow();
        }

        internal static int Count
        {
            get { return Placements.Length; }
        }

        internal static bool TryGetRuntimeActive(
            int npcId,
            out IccShuttleportPlacementRecord placement,
            out string failure)
        {
            if (!PlacementsByNpcId.TryGetValue(npcId, out placement))
            {
                failure = "authoritative placement is missing";
                return false;
            }

            if (!placement.TemplateMapped)
            {
                failure = "template hash is unresolved";
                return false;
            }

            if (!placement.BehaviorProven)
            {
                failure = "required behavior is unresolved";
                return false;
            }

            if (!placement.RuntimeEligible || !placement.RuntimeActive)
            {
                failure = "placement is not runtime active";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static Dictionary<int, IccShuttleportPlacementRecord> BuildPlacementIndex()
        {
            var index = new Dictionary<int, IccShuttleportPlacementRecord>();
            foreach (IccShuttleportPlacementRecord placement in Placements)
            {
                if (index.ContainsKey(placement.NpcId))
                {
                    throw new InvalidOperationException(
                        "Duplicate authoritative PF4582 NpcId " + placement.NpcId);
                }

                index.Add(placement.NpcId, placement);
            }

            return index;
        }

        private static void ValidateOrThrow()
        {
            if (Placements.Length != SourcePlacementCount)
            {
                throw new InvalidOperationException(
                    "PF4582 placement count mismatch expected=" + SourcePlacementCount
                    + " actual=" + Placements.Length);
            }

            int runtimeEligible = 0;
            int behaviorProven = 0;
            int runtimeActive = 0;
            foreach (IccShuttleportPlacementRecord placement in Placements)
            {
                if (!placement.PlacementKnown)
                {
                    throw new InvalidOperationException(
                        "PF4582 placement lacks placement authority NpcId=" + placement.NpcId);
                }

                if (placement.SpawnUnknowns == null || placement.SpawnUnknowns.Length != 4)
                {
                    throw new InvalidOperationException(
                        "PF4582 placement lost SpawnUnknowns NpcId=" + placement.NpcId);
                }

                if (placement.RuntimeEligible
                    && (!placement.TemplateMapped || !placement.BehaviorProven))
                {
                    throw new InvalidOperationException(
                        "PF4582 placement became eligible without mapped/proven behavior NpcId="
                        + placement.NpcId);
                }

                if (placement.RuntimeActive && !placement.RuntimeEligible)
                {
                    throw new InvalidOperationException(
                        "PF4582 placement became active while ineligible NpcId="
                        + placement.NpcId);
                }

                if (placement.RuntimeEligible)
                {
                    runtimeEligible++;
                }

                if (placement.BehaviorProven)
                {
                    behaviorProven++;
                }

                if (placement.RuntimeActive)
                {
                    runtimeActive++;
                }
            }

            if (behaviorProven != BehaviorProvenPlacementCount)
            {
                throw new InvalidOperationException(
                    "PF4582 behavior-proven count mismatch expected="
                    + BehaviorProvenPlacementCount + " actual=" + behaviorProven);
            }

            if (runtimeEligible != RuntimeEligiblePlacementCount)
            {
                throw new InvalidOperationException(
                    "PF4582 runtime eligibility count mismatch expected="
                    + RuntimeEligiblePlacementCount + " actual=" + runtimeEligible);
            }

            if (runtimeActive != RuntimeActivePlacementCount)
            {
                throw new InvalidOperationException(
                    "PF4582 runtime-active count mismatch expected="
                    + RuntimeActivePlacementCount + " actual=" + runtimeActive);
            }
        }
    }
}
