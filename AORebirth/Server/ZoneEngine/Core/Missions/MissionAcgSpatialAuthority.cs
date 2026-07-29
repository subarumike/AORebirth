namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    #endregion

    /// <summary>
    /// Unvalidated coordinate input used by the envelope derivation boundary. Captured record
    /// constructors already reject non-finite values; this type keeps the derivation itself
    /// independently fail-closed and directly testable.
    /// </summary>
    internal sealed class MissionAcgSpatialPoint
    {
        internal MissionAcgSpatialPoint(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        internal float X { get; private set; }

        internal float Y { get; private set; }

        internal float Z { get; private set; }
    }

    /// <summary>
    /// Conservative axis-aligned spatial authority derived only from immutable captured
    /// coordinates. It does not represent walls, floors, rooms, connectivity, or navigation.
    /// </summary>
    internal sealed class MissionAcgSpatialEnvelope
    {
        internal const float CoordinateTolerance = 2.0f;

        internal const int MinimumDistinctCapturedCoordinates = 3;

        private MissionAcgSpatialEnvelope(
            string bundleId,
            float minimumX,
            float minimumY,
            float minimumZ,
            float maximumX,
            float maximumY,
            float maximumZ,
            int capturedCoordinateCount)
        {
            this.BundleId = bundleId;
            this.MinimumX = minimumX;
            this.MinimumY = minimumY;
            this.MinimumZ = minimumZ;
            this.MaximumX = maximumX;
            this.MaximumY = maximumY;
            this.MaximumZ = maximumZ;
            this.CapturedCoordinateCount = capturedCoordinateCount;
        }

        internal string BundleId { get; private set; }

        internal float MinimumX { get; private set; }

        internal float MinimumY { get; private set; }

        internal float MinimumZ { get; private set; }

        internal float MaximumX { get; private set; }

        internal float MaximumY { get; private set; }

        internal float MaximumZ { get; private set; }

        internal int CapturedCoordinateCount { get; private set; }

        internal double MaximumInternalDistance
        {
            get
            {
                double dx = this.MaximumX - this.MinimumX;
                double dy = this.MaximumY - this.MinimumY;
                double dz = this.MaximumZ - this.MinimumZ;
                return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            }
        }

        internal bool Contains(MissionAcgPointRecord point)
        {
            return point != null
                   && this.Contains(point.X, point.Y, point.Z);
        }

        internal bool Contains(float x, float y, float z)
        {
            return IsFinite(x)
                   && IsFinite(y)
                   && IsFinite(z)
                   && x >= this.MinimumX
                   && x <= this.MaximumX
                   && y >= this.MinimumY
                   && y <= this.MaximumY
                   && z >= this.MinimumZ
                   && z <= this.MaximumZ;
        }

        internal static bool TryDerive(
            MissionAcgLayoutBundle bundle,
            out MissionAcgSpatialEnvelope envelope,
            out string failure)
        {
            envelope = null;
            failure = string.Empty;
            if (bundle == null)
            {
                failure = "Layout bundle is required.";
                return false;
            }

            var points = new List<MissionAcgSpatialPoint>();
            Add(points, bundle.EntryPoint);
            if (bundle.Exit != null)
            {
                Add(points, bundle.Exit.Position);
            }

            for (int i = 0; i < bundle.Dynels.Count; i++)
            {
                Add(points, bundle.Dynels[i].Position);
            }

            for (int i = 0; i < bundle.NpcSlots.Count; i++)
            {
                Add(points, bundle.NpcSlots[i].Position);
            }

            for (int i = 0; i < bundle.ObjectiveSlots.Count; i++)
            {
                Add(points, bundle.ObjectiveSlots[i].Position);
            }

            return TryDerive(bundle.LayoutId, points, out envelope, out failure);
        }

        internal static bool TryDerive(
            string bundleId,
            IEnumerable<MissionAcgSpatialPoint> coordinates,
            out MissionAcgSpatialEnvelope envelope,
            out string failure)
        {
            envelope = null;
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(bundleId) || coordinates == null)
            {
                failure = "Bundle identity and captured coordinates are required.";
                return false;
            }

            float minimumX = float.MaxValue;
            float minimumY = float.MaxValue;
            float minimumZ = float.MaxValue;
            float maximumX = float.MinValue;
            float maximumY = float.MinValue;
            float maximumZ = float.MinValue;
            int count = 0;
            var distinct = new HashSet<string>(StringComparer.Ordinal);
            foreach (MissionAcgSpatialPoint point in coordinates)
            {
                if (point == null
                    || !IsFinite(point.X)
                    || !IsFinite(point.Y)
                    || !IsFinite(point.Z))
                {
                    failure = "Captured spatial coordinates are null or non-finite.";
                    return false;
                }

                distinct.Add(
                    point.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                    + ":"
                    + point.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                    + ":"
                    + point.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                minimumX = Math.Min(minimumX, point.X);
                minimumY = Math.Min(minimumY, point.Y);
                minimumZ = Math.Min(minimumZ, point.Z);
                maximumX = Math.Max(maximumX, point.X);
                maximumY = Math.Max(maximumY, point.Y);
                maximumZ = Math.Max(maximumZ, point.Z);
                count++;
            }

            if (count < MinimumDistinctCapturedCoordinates
                || distinct.Count < MinimumDistinctCapturedCoordinates)
            {
                failure = "Captured coordinate evidence is insufficient for a safe envelope.";
                return false;
            }

            minimumX -= CoordinateTolerance;
            minimumY -= CoordinateTolerance;
            minimumZ -= CoordinateTolerance;
            maximumX += CoordinateTolerance;
            maximumY += CoordinateTolerance;
            maximumZ += CoordinateTolerance;
            if (!IsFinite(minimumX)
                || !IsFinite(minimumY)
                || !IsFinite(minimumZ)
                || !IsFinite(maximumX)
                || !IsFinite(maximumY)
                || !IsFinite(maximumZ)
                || minimumX > maximumX
                || minimumY > maximumY
                || minimumZ > maximumZ)
            {
                failure = "Derived spatial envelope is invalid.";
                return false;
            }

            envelope =
                new MissionAcgSpatialEnvelope(
                    bundleId.Trim(),
                    minimumX,
                    minimumY,
                    minimumZ,
                    maximumX,
                    maximumY,
                    maximumZ,
                    count);
            return true;
        }

        private static void Add(
            ICollection<MissionAcgSpatialPoint> points,
            MissionAcgPointRecord point)
        {
            if (point == null)
            {
                points.Add(null);
                return;
            }

            points.Add(new MissionAcgSpatialPoint(point.X, point.Y, point.Z));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal enum MissionAcgLineOfSightDecision
    {
        AllowedRangeAndOwnershipOnly = 1,
        UnresolvedGeometryUnavailable = 2,
        DeniedInvalidSpatialOwnership = 3
    }

    /// <summary>
    /// Generated mission PF2s have no registered collision geometry. Range/ownership-only
    /// operations may proceed; any operation requiring authoritative geometry remains unresolved.
    /// </summary>
    internal static class MissionAcgLineOfSightPolicy
    {
        internal static MissionAcgLineOfSightDecision Evaluate(
            bool requiresAuthoritativeGeometry,
            bool exactInstanceOwnership,
            bool finiteSegment,
            bool segmentInsideEnvelope)
        {
            if (!exactInstanceOwnership || !finiteSegment || !segmentInsideEnvelope)
            {
                return MissionAcgLineOfSightDecision.DeniedInvalidSpatialOwnership;
            }

            return requiresAuthoritativeGeometry
                       ? MissionAcgLineOfSightDecision.UnresolvedGeometryUnavailable
                       : MissionAcgLineOfSightDecision.AllowedRangeAndOwnershipOnly;
        }
    }

    internal enum MissionAcgSpatialCleanupState
    {
        Active = 1,
        CleanupPending = 2,
        Completed = 3
    }

    /// <summary>
    /// Minimal durable Stage 6 state. The envelope is deliberately absent because it is derived
    /// deterministically from the immutable selected bundle.
    /// </summary>
    internal sealed class MissionAcgSpatialState
    {
        internal const int CurrentFormatVersion = 1;

        internal MissionAcgSpatialState(
            int formatVersion,
            MissionAcgIdentityRecord acceptedQuestIdentity,
            MissionAcgIdentityRecord ownerIdentity,
            int allocatedLivePlayfield2,
            string bundleId,
            string bundlePayloadSha256,
            MissionAcgIdentityRecord buildingIdentity,
            bool hasLastValidPlayerPosition,
            MissionAcgPointRecord lastValidPlayerPosition,
            MissionAcgSpatialCleanupState cleanupState,
            DateTime updatedUtc)
        {
            if (formatVersion != CurrentFormatVersion
                || acceptedQuestIdentity == null
                || ownerIdentity == null
                || buildingIdentity == null
                || allocatedLivePlayfield2 <= 0
                || string.IsNullOrWhiteSpace(bundleId)
                || string.IsNullOrWhiteSpace(bundlePayloadSha256)
                || lastValidPlayerPosition == null
                || !Enum.IsDefined(typeof(MissionAcgSpatialCleanupState), cleanupState)
                || updatedUtc == DateTime.MinValue)
            {
                throw new ArgumentException("Mission ACG spatial state identity is invalid.");
            }

            this.FormatVersion = formatVersion;
            this.AcceptedQuestIdentity = acceptedQuestIdentity;
            this.OwnerIdentity = ownerIdentity;
            this.AllocatedLivePlayfield2 = allocatedLivePlayfield2;
            this.BundleId = bundleId.Trim();
            this.BundlePayloadSha256 = bundlePayloadSha256.Trim().ToLowerInvariant();
            this.BuildingIdentity = buildingIdentity;
            this.HasLastValidPlayerPosition = hasLastValidPlayerPosition;
            this.LastValidPlayerPosition =
                new MissionAcgPointRecord(
                    lastValidPlayerPosition.X,
                    lastValidPlayerPosition.Y,
                    lastValidPlayerPosition.Z);
            this.CleanupState = cleanupState;
            this.UpdatedUtc =
                updatedUtc.Kind == DateTimeKind.Utc
                    ? updatedUtc
                    : updatedUtc.ToUniversalTime();
        }

        internal int FormatVersion { get; private set; }

        internal MissionAcgIdentityRecord AcceptedQuestIdentity { get; private set; }

        internal MissionAcgIdentityRecord OwnerIdentity { get; private set; }

        internal int AllocatedLivePlayfield2 { get; private set; }

        internal string BundleId { get; private set; }

        internal string BundlePayloadSha256 { get; private set; }

        internal MissionAcgIdentityRecord BuildingIdentity { get; private set; }

        internal bool HasLastValidPlayerPosition { get; private set; }

        internal MissionAcgPointRecord LastValidPlayerPosition { get; private set; }

        internal MissionAcgSpatialCleanupState CleanupState { get; private set; }

        internal DateTime UpdatedUtc { get; private set; }

        internal MissionAcgSpatialState WithLastValidPlayerPosition(
            MissionAcgPointRecord position,
            DateTime updatedUtc)
        {
            return this.Copy(true, position, this.CleanupState, updatedUtc);
        }

        internal MissionAcgSpatialState BeginCleanup(DateTime updatedUtc)
        {
            return this.Copy(
                this.HasLastValidPlayerPosition,
                this.LastValidPlayerPosition,
                MissionAcgSpatialCleanupState.CleanupPending,
                updatedUtc);
        }

        internal MissionAcgSpatialState CompleteCleanup(DateTime updatedUtc)
        {
            return this.Copy(
                this.HasLastValidPlayerPosition,
                this.LastValidPlayerPosition,
                MissionAcgSpatialCleanupState.Completed,
                updatedUtc);
        }

        private MissionAcgSpatialState Copy(
            bool hasLastValidPlayerPosition,
            MissionAcgPointRecord lastValidPlayerPosition,
            MissionAcgSpatialCleanupState cleanupState,
            DateTime updatedUtc)
        {
            return new MissionAcgSpatialState(
                CurrentFormatVersion,
                this.AcceptedQuestIdentity,
                this.OwnerIdentity,
                this.AllocatedLivePlayfield2,
                this.BundleId,
                this.BundlePayloadSha256,
                this.BuildingIdentity,
                hasLastValidPlayerPosition,
                lastValidPlayerPosition,
                cleanupState,
                updatedUtc);
        }
    }
}
