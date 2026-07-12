namespace ZoneEngine.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

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
            this.PlayerFallbackDamage = CombatDamageRules.PlayerFallbackDamage;
            this.NpcFallbackDamage = CombatDamageRules.NpcFallbackDamage;
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

            result.MinimumDamageFloor = fallbackFloor;
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

            result.Trace.Add("ApplyMinimumDamageFloor", DamageCalculationStageStatus.Preserved, afterFlatAdd, Math.Max(fallbackFloor, afterFlatAdd), evidence, "repository legacy fallback floor");

            int currentDamage = Math.Max(fallbackFloor, afterFlatAdd);

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
}
