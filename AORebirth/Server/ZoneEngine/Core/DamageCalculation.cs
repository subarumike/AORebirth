namespace ZoneEngine.Core
{
    using System;
    using System.Collections.ObjectModel;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    public enum DamageEvidenceClassification
    {
        Unknown,
        ProvenRepositoryBehavior,
        ProvenCapturedBehavior,
        ProvenDatabaseContract,
        ControlledTestConfirmed,
        CommunityDocumented,
        Inferred
    }

    public enum DamageCalculationMode
    {
        PvM,
        PvP
    }

    public enum DamageSourceCategory
    {
        Unknown,
        Player,
        Npc,
        Pet,
        Nano,
        Perk,
        Proc,
        Environment
    }

    public enum DamageTargetCategory
    {
        Unknown,
        Player,
        Npc,
        Pet
    }

    public enum DamageAttackCategory
    {
        Unknown,
        RegularAttack,
        SpecialAttack,
        NanoDirect,
        NanoDamageOverTime,
        Perk,
        Proc,
        FixedDamage,
        PercentageHealth,
        Environment
    }

    public enum SpecialAttackCategory
    {
        None,
        FlingShot,
        FastAttack,
        Burst,
        FullAuto,
        AimedShot,
        SneakAttack,
        Backstab,
        Brawl,
        Dimach,
        SharpObjects,
        MartialArts
    }

    public enum DamageType
    {
        Unknown,
        Melee,
        Projectile,
        Energy,
        Chemical,
        Radiation,
        Cold,
        Fire,
        Poison,
        Disease,
        Nano
    }

    public enum DamageHitOutcome
    {
        Hit,
        Miss,
        Blocked,
        EvidenceBlocked
    }

    public enum DamageCalculationStageStatus
    {
        Applied,
        Preserved,
        Skipped,
        EvidenceBlocked
    }

    public enum DamageCalculationStrategyKind
    {
        LegacyFallback,
        FixedCapturedDamage,
        EvidenceBackedWeaponDamage,
        EvidenceBlocked
    }

    public interface IDamageRandomSource
    {
        int NextInclusive(int minimumInclusive, int maximumInclusive);

        bool NextChance(int chanceBasisPoints);
    }

    public interface IDamageCalculationStage
    {
        string Name { get; }
    }

    public interface IDamageAttackStrategy
    {
        string Name { get; }

        DamageCalculationResult Calculate(DamageCalculationRequest request, IDamageRandomSource randomSource);
    }

    public sealed class DamageCalculationRequest
    {
        public DamageCalculationRequest()
        {
            this.Context = new DamageCalculationContext();
            this.Source = new DamageSourceSnapshot();
            this.Target = new DamageTargetSnapshot();
            this.Definition = new DamageDefinition();
            this.Modifiers = new DamageModifierSet();
            this.Mitigation = new DamageMitigationSet();
            this.Policy = DamageCalculationPolicy.RepositoryLegacyNormalHit(false);
            this.EvidenceClassification = DamageEvidenceClassification.Unknown;
            this.HitOutcome = DamageHitOutcome.Hit;
        }

        public DamageCalculationContext Context { get; set; }

        public DamageSourceSnapshot Source { get; set; }

        public DamageTargetSnapshot Target { get; set; }

        public DamageDefinition Definition { get; set; }

        public DamageModifierSet Modifiers { get; set; }

        public DamageMitigationSet Mitigation { get; set; }

        public DamageCalculationPolicy Policy { get; set; }

        public DamageEvidenceClassification EvidenceClassification { get; set; }

        public DamageHitOutcome HitOutcome { get; set; }

        public DamageType DamageTypeOverride { get; set; }
    }

    public sealed class DamageCalculationContext
    {
        public DamageCalculationContext()
        {
            this.Mode = DamageCalculationMode.PvM;
            this.AttackCategory = DamageAttackCategory.RegularAttack;
            this.SpecialAttackCategory = SpecialAttackCategory.None;
            this.CompatibilityPolicy = string.Empty;
            this.EvidenceSource = string.Empty;
        }

        public DamageCalculationMode Mode { get; set; }

        public DamageAttackCategory AttackCategory { get; set; }

        public SpecialAttackCategory SpecialAttackCategory { get; set; }

        public string CompatibilityPolicy { get; set; }

        public string EvidenceSource { get; set; }
    }

    public sealed class DamageSourceSnapshot
    {
        public DamageSourceSnapshot()
        {
            this.Identity = string.Empty;
            this.Category = DamageSourceCategory.Unknown;
            this.AttackSkillContributions = new List<AttackSkillContribution>();
        }

        public string Identity { get; set; }

        public DamageSourceCategory Category { get; set; }

        public int Level { get; set; }

        public int AttackRating { get; set; }

        public int AddAllOff { get; set; }

        public IList<AttackSkillContribution> AttackSkillContributions { get; private set; }
    }

    public sealed class AttackSkillContribution
    {
        public int StatId { get; set; }

        public int Percentage { get; set; }

        public int Value { get; set; }

        public int Contribution
        {
            get
            {
                return (this.Value * this.Percentage) / 100;
            }
        }
    }

    public sealed class DamageTargetSnapshot
    {
        public DamageTargetSnapshot()
        {
            this.Identity = string.Empty;
            this.Category = DamageTargetCategory.Unknown;
        }

        public string Identity { get; set; }

        public DamageTargetCategory Category { get; set; }

        public int CurrentHealth { get; set; }

        public int MaximumHealth { get; set; }

        public int AddAllDef { get; set; }
    }

    public class DamageDefinition
    {
        public DamageDefinition()
        {
            this.DamageType = DamageType.Unknown;
            this.AttackSpecificCap = 0;
            this.EvidenceClassification = DamageEvidenceClassification.Unknown;
        }

        public int BaseMinimum { get; set; }

        public int BaseMaximum { get; set; }

        public int CriticalBonus { get; set; }

        public bool HasCriticalState { get; set; }

        public bool HasCriticalBonus { get; set; }

        public int FixedDamage { get; set; }

        public int PercentageHealthDamage { get; set; }

        public DamageType DamageType { get; set; }

        public int WeaponTemplateId { get; set; }

        public int AttackRatingCap { get; set; }

        public bool HasAttackRatingCap { get; set; }

        public bool IsCritical { get; set; }

        public int BulletCount { get; set; }

        public int AmmoLimitedCount { get; set; }

        public int AttackSpecificCap { get; set; }

        public DamageEvidenceClassification EvidenceClassification { get; set; }
    }

    public sealed class WeaponDamageDefinition : DamageDefinition
    {
    }

    public sealed class NanoDamageDefinition : DamageDefinition
    {
        public int NanoScalingInput { get; set; }
    }

    public sealed class SpecialAttackDefinition : DamageDefinition
    {
        public SpecialAttackCategory SpecialAttackCategory { get; set; }
    }

    public sealed class DamageModifierSet
    {
        public int FlatAddDamage { get; set; }

        public int LegacyDamageBonus { get; set; }

        public int TypeSpecificAddDamage { get; set; }

        public int UniversalAddDamage { get; set; }

        public int CriticalModifier { get; set; }

        public int AddNanoDamage { get; set; }
    }

    public sealed class DamageMitigationSet
    {
        public int MatchingArmor { get; set; }

        public bool HasMatchingArmor { get; set; }

        public int ReflectPercentage { get; set; }

        public int ReflectCap { get; set; }

        public int TypedAbsorbPool { get; set; }

        public int UniversalAbsorbPool { get; set; }

        public int DamageShield { get; set; }

        public bool Immune { get; set; }

        public bool Invulnerable { get; set; }
    }

    public sealed class DamageCalculationPolicy
    {
        public DamageCalculationPolicy()
        {
            this.Name = string.Empty;
            this.EvidenceClassification = DamageEvidenceClassification.Unknown;
            this.UseRepositoryLegacyNormalHit = true;
            this.PreserveLegacyFallbackFloor = true;
            this.EnableArmorMitigation = false;
            this.EnableCriticalDamage = false;
            this.EnableAttackRatingScaling = false;
            this.EnableReflect = false;
            this.EnableAbsorb = false;
            this.EnablePvP = false;
            this.EnableSpecialAggregation = false;
            this.EnablePercentageHealthDamage = false;
            this.EnableReturnedDamage = false;
            this.IsFixedCapturedDamage = false;
            this.EnableEvidenceBackedWeaponFormula = false;
            this.PlayerFallbackDamage = 15;
            this.NpcFallbackDamage = 1;
        }

        public string Name { get; set; }

        public DamageEvidenceClassification EvidenceClassification { get; set; }

        public bool UseRepositoryLegacyNormalHit { get; set; }

        public bool PreserveLegacyFallbackFloor { get; set; }

        public bool EnableArmorMitigation { get; set; }

        public bool EnableCriticalDamage { get; set; }

        public bool EnableAttackRatingScaling { get; set; }

        public bool EnableReflect { get; set; }

        public bool EnableAbsorb { get; set; }

        public bool EnablePvP { get; set; }

        public bool EnableSpecialAggregation { get; set; }

        public bool EnablePercentageHealthDamage { get; set; }

        public bool EnableReturnedDamage { get; set; }

        public bool IsFixedCapturedDamage { get; set; }

        public bool EnableEvidenceBackedWeaponFormula { get; set; }

        public int PlayerFallbackDamage { get; set; }

        public int NpcFallbackDamage { get; set; }

        public static DamageCalculationPolicy RepositoryLegacyNormalHit(bool isPlayer)
        {
            return new DamageCalculationPolicy
            {
                Name = isPlayer ? "repository-player-legacy-normal-hit" : "repository-npc-legacy-normal-hit",
                EvidenceClassification = DamageEvidenceClassification.ProvenRepositoryBehavior,
                UseRepositoryLegacyNormalHit = true,
                PreserveLegacyFallbackFloor = true
            };
        }

        public static DamageCalculationPolicy CapturedFixedDamage(string name)
        {
            return new DamageCalculationPolicy
            {
                Name = name,
                EvidenceClassification = DamageEvidenceClassification.ProvenCapturedBehavior,
                UseRepositoryLegacyNormalHit = true,
                PreserveLegacyFallbackFloor = true,
                IsFixedCapturedDamage = true
            };
        }

        public static DamageCalculationPolicy EvidenceBackedWeaponFormula(string name)
        {
            return new DamageCalculationPolicy
            {
                Name = name,
                EvidenceClassification = DamageEvidenceClassification.Unknown,
                UseRepositoryLegacyNormalHit = false,
                PreserveLegacyFallbackFloor = false,
                EnableEvidenceBackedWeaponFormula = true,
                EnableArmorMitigation = true,
                EnableCriticalDamage = true,
                EnableAttackRatingScaling = true
            };
        }
    }

    public sealed class DamageCalculationResult
    {
        public DamageCalculationResult()
        {
            this.SelectedDamageType = DamageType.Unknown;
            this.HitOutcome = DamageHitOutcome.Hit;
            this.EvidenceClassification = DamageEvidenceClassification.Unknown;
            this.Trace = new DamageCalculationTrace();
            this.SubHitResults = new List<DamageCalculationResult>();
            this.Clamps = new List<string>();
            this.Strategy = DamageCalculationStrategyKind.LegacyFallback;
            this.StrategyReason = string.Empty;
        }

        public DamageCalculationStrategyKind Strategy { get; set; }

        public string StrategyReason { get; set; }

        public DamageHitOutcome HitOutcome { get; set; }

        public bool CriticalOutcome { get; set; }

        public DamageType SelectedDamageType { get; set; }

        public int BaseRoll { get; set; }

        public int EffectiveAttackRating { get; set; }

        public int AttackRatingCapResult { get; set; }

        public int Pre1000AttackRatingContribution { get; set; }

        public int Post1000AttackRatingContribution { get; set; }

        public int FinalAttackRatingMultiplierBasisPoints { get; set; }

        public int ScaledBaseDamage { get; set; }

        public int CriticalContribution { get; set; }

        public int ArmorReduction { get; set; }

        public int MinimumDamageFloor { get; set; }

        public int FlatAddDamageContribution { get; set; }

        public int LegacyDamageBonusContribution { get; set; }

        public int TypeSpecificAddDamageContribution { get; set; }

        public int UniversalAddDamageContribution { get; set; }

        public IList<DamageCalculationResult> SubHitResults { get; private set; }

        public int AggregateSpecialDamage { get; set; }

        public int SpecialCompression { get; set; }

        public int SpecialCap { get; set; }

        public int PvPConversion { get; set; }

        public int PvPHealthCap { get; set; }

        public int ReflectPrevention { get; set; }

        public int ReflectReturn { get; set; }

        public int TypedAbsorbConsumption { get; set; }

        public int UniversalAbsorbConsumption { get; set; }

        public int DamageShieldReturn { get; set; }

        public int FinalTargetDamage { get; set; }

        public int FinalAttackerDamage { get; set; }

        public IList<string> Clamps { get; private set; }

        public DamageEvidenceClassification EvidenceClassification { get; set; }

        public DamageCalculationTrace Trace { get; private set; }
    }

    public sealed class DamageCalculationStageResult
    {
        public DamageCalculationStageResult()
        {
            this.Stage = string.Empty;
            this.Status = DamageCalculationStageStatus.Skipped;
            this.Input = 0;
            this.Output = 0;
            this.EvidenceClassification = DamageEvidenceClassification.Unknown;
            this.Note = string.Empty;
        }

        public string Stage { get; set; }

        public DamageCalculationStageStatus Status { get; set; }

        public int Input { get; set; }

        public int Output { get; set; }

        public DamageEvidenceClassification EvidenceClassification { get; set; }

        public string Note { get; set; }
    }

    public sealed class DamageCalculationTrace
    {
        public DamageCalculationTrace()
        {
            this.Stages = new List<DamageCalculationStageResult>();
        }

        public IList<DamageCalculationStageResult> Stages { get; private set; }

        public void Add(
            string stage,
            DamageCalculationStageStatus status,
            int input,
            int output,
            DamageEvidenceClassification evidenceClassification,
            string note)
        {
            this.Stages.Add(
                new DamageCalculationStageResult
                {
                    Stage = stage,
                    Status = status,
                    Input = input,
                    Output = output,
                    EvidenceClassification = evidenceClassification,
                    Note = note ?? string.Empty
                });
        }
    }

    public sealed class SystemDamageRandomSource : IDamageRandomSource
    {
        private readonly Random random;

        private readonly object randomLock;

        public SystemDamageRandomSource()
            : this(new Random())
        {
        }

        public SystemDamageRandomSource(Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException("random");
            }

            this.random = random;
            this.randomLock = new object();
        }

        public int NextInclusive(int minimumInclusive, int maximumInclusive)
        {
            if (maximumInclusive < minimumInclusive)
            {
                throw new ArgumentOutOfRangeException("maximumInclusive");
            }

            if (maximumInclusive == minimumInclusive)
            {
                return minimumInclusive;
            }

            lock (this.randomLock)
            {
                return this.random.Next(minimumInclusive, maximumInclusive + 1);
            }
        }

        public bool NextChance(int chanceBasisPoints)
        {
            if (chanceBasisPoints <= 0)
            {
                return false;
            }

            if (chanceBasisPoints >= 10000)
            {
                return true;
            }

            lock (this.randomLock)
            {
                return this.random.Next(0, 10000) < chanceBasisPoints;
            }
        }
    }

    public static class DamageCalculator
    {
        public static bool TryGetArmorStatForDamageType(DamageType damageType, out int statId)
        {
            switch (damageType)
            {
                case DamageType.Projectile:
                    statId = 90;
                    return true;
                case DamageType.Melee:
                    statId = 91;
                    return true;
                case DamageType.Energy:
                    statId = 92;
                    return true;
                case DamageType.Chemical:
                    statId = 93;
                    return true;
                case DamageType.Radiation:
                    statId = 94;
                    return true;
                case DamageType.Cold:
                    statId = 95;
                    return true;
                case DamageType.Poison:
                    statId = 96;
                    return true;
                case DamageType.Fire:
                    statId = 97;
                    return true;
                case DamageType.Nano:
                    statId = 168;
                    return true;
                default:
                    statId = 0;
                    return false;
            }
        }

        public static bool TryGetAddDamageStatForDamageType(DamageType damageType, out int statId)
        {
            switch (damageType)
            {
                case DamageType.Projectile:
                    statId = 278;
                    return true;
                case DamageType.Melee:
                    statId = 279;
                    return true;
                case DamageType.Energy:
                    statId = 280;
                    return true;
                case DamageType.Chemical:
                    statId = 281;
                    return true;
                case DamageType.Radiation:
                    statId = 282;
                    return true;
                case DamageType.Cold:
                    statId = 311;
                    return true;
                case DamageType.Nano:
                    statId = 315;
                    return true;
                case DamageType.Fire:
                    statId = 316;
                    return true;
                case DamageType.Poison:
                    statId = 317;
                    return true;
                default:
                    statId = 0;
                    return false;
            }
        }

        public static DamageCalculationResult Calculate(
            DamageCalculationRequest request,
            IDamageRandomSource randomSource)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (randomSource == null)
            {
                throw new ArgumentNullException("randomSource");
            }

            NormalizeRequest(request);

            DamageCalculationResult result = new DamageCalculationResult();
            result.EvidenceClassification = request.EvidenceClassification;
            result.HitOutcome = request.HitOutcome;

            DamageEvidenceClassification evidence = ResolveEvidence(request);
            string strategyReason;
            result.Strategy = SelectStrategy(request, out strategyReason);
            result.StrategyReason = strategyReason;
            result.Trace.Add("ValidateRequest", DamageCalculationStageStatus.Applied, 0, 0, evidence, "request and deterministic random source present");
            result.Trace.Add("ResolveModeAndPolicy", DamageCalculationStageStatus.Applied, (int)request.Context.Mode, (int)request.Context.Mode, request.Policy.EvidenceClassification, request.Policy.Name);
            result.Trace.Add("SelectDamageStrategy", result.Strategy == DamageCalculationStrategyKind.EvidenceBlocked ? DamageCalculationStageStatus.EvidenceBlocked : DamageCalculationStageStatus.Applied, 0, (int)result.Strategy, evidence, result.StrategyReason);

            DamageType selectedDamageType = request.DamageTypeOverride == DamageType.Unknown
                                                ? request.Definition.DamageType
                                                : request.DamageTypeOverride;
            result.SelectedDamageType = selectedDamageType;
            result.Trace.Add("ResolveDamageType", DamageCalculationStageStatus.Applied, (int)request.Definition.DamageType, (int)selectedDamageType, evidence, "override wins when present");

            if (request.Mitigation.Immune || request.Mitigation.Invulnerable)
            {
                result.FinalTargetDamage = 0;
                result.Trace.Add("ResolveImmunity", DamageCalculationStageStatus.Applied, 0, 0, evidence, "immune or invulnerable target takes no damage");
                return result;
            }

            if (request.HitOutcome != DamageHitOutcome.Hit)
            {
                result.FinalTargetDamage = 0;
                result.Trace.Add("ResolveHitOutcome", DamageCalculationStageStatus.Applied, 0, 0, evidence, "non-hit outcome stops damage");
                return result;
            }

            result.Trace.Add("ResolveHitOutcome", DamageCalculationStageStatus.Preserved, 1, 1, evidence, "current repository callers enter only after hit eligibility");
            result.Trace.Add("ResolveCritical", request.Definition.IsCritical && !request.Policy.EnableCriticalDamage ? DamageCalculationStageStatus.EvidenceBlocked : DamageCalculationStageStatus.Skipped, 0, 0, request.Definition.IsCritical ? DamageEvidenceClassification.Unknown : evidence, request.Definition.IsCritical ? "critical formula not proven for migrated callers" : "not critical");

            int effectiveAttackRating = request.Source.AttackRating + request.Source.AddAllOff;
            if (request.Source.AttackSkillContributions.Count > 0)
            {
                effectiveAttackRating = request.Source.AttackSkillContributions.Sum(x => x.Contribution) + request.Source.AddAllOff;
            }

            result.EffectiveAttackRating = effectiveAttackRating;
            result.Trace.Add("ResolveEffectiveAttackRating", DamageCalculationStageStatus.Preserved, request.Source.AttackRating, effectiveAttackRating, evidence, request.Source.AttackSkillContributions.Count > 0 ? "weighted attack-skill contributions are represented but not active for migrated callers" : "current migrated callers do not scale by attack rating");

            if (request.Definition.HasAttackRatingCap)
            {
                if (request.Definition.AttackRatingCap > 0)
                {
                    result.AttackRatingCapResult = Math.Min(effectiveAttackRating, request.Definition.AttackRatingCap);
                    result.Trace.Add("ApplyAttackRatingCap", DamageCalculationStageStatus.Applied, effectiveAttackRating, result.AttackRatingCapResult, DamageEvidenceClassification.ControlledTestConfirmed, "cap arithmetic only; scaling remains policy-gated");
                }
                else
                {
                    result.AttackRatingCapResult = effectiveAttackRating;
                    result.Trace.Add("ApplyAttackRatingCap", DamageCalculationStageStatus.EvidenceBlocked, effectiveAttackRating, effectiveAttackRating, DamageEvidenceClassification.Unknown, "zero or invalid cap semantics are unresolved");
                }
            }
            else
            {
                result.AttackRatingCapResult = effectiveAttackRating;
                result.Trace.Add("ApplyAttackRatingCap", DamageCalculationStageStatus.Skipped, effectiveAttackRating, effectiveAttackRating, evidence, "missing cap preserves effective attack rating");
            }

            result.Trace.Add("ApplyPre1000AttackRatingScaling", request.Policy.EnableAttackRatingScaling ? DamageCalculationStageStatus.EvidenceBlocked : DamageCalculationStageStatus.Skipped, 0, 0, DamageEvidenceClassification.Unknown, "no proven production AR multiplier in migrated callers");
            result.Trace.Add("ApplyPost1000AttackRatingScaling", request.Policy.EnableAttackRatingScaling ? DamageCalculationStageStatus.EvidenceBlocked : DamageCalculationStageStatus.Skipped, 0, 0, DamageEvidenceClassification.Unknown, "profession and NPC post-1000 factors are unresolved");

            int normalizedMinimum = Math.Max(0, request.Definition.BaseMinimum);
            int normalizedMaximum = Math.Max(normalizedMinimum, request.Definition.BaseMaximum);
            int fallbackFloor = ResolveFallbackFloor(request);
            int baseDamage = ResolveBaseDamage(request, randomSource, normalizedMinimum, normalizedMaximum, fallbackFloor, result);
            result.BaseRoll = baseDamage;
            result.ScaledBaseDamage = baseDamage;
            result.FinalAttackRatingMultiplierBasisPoints = 10000;

            result.Trace.Add("ApplyCriticalContribution", request.Definition.IsCritical && !request.Policy.EnableCriticalDamage ? DamageCalculationStageStatus.EvidenceBlocked : DamageCalculationStageStatus.Skipped, baseDamage, baseDamage, DamageEvidenceClassification.Unknown, "critical contribution is not proven for migrated callers");

            int afterMitigation = baseDamage;
            if (request.Mitigation.MatchingArmor != 0)
            {
                result.Trace.Add("ApplyArmorMitigation", request.Policy.EnableArmorMitigation ? DamageCalculationStageStatus.EvidenceBlocked : DamageCalculationStageStatus.EvidenceBlocked, afterMitigation, afterMitigation, DamageEvidenceClassification.Unknown, "AC ordering and division are unresolved for migrated production damage");
                int armorStatId;
                if (TryGetArmorStatForDamageType(selectedDamageType, out armorStatId))
                {
                    result.Trace.Add("ResolveArmorStat", DamageCalculationStageStatus.Applied, (int)selectedDamageType, armorStatId, DamageEvidenceClassification.ProvenDatabaseContract, "damage-type to AC stat mapping only; no AC formula activated");
                }
            }
            else
            {
                result.Trace.Add("ApplyArmorMitigation", DamageCalculationStageStatus.Skipped, afterMitigation, afterMitigation, evidence, "no matching armor supplied by migrated callers");
            }

            bool hasRealDamageRange = normalizedMaximum > 0
                                      && !request.Policy.IsFixedCapturedDamage
                                      && request.Definition.FixedDamage <= 0;
            int activeMinimumDamageFloor = hasRealDamageRange ? 0 : fallbackFloor;
            result.MinimumDamageFloor = activeMinimumDamageFloor;
            int normalizedLegacyDamageBonus = Math.Max(0, request.Modifiers.LegacyDamageBonus);
            if (normalizedLegacyDamageBonus == 0 && request.Modifiers.FlatAddDamage != 0)
            {
                normalizedLegacyDamageBonus = Math.Max(0, request.Modifiers.FlatAddDamage);
            }

            result.LegacyDamageBonusContribution = normalizedLegacyDamageBonus;
            result.TypeSpecificAddDamageContribution = request.Modifiers.TypeSpecificAddDamage;
            result.UniversalAddDamageContribution = request.Modifiers.UniversalAddDamage;
            result.FlatAddDamageContribution = normalizedLegacyDamageBonus;
            int afterFlatAdd = afterMitigation + normalizedLegacyDamageBonus;
            result.Trace.Add("ApplyFlatDamageModifiers", DamageCalculationStageStatus.Preserved, afterMitigation, afterFlatAdd, evidence, "legacy damagebonus is kept separate from type-specific and universal add damage");
            if (request.Modifiers.TypeSpecificAddDamage != 0 || request.Modifiers.UniversalAddDamage != 0)
            {
                int addDamageStatId;
                if (TryGetAddDamageStatForDamageType(selectedDamageType, out addDamageStatId))
                {
                    result.Trace.Add("ResolveAddDamageStat", DamageCalculationStageStatus.Applied, (int)selectedDamageType, addDamageStatId, DamageEvidenceClassification.ProvenDatabaseContract, "type-specific add-damage stat mapping only; add-damage formula remains inactive");
                }
            }

            result.Trace.Add("ApplyMinimumDamageFloor", DamageCalculationStageStatus.Preserved, afterFlatAdd, Math.Max(activeMinimumDamageFloor, afterFlatAdd), evidence, hasRealDamageRange ? "real min/max damage range preserves rolled weapon damage" : "repository legacy fallback floor");

            int currentDamage = Math.Max(activeMinimumDamageFloor, afterFlatAdd);

            TraceBlockedIfNeeded(result, request.Definition.BulletCount > 1 || request.Context.SpecialAttackCategory != SpecialAttackCategory.None, "ResolveSpecialSubHits", currentDamage, "special attack formulas are represented but not active without evidence");
            TraceBlockedIfNeeded(result, request.Definition.BulletCount > 1, "AggregateSubHits", currentDamage, "multi-hit aggregation ordering is unresolved");
            TraceBlockedIfNeeded(result, request.Definition.BulletCount > 1, "ApplySpecialCompression", currentDamage, "special compression thresholds are unresolved");
            TraceBlockedIfNeeded(result, request.Definition.AttackSpecificCap > 0, "ApplyAttackSpecificCap", currentDamage, "attack-specific cap ordering is unresolved");
            TraceBlockedIfNeeded(result, request.Context.Mode == DamageCalculationMode.PvP, "ApplyPvPConversion", currentDamage, "PvP conversion ratio and rounding are unresolved");
            TraceBlockedIfNeeded(result, request.Context.Mode == DamageCalculationMode.PvP, "ApplyPvPMaximumHealthCap", currentDamage, "PvP maximum-health cap semantics are unresolved");
            TraceBlockedIfNeeded(result, request.Mitigation.ReflectPercentage != 0 || request.Mitigation.ReflectCap != 0, "ApplyReflect", currentDamage, "reflect prevention and cap ordering are unresolved");
            TraceBlockedIfNeeded(result, request.Mitigation.TypedAbsorbPool != 0, "ConsumeTypedAbsorbs", currentDamage, "typed absorb ordering and mutation are unresolved");
            TraceBlockedIfNeeded(result, request.Mitigation.UniversalAbsorbPool != 0, "ConsumeUniversalAbsorbs", currentDamage, "universal absorb ordering and mutation are unresolved");
            TraceBlockedIfNeeded(result, request.Mitigation.ReflectPercentage != 0, "ResolveReflectedReturnDamage", currentDamage, "returned reflect damage is not wired to production events");
            TraceBlockedIfNeeded(result, request.Mitigation.DamageShield != 0, "ResolveDamageShieldReturnDamage", currentDamage, "damage-shield return events are not wired to production events");

            result.FinalTargetDamage = currentDamage < 0 ? 0 : currentDamage;
            result.Trace.Add("ClampFinalValues", DamageCalculationStageStatus.Applied, currentDamage, result.FinalTargetDamage, evidence, "final target damage cannot be negative");
            result.Trace.Add("ReturnTrace", DamageCalculationStageStatus.Applied, result.FinalTargetDamage, result.FinalTargetDamage, evidence, "side-effect-free result only");

            return result;
        }

        private static DamageCalculationStrategyKind SelectStrategy(
            DamageCalculationRequest request,
            out string reason)
        {
            if (request.Policy.IsFixedCapturedDamage || request.Definition.FixedDamage > 0)
            {
                reason = "fixed captured damage selected; AR, AC, criticals, and add-damage are not applied";
                return DamageCalculationStrategyKind.FixedCapturedDamage;
            }

            if (request.Policy.EnableEvidenceBackedWeaponFormula)
            {
                if (!IsFormulaBackedWeaponRequestComplete(request, out reason))
                {
                    return DamageCalculationStrategyKind.EvidenceBlocked;
                }

                reason = "formula-ready request shape is present, but production AO weapon ordering is not proven in this repository";
                return DamageCalculationStrategyKind.EvidenceBlocked;
            }

            reason = "legacy repository damage selected because AO weapon formula evidence is incomplete";
            return DamageCalculationStrategyKind.LegacyFallback;
        }

        private static bool IsFormulaBackedWeaponRequestComplete(
            DamageCalculationRequest request,
            out string reason)
        {
            if (request.Definition.BaseMinimum < 0 || request.Definition.BaseMaximum <= 0)
            {
                reason = "missing or invalid weapon min/max damage";
                return false;
            }

            if (request.Definition.DamageType == DamageType.Unknown)
            {
                reason = "missing proven weapon damage type";
                return false;
            }

            if (request.Source.AttackRating == 0 && request.Source.AttackSkillContributions.Count == 0)
            {
                reason = "missing proven effective attack rating or weighted skill contributions";
                return false;
            }

            if (!request.Mitigation.HasMatchingArmor)
            {
                reason = "missing explicit target matching AC input; zero AC must be supplied as known zero";
                return false;
            }

            if (!request.Definition.HasCriticalState)
            {
                reason = "missing resolved critical state";
                return false;
            }

            if (request.Definition.IsCritical && !request.Definition.HasCriticalBonus)
            {
                reason = "critical hit is requested without a proven critical bonus input";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static void NormalizeRequest(DamageCalculationRequest request)
        {
            if (request.Context == null)
            {
                request.Context = new DamageCalculationContext();
            }

            if (request.Source == null)
            {
                request.Source = new DamageSourceSnapshot();
            }

            if (request.Target == null)
            {
                request.Target = new DamageTargetSnapshot();
            }

            if (request.Definition == null)
            {
                request.Definition = new DamageDefinition();
            }

            if (request.Modifiers == null)
            {
                request.Modifiers = new DamageModifierSet();
            }

            if (request.Mitigation == null)
            {
                request.Mitigation = new DamageMitigationSet();
            }

            if (request.Policy == null)
            {
                request.Policy = DamageCalculationPolicy.RepositoryLegacyNormalHit(false);
            }
        }

        private static DamageEvidenceClassification ResolveEvidence(DamageCalculationRequest request)
        {
            if (request.EvidenceClassification != DamageEvidenceClassification.Unknown)
            {
                return request.EvidenceClassification;
            }

            if (request.Definition.EvidenceClassification != DamageEvidenceClassification.Unknown)
            {
                return request.Definition.EvidenceClassification;
            }

            return request.Policy.EvidenceClassification;
        }

        private static int ResolveFallbackFloor(DamageCalculationRequest request)
        {
            return request.Source.Category == DamageSourceCategory.Player
                       ? request.Policy.PlayerFallbackDamage
                       : request.Policy.NpcFallbackDamage;
        }

        private static int ResolveBaseDamage(
            DamageCalculationRequest request,
            IDamageRandomSource randomSource,
            int normalizedMinimum,
            int normalizedMaximum,
            int fallbackFloor,
            DamageCalculationResult result)
        {
            if (request.Policy.IsFixedCapturedDamage || request.Definition.FixedDamage > 0)
            {
                int fixedDamage = request.Definition.FixedDamage > 0 ? request.Definition.FixedDamage : normalizedMaximum;
                result.Trace.Add("RollOrSelectBaseDamage", DamageCalculationStageStatus.Applied, fixedDamage, fixedDamage, DamageEvidenceClassification.ProvenCapturedBehavior, "fixed captured damage bypasses unproven AR and AC formulas");
                return fixedDamage;
            }

            if (normalizedMaximum > 0)
            {
                int rolledDamage = normalizedMinimum == normalizedMaximum
                                       ? normalizedMaximum
                                       : randomSource.NextInclusive(normalizedMinimum, normalizedMaximum);
                result.Trace.Add("RollOrSelectBaseDamage", DamageCalculationStageStatus.Preserved, normalizedMinimum, rolledDamage, DamageEvidenceClassification.ProvenRepositoryBehavior, "inclusive repository min/max roll");
                return rolledDamage;
            }

            int levelDamage = Math.Max(fallbackFloor, request.Source.Level);
            result.Trace.Add("RollOrSelectBaseDamage", DamageCalculationStageStatus.Preserved, request.Source.Level, levelDamage, DamageEvidenceClassification.ProvenRepositoryBehavior, "repository level fallback when no max damage exists");
            return levelDamage;
        }

        private static void TraceBlockedIfNeeded(
            DamageCalculationResult result,
            bool condition,
            string stage,
            int currentDamage,
            string note)
        {
            if (condition)
            {
                result.Trace.Add(stage, DamageCalculationStageStatus.EvidenceBlocked, currentDamage, currentDamage, DamageEvidenceClassification.Unknown, note);
            }
            else
            {
                result.Trace.Add(stage, DamageCalculationStageStatus.Skipped, currentDamage, currentDamage, DamageEvidenceClassification.ProvenRepositoryBehavior, "not active for migrated repository behavior");
            }
        }
    }

    public enum WeaponDamageRequestBuildClassification
    {
        FormulaInputComplete,
        FormulaInputIncomplete,
        LegacyRequired,
        FixedCaptured,
        MalformedData
    }

    public enum WeaponDamageInputIssueKind
    {
        MissingWeaponTemplate,
        MissingMinimum,
        MissingMaximum,
        MinimumGreaterThanMaximum,
        MissingDamageType,
        UnknownDamageType,
        MissingAttackSkill,
        UnknownAttackStat,
        InvalidSkillWeight,
        MissingAttackerStat,
        DuplicateAttackerStat,
        MissingArmorStat,
        UnknownArmorMapping,
        MissingAmsCapSemantics,
        NegativeAmsCap,
        MissingAddDamageSource,
        MissingCriticalState,
        MissingCriticalBonus
    }

    public enum WeaponDamageAttackerReadiness
    {
        CompleteStatProvenance,
        PartialStatProvenance,
        LegacyOnly,
        FixedCaptured
    }

    public sealed class WeaponDamageInputProvenance
    {
        public WeaponDamageInputProvenance()
        {
            this.InputName = string.Empty;
            this.StorageSource = string.Empty;
            this.DatabaseSource = string.Empty;
            this.FieldOrStatId = string.Empty;
            this.LoadPath = string.Empty;
            this.RuntimeOwner = string.Empty;
            this.LookupPath = string.Empty;
            this.DataType = string.Empty;
            this.Signedness = string.Empty;
            this.DefaultBehavior = string.Empty;
            this.MissingDataBehavior = string.Empty;
            this.DuplicateDataBehavior = string.Empty;
            this.ActiveCallerAvailability = string.Empty;
            this.ValueState = string.Empty;
        }

        public string InputName { get; set; }

        public string StorageSource { get; set; }

        public string DatabaseSource { get; set; }

        public string FieldOrStatId { get; set; }

        public string LoadPath { get; set; }

        public string RuntimeOwner { get; set; }

        public string LookupPath { get; set; }

        public string DataType { get; set; }

        public string Signedness { get; set; }

        public string DefaultBehavior { get; set; }

        public string MissingDataBehavior { get; set; }

        public string DuplicateDataBehavior { get; set; }

        public DamageEvidenceClassification EvidenceClassification { get; set; }

        public string ActiveCallerAvailability { get; set; }

        public string ValueState { get; set; }

        public int? ResolvedValue { get; set; }
    }

    public sealed class WeaponDamageInputIssue
    {
        public WeaponDamageInputIssue()
        {
            this.InputName = string.Empty;
            this.Detail = string.Empty;
            this.EvidenceClassification = DamageEvidenceClassification.Unknown;
        }

        public WeaponDamageInputIssueKind Kind { get; set; }

        public string InputName { get; set; }

        public string Detail { get; set; }

        public DamageEvidenceClassification EvidenceClassification { get; set; }
    }

    public sealed class WeaponDamageStatSnapshot
    {
        public WeaponDamageStatSnapshot()
        {
            this.Source = string.Empty;
        }

        public int StatId { get; set; }

        public int Value { get; set; }

        public string Source { get; set; }
    }

    public sealed class WeaponDamageWeaponSnapshot
    {
        public WeaponDamageWeaponSnapshot()
        {
            this.TemplateIdentity = string.Empty;
            this.TemplateSource = string.Empty;
            this.AttackSkillContributions = new List<AttackSkillContribution>();
        }

        public string TemplateIdentity { get; set; }

        public string TemplateSource { get; set; }

        public int QualityLevel { get; set; }

        public bool HasMinimumDamage { get; set; }

        public int MinimumDamage { get; set; }

        public bool HasMaximumDamage { get; set; }

        public int MaximumDamage { get; set; }

        public bool HasCriticalBonus { get; set; }

        public int CriticalBonus { get; set; }

        public bool HasDamageType { get; set; }

        public DamageType DamageType { get; set; }

        public int RawDamageTypeStat { get; set; }

        public bool HasAmsCap { get; set; }

        public int AmsCap { get; set; }

        public bool HasAttackTime { get; set; }

        public int AttackTime { get; set; }

        public bool HasRechargeTime { get; set; }

        public int RechargeTime { get; set; }

        public int WeaponCategory { get; set; }

        public int WeaponSlot { get; set; }

        public IList<AttackSkillContribution> AttackSkillContributions { get; private set; }
    }

    public sealed class WeaponDamageActorSnapshot
    {
        public WeaponDamageActorSnapshot()
        {
            this.Identity = string.Empty;
            this.Stats = new List<WeaponDamageStatSnapshot>();
            this.Category = DamageSourceCategory.Unknown;
            this.Readiness = WeaponDamageAttackerReadiness.PartialStatProvenance;
        }

        public string Identity { get; set; }

        public DamageSourceCategory Category { get; set; }

        public WeaponDamageAttackerReadiness Readiness { get; set; }

        public IList<WeaponDamageStatSnapshot> Stats { get; private set; }
    }

    public sealed class WeaponDamageRequestBuildInput
    {
        public WeaponDamageRequestBuildInput()
        {
            this.CallerName = string.Empty;
            this.Weapon = new WeaponDamageWeaponSnapshot();
            this.Attacker = new WeaponDamageActorSnapshot();
            this.Target = new WeaponDamageActorSnapshot();
        }

        public string CallerName { get; set; }

        public bool IsFixedCapturedDamage { get; set; }

        public int FixedCapturedDamage { get; set; }

        public WeaponDamageWeaponSnapshot Weapon { get; set; }

        public WeaponDamageActorSnapshot Attacker { get; set; }

        public WeaponDamageActorSnapshot Target { get; set; }

        public bool HasCriticalState { get; set; }

        public bool IsCritical { get; set; }

        public bool HasUniversalAddDamageSource { get; set; }

        public int UniversalAddDamage { get; set; }
    }

    public sealed class WeaponDamageRequestBuildResult
    {
        public WeaponDamageRequestBuildResult()
        {
            this.Provenance = new List<WeaponDamageInputProvenance>();
            this.Issues = new List<WeaponDamageInputIssue>();
            this.Request = new DamageCalculationRequest();
            this.ExpectedActiveStrategy = DamageCalculationStrategyKind.LegacyFallback;
        }

        public WeaponDamageRequestBuildClassification Classification { get; set; }

        public DamageCalculationStrategyKind ExpectedActiveStrategy { get; set; }

        public DamageCalculationRequest Request { get; set; }

        public IList<WeaponDamageInputProvenance> Provenance { get; private set; }

        public IList<WeaponDamageInputIssue> Issues { get; private set; }

        public bool HasIssue(WeaponDamageInputIssueKind kind)
        {
            return this.Issues.Any(x => x.Kind == kind);
        }
    }

    public static class WeaponDamageRequestBuilder
    {
        private const int AddAllOffStatId = 276;

        public static WeaponDamageRequestBuildResult Build(WeaponDamageRequestBuildInput input)
        {
            if (input == null)
            {
                input = new WeaponDamageRequestBuildInput();
            }

            if (input.Weapon == null)
            {
                input.Weapon = new WeaponDamageWeaponSnapshot();
            }

            if (input.Attacker == null)
            {
                input.Attacker = new WeaponDamageActorSnapshot();
            }

            if (input.Target == null)
            {
                input.Target = new WeaponDamageActorSnapshot();
            }

            WeaponDamageRequestBuildResult result = new WeaponDamageRequestBuildResult();
            AddCommonProvenance(result, input);

            if (input.IsFixedCapturedDamage)
            {
                BuildFixedCaptured(input, result);
                return result;
            }

            ValidateWeaponTemplate(input, result);
            ValidateDamageRange(input, result);
            ValidateDamageType(input, result);
            ValidateAttackSkills(input, result);
            ResolveAttackerStat(input.Attacker, AddAllOffStatId, "Add All Off", result);
            ValidateAmsCap(input, result);
            ValidateArmor(input, result);
            ValidateAddDamage(input, result);
            ValidateCriticalState(input, result);
            BuildDiagnosticRequest(input, result);
            Classify(result);
            return result;
        }

        private static void BuildFixedCaptured(
            WeaponDamageRequestBuildInput input,
            WeaponDamageRequestBuildResult result)
        {
            result.Classification = WeaponDamageRequestBuildClassification.FixedCaptured;
            result.ExpectedActiveStrategy = DamageCalculationStrategyKind.FixedCapturedDamage;
            result.Request = new DamageCalculationRequest
            {
                Source = new DamageSourceSnapshot
                {
                    Category = input.Attacker.Category
                },
                Definition = new DamageDefinition
                {
                    FixedDamage = input.FixedCapturedDamage,
                    BaseMinimum = input.FixedCapturedDamage,
                    BaseMaximum = input.FixedCapturedDamage,
                    DamageType = input.Weapon.DamageType,
                    EvidenceClassification = DamageEvidenceClassification.ProvenCapturedBehavior
                },
                Policy = DamageCalculationPolicy.CapturedFixedDamage(input.CallerName),
                EvidenceClassification = DamageEvidenceClassification.ProvenCapturedBehavior
            };
        }

        private static void ValidateWeaponTemplate(
            WeaponDamageRequestBuildInput input,
            WeaponDamageRequestBuildResult result)
        {
            if (string.IsNullOrEmpty(input.Weapon.TemplateIdentity))
            {
                AddIssue(result, WeaponDamageInputIssueKind.MissingWeaponTemplate, "equipped weapon identity", "no weapon template identity was supplied");
            }
        }

        private static void ValidateDamageRange(
            WeaponDamageRequestBuildInput input,
            WeaponDamageRequestBuildResult result)
        {
            if (!input.Weapon.HasMinimumDamage)
            {
                AddIssue(result, WeaponDamageInputIssueKind.MissingMinimum, "minimum damage", "weapon template has no explicit minimum damage stat");
            }

            if (!input.Weapon.HasMaximumDamage)
            {
                AddIssue(result, WeaponDamageInputIssueKind.MissingMaximum, "maximum damage", "weapon template has no explicit maximum damage stat");
            }

            if (input.Weapon.HasMinimumDamage
                && input.Weapon.HasMaximumDamage
                && input.Weapon.MinimumDamage > input.Weapon.MaximumDamage)
            {
                AddIssue(result, WeaponDamageInputIssueKind.MinimumGreaterThanMaximum, "minimum damage", "minimum damage is greater than maximum damage");
            }
        }

        private static void ValidateDamageType(
            WeaponDamageRequestBuildInput input,
            WeaponDamageRequestBuildResult result)
        {
            if (!input.Weapon.HasDamageType)
            {
                AddIssue(result, WeaponDamageInputIssueKind.MissingDamageType, "damage type", "weapon template has no explicit damage type stat");
                return;
            }

            if (input.Weapon.DamageType == DamageType.Unknown)
            {
                AddIssue(result, WeaponDamageInputIssueKind.UnknownDamageType, "damage type", "damage type stat does not map to a supported calculator damage type");
            }
        }

        private static void ValidateAttackSkills(
            WeaponDamageRequestBuildInput input,
            WeaponDamageRequestBuildResult result)
        {
            if (input.Weapon.AttackSkillContributions.Count == 0)
            {
                AddIssue(result, WeaponDamageInputIssueKind.MissingAttackSkill, "attack skills", "weapon template has no attack-skill contribution entries");
                return;
            }

            int totalWeight = 0;
            foreach (AttackSkillContribution contribution in input.Weapon.AttackSkillContributions)
            {
                totalWeight += contribution.Percentage;
                if (contribution.StatId <= 0)
                {
                    AddIssue(result, WeaponDamageInputIssueKind.UnknownAttackStat, "attack skills", "attack-skill stat id is missing or unsupported");
                    continue;
                }

                if (contribution.Percentage <= 0 || contribution.Percentage > 100)
                {
                    AddIssue(result, WeaponDamageInputIssueKind.InvalidSkillWeight, "attack skills", "attack-skill percentage must be between 1 and 100");
                }

                ResolveAttackerStat(input.Attacker, contribution.StatId, "attack skill " + contribution.StatId, result);
            }

            if (totalWeight != 100)
            {
                AddIssue(result, WeaponDamageInputIssueKind.InvalidSkillWeight, "attack skills", "attack-skill percentages must total 100 for formula readiness");
            }
        }

        private static void ValidateAmsCap(
            WeaponDamageRequestBuildInput input,
            WeaponDamageRequestBuildResult result)
        {
            if (!input.Weapon.HasAmsCap)
            {
                AddIssue(result, WeaponDamageInputIssueKind.MissingAmsCapSemantics, "AMS cap", "weapon AMS cap is absent; absence is not proven equivalent to unlimited");
                return;
            }

            if (input.Weapon.AmsCap == 0)
            {
                AddIssue(result, WeaponDamageInputIssueKind.MissingAmsCapSemantics, "AMS cap", "zero AMS cap semantics are not proven");
            }

            if (input.Weapon.AmsCap < 0)
            {
                AddIssue(result, WeaponDamageInputIssueKind.NegativeAmsCap, "AMS cap", "negative AMS cap is malformed for formula request construction");
            }
        }

        private static void ValidateArmor(
            WeaponDamageRequestBuildInput input,
            WeaponDamageRequestBuildResult result)
        {
            int armorStatId;
            if (!DamageCalculator.TryGetArmorStatForDamageType(input.Weapon.DamageType, out armorStatId))
            {
                AddIssue(result, WeaponDamageInputIssueKind.UnknownArmorMapping, "matching armor", "no proven armor stat mapping exists for the selected damage type");
                return;
            }

            ResolveTargetStat(input.Target, armorStatId, "matching armor", WeaponDamageInputIssueKind.MissingArmorStat, result);
        }

        private static void ValidateAddDamage(
            WeaponDamageRequestBuildInput input,
            WeaponDamageRequestBuildResult result)
        {
            int addDamageStatId;
            if (!DamageCalculator.TryGetAddDamageStatForDamageType(input.Weapon.DamageType, out addDamageStatId))
            {
                AddIssue(result, WeaponDamageInputIssueKind.MissingAddDamageSource, "type-specific add damage", "no proven type-specific add-damage stat mapping exists for the selected damage type");
            }
            else
            {
                ResolveAttackerStat(input.Attacker, addDamageStatId, "type-specific add damage", result);
            }

            if (!input.HasUniversalAddDamageSource)
            {
                AddIssue(result, WeaponDamageInputIssueKind.MissingAddDamageSource, "universal add damage", "no universal weapon add-damage stat contract is proven in this repository");
            }
        }

        private static void ValidateCriticalState(
            WeaponDamageRequestBuildInput input,
            WeaponDamageRequestBuildResult result)
        {
            if (!input.HasCriticalState)
            {
                AddIssue(result, WeaponDamageInputIssueKind.MissingCriticalState, "critical state", "current ordinary weapon callers do not provide a resolved normal/critical state");
            }

            if (input.IsCritical && !input.Weapon.HasCriticalBonus)
            {
                AddIssue(result, WeaponDamageInputIssueKind.MissingCriticalBonus, "critical bonus", "critical hit requested without a proven critical bonus input");
            }
        }

        private static int? ResolveAttackerStat(
            WeaponDamageActorSnapshot actor,
            int statId,
            string inputName,
            WeaponDamageRequestBuildResult result)
        {
            IList<WeaponDamageStatSnapshot> matches = actor.Stats.Where(x => x.StatId == statId).ToList();
            if (matches.Count == 0)
            {
                AddIssue(result, WeaponDamageInputIssueKind.MissingAttackerStat, inputName, "attacker stat " + statId + " is not available");
                return null;
            }

            if (matches.Count > 1)
            {
                AddIssue(result, WeaponDamageInputIssueKind.DuplicateAttackerStat, inputName, "attacker stat " + statId + " has duplicate entries");
                return null;
            }

            return matches[0].Value;
        }

        private static int? ResolveTargetStat(
            WeaponDamageActorSnapshot actor,
            int statId,
            string inputName,
            WeaponDamageInputIssueKind missingKind,
            WeaponDamageRequestBuildResult result)
        {
            IList<WeaponDamageStatSnapshot> matches = actor.Stats.Where(x => x.StatId == statId).ToList();
            if (matches.Count == 0)
            {
                AddIssue(result, missingKind, inputName, "target stat " + statId + " is not available");
                return null;
            }

            if (matches.Count > 1)
            {
                AddIssue(result, WeaponDamageInputIssueKind.DuplicateAttackerStat, inputName, "target stat " + statId + " has duplicate entries");
                return null;
            }

            return matches[0].Value;
        }

        private static void BuildDiagnosticRequest(
            WeaponDamageRequestBuildInput input,
            WeaponDamageRequestBuildResult result)
        {
            DamageCalculationRequest request = new DamageCalculationRequest
            {
                Source = new DamageSourceSnapshot
                {
                    Category = input.Attacker.Category,
                    Identity = input.Attacker.Identity
                },
                Definition = new DamageDefinition
                {
                    BaseMinimum = input.Weapon.MinimumDamage,
                    BaseMaximum = input.Weapon.MaximumDamage,
                    CriticalBonus = input.Weapon.CriticalBonus,
                    DamageType = input.Weapon.DamageType,
                    HasAttackRatingCap = input.Weapon.HasAmsCap,
                    AttackRatingCap = input.Weapon.AmsCap,
                    HasCriticalState = input.HasCriticalState,
                    IsCritical = input.IsCritical,
                    HasCriticalBonus = input.Weapon.HasCriticalBonus
                },
                Policy = DamageCalculationPolicy.EvidenceBackedWeaponFormula(input.CallerName),
                EvidenceClassification = DamageEvidenceClassification.Unknown
            };

            foreach (AttackSkillContribution contribution in input.Weapon.AttackSkillContributions)
            {
                int? statValue = ResolveStatValueOnly(input.Attacker, contribution.StatId);
                request.Source.AttackSkillContributions.Add(
                    new AttackSkillContribution
                    {
                        StatId = contribution.StatId,
                        Percentage = contribution.Percentage,
                        Value = statValue.HasValue ? statValue.Value : contribution.Value
                    });
            }

            int? addAllOff = ResolveStatValueOnly(input.Attacker, AddAllOffStatId);
            request.Source.AddAllOff = addAllOff.HasValue ? addAllOff.Value : 0;

            int armorStatId;
            if (DamageCalculator.TryGetArmorStatForDamageType(input.Weapon.DamageType, out armorStatId))
            {
                int? armor = ResolveStatValueOnly(input.Target, armorStatId);
                if (armor.HasValue)
                {
                    request.Mitigation.HasMatchingArmor = true;
                    request.Mitigation.MatchingArmor = armor.Value;
                }
            }

            int addDamageStatId;
            if (DamageCalculator.TryGetAddDamageStatForDamageType(input.Weapon.DamageType, out addDamageStatId))
            {
                int? addDamage = ResolveStatValueOnly(input.Attacker, addDamageStatId);
                request.Modifiers.TypeSpecificAddDamage = addDamage.HasValue ? addDamage.Value : 0;
            }

            if (input.HasUniversalAddDamageSource)
            {
                request.Modifiers.UniversalAddDamage = input.UniversalAddDamage;
            }

            result.Request = request;
            result.ExpectedActiveStrategy = DamageCalculationStrategyKind.LegacyFallback;
        }

        private static int? ResolveStatValueOnly(WeaponDamageActorSnapshot actor, int statId)
        {
            IList<WeaponDamageStatSnapshot> matches = actor.Stats.Where(x => x.StatId == statId).ToList();
            if (matches.Count == 1)
            {
                return matches[0].Value;
            }

            return null;
        }

        private static void AddCommonProvenance(
            WeaponDamageRequestBuildResult result,
            WeaponDamageRequestBuildInput input)
        {
            AddProvenance(result, "minimum damage", "ItemTemplate.Stats", "items.dat", "StatIds.mindamage / 286", "ItemLoader.CacheAllItems -> Item.GetAttribute", "combat attack-source builder", "weapon.GetAttribute(286)", "int", "signed", "StatNamesDefaults fallback if not explicit", "formula builder reports missing explicit stat", "dictionary duplicate keys not representable", DamageEvidenceClassification.ProvenRepositoryBehavior, input.Weapon.HasMinimumDamage ? "available" : "missing", input.Weapon.MinimumDamage);
            AddProvenance(result, "maximum damage", "ItemTemplate.Stats", "items.dat", "StatIds.maxdamage / 285", "ItemLoader.CacheAllItems -> Item.GetAttribute", "combat attack-source builder", "weapon.GetAttribute(285)", "int", "signed", "StatNamesDefaults fallback if not explicit", "formula builder reports missing explicit stat", "dictionary duplicate keys not representable", DamageEvidenceClassification.ProvenRepositoryBehavior, input.Weapon.HasMaximumDamage ? "available" : "missing", input.Weapon.MaximumDamage);
            AddProvenance(result, "legacy DamageBonus", "ItemTemplate.Stats or character stats", "items.dat or character stats table", "StatIds.damagebonus / 284", "Item.GetAttribute or character.Stats", "CombatStrikeDamageCalculator", "damageBonus parameter", "int", "signed then clamped by legacy rule", "missing item stat falls back through StatNamesDefaults", "kept separate from AO add damage", "stat-list duplicates surface through Single-based accessors", DamageEvidenceClassification.ProvenRepositoryBehavior, "active legacy only", null);
            AddProvenance(result, "damage type", "ItemTemplate.Stats", "items.dat", "StatIds.damagetype / 436", "ItemLoader.CacheAllItems -> Item.GetAttribute", "combat attack-source builder", "weapon.GetAttribute(436)", "int enum", "signed", "missing explicit stat is not formula-ready", "formula builder reports missing damage type", "dictionary duplicate keys not representable", DamageEvidenceClassification.ProvenRepositoryBehavior, input.Weapon.HasDamageType ? "available" : "missing", input.Weapon.RawDamageTypeStat);
            AddProvenance(result, "attack skills", "ItemTemplate.Attack dictionary", "items.dat", "attack stat id -> percentage", "ItemLoader.CacheAllItems", "not used by active combat callers", "template.Attack", "int/int", "signed", "missing dictionary entries are absent", "formula builder reports missing attack skill", "dictionary duplicate keys not representable", DamageEvidenceClassification.ProvenDatabaseContract, input.Weapon.AttackSkillContributions.Count > 0 ? "available diagnostic-only" : "missing", input.Weapon.AttackSkillContributions.Count);
            AddProvenance(result, "Add All Off", "character stat", "character stats table plus runtime modifiers", "AMSModifier / 276", "Stats.ReadStatsfromSql -> Stat.Value", "attacker stat owner", "character.Stats[276].Value", "int", "signed calculated", "stat default exists in Stats list", "builder reports missing supplied stat snapshot", "duplicates surface as malformed in builder", DamageEvidenceClassification.ProvenRepositoryBehavior, "not used by active damage", null);
            AddProvenance(result, "AMS cap", "ItemTemplate.Stats", "items.dat", "AMSCap / 538", "ItemLoader.CacheAllItems -> Item.GetAttribute", "not used by active combat callers", "weapon.GetAttribute(538)", "int", "signed", "zero/absence semantics unresolved", "builder reports missing or zero cap semantics", "dictionary duplicate keys not representable", DamageEvidenceClassification.ProvenDatabaseContract, input.Weapon.HasAmsCap ? "available diagnostic-only" : "missing", input.Weapon.AmsCap);
            AddProvenance(result, "matching armor", "target stats", "character stats table plus runtime modifiers", "damage-type AC stat mapping", "Stats.ReadStatsfromSql -> Stat.Value", "target stat owner", "target.Stats[armorStat].Value", "int", "signed calculated", "missing is not proven zero", "builder reports missing armor stat", "duplicates surface as malformed in builder", DamageEvidenceClassification.ProvenDatabaseContract, "not used by active damage", null);
            AddProvenance(result, "type-specific add damage", "attacker stats", "character stats table plus runtime modifiers", "damage-type add-damage stat mapping", "Stats.ReadStatsfromSql -> Stat.Value", "attacker stat owner", "attacker.Stats[addDamageStat].Value", "int", "signed calculated", "missing is not proven zero", "builder reports missing add-damage source", "duplicates surface as malformed in builder", DamageEvidenceClassification.ProvenDatabaseContract, "not used by active damage", null);
            AddProvenance(result, "universal add damage", "unknown", "unknown", "none proven", "none", "none", "none", "unknown", "unknown", "no default allowed", "builder reports missing add-damage source", "unknown", DamageEvidenceClassification.Unknown, "unavailable", null);
            AddProvenance(result, "critical state", "unknown active caller state", "none", "normal/critical resolution", "none", "combat hit resolution seam", "not supplied by ordinary callers", "bool", "n/a", "no random criticals introduced", "builder reports missing critical state", "n/a", DamageEvidenceClassification.Unknown, input.HasCriticalState ? "supplied diagnostic-only" : "missing", input.HasCriticalState ? 1 : 0);
        }

        private static void AddProvenance(
            WeaponDamageRequestBuildResult result,
            string inputName,
            string storageSource,
            string databaseSource,
            string fieldOrStatId,
            string loadPath,
            string runtimeOwner,
            string lookupPath,
            string dataType,
            string signedness,
            string defaultBehavior,
            string missingDataBehavior,
            string duplicateDataBehavior,
            DamageEvidenceClassification evidenceClassification,
            string valueState,
            int? resolvedValue)
        {
            result.Provenance.Add(
                new WeaponDamageInputProvenance
                {
                    InputName = inputName,
                    StorageSource = storageSource,
                    DatabaseSource = databaseSource,
                    FieldOrStatId = fieldOrStatId,
                    LoadPath = loadPath,
                    RuntimeOwner = runtimeOwner,
                    LookupPath = lookupPath,
                    DataType = dataType,
                    Signedness = signedness,
                    DefaultBehavior = defaultBehavior,
                    MissingDataBehavior = missingDataBehavior,
                    DuplicateDataBehavior = duplicateDataBehavior,
                    EvidenceClassification = evidenceClassification,
                    ActiveCallerAvailability = valueState,
                    ValueState = valueState,
                    ResolvedValue = resolvedValue
                });
        }

        private static void AddIssue(
            WeaponDamageRequestBuildResult result,
            WeaponDamageInputIssueKind kind,
            string inputName,
            string detail)
        {
            result.Issues.Add(
                new WeaponDamageInputIssue
                {
                    Kind = kind,
                    InputName = inputName,
                    Detail = detail,
                    EvidenceClassification = DamageEvidenceClassification.Unknown
                });
        }

        private static void Classify(WeaponDamageRequestBuildResult result)
        {
            if (result.HasIssue(WeaponDamageInputIssueKind.MinimumGreaterThanMaximum)
                || result.HasIssue(WeaponDamageInputIssueKind.NegativeAmsCap)
                || result.HasIssue(WeaponDamageInputIssueKind.DuplicateAttackerStat))
            {
                result.Classification = WeaponDamageRequestBuildClassification.MalformedData;
                return;
            }

            if (result.HasIssue(WeaponDamageInputIssueKind.MissingWeaponTemplate))
            {
                result.Classification = WeaponDamageRequestBuildClassification.LegacyRequired;
                return;
            }

            result.Classification = result.Issues.Count == 0
                                        ? WeaponDamageRequestBuildClassification.FormulaInputComplete
                                        : WeaponDamageRequestBuildClassification.FormulaInputIncomplete;
        }
    }

    public enum WeaponDamageObservationSourceKind
    {
        RepositorySynthetic,
        PrivateServerControlled,
        ExternalLiveClient
    }

    public enum WeaponDamageHitKind
    {
        KnownNormal,
        KnownCritical,
        UnknownHitKind,
        Miss,
        Evade,
        Block,
        Parry,
        Riposte
    }

    public enum WeaponDamageObservationValidationStatus
    {
        Complete,
        Incomplete,
        Rejected
    }

    public enum WeaponDamageObservationIssueKind
    {
        HealthDeltaMismatch,
        AmbiguousWeaponIdentity,
        UnknownDamageType,
        ContradictoryAttackerStats,
        AmbiguousTargetArmor,
        MultipleDamageSourcesPossible,
        ExternalDamagePossible,
        CriticalStateClaimedWithoutEvidence,
        IncompletePacketOrder,
        MissingAddAllOff,
        MissingAmsCapSemantics,
        MissingArmor,
        UnknownHitKind
    }

    public enum WeaponDamageCandidateArOrdering
    {
        BasePlusTruncatedBaseTimesArOver400,
        TruncateBaseTimes400PlusArOver400,
        TruncateBaseTimesMultiplier
    }

    public enum WeaponDamageCandidateAcOrdering
    {
        None,
        SubtractTruncatedAcOver10BeforeMinimumFloor,
        SubtractTruncatedAcOver10AfterMinimumFloor,
        ApplyToCriticalBonus,
        DoNotApplyToCriticalBonus
    }

    public enum WeaponDamageCandidateAddDamageOrdering
    {
        AfterArAndAc,
        BeforeMinimumFloor,
        AfterMinimumFloor,
        ArScaled,
        NotArScaled
    }

    public enum WeaponDamageCandidateCriticalOrdering
    {
        None,
        MaximumPlusCriticalBonus,
        RollPlusCriticalBonus,
        CriticalBonusArScaled,
        CriticalBonusUnscaled,
        CriticalBonusAcReduced,
        CriticalMinimumFloor
    }

    public enum WeaponDamageCandidateAmsCapBehavior
    {
        MissingCapMeansNoCap,
        ZeroCapMeansNoCap,
        ZeroCapMeansLiteralZero,
        NegativeCapInvalid,
        CapAppliedBeforePost1000Handling
    }

    public sealed class WeaponDamageObservationSource
    {
        public WeaponDamageObservationSource()
        {
            this.ObservationId = string.Empty;
            this.CaptureDate = string.Empty;
            this.Environment = string.Empty;
            this.PacketEvidenceReference = string.Empty;
            this.LogEvidenceReference = string.Empty;
            this.TimingReference = string.Empty;
            this.Classification = DamageEvidenceClassification.Unknown;
            this.SourceKind = WeaponDamageObservationSourceKind.RepositorySynthetic;
        }

        public string ObservationId { get; set; }

        public WeaponDamageObservationSourceKind SourceKind { get; set; }

        public string CaptureDate { get; set; }

        public string Environment { get; set; }

        public string PacketEvidenceReference { get; set; }

        public string LogEvidenceReference { get; set; }

        public string TimingReference { get; set; }

        public DamageEvidenceClassification Classification { get; set; }
    }

    public sealed class WeaponDamageObservationInput
    {
        public WeaponDamageObservationInput()
        {
            this.AttackerIdentity = string.Empty;
            this.TargetIdentity = string.Empty;
            this.WeaponTemplateIdentity = string.Empty;
            this.WeaponInstanceIdentity = string.Empty;
            this.AttackSkillDefinitions = new List<AttackSkillContribution>();
            this.KnownUncertainties = new List<string>();
        }

        public string AttackerIdentity { get; set; }

        public DamageSourceCategory AttackerCategory { get; set; }

        public string TargetIdentity { get; set; }

        public string WeaponTemplateIdentity { get; set; }

        public string WeaponInstanceIdentity { get; set; }

        public int? WeaponQualityLevel { get; set; }

        public int? WeaponMinimum { get; set; }

        public int? WeaponMaximum { get; set; }

        public int? BaseRoll { get; set; }

        public int? LegacyDamageBonus { get; set; }

        public int? CriticalBonus { get; set; }

        public int? RawDamageType { get; set; }

        public DamageType MappedDamageType { get; set; }

        public int? AttackRating { get; set; }

        public int? AddAllOff { get; set; }

        public int? TemporaryOffensiveModifiers { get; set; }

        public int? AmsCap { get; set; }

        public bool? AmsCapPresent { get; set; }

        public int? TargetArmor { get; set; }

        public int? TypeSpecificAddDamage { get; set; }

        public int? UniversalAddDamage { get; set; }

        public bool? MultipleDamageSourcesPossible { get; set; }

        public bool? ReflectAbsorbShieldProcNanoDotOrEnvironmentalPossible { get; set; }

        public bool? PacketOrderComplete { get; set; }

        public bool? CriticalStateEvidencePresent { get; set; }

        public IList<AttackSkillContribution> AttackSkillDefinitions { get; private set; }

        public IList<string> KnownUncertainties { get; private set; }
    }

    public sealed class WeaponDamageObservationResult
    {
        public WeaponDamageObservationResult()
        {
            this.HitKind = WeaponDamageHitKind.UnknownHitKind;
        }

        public WeaponDamageHitKind HitKind { get; set; }

        public int? ObservedDamage { get; set; }

        public int? TargetHealthBefore { get; set; }

        public int? TargetHealthAfter { get; set; }
    }

    public sealed class WeaponDamageObservationIssue
    {
        public WeaponDamageObservationIssue(WeaponDamageObservationIssueKind kind, string detail)
        {
            this.Kind = kind;
            this.Detail = detail ?? string.Empty;
        }

        public WeaponDamageObservationIssueKind Kind { get; private set; }

        public string Detail { get; private set; }
    }

    public sealed class WeaponDamageObservationDraft
    {
        public WeaponDamageObservationDraft()
        {
            this.Source = new WeaponDamageObservationSource();
            this.Input = new WeaponDamageObservationInput();
            this.Result = new WeaponDamageObservationResult();
        }

        public WeaponDamageObservationSource Source { get; set; }

        public WeaponDamageObservationInput Input { get; set; }

        public WeaponDamageObservationResult Result { get; set; }
    }

    public sealed class WeaponDamageObservation
    {
        internal WeaponDamageObservation(
            WeaponDamageObservationSource source,
            WeaponDamageObservationInput input,
            WeaponDamageObservationResult result,
            IEnumerable<WeaponDamageObservationIssue> issues,
            WeaponDamageObservationValidationStatus status)
        {
            this.Source = source;
            this.Input = input;
            this.Result = result;
            this.Issues = new ReadOnlyCollection<WeaponDamageObservationIssue>((issues ?? new WeaponDamageObservationIssue[0]).ToList());
            this.ValidationStatus = status;
        }

        public WeaponDamageObservationSource Source { get; private set; }

        public WeaponDamageObservationInput Input { get; private set; }

        public WeaponDamageObservationResult Result { get; private set; }

        public ReadOnlyCollection<WeaponDamageObservationIssue> Issues { get; private set; }

        public WeaponDamageObservationValidationStatus ValidationStatus { get; private set; }

        public bool IsComplete
        {
            get
            {
                return this.ValidationStatus == WeaponDamageObservationValidationStatus.Complete;
            }
        }
    }

    public static class WeaponDamageObservationValidator
    {
        public static WeaponDamageObservation Validate(WeaponDamageObservationDraft draft)
        {
            if (draft == null)
            {
                draft = new WeaponDamageObservationDraft();
            }

            if (draft.Source == null)
            {
                draft.Source = new WeaponDamageObservationSource();
            }

            if (draft.Input == null)
            {
                draft.Input = new WeaponDamageObservationInput();
            }

            if (draft.Result == null)
            {
                draft.Result = new WeaponDamageObservationResult();
            }

            List<WeaponDamageObservationIssue> issues = new List<WeaponDamageObservationIssue>();
            if (draft.Result.TargetHealthBefore.HasValue
                && draft.Result.TargetHealthAfter.HasValue
                && draft.Result.ObservedDamage.HasValue
                && !ObservedDamageMatchesHealthDelta(
                    draft.Result.TargetHealthBefore.Value,
                    draft.Result.TargetHealthAfter.Value,
                    draft.Result.ObservedDamage.Value))
            {
                issues.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.HealthDeltaMismatch, "health delta does not match observed damage"));
            }

            if (string.IsNullOrEmpty(draft.Input.WeaponTemplateIdentity))
            {
                issues.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.AmbiguousWeaponIdentity, "weapon template identity is missing or ambiguous"));
            }

            if (draft.Input.MappedDamageType == DamageType.Unknown)
            {
                issues.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.UnknownDamageType, "mapped damage type is unknown"));
            }

            if (draft.Input.AttackSkillDefinitions.GroupBy(x => x.StatId).Any(x => x.Count() > 1))
            {
                issues.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.ContradictoryAttackerStats, "duplicate attack-skill definitions"));
            }

            if (!draft.Input.TargetArmor.HasValue)
            {
                issues.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.MissingArmor, "target matching armor was not supplied"));
            }

            if (draft.Input.MultipleDamageSourcesPossible == true)
            {
                issues.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.MultipleDamageSourcesPossible, "multiple attacks could have contributed to the health change"));
            }

            if (draft.Input.ReflectAbsorbShieldProcNanoDotOrEnvironmentalPossible == true)
            {
                issues.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.ExternalDamagePossible, "reflect, absorb, shield, proc, nano, DoT, or environmental damage may be present"));
            }

            if (draft.Result.HitKind == WeaponDamageHitKind.KnownCritical && draft.Input.CriticalStateEvidencePresent != true)
            {
                issues.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.CriticalStateClaimedWithoutEvidence, "critical state was claimed without evidence"));
            }

            if (draft.Result.HitKind == WeaponDamageHitKind.UnknownHitKind)
            {
                issues.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.UnknownHitKind, "hit kind is unknown"));
            }

            if (draft.Input.PacketOrderComplete != true)
            {
                issues.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.IncompletePacketOrder, "packet order is incomplete or unproven"));
            }

            if (!draft.Input.AddAllOff.HasValue)
            {
                issues.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.MissingAddAllOff, "Add All Off was not supplied"));
            }

            if (!draft.Input.AmsCapPresent.HasValue || (draft.Input.AmsCapPresent == true && draft.Input.AmsCap == 0))
            {
                issues.Add(new WeaponDamageObservationIssue(WeaponDamageObservationIssueKind.MissingAmsCapSemantics, "AMSCap absence or zero semantics are not distinguished"));
            }

            WeaponDamageObservationValidationStatus status = issues.Any(x => x.Kind == WeaponDamageObservationIssueKind.HealthDeltaMismatch)
                                                                 ? WeaponDamageObservationValidationStatus.Rejected
                                                                 : (issues.Count == 0 ? WeaponDamageObservationValidationStatus.Complete : WeaponDamageObservationValidationStatus.Incomplete);
            return new WeaponDamageObservation(CloneSource(draft.Source), CloneInput(draft.Input), CloneResult(draft.Result), issues, status);
        }

        private static bool ObservedDamageMatchesHealthDelta(
            int targetHealthBefore,
            int targetHealthAfter,
            int observedDamage)
        {
            int healthDelta = targetHealthBefore - targetHealthAfter;
            int expectedHealthDelta = Math.Min(observedDamage, targetHealthBefore);
            return healthDelta == expectedHealthDelta;
        }

        private static WeaponDamageObservationSource CloneSource(WeaponDamageObservationSource source)
        {
            return new WeaponDamageObservationSource
            {
                ObservationId = source.ObservationId,
                SourceKind = source.SourceKind,
                CaptureDate = source.CaptureDate,
                Environment = source.Environment,
                PacketEvidenceReference = source.PacketEvidenceReference,
                LogEvidenceReference = source.LogEvidenceReference,
                TimingReference = source.TimingReference,
                Classification = source.Classification
            };
        }

        private static WeaponDamageObservationInput CloneInput(WeaponDamageObservationInput input)
        {
            WeaponDamageObservationInput clone = new WeaponDamageObservationInput
            {
                AttackerIdentity = input.AttackerIdentity,
                AttackerCategory = input.AttackerCategory,
                TargetIdentity = input.TargetIdentity,
                WeaponTemplateIdentity = input.WeaponTemplateIdentity,
                WeaponInstanceIdentity = input.WeaponInstanceIdentity,
                WeaponQualityLevel = input.WeaponQualityLevel,
                WeaponMinimum = input.WeaponMinimum,
                WeaponMaximum = input.WeaponMaximum,
                BaseRoll = input.BaseRoll,
                LegacyDamageBonus = input.LegacyDamageBonus,
                CriticalBonus = input.CriticalBonus,
                RawDamageType = input.RawDamageType,
                MappedDamageType = input.MappedDamageType,
                AttackRating = input.AttackRating,
                AddAllOff = input.AddAllOff,
                TemporaryOffensiveModifiers = input.TemporaryOffensiveModifiers,
                AmsCap = input.AmsCap,
                AmsCapPresent = input.AmsCapPresent,
                TargetArmor = input.TargetArmor,
                TypeSpecificAddDamage = input.TypeSpecificAddDamage,
                UniversalAddDamage = input.UniversalAddDamage,
                MultipleDamageSourcesPossible = input.MultipleDamageSourcesPossible,
                ReflectAbsorbShieldProcNanoDotOrEnvironmentalPossible = input.ReflectAbsorbShieldProcNanoDotOrEnvironmentalPossible,
                PacketOrderComplete = input.PacketOrderComplete,
                CriticalStateEvidencePresent = input.CriticalStateEvidencePresent
            };
            foreach (AttackSkillContribution contribution in input.AttackSkillDefinitions)
            {
                clone.AttackSkillDefinitions.Add(new AttackSkillContribution { StatId = contribution.StatId, Percentage = contribution.Percentage, Value = contribution.Value });
            }

            foreach (string uncertainty in input.KnownUncertainties)
            {
                clone.KnownUncertainties.Add(uncertainty);
            }

            return clone;
        }

        private static WeaponDamageObservationResult CloneResult(WeaponDamageObservationResult result)
        {
            return new WeaponDamageObservationResult
            {
                HitKind = result.HitKind,
                ObservedDamage = result.ObservedDamage,
                TargetHealthBefore = result.TargetHealthBefore,
                TargetHealthAfter = result.TargetHealthAfter
            };
        }
    }

    public sealed class WeaponDamageCandidateFormula
    {
        public WeaponDamageCandidateFormula()
        {
            this.Name = string.Empty;
            this.ArOrdering = WeaponDamageCandidateArOrdering.BasePlusTruncatedBaseTimesArOver400;
            this.AcOrdering = WeaponDamageCandidateAcOrdering.SubtractTruncatedAcOver10BeforeMinimumFloor;
            this.AddDamageOrdering = WeaponDamageCandidateAddDamageOrdering.AfterArAndAc;
            this.CriticalOrdering = WeaponDamageCandidateCriticalOrdering.None;
            this.AmsCapBehavior = WeaponDamageCandidateAmsCapBehavior.MissingCapMeansNoCap;
            this.MinimumFloorAfterAc = true;
            this.MultiplierNumerator = 1;
            this.MultiplierDenominator = 1;
        }

        public string Name { get; set; }

        public WeaponDamageCandidateArOrdering ArOrdering { get; set; }

        public WeaponDamageCandidateAcOrdering AcOrdering { get; set; }

        public WeaponDamageCandidateAddDamageOrdering AddDamageOrdering { get; set; }

        public WeaponDamageCandidateCriticalOrdering CriticalOrdering { get; set; }

        public WeaponDamageCandidateAmsCapBehavior AmsCapBehavior { get; set; }

        public bool MinimumFloorAfterAc { get; set; }

        public int MultiplierNumerator { get; set; }

        public int MultiplierDenominator { get; set; }
    }

    public sealed class WeaponDamageCandidateStage
    {
        public WeaponDamageCandidateStage(string name, int? before, int? after, string assumption)
        {
            this.Name = name ?? string.Empty;
            this.Before = before;
            this.After = after;
            this.Assumption = assumption ?? string.Empty;
        }

        public string Name { get; private set; }

        public int? Before { get; private set; }

        public int? After { get; private set; }

        public string Assumption { get; private set; }
    }

    public sealed class WeaponDamageCandidateEvaluation
    {
        public WeaponDamageCandidateEvaluation()
        {
            this.FormulaName = string.Empty;
            this.Stages = new List<WeaponDamageCandidateStage>();
            this.Assumptions = new List<string>();
            this.UnknownInputs = new List<string>();
        }

        public string FormulaName { get; set; }

        public bool Evaluable { get; set; }

        public int? PredictedDamage { get; set; }

        public int? DifferenceFromObservation { get; set; }

        public bool ExactMatch { get; set; }

        public bool MultipleCandidatesAlsoMatched { get; set; }

        public IList<WeaponDamageCandidateStage> Stages { get; private set; }

        public IList<string> Assumptions { get; private set; }

        public IList<string> UnknownInputs { get; private set; }
    }

    public static class WeaponDamageCandidateEvaluator
    {
        public static WeaponDamageCandidateEvaluation Evaluate(WeaponDamageObservation observation, WeaponDamageCandidateFormula formula)
        {
            WeaponDamageCandidateEvaluation evaluation = new WeaponDamageCandidateEvaluation { FormulaName = formula == null ? string.Empty : formula.Name };
            if (observation == null || formula == null)
            {
                evaluation.UnknownInputs.Add("observation or formula missing");
                return evaluation;
            }

            WeaponDamageObservationInput input = observation.Input;
            Require(input.BaseRoll, "base roll", evaluation);
            Require(input.AttackRating, "attack rating", evaluation);
            Require(input.AddAllOff, "Add All Off", evaluation);
            Require(input.TargetArmor, "target armor", evaluation);
            Require(input.WeaponMinimum, "weapon minimum", evaluation);
            Require(observation.Result.ObservedDamage, "observed damage", evaluation);
            if (observation.Result.HitKind == WeaponDamageHitKind.KnownCritical)
            {
                Require(input.CriticalBonus, "critical bonus", evaluation);
            }

            if (evaluation.UnknownInputs.Count > 0 || observation.ValidationStatus == WeaponDamageObservationValidationStatus.Rejected)
            {
                evaluation.Evaluable = false;
                return evaluation;
            }

            int baseDamage = input.BaseRoll.Value;
            evaluation.Stages.Add(new WeaponDamageCandidateStage("BaseRoll", null, baseDamage, "observation supplied base roll"));
            if (observation.Result.HitKind == WeaponDamageHitKind.KnownCritical)
            {
                baseDamage = ApplyCritical(baseDamage, input, formula, evaluation);
            }

            int effectiveAr = ResolveEffectiveAr(input, formula, evaluation);
            int arDamage = ApplyAr(baseDamage, effectiveAr, formula, evaluation);
            int acDamage = ApplyAc(arDamage, input, formula, evaluation);
            int floored = formula.MinimumFloorAfterAc && input.WeaponMinimum.HasValue ? Math.Max(input.WeaponMinimum.Value, acDamage) : acDamage;
            evaluation.Stages.Add(new WeaponDamageCandidateStage("MinimumFloor", acDamage, floored, formula.MinimumFloorAfterAc ? "minimum floor after AC" : "minimum floor disabled before add damage"));
            int final = ApplyAddDamage(floored, input, formula, evaluation);
            evaluation.Evaluable = true;
            evaluation.PredictedDamage = final;
            evaluation.DifferenceFromObservation = final - observation.Result.ObservedDamage.Value;
            evaluation.ExactMatch = evaluation.DifferenceFromObservation == 0;
            return evaluation;
        }

        public static IList<WeaponDamageCandidateEvaluation> EvaluateAll(WeaponDamageObservation observation, IEnumerable<WeaponDamageCandidateFormula> formulas)
        {
            IList<WeaponDamageCandidateEvaluation> evaluations = (formulas ?? new WeaponDamageCandidateFormula[0]).Select(x => Evaluate(observation, x)).ToList();
            int matchCount = evaluations.Count(x => x.ExactMatch);
            foreach (WeaponDamageCandidateEvaluation evaluation in evaluations)
            {
                evaluation.MultipleCandidatesAlsoMatched = evaluation.ExactMatch && matchCount > 1;
            }

            return evaluations;
        }

        private static void Require(int? value, string name, WeaponDamageCandidateEvaluation evaluation)
        {
            if (!value.HasValue)
            {
                evaluation.UnknownInputs.Add(name);
            }
        }

        private static int ResolveEffectiveAr(WeaponDamageObservationInput input, WeaponDamageCandidateFormula formula, WeaponDamageCandidateEvaluation evaluation)
        {
            int ar = input.AttackRating.Value;
            int addAllOff = input.AddAllOff.Value;
            if (input.AmsCapPresent == false && formula.AmsCapBehavior == WeaponDamageCandidateAmsCapBehavior.MissingCapMeansNoCap)
            {
                evaluation.Assumptions.Add("missing AMSCap means no cap");
            }
            else if (input.AmsCapPresent == true && input.AmsCap == 0 && formula.AmsCapBehavior == WeaponDamageCandidateAmsCapBehavior.ZeroCapMeansLiteralZero)
            {
                ar = 0;
                evaluation.Assumptions.Add("zero AMSCap means literal zero");
            }
            else if (input.AmsCapPresent == true && input.AmsCap == 0 && formula.AmsCapBehavior == WeaponDamageCandidateAmsCapBehavior.ZeroCapMeansNoCap)
            {
                evaluation.Assumptions.Add("zero AMSCap means no cap");
            }
            else if (input.AmsCapPresent == true && input.AmsCap.HasValue && input.AmsCap.Value > 0)
            {
                ar = Math.Min(ar, input.AmsCap.Value);
                evaluation.Assumptions.Add("positive AMSCap applied before AR stage");
            }

            int effective = ar + addAllOff + (input.TemporaryOffensiveModifiers ?? 0);
            evaluation.Stages.Add(new WeaponDamageCandidateStage("ResolveEffectiveAR", ar, effective, "Add All Off and temporary offensive modifiers applied after cap in report-only model"));
            return effective;
        }

        private static int ApplyAr(int baseDamage, int effectiveAr, WeaponDamageCandidateFormula formula, WeaponDamageCandidateEvaluation evaluation)
        {
            int result;
            switch (formula.ArOrdering)
            {
                case WeaponDamageCandidateArOrdering.TruncateBaseTimes400PlusArOver400:
                    result = (baseDamage * (400 + effectiveAr)) / 400;
                    evaluation.Stages.Add(new WeaponDamageCandidateStage("AR", baseDamage, result, "truncate(base * (400 + AR) / 400)"));
                    return result;
                case WeaponDamageCandidateArOrdering.TruncateBaseTimesMultiplier:
                    int denominator = formula.MultiplierDenominator == 0 ? 1 : formula.MultiplierDenominator;
                    result = (baseDamage * formula.MultiplierNumerator) / denominator;
                    evaluation.Stages.Add(new WeaponDamageCandidateStage("AR", baseDamage, result, "truncate(base * multiplierNumerator / multiplierDenominator)"));
                    return result;
                default:
                    result = baseDamage + ((baseDamage * effectiveAr) / 400);
                    evaluation.Stages.Add(new WeaponDamageCandidateStage("AR", baseDamage, result, "base + truncate(base * AR / 400)"));
                    return result;
            }
        }

        private static int ApplyAc(int current, WeaponDamageObservationInput input, WeaponDamageCandidateFormula formula, WeaponDamageCandidateEvaluation evaluation)
        {
            if (formula.AcOrdering == WeaponDamageCandidateAcOrdering.None)
            {
                evaluation.Stages.Add(new WeaponDamageCandidateStage("AC", current, current, "AC not applied"));
                return current;
            }

            int reduction = input.TargetArmor.Value / 10;
            int result = Math.Max(0, current - reduction);
            evaluation.Stages.Add(new WeaponDamageCandidateStage("AC", current, result, formula.AcOrdering.ToString()));
            return result;
        }

        private static int ApplyAddDamage(int current, WeaponDamageObservationInput input, WeaponDamageCandidateFormula formula, WeaponDamageCandidateEvaluation evaluation)
        {
            int addDamage = (input.TypeSpecificAddDamage ?? 0) + (input.UniversalAddDamage ?? 0);
            int result = current;
            switch (formula.AddDamageOrdering)
            {
                case WeaponDamageCandidateAddDamageOrdering.ArScaled:
                    result = current + addDamage + ((addDamage * (input.AttackRating ?? 0)) / 400);
                    break;
                default:
                    result = current + addDamage;
                    break;
            }

            evaluation.Stages.Add(new WeaponDamageCandidateStage("AddDamage", current, result, formula.AddDamageOrdering.ToString()));
            return result;
        }

        private static int ApplyCritical(int current, WeaponDamageObservationInput input, WeaponDamageCandidateFormula formula, WeaponDamageCandidateEvaluation evaluation)
        {
            int max = input.WeaponMaximum ?? current;
            int bonus = input.CriticalBonus ?? 0;
            int result;
            switch (formula.CriticalOrdering)
            {
                case WeaponDamageCandidateCriticalOrdering.MaximumPlusCriticalBonus:
                    result = max + bonus;
                    break;
                case WeaponDamageCandidateCriticalOrdering.CriticalBonusArScaled:
                    result = current + bonus + ((bonus * (input.AttackRating ?? 0)) / 400);
                    break;
                case WeaponDamageCandidateCriticalOrdering.CriticalBonusAcReduced:
                    result = current + Math.Max(0, bonus - ((input.TargetArmor ?? 0) / 10));
                    break;
                default:
                    result = current + bonus;
                    break;
            }

            evaluation.Stages.Add(new WeaponDamageCandidateStage("Critical", current, result, formula.CriticalOrdering.ToString()));
            return result;
        }
    }

    public sealed class WeaponDamageEvidenceSet
    {
        public WeaponDamageEvidenceSet()
        {
            this.Observations = new List<WeaponDamageObservation>();
            this.CandidateFormulas = new List<WeaponDamageCandidateFormula>();
        }

        public IList<WeaponDamageObservation> Observations { get; private set; }

        public IList<WeaponDamageCandidateFormula> CandidateFormulas { get; private set; }
    }

    public sealed class WeaponDamageParityReport
    {
        public WeaponDamageParityReport()
        {
            this.CandidatesMatchingEveryObservation = new List<string>();
            this.CandidatesMatchingOnlySubsets = new List<string>();
            this.UnderdeterminedObservations = new List<string>();
            this.ContradictoryObservations = new List<string>();
            this.MissingObservationsNeeded = new List<string>();
            this.PossibleRoundingBoundaries = new List<string>();
            this.PossibleHiddenModifiers = new List<string>();
        }

        public IList<string> CandidatesMatchingEveryObservation { get; private set; }

        public IList<string> CandidatesMatchingOnlySubsets { get; private set; }

        public IList<string> UnderdeterminedObservations { get; private set; }

        public IList<string> ContradictoryObservations { get; private set; }

        public IList<string> MissingObservationsNeeded { get; private set; }

        public IList<string> PossibleRoundingBoundaries { get; private set; }

        public IList<string> PossibleHiddenModifiers { get; private set; }

        public bool FormulaProven
        {
            get
            {
                return this.CandidatesMatchingEveryObservation.Count == 1
                       && this.CandidatesMatchingOnlySubsets.Count == 0
                       && this.UnderdeterminedObservations.Count == 0
                       && this.ContradictoryObservations.Count == 0
                       && this.MissingObservationsNeeded.Count == 0;
            }
        }
    }

    public static class WeaponDamageParityReporter
    {
        private static readonly string[] RequiredObservationTags =
        {
            "base-roll-variation",
            "attack-rating-variation",
            "target-ac-variation",
            "minimum-floor-boundary",
            "critical-versus-normal",
            "type-specific-add-damage",
            "universal-add-damage",
            "amscap-boundary",
            "single-skill-weapon",
            "multi-skill-weapon",
            "ar-below-1000",
            "ar-exactly-1000",
            "ar-above-1000"
        };

        public static WeaponDamageParityReport Generate(WeaponDamageEvidenceSet evidenceSet)
        {
            WeaponDamageParityReport report = new WeaponDamageParityReport();
            if (evidenceSet == null)
            {
                foreach (string tag in RequiredObservationTags)
                {
                    report.MissingObservationsNeeded.Add(tag);
                }

                return report;
            }

            IList<WeaponDamageObservation> completeObservations = evidenceSet.Observations.Where(x => x.ValidationStatus == WeaponDamageObservationValidationStatus.Complete).ToList();
            foreach (WeaponDamageCandidateFormula formula in evidenceSet.CandidateFormulas)
            {
                int exactMatches = completeObservations.Count(x => WeaponDamageCandidateEvaluator.Evaluate(x, formula).ExactMatch);
                if (completeObservations.Count > 0 && exactMatches == completeObservations.Count)
                {
                    report.CandidatesMatchingEveryObservation.Add(formula.Name);
                }
                else if (exactMatches > 0)
                {
                    report.CandidatesMatchingOnlySubsets.Add(formula.Name);
                }
            }

            foreach (WeaponDamageObservation observation in completeObservations)
            {
                IList<WeaponDamageCandidateEvaluation> evaluations = WeaponDamageCandidateEvaluator.EvaluateAll(observation, evidenceSet.CandidateFormulas);
                int exact = evaluations.Count(x => x.ExactMatch);
                if (exact == 0)
                {
                    report.ContradictoryObservations.Add(observation.Source.ObservationId);
                    report.PossibleHiddenModifiers.Add(observation.Source.ObservationId);
                }
                else if (exact > 1)
                {
                    report.UnderdeterminedObservations.Add(observation.Source.ObservationId);
                }

                if (evaluations.Any(x => x.Stages.Any(y => y.Assumption.Contains("truncate"))))
                {
                    report.PossibleRoundingBoundaries.Add(observation.Source.ObservationId);
                }
            }

            foreach (string tag in RequiredObservationTags)
            {
                if (!evidenceSet.Observations.Any(x => x.Input.KnownUncertainties.Contains(tag)))
                {
                    report.MissingObservationsNeeded.Add(tag);
                }
            }

            return report;
        }
    }

    public sealed class WeaponDamageObservationImportResult
    {
        public WeaponDamageObservationImportResult()
        {
            this.Diagnostics = new List<string>();
        }

        public bool Success { get; set; }

        public WeaponDamageObservation Observation { get; set; }

        public IList<string> Diagnostics { get; private set; }
    }

    public static class WeaponDamageObservationJsonImporter
    {
        public const string SupportedSchemaVersion = "1.0";

        public static WeaponDamageObservationImportResult Import(string json)
        {
            WeaponDamageObservationImportResult result = new WeaponDamageObservationImportResult();
            if (string.IsNullOrWhiteSpace(json))
            {
                result.Diagnostics.Add("empty JSON");
                return result;
            }

            string schemaVersion = ExtractString(json, "schemaVersion");
            if (schemaVersion != SupportedSchemaVersion)
            {
                result.Diagnostics.Add("unsupported schemaVersion");
                return result;
            }

            WeaponDamageObservationDraft draft = new WeaponDamageObservationDraft();
            draft.Source.ObservationId = ExtractString(json, "observationId");
            draft.Source.CaptureDate = ExtractString(json, "captureDate");
            draft.Source.Environment = ExtractString(json, "environment");
            draft.Source.PacketEvidenceReference = ExtractString(json, "packetReference");
            draft.Source.LogEvidenceReference = ExtractString(json, "logReference");
            draft.Source.Classification = DamageEvidenceClassification.Unknown;
            draft.Input.WeaponTemplateIdentity = ExtractString(json, "weaponTemplateIdentity");
            draft.Input.WeaponMinimum = ExtractInt(json, "weaponMinimum");
            draft.Input.WeaponMaximum = ExtractInt(json, "weaponMaximum");
            draft.Input.BaseRoll = ExtractInt(json, "baseRoll");
            draft.Input.AttackRating = ExtractInt(json, "attackRating");
            draft.Input.AddAllOff = ExtractInt(json, "addAllOff");
            draft.Input.TargetArmor = ExtractInt(json, "targetArmor");
            draft.Input.AmsCap = ExtractInt(json, "amsCap");
            draft.Input.AmsCapPresent = json.Contains("\"amsCap\"");
            draft.Input.MappedDamageType = ParseDamageType(ExtractString(json, "mappedDamageType"));
            draft.Input.PacketOrderComplete = ExtractBool(json, "packetOrderComplete");
            draft.Input.CriticalStateEvidencePresent = ExtractBool(json, "criticalStateEvidencePresent");
            draft.Result.HitKind = ParseHitKind(ExtractString(json, "hitKind"));
            draft.Result.ObservedDamage = ExtractInt(json, "observedDamage");
            draft.Result.TargetHealthBefore = ExtractInt(json, "targetHealthBefore");
            draft.Result.TargetHealthAfter = ExtractInt(json, "targetHealthAfter");
            result.Observation = WeaponDamageObservationValidator.Validate(draft);
            result.Success = result.Observation.ValidationStatus != WeaponDamageObservationValidationStatus.Rejected;
            foreach (WeaponDamageObservationIssue issue in result.Observation.Issues)
            {
                result.Diagnostics.Add(issue.Kind + ": " + issue.Detail);
            }

            return result;
        }

        private static string ExtractString(string json, string property)
        {
            Match match = Regex.Match(json, "\"" + Regex.Escape(property) + "\"\\s*:\\s*\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static int? ExtractInt(string json, string property)
        {
            Match match = Regex.Match(json, "\"" + Regex.Escape(property) + "\"\\s*:\\s*(-?\\d+)");
            if (!match.Success)
            {
                return null;
            }

            int value;
            return int.TryParse(match.Groups[1].Value, out value) ? (int?)value : null;
        }

        private static bool? ExtractBool(string json, string property)
        {
            Match match = Regex.Match(json, "\"" + Regex.Escape(property) + "\"\\s*:\\s*(true|false)");
            if (!match.Success)
            {
                return null;
            }

            return match.Groups[1].Value == "true";
        }

        private static DamageType ParseDamageType(string value)
        {
            DamageType damageType;
            return Enum.TryParse(value, true, out damageType) ? damageType : DamageType.Unknown;
        }

        private static WeaponDamageHitKind ParseHitKind(string value)
        {
            WeaponDamageHitKind hitKind;
            return Enum.TryParse(value, true, out hitKind) ? hitKind : WeaponDamageHitKind.UnknownHitKind;
        }
    }

    public sealed class WeaponDamageDiagnosticSnapshot
    {
        public WeaponDamageDiagnosticSnapshot()
        {
            this.CandidateEvaluations = new List<WeaponDamageCandidateEvaluation>();
            this.MissingInputs = new List<string>();
        }

        public WeaponDamageRequestBuildResult RequestBuilderResult { get; set; }

        public DamageCalculationStrategyKind SelectedStrategy { get; set; }

        public int ActualLegacyResult { get; set; }

        public IList<WeaponDamageCandidateEvaluation> CandidateEvaluations { get; private set; }

        public IList<string> MissingInputs { get; private set; }
    }

    public static class WeaponDamageDiagnosticSnapshotBuilder
    {
        public static WeaponDamageDiagnosticSnapshot Build(
            bool enabled,
            WeaponDamageRequestBuildResult requestBuilderResult,
            DamageCalculationResult productionResult,
            WeaponDamageObservation observation,
            IEnumerable<WeaponDamageCandidateFormula> formulas)
        {
            if (!enabled)
            {
                return null;
            }

            WeaponDamageDiagnosticSnapshot snapshot = new WeaponDamageDiagnosticSnapshot
            {
                RequestBuilderResult = requestBuilderResult,
                SelectedStrategy = productionResult == null ? DamageCalculationStrategyKind.LegacyFallback : productionResult.Strategy,
                ActualLegacyResult = productionResult == null ? 0 : productionResult.FinalTargetDamage
            };

            if (requestBuilderResult != null)
            {
                foreach (WeaponDamageInputIssue issue in requestBuilderResult.Issues)
                {
                    snapshot.MissingInputs.Add(issue.Kind + ": " + issue.InputName);
                }
            }

            if (observation != null)
            {
                foreach (WeaponDamageCandidateEvaluation evaluation in WeaponDamageCandidateEvaluator.EvaluateAll(observation, formulas))
                {
                    snapshot.CandidateEvaluations.Add(evaluation);
                }
            }

            return snapshot;
        }
    }
}
