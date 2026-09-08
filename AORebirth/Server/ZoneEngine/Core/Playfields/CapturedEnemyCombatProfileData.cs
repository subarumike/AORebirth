namespace AORebirth.Core.Playfields
{
    using System;
    using System.Linq;

    internal sealed partial class CapturedEnemyCombatProfileStreamDefinition
    {
        internal CapturedEnemyCombatProfileStreamDefinition(
            int minimumObservedDamage,
            int maximumObservedDamage,
            int initialAmmoCount,
            int weaponSlot,
            int damageTypeWire,
            int hitTypeWire,
            int weaponInstance,
            byte n3Unknown,
            double observedRechargeSeconds,
            int[] capturedDamageObservations = null,
            double[] capturedAttackStartDelayObservationsSeconds = null,
            double[] capturedFirstHitDelayObservationsSeconds = null,
            double[] capturedLandedIntervalObservationsSeconds = null,
            int? capturedDamageBonus = null,
            bool? capturedUsesEquippedWeapon = null,
            double? capturedAttackRange = null,
            bool? capturedSendAttackInfo = null,
            bool capturedTerminalHitOnly = false)
        {
            this.MinimumObservedDamage = minimumObservedDamage;
            this.MaximumObservedDamage = maximumObservedDamage;
            this.InitialAmmoCount = initialAmmoCount;
            this.WeaponSlot = weaponSlot;
            this.DamageTypeWire = damageTypeWire;
            this.HitTypeWire = hitTypeWire;
            this.WeaponInstance = weaponInstance;
            this.N3Unknown = n3Unknown;
            this.ObservedRechargeSeconds = observedRechargeSeconds;
            this.CapturedDamageObservations = capturedDamageObservations == null
                                                  ? new int[0]
                                                  : capturedDamageObservations.ToArray();
            this.CapturedAttackStartDelayObservationsSeconds =
                capturedAttackStartDelayObservationsSeconds == null
                    ? new double[0]
                    : capturedAttackStartDelayObservationsSeconds.ToArray();
            this.CapturedFirstHitDelayObservationsSeconds =
                capturedFirstHitDelayObservationsSeconds == null
                    ? new double[0]
                    : capturedFirstHitDelayObservationsSeconds.ToArray();
            this.CapturedLandedIntervalObservationsSeconds =
                capturedLandedIntervalObservationsSeconds == null
                    ? new double[0]
                    : capturedLandedIntervalObservationsSeconds.ToArray();
            this.CapturedDamageBonus = capturedDamageBonus;
            this.CapturedUsesEquippedWeapon = capturedUsesEquippedWeapon;
            this.CapturedAttackRange = capturedAttackRange;
            this.CapturedSendAttackInfo = capturedSendAttackInfo;
            this.CapturedTerminalHitOnly = capturedTerminalHitOnly;
        }

        internal int MinimumObservedDamage { get; private set; }
        internal int MaximumObservedDamage { get; private set; }
        internal int InitialAmmoCount { get; private set; }
        internal int WeaponSlot { get; private set; }
        internal int DamageTypeWire { get; private set; }
        internal int HitTypeWire { get; private set; }
        internal int WeaponInstance { get; private set; }
        internal byte N3Unknown { get; private set; }
        internal double ObservedRechargeSeconds { get; private set; }
        internal int[] CapturedDamageObservations { get; private set; }
        internal double[] CapturedAttackStartDelayObservationsSeconds { get; private set; }
        internal double[] CapturedFirstHitDelayObservationsSeconds { get; private set; }
        internal double[] CapturedLandedIntervalObservationsSeconds { get; private set; }
        internal int? CapturedDamageBonus { get; private set; }
        internal bool? CapturedUsesEquippedWeapon { get; private set; }
        internal double? CapturedAttackRange { get; private set; }
        internal bool? CapturedSendAttackInfo { get; private set; }
        internal bool CapturedTerminalHitOnly { get; private set; }

        internal bool HasCompleteFixedRuntimeEvidence
        {
            get
            {
                return this.CapturedDamageObservations.Length > 0
                       && this.CapturedDamageObservations.All(value => value > 0)
                       && this.CapturedDamageObservations.Min() == this.MinimumObservedDamage
                       && this.CapturedDamageObservations.Max() == this.MaximumObservedDamage
                       && this.CapturedAttackStartDelayObservationsSeconds.Length > 0
                       && this.CapturedAttackStartDelayObservationsSeconds.All(IsValidDelay)
                       && this.CapturedFirstHitDelayObservationsSeconds.Length > 0
                       && this.CapturedFirstHitDelayObservationsSeconds.All(IsValidDelay)
                       && this.CapturedAttackStartDelayObservationsSeconds.Length
                          == this.CapturedFirstHitDelayObservationsSeconds.Length
                       && this.CapturedDamageBonus.HasValue
                       && this.CapturedUsesEquippedWeapon.HasValue
                       && this.CapturedSendAttackInfo.HasValue
                       && this.CapturedSendAttackInfo.Value;
            }
        }

        private static bool IsValidDelay(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0.0d;
        }

    }

    internal sealed partial class CapturedEnemyCombatProfileDefinition
    {
        internal CapturedEnemyCombatProfileDefinition(
            string profileId,
            string evidence,
            int resourceId,
            string name,
            int monsterData,
            int level,
            bool semanticFallbackCaptureProven,
            bool captureEvidenceSafe,
            bool deterministicRuntimeInitializationProven,
            int[] sourceIdentities,
            int representativeEvidenceSourceIdentity,
            CapturedEnemyWeaponDefinition weaponDefinition,
            CapturedEnemySpecialAttackDefinition[] specialAttacks,
            byte specialAttackWeaponN3Unknown,
            int specialAttackWeaponUnknown1,
            int specialAttackWeaponUnknown2,
            int specialAttackWeaponUnknown3,
            int specialAttackWeaponUnknown4,
            int specialAttackWeaponUnknown5,
            byte attackN3Unknown,
            byte attackAction,
            CapturedEnemyCombatProfileStreamDefinition[] streams,
            int[] specialAttackWeaponUnknown5Observations = null)
        {
            this.ProfileId = profileId ?? string.Empty;
            this.Evidence = evidence ?? string.Empty;
            this.ResourceId = resourceId;
            this.Name = name ?? string.Empty;
            this.MonsterData = monsterData;
            this.Level = level;
            this.SemanticFallbackCaptureProven = semanticFallbackCaptureProven;
            this.CaptureEvidenceSafe = captureEvidenceSafe;
            this.DeterministicRuntimeInitializationProven =
                deterministicRuntimeInitializationProven;
            this.SourceIdentities = sourceIdentities ?? new int[0];
            this.RepresentativeEvidenceSourceIdentity = representativeEvidenceSourceIdentity;
            this.WeaponDefinition = weaponDefinition;
            this.SpecialAttacks = specialAttacks ?? new CapturedEnemySpecialAttackDefinition[0];
            this.SpecialAttackWeaponN3Unknown = specialAttackWeaponN3Unknown;
            this.SpecialAttackWeaponUnknown1 = specialAttackWeaponUnknown1;
            this.SpecialAttackWeaponUnknown2 = specialAttackWeaponUnknown2;
            this.SpecialAttackWeaponUnknown3 = specialAttackWeaponUnknown3;
            this.SpecialAttackWeaponUnknown4 = specialAttackWeaponUnknown4;
            this.SpecialAttackWeaponUnknown5 = specialAttackWeaponUnknown5;
            this.SpecialAttackWeaponUnknown5Observations =
                specialAttackWeaponUnknown5Observations == null
                    ? new[] { specialAttackWeaponUnknown5 }
                    : specialAttackWeaponUnknown5Observations.ToArray();
            this.AttackN3Unknown = attackN3Unknown;
            this.AttackAction = attackAction;
            this.Streams = streams ?? new CapturedEnemyCombatProfileStreamDefinition[0];
        }

        internal string ProfileId { get; private set; }
        internal string Evidence { get; private set; }
        internal int ResourceId { get; private set; }
        internal string Name { get; private set; }
        internal int MonsterData { get; private set; }
        internal int Level { get; private set; }
        internal bool SemanticFallbackCaptureProven { get; private set; }
        internal bool CaptureEvidenceSafe { get; private set; }
        internal bool DeterministicRuntimeInitializationProven { get; private set; }
        internal bool CaptureRuntimeEvidenceSafe
        {
            get
            {
                return this.CaptureEvidenceSafe
                       && (this.DeterministicRuntimeInitializationProven
                           || this.HasCapturedOrderedSpecialAttackWeaponState);
            }
        }
        internal int[] SourceIdentities { get; private set; }
        internal int RepresentativeEvidenceSourceIdentity { get; private set; }
        internal CapturedEnemyWeaponDefinition WeaponDefinition { get; private set; }
        internal CapturedEnemySpecialAttackDefinition[] SpecialAttacks { get; private set; }
        internal byte SpecialAttackWeaponN3Unknown { get; private set; }
        internal int SpecialAttackWeaponUnknown1 { get; private set; }
        internal int SpecialAttackWeaponUnknown2 { get; private set; }
        internal int SpecialAttackWeaponUnknown3 { get; private set; }
        internal int SpecialAttackWeaponUnknown4 { get; private set; }
        internal int SpecialAttackWeaponUnknown5 { get; private set; }
        internal int[] SpecialAttackWeaponUnknown5Observations { get; private set; }
        internal bool HasCapturedOrderedSpecialAttackWeaponState
        {
            get
            {
                return this.SpecialAttackWeaponUnknown5Observations.Length > 1
                       && this.SpecialAttackWeaponUnknown5Observations[0]
                          == this.SpecialAttackWeaponUnknown5;
            }
        }
        internal byte AttackN3Unknown { get; private set; }
        internal byte AttackAction { get; private set; }
        internal CapturedEnemyCombatProfileStreamDefinition[] Streams { get; private set; }

    }
}
