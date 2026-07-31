// Copyright (c) 2026 Michael K. All Rights Reserved.

namespace AORebirth.Core.Playfields
{
    using System;

    internal enum CapturedTempleNamedRespawnMode
    {
        CapturedAfterNpcDespawn,
        TemplePolicyAfterNpcDespawn,
        SuccessorOnly,
        ChainResetAfterNpcDespawn
    }

    internal enum CapturedTempleNanoEffectOwnership
    {
        PacketOnly,
        InstantSelfNanoData,
        ReanimatedAddLifecycle
    }

    internal static class CapturedTempleOfThreeWindsEncounterRules
    {
        internal const double NamedRespawnAfterNpcDespawnSeconds = 600.0;

        internal const int DefenderPrimaryNanoId = 205389;
        internal const int DefenderSecondaryNanoId = 205561;
        internal const int DefenderUnscheduledNanoId = 209924;
        internal const int YatilaPrimaryNanoId = 205600;
        internal const int YatilaSecondaryNanoId = 205594;
        internal const int YatilaTertiaryNanoId = 205592;
        internal const int GulardNanoId = 205584;
        internal const int ReAnimatorNanoId = 205604;
        internal const int BetanyNanoId = 205383;
        internal const int CuratorNanoId = 205565;
        internal const int NematetPrimaryNanoId = 205395;
        internal const int NematetSecondaryNanoId = 205563;
        internal const int GartuaNanoId = 205590;
        internal const int UkleshUnscheduledNanoId = 204830;
        internal const int MurialUnscheduledNanoId = 70294;

        internal static bool TryResolveNamedRespawnDelay(
            CapturedTempleNamedRespawnMode mode,
            out double delaySeconds)
        {
            if (mode == CapturedTempleNamedRespawnMode.CapturedAfterNpcDespawn
                || mode == CapturedTempleNamedRespawnMode.TemplePolicyAfterNpcDespawn)
            {
                delaySeconds = NamedRespawnAfterNpcDespawnSeconds;
                return true;
            }

            delaySeconds = 0.0;
            return false;
        }

        internal static DateTime? ResolveNamedRespawnDueAtUtc(
            CapturedTempleNamedRespawnMode mode,
            DateTime? existingDueAtUtc,
            DateTime npcDespawnedAtUtc)
        {
            double delaySeconds;
            if (TryResolveNamedRespawnDelay(mode, out delaySeconds))
            {
                return npcDespawnedAtUtc.AddSeconds(delaySeconds);
            }

            return mode == CapturedTempleNamedRespawnMode.SuccessorOnly
                       ? existingDueAtUtc
                       : null;
        }

        internal static bool TryResolveMainRoomResetDue(
            DateTime resetAtUtc,
            bool ukleshActive,
            bool khalumActive,
            bool azturActive,
            bool ukleshScheduled,
            bool khalumScheduled,
            bool azturScheduled,
            out DateTime resetDueAtUtc)
        {
            if (ukleshActive
                || khalumActive
                || azturActive
                || ukleshScheduled
                || khalumScheduled
                || azturScheduled)
            {
                resetDueAtUtc = default(DateTime);
                return false;
            }

            resetDueAtUtc =
                resetAtUtc.AddSeconds(NamedRespawnAfterNpcDespawnSeconds);
            return true;
        }

        internal static bool IsLivingMainRoomStage(int identityInstance, bool dead)
        {
            return identityInstance != 0 && !dead;
        }

        internal static bool TryGetCapturedNanoEffectOwnership(
            int nanoId,
            out CapturedTempleNanoEffectOwnership ownership)
        {
            if (nanoId == ReAnimatorNanoId)
            {
                ownership = CapturedTempleNanoEffectOwnership.ReanimatedAddLifecycle;
                return true;
            }

            if (nanoId == GulardNanoId)
            {
                ownership = CapturedTempleNanoEffectOwnership.InstantSelfNanoData;
                return true;
            }

            switch (nanoId)
            {
                case DefenderPrimaryNanoId:
                case DefenderSecondaryNanoId:
                case DefenderUnscheduledNanoId:
                case YatilaPrimaryNanoId:
                case YatilaSecondaryNanoId:
                case YatilaTertiaryNanoId:
                case BetanyNanoId:
                case CuratorNanoId:
                case NematetPrimaryNanoId:
                case NematetSecondaryNanoId:
                case GartuaNanoId:
                case UkleshUnscheduledNanoId:
                case MurialUnscheduledNanoId:
                    ownership = CapturedTempleNanoEffectOwnership.PacketOnly;
                    return true;
            }

            ownership = CapturedTempleNanoEffectOwnership.PacketOnly;
            return false;
        }
    }
}
