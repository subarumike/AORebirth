using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZoneEngine.Core;
using ZoneEngine.Core.Playfields;

namespace AORebirth.Core.Playfields;

internal sealed class OrdinaryEnemyCatalog
{
	private sealed class OrdinaryEnemySpawnPolicyConfiguration
	{
		internal OrdinaryEnemySpawnLevelDefinition LevelDefinition { get; private set; }

		internal WorldRespawnPolicyAssignment RespawnPolicy { get; private set; }

		internal OrdinaryEnemySpawnPolicyConfiguration(OrdinaryEnemySpawnLevelDefinition levelDefinition, WorldRespawnPolicyAssignment respawnPolicy)
		{
			LevelDefinition = levelDefinition;
			RespawnPolicy = respawnPolicy;
		}
	}

	internal const int SubwayPlayfieldInstance = 127;

	private const int BloodcreeperMonsterData = 30379;

	private const int DerangedShopperMonsterData = 203736;

	private const int LooterMonsterData = 203745;

	private const int RedundantScanMonsterData = 204178;

	private const int IncompleteRebuildMonsterData = 203728;

	private const int FragmentedSoulMonsterData = 203729;

	private const int PrematurePatternMonsterData = 203727;

	private const int PrematurePatternVariantSource = 2035569494;

	private const int WorkmanStrikerMonsterData = 203854;

	private const int ViolentVagabondMonsterData = 203733;

	private const double BloodcreeperAutomaticAggroRadius = 7.0;

	private const double IncompleteRebuildAutomaticAggroRadius = 7.0;

	private const double RedundantScanAutomaticAggroRadius = 7.0;

	private static readonly Dictionary<string, OrdinaryEnemySpawnPolicyConfiguration> CapturedOrdinarySpawnPolicies = BuildCapturedOrdinarySpawnPolicies();

	private static readonly OrdinaryEnemyAggressionProfile RetaliateChasingAggression = new OrdinaryEnemyAggressionProfile(OrdinaryEnemyAggressionMode.Retaliate, null, chase: true, returnToSpawn: false, OrdinaryEnemyEvidenceState.Observed);

	private static readonly OrdinaryEnemyAggressionProfile BloodcreeperAutomaticAggression = new OrdinaryEnemyAggressionProfile(OrdinaryEnemyAggressionMode.Auto, 7.0, chase: true, returnToSpawn: false, OrdinaryEnemyEvidenceState.Observed);

	private static readonly OrdinaryEnemyAggressionProfile IncompleteRebuildAutomaticAggression = new OrdinaryEnemyAggressionProfile(OrdinaryEnemyAggressionMode.Auto, 7.0, chase: true, returnToSpawn: true, OrdinaryEnemyEvidenceState.Policy);

	private static readonly OrdinaryEnemyAggressionProfile RedundantScanAutomaticAggression = new OrdinaryEnemyAggressionProfile(OrdinaryEnemyAggressionMode.Auto, 7.0, chase: true, returnToSpawn: false, OrdinaryEnemyEvidenceState.Policy);

	private static readonly OrdinaryEnemyCorpseProfile StandardGenericCorpse = new OrdinaryEnemyCorpseProfile(OrdinaryEnemyCorpsePacketProfile.Generic, 3.0, 240.0, 3.0);

	private static readonly OrdinaryEnemyCorpseProfile CapturedThiefCorpse = new OrdinaryEnemyCorpseProfile(OrdinaryEnemyCorpsePacketProfile.CapturedThief, 3.0, 240.0, 3.0);

	private static readonly OrdinaryEnemyCorpseProfile CapturedFilthFleaCorpse = new OrdinaryEnemyCorpseProfile(OrdinaryEnemyCorpsePacketProfile.CapturedFilthFlea, 3.0, 240.0, 3.0);

	private readonly Dictionary<string, OrdinaryEnemyProfile> profilesByKey;

	private readonly OrdinaryEnemyProfile[] profiles;

	private readonly OrdinaryEnemySpawnDefinition[] spawns;

	internal OrdinaryEnemyCatalog(CapturedSubwayContentProvider supportedContent, CapturedSubwayOrdinaryContentProvider ordinaryContent)
	{
		if (supportedContent == null)
		{
			throw new ArgumentNullException("supportedContent");
		}
		if (ordinaryContent == null)
		{
			throw new ArgumentNullException("ordinaryContent");
		}
		List<OrdinaryEnemyProfile> source = new List<OrdinaryEnemyProfile>();
		List<OrdinaryEnemySpawnDefinition> source2 = new List<OrdinaryEnemySpawnDefinition>();
		BuildSupportedRows(supportedContent, ordinaryContent, source, source2);
		BuildCapturedOrdinaryRows(ordinaryContent, source, source2);
		profiles = source.OrderBy((OrdinaryEnemyProfile value) => value.ProfileKey, StringComparer.Ordinal).ToArray();
		spawns = source2.OrderBy((OrdinaryEnemySpawnDefinition value) => value.SourceIdentity).ToArray();
		OrdinaryEnemyProfileValidator.Validate(profiles, spawns);
		ValidateViolentVagabondEvidenceBoundary(profiles, spawns);
		profilesByKey = profiles.ToDictionary((OrdinaryEnemyProfile value) => value.ProfileKey, StringComparer.Ordinal);
	}

	internal OrdinaryEnemyProfile[] GetProfiles()
	{
		return (OrdinaryEnemyProfile[])profiles.Clone();
	}

	internal OrdinaryEnemySpawnDefinition[] GetSpawns()
	{
		return (OrdinaryEnemySpawnDefinition[])spawns.Clone();
	}

	internal OrdinaryEnemySpawnDefinition[] GetRuntimeSpawns(int playfieldInstance)
	{
		return spawns.Where((OrdinaryEnemySpawnDefinition spawn) => spawn.PlayfieldInstance == playfieldInstance && (spawn.Disposition == OrdinaryEnemyRuntimeDisposition.Active || SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined(spawn.SourceIdentity))).ToArray();
	}

	internal bool TryGetProfile(string profileKey, out OrdinaryEnemyProfile profile)
	{
		return profilesByKey.TryGetValue(profileKey, out profile);
	}

	internal CombatLootTableEntry[] BuildCombatLootTableEntries()
	{
		List<CombatLootTableEntry> list = new List<CombatLootTableEntry>();
		OrdinaryEnemyProfile[] array = profiles;
		foreach (OrdinaryEnemyProfile ordinaryEnemyProfile in array)
		{
			if (ordinaryEnemyProfile.Loot.PoolMode == OrdinaryEnemyLootPoolMode.IndependentEntries)
			{
				OrdinaryEnemyLootEntry[] entries = ordinaryEnemyProfile.Loot.Entries;
				foreach (OrdinaryEnemyLootEntry ordinaryEnemyLootEntry in entries)
				{
					list.Add(new CombatLootTableEntry
					{
						ExactName = ordinaryEnemyProfile.DisplayName,
						MonsterData = ordinaryEnemyProfile.MonsterData,
						NpcFamily = ordinaryEnemyProfile.Appearance.NpcFamily,
						Slot = ordinaryEnemyLootEntry.Slot,
						DropChanceBasisPoints = ordinaryEnemyLootEntry.BasisPoints,
						ItemTemplates = new CombatLootItemTemplate[1]
						{
							new CombatLootItemTemplate
							{
								LowId = ordinaryEnemyLootEntry.LowId,
								HighId = ordinaryEnemyLootEntry.HighId,
								MinQuality = ordinaryEnemyLootEntry.Quality,
								MaxQuality = ordinaryEnemyLootEntry.Quality,
								RangeCheck = 0,
								DropGroupHash = "ordinary-enemy-profile"
							}
						}
					});
				}
			}
		}
		return list.ToArray();
	}

	private static void BuildSupportedRows(CapturedSubwayContentProvider content, CapturedSubwayOrdinaryContentProvider ordinaryContent, ICollection<OrdinaryEnemyProfile> profiles, ICollection<OrdinaryEnemySpawnDefinition> spawns)
	{
		CapturedSubwaySpawnDefinition[] allSpawnDefinitions = content.GetAllSpawnDefinitions();
		CapturedSubwayLootDefinition[] lootDefinitions = content.GetLootDefinitions();
		foreach (IGrouping<int, CapturedSubwaySpawnDefinition> item in (from value in allSpawnDefinitions
			group value by value.MonsterData).OrderBy((IGrouping<int, CapturedSubwaySpawnDefinition> value) => SupportedProfileKey(value.First()), StringComparer.Ordinal))
		{
			CapturedSubwaySpawnDefinition first = item.First();
			Func<int, CapturedEnemyCombatContract> contractResolver = ((first.MonsterData == 17649) ? ((Func<int, CapturedEnemyCombatContract>)((int level) => CapturedSubwayCombatCatalog.For(first.Name, first.MonsterData, level))) : null);
			CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence = ordinaryContent.GetSourceWeaponEvidence(first.MonsterData);
			Func<int, int, CapturedEnemyCombatContract> sourceContractResolver = ((sourceWeaponEvidence.Length != 0) ? ((Func<int, int, CapturedEnemyCombatContract>)((int sourceIdentity, int level) => CapturedSubwayCombatCatalog.ForSupportedSourceWeapon(first.Name, first.MonsterData, sourceWeaponEvidence, sourceIdentity))) : null);
			CapturedSubwayStrictLootProfileDefinition strictLootProfile = ordinaryContent.GetStrictLootProfile(first.MonsterData);
			OrdinaryEnemyLootEntry[] entries = ((strictLootProfile == null) ? (from value in lootDefinitions
				where value.MonsterData == first.MonsterData
				select new OrdinaryEnemyLootEntry(value.LowId, value.HighId, value.Quality, value.Slot, value.Quantity, value.RuntimeWeight, value.ObservedBasisPoints, (value.ObservedBasisPoints == 10000) ? OrdinaryEnemyLootEvidence.GuaranteedProven : OrdinaryEnemyLootEvidence.ObservedAvailableLoot, value.LinkageEvidence, value.ProbabilityEvidence, value.ObservedCount, value.ObservedCorpses, value.EvidenceReference)).ToArray() : BuildStrictLootEntries(strictLootProfile));
			CapturedEnemyCombatContract combat = first.Combat;
			CapturedSubwayCorpseEvidenceDefinition[] corpseEvidence = ordinaryContent.GetCorpseEvidence(first.MonsterData);
			profiles.Add(new OrdinaryEnemyProfile(SupportedProfileKey(first), "subway.supported", first.Name, first.MonsterData, OrdinaryEnemyConstructionMode.TemplateBacked, first.TemplateHash, BuildSupportedAppearance(first), AggressionFor(first.MonsterData), BuildCombatProfile(combat, first.MonsterData, contractResolver, sourceContractResolver), BuildLootProfile(first.MonsterData, entries, corpseEvidence, strictLootProfile), StandardCorpseProfile(first.MonsterData, corpseEvidence), item.Select((CapturedSubwaySpawnDefinition value) => value.ContentSection).Distinct(StringComparer.Ordinal).OrderBy((string value) => value, StringComparer.Ordinal)
				.ToArray(), bossOrScripted: false, ownedSummon: false));
		}
		CapturedSubwaySpawnDefinition[] array = allSpawnDefinitions;
		foreach (CapturedSubwaySpawnDefinition capturedSubwaySpawnDefinition in array)
		{
			CapturedSubwayPatrolReplaySegment[] patrolReplaySegments = content.GetPatrolReplaySegments(capturedSubwaySpawnDefinition.SourceInstance);
			OrdinaryEnemyWaypoint[] waypoints = (capturedSubwaySpawnDefinition.HasPatrolWaypoint ? new OrdinaryEnemyWaypoint[2]
			{
				new OrdinaryEnemyWaypoint(capturedSubwaySpawnDefinition.X, capturedSubwaySpawnDefinition.Y, capturedSubwaySpawnDefinition.Z),
				new OrdinaryEnemyWaypoint(capturedSubwaySpawnDefinition.PatrolX.Value, capturedSubwaySpawnDefinition.PatrolY.Value, capturedSubwaySpawnDefinition.PatrolZ.Value)
			} : new OrdinaryEnemyWaypoint[0]);
			bool flag = capturedSubwaySpawnDefinition.HasPatrolWaypoint || patrolReplaySegments.Length != 0;
			bool flag2 = capturedSubwaySpawnDefinition.MonsterData == 203733;
			OrdinaryEnemyEvidenceState respawnEvidence = (capturedSubwaySpawnDefinition.HasRespawnDelay ? OrdinaryEnemyEvidenceState.Observed : (flag2 ? OrdinaryEnemyEvidenceState.Policy : OrdinaryEnemyEvidenceState.Unresolved));
			double? respawnDelaySeconds = (capturedSubwaySpawnDefinition.HasRespawnDelay ? capturedSubwaySpawnDefinition.RespawnDelaySeconds : (flag2 ? new double?(450.0) : null));
			spawns.Add(new OrdinaryEnemySpawnDefinition(SpawnKey(capturedSubwaySpawnDefinition.SourceInstance), capturedSubwaySpawnDefinition.SourceInstance, SupportedProfileKey(capturedSubwaySpawnDefinition), 127, capturedSubwaySpawnDefinition.Level, capturedSubwaySpawnDefinition.Health, capturedSubwaySpawnDefinition.HealthDamage, capturedSubwaySpawnDefinition.MonsterScale, capturedSubwaySpawnDefinition.RunSpeed, capturedSubwaySpawnDefinition.X, capturedSubwaySpawnDefinition.Y, capturedSubwaySpawnDefinition.Z, 0f, 0f, 0f, 1f, (!flag) ? OrdinaryEnemyMovementMode.Static : OrdinaryEnemyMovementMode.Patrol, waypoints, patrolReplaySegments.Length != 0, capturedSubwaySpawnDefinition.UseSpawnAsPatrolStart, hasCapturedScfuOverride: false, 0u, 0, new byte[0], 0, respawnEvidence, respawnDelaySeconds, (!CapturedSubwayContentProvider.IsRuntimeQuarantined(capturedSubwaySpawnDefinition.SourceInstance)) ? OrdinaryEnemyRuntimeDisposition.Active : OrdinaryEnemyRuntimeDisposition.Quarantined, string.Empty, capturedSubwaySpawnDefinition.ContentSection, string.Empty, null, flag2 ? ViolentVagabondRespawnPolicy() : null));
		}
	}

	private static void BuildCapturedOrdinaryRows(CapturedSubwayOrdinaryContentProvider content, ICollection<OrdinaryEnemyProfile> profiles, ICollection<OrdinaryEnemySpawnDefinition> spawns)
	{
		//IL_042a: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b5: Expected I4, but got Unknown
		CapturedSubwayOrdinaryArchetypeDefinition[] archetypes = content.GetArchetypes();
		foreach (CapturedSubwayOrdinaryArchetypeDefinition archetype in archetypes)
		{
			uint appearanceValue = archetype.AppearanceValue;
			CapturedEnemyCombatContract contract = CapturedSubwayCombatCatalog.ForOrdinary(archetype);
			Func<int, int, CapturedEnemyCombatContract> sourceContractResolver = ((archetype.MonsterData == 203736 || archetype.MonsterData == 203854 || archetype.MonsterData == 203745) ? ((Func<int, int, CapturedEnemyCombatContract>)((int sourceIdentity, int level) => CapturedSubwayCombatCatalog.ForOrdinary(archetype, sourceIdentity))) : null);
			Func<int, OrdinaryEnemySpawnVariant, CapturedEnemyCombatContract> sourceVariantContractResolver = ((archetype.MonsterData == 203728 || archetype.MonsterData == 204178 || archetype.MonsterData == 203729) ? ((Func<int, OrdinaryEnemySpawnVariant, CapturedEnemyCombatContract>)((int sourceIdentity, OrdinaryEnemySpawnVariant variant) => CapturedSubwayCombatCatalog.ForOrdinary(archetype, sourceIdentity, variant, content.GetGenerationVariants(archetype.MonsterData, sourceIdentity)))) : null);
			CapturedSubwayStrictLootProfileDefinition strictLootProfile = content.GetStrictLootProfile(archetype.MonsterData);
			OrdinaryEnemyLootEntry[] entries = ((strictLootProfile == null) ? archetype.LootEvidence.Select((CapturedSubwayLootEvidenceDefinition value, int index) => new OrdinaryEnemyLootEntry(value.LowId, value.HighId, value.Quality, index, 1, 0, value.ObservedBasisPoints, OrdinaryEnemyLootEvidence.ObservedAvailableLoot, OrdinaryEnemyLootLinkageEvidence.ImportedCaptureEvidence, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, value.ObservedCount, value.ObservedCorpses, string.Join(",", archetype.EvidenceCaptures))).ToArray() : BuildStrictLootEntries(strictLootProfile));
			profiles.Add(new OrdinaryEnemyProfile(OrdinaryProfileKey(archetype.Key), "subway.ordinary." + archetype.FamilyKey, archetype.Name, archetype.MonsterData, OrdinaryEnemyConstructionMode.CapturedDirect, string.Empty, new OrdinaryEnemyAppearanceProfile((int)(appearanceValue & 7), (int)((appearanceValue & 0x1F) >> 3), Math.Max(1, Math.Min(7, (int)((appearanceValue & 0xFF) >> 5))), (int)((appearanceValue & 0x3FF) >> 8), (int)(appearanceValue >> 10), archetype.CharacterFlags, archetype.AccountFlags, archetype.Expansions, archetype.NpcFamily, archetype.NpcLosHeight, archetype.VisualFlags, archetype.VisibleTitle, appearanceValue, archetype.HeadMesh, replaceTextures: true, clearTemplateHeadWhenZero: false, archetype.Textures.Select((CapturedSubwayTextureDefinition value) => new OrdinaryEnemyTextureProfile(value.Place, value.Id, value.Unknown)).ToArray(), archetype.Meshes.Select((CapturedSubwayMeshDefinition value) => new OrdinaryEnemyMeshProfile(value.Position, value.Id, value.OverrideTextureId, value.Layer)).ToArray(), OrdinaryEnemyScfuProfile.CapturedExact), AggressionFor(archetype.MonsterData), BuildCombatProfile(contract, archetype.MonsterData, null, sourceContractResolver, sourceVariantContractResolver, archetype.Combat != null && archetype.Combat.Observed), BuildLootProfile(archetype.MonsterData, entries, archetype.CorpseEvidence, strictLootProfile), StandardCorpseProfile(archetype.MonsterData, archetype.CorpseEvidence), archetype.EvidenceCaptures, bossOrScripted: false, ownedSummon: false, SupportNanoFor(archetype.MonsterData)));
		}
		CapturedSubwayOrdinarySpawnDefinition[] allSpawns = content.GetAllSpawns();
		foreach (CapturedSubwayOrdinarySpawnDefinition capturedSubwayOrdinarySpawnDefinition in allSpawns)
		{
			CapturedOrdinarySpawnPolicies.TryGetValue(capturedSubwayOrdinarySpawnDefinition.ArchetypeKey, out var value2);
			OrdinaryEnemyWaypoint[] array = capturedSubwayOrdinarySpawnDefinition.Waypoints.Select((CapturedSubwayWaypointDefinition value) => new OrdinaryEnemyWaypoint(value.X, value.Y, value.Z)).ToArray();
			OrdinaryEnemySpawnLevelDefinition levelDefinition = BuildCapturedLevelDefinition(content, capturedSubwayOrdinarySpawnDefinition, value2?.LevelDefinition);
			spawns.Add(new OrdinaryEnemySpawnDefinition(SpawnKey(capturedSubwayOrdinarySpawnDefinition.SourceInstance), capturedSubwayOrdinarySpawnDefinition.SourceInstance, OrdinaryProfileKey(capturedSubwayOrdinarySpawnDefinition.ArchetypeKey), 127, capturedSubwayOrdinarySpawnDefinition.Level, capturedSubwayOrdinarySpawnDefinition.Health, capturedSubwayOrdinarySpawnDefinition.HealthDamage, capturedSubwayOrdinarySpawnDefinition.MonsterScale, capturedSubwayOrdinarySpawnDefinition.RunSpeed, capturedSubwayOrdinarySpawnDefinition.X, capturedSubwayOrdinarySpawnDefinition.Y, capturedSubwayOrdinarySpawnDefinition.Z, capturedSubwayOrdinarySpawnDefinition.HeadingX, capturedSubwayOrdinarySpawnDefinition.HeadingY, capturedSubwayOrdinarySpawnDefinition.HeadingZ, capturedSubwayOrdinarySpawnDefinition.HeadingW, (array.Length <= 1) ? OrdinaryEnemyMovementMode.Static : OrdinaryEnemyMovementMode.Patrol, array, useCapturedPatrolReplay: false, useSpawnAsPatrolStart: false, hasCapturedScfuOverride: true, (uint)(int)capturedSubwayOrdinarySpawnDefinition.CapturedFlags, capturedSubwayOrdinarySpawnDefinition.CapturedFlags2, capturedSubwayOrdinarySpawnDefinition.Unknown1, capturedSubwayOrdinarySpawnDefinition.Unknown2, (value2 != null && value2.RespawnPolicy.Mode == WorldRespawnPolicyAssignmentMode.Explicit) ? OrdinaryEnemyEvidenceState.Policy : OrdinaryEnemyEvidenceState.Unresolved, (value2 != null && value2.RespawnPolicy.ExplicitPolicy != null) ? value2.RespawnPolicy.ExplicitPolicy.FixedDelaySeconds : null, OrdinaryEnemyRuntimeDisposition.Active, capturedSubwayOrdinarySpawnDefinition.SourceOwnerIdentity, capturedSubwayOrdinarySpawnDefinition.EvidenceCapture, capturedSubwayOrdinarySpawnDefinition.EvidenceTimestamp, levelDefinition, value2?.RespawnPolicy));
		}
	}

	private static Dictionary<string, OrdinaryEnemySpawnPolicyConfiguration> BuildCapturedOrdinarySpawnPolicies()
	{
		Dictionary<string, OrdinaryEnemySpawnPolicyConfiguration> dictionary = new Dictionary<string, OrdinaryEnemySpawnPolicyConfiguration>(StringComparer.Ordinal);
		dictionary.Add("bloodcreeper", new OrdinaryEnemySpawnPolicyConfiguration(new OrdinaryEnemySpawnLevelDefinition(OrdinaryEnemySpawnLevelMode.InclusiveRange, 15, 25, 24, 691, 33, 0, 70, 83, 3, OrdinaryEnemyLevelRerollPolicy.NewPopulationGeneration, OrdinaryEnemyEvidenceState.Policy, "community-range:docs/generated/enemy_catalog/enemy_catalog.csv;captured-anchor:20260709-222339;focused-combat:20260716-033326,20260716-034104"), WorldRespawnPolicyAssignment.Explicit(new RespawnPolicyDefinition
		{
			RespawnPolicyKey = "ordinary.bloodcreeper.240",
			Mode = WorldRespawnMode.FixedDelay,
			FixedDelaySeconds = 240.0,
			RespawnAtOriginalPosition = true,
			ResetHealth = true,
			ResetMovementState = true,
			ResetAggressionState = true,
			DelayStartsAt = RespawnDelayStartsAt.NpcDespawn,
			Evidence = "private-regular-enemy-policy;20260716-033326;20260716-034104",
			Confidence = "POLICY",
			Enabled = true
		})));
		dictionary.Add("incomplete_rebuild", new OrdinaryEnemySpawnPolicyConfiguration(null, WorldRespawnPolicyAssignment.Explicit(new RespawnPolicyDefinition
		{
			RespawnPolicyKey = "ordinary.incomplete-rebuild.240",
			Mode = WorldRespawnMode.FixedDelay,
			FixedDelaySeconds = 240.0,
			RespawnAtOriginalPosition = true,
			ResetHealth = true,
			ResetMovementState = true,
			ResetAggressionState = true,
			DelayStartsAt = RespawnDelayStartsAt.NpcDespawn,
			Evidence = "private-regular-enemy-policy;20260709-222339;20260716-034559;20260716-222007;20260717-215250",
			Confidence = "POLICY",
			Enabled = true
		})));
		dictionary.Add("slum_runner", new OrdinaryEnemySpawnPolicyConfiguration(null, WorldRespawnPolicyAssignment.Explicit(new RespawnPolicyDefinition
		{
			RespawnPolicyKey = "ordinary.slum-runner.60",
			Mode = WorldRespawnMode.FixedDelay,
			FixedDelaySeconds = 60.0,
			RespawnAtOriginalPosition = true,
			ResetHealth = true,
			ResetMovementState = true,
			ResetAggressionState = true,
			DelayStartsAt = RespawnDelayStartsAt.NpcDespawn,
			Evidence = "official-live:20260716-215947;enemy-respawns.csv;death-to-respawn=59.433;corpse-remained-present",
			Confidence = "CAPTURE_BOUNDED",
			Enabled = true
		})));
		return dictionary;
	}

	private static OrdinaryEnemySpawnLevelDefinition BuildCapturedLevelDefinition(CapturedSubwayOrdinaryContentProvider content, CapturedSubwayOrdinarySpawnDefinition source, OrdinaryEnemySpawnLevelDefinition configuredDefinition)
	{
		int expectedMonsterData = (string.Equals(source.ArchetypeKey, "incomplete_rebuild", StringComparison.Ordinal) ? 203728 : (string.Equals(source.ArchetypeKey, "redundant_scan", StringComparison.Ordinal) ? 204178 : (string.Equals(source.ArchetypeKey, "fragmented_soul", StringComparison.Ordinal) ? 203729 : ((string.Equals(source.ArchetypeKey, "premature_pattern", StringComparison.Ordinal) && source.SourceInstance == 2035569494) ? 203727 : 0))));
		CapturedSubwayGenerationVariantDefinition[] array = ((expectedMonsterData == 0) ? new CapturedSubwayGenerationVariantDefinition[0] : content.GetGenerationVariants(expectedMonsterData, source.SourceInstance));
		if (array.Length == 0)
		{
			if (expectedMonsterData != 0)
			{
				throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Captured atomic-generation source 0x{0:X8} has no reviewed variants.", source.SourceInstance));
			}
			return configuredDefinition;
		}
		if (expectedMonsterData == 0 || array.Any((CapturedSubwayGenerationVariantDefinition value) => value.MonsterData != expectedMonsterData || value.SourceInstance != source.SourceInstance || ((value.WeaponLowId <= 0 || value.WeaponHighId <= 0 || value.WeaponQuality <= 0) && (value.WeaponLowId != 0 || value.WeaponHighId != 0 || value.WeaponQuality != 0))))
		{
			throw new InvalidOperationException("Captured ordinary generation variants are attached to an unexpected source.");
		}
		bool variantsHaveWeapon = array.All((CapturedSubwayGenerationVariantDefinition value) => value.WeaponLowId > 0 && value.WeaponHighId > 0 && value.WeaponQuality > 0);
		if (!variantsHaveWeapon && array.Any((CapturedSubwayGenerationVariantDefinition value) => value.WeaponLowId != 0 || value.WeaponHighId != 0 || value.WeaponQuality != 0))
		{
			throw new InvalidOperationException("Captured ordinary generation variants mix weapon and weaponless rows.");
		}
		OrdinaryEnemySpawnVariant[] variants = array.Select((CapturedSubwayGenerationVariantDefinition value) => new OrdinaryEnemySpawnVariant(value.Level, value.Health, value.HealthDamage, value.MonsterScale, value.RunSpeed, value.Evidence, variantsHaveWeapon ? new OrdinaryEnemySpawnWeaponLoadout(value.WeaponLowId, value.WeaponHighId, value.WeaponQuality, value.Evidence) : null)).ToArray();
		return OrdinaryEnemySpawnLevelDefinition.ExplicitObservedVariants(variants, "uniform-selection-private-policy;" + (variantsHaveWeapon ? "capture-reviewed atomic level/stat/weapon generations;" : "capture-reviewed atomic level/stat generations;no weapon loadout captured;") + string.Join(",", array.Select((CapturedSubwayGenerationVariantDefinition value) => value.Evidence).Distinct(StringComparer.Ordinal)));
	}

	private static OrdinaryEnemyAppearanceProfile BuildSupportedAppearance(CapturedSubwaySpawnDefinition source)
	{
		OrdinaryEnemyTextureProfile[] textures = new OrdinaryEnemyTextureProfile[0];
		OrdinaryEnemyMeshProfile[] meshes = new OrdinaryEnemyMeshProfile[0];
		bool replaceTextures = false;
		OrdinaryEnemyScfuProfile scfuProfile = OrdinaryEnemyScfuProfile.Generic;
		if (source.MonsterData == 26092 || source.MonsterData == 203734)
		{
			replaceTextures = true;
			textures = new OrdinaryEnemyTextureProfile[5]
			{
				new OrdinaryEnemyTextureProfile(0, 9418, 0),
				new OrdinaryEnemyTextureProfile(1, 8729, 0),
				new OrdinaryEnemyTextureProfile(2, 9420, 0),
				new OrdinaryEnemyTextureProfile(3, 9419, 0),
				new OrdinaryEnemyTextureProfile(4, 9421, 0)
			};
			meshes = new OrdinaryEnemyMeshProfile[3]
			{
				new OrdinaryEnemyMeshProfile(0, 160561u, 0, 2),
				new OrdinaryEnemyMeshProfile(0, (uint)source.HeadMesh, 0, 4),
				new OrdinaryEnemyMeshProfile(1, 7777u, 0, 2)
			};
			if (source.MonsterData == 26092)
			{
				scfuProfile = OrdinaryEnemyScfuProfile.CapturedThief;
			}
		}
		else if (source.MonsterData == 203733)
		{
			replaceTextures = true;
			textures = new OrdinaryEnemyTextureProfile[5]
			{
				new OrdinaryEnemyTextureProfile(0, 0, 0),
				new OrdinaryEnemyTextureProfile(1, 21824, 0),
				new OrdinaryEnemyTextureProfile(2, 0, 0),
				new OrdinaryEnemyTextureProfile(3, 21819, 0),
				new OrdinaryEnemyTextureProfile(4, 21831, 0)
			};
			meshes = new OrdinaryEnemyMeshProfile[2]
			{
				new OrdinaryEnemyMeshProfile(0, (uint)source.HeadMesh, 0, 4),
				new OrdinaryEnemyMeshProfile(1, 136583u, 0, 2)
			};
		}
		else if (source.MonsterData == 17657)
		{
			scfuProfile = OrdinaryEnemyScfuProfile.CapturedFilthFlea;
		}
		return new OrdinaryEnemyAppearanceProfile(3, 1, Math.Max(1, Math.Min(7, source.Breed)), source.Sex, 1, source.CharacterFlags, 0, 0, source.NpcFamily, 0, 31, 0, (source.MonsterData == 26092) ? 1187842u : 0u, source.HeadMesh, replaceTextures, source.HeadMesh == 0, textures, meshes, scfuProfile);
	}

	private static OrdinaryEnemyAggressionProfile RetaliateAggression()
	{
		return RetaliateChasingAggression;
	}

	private static OrdinaryEnemyAggressionProfile AggressionFor(int monsterData)
	{
		OrdinaryEnemyAggressionProfile result;
		switch (monsterData)
		{
		case 30379:
			return BloodcreeperAutomaticAggression;
		case 203728:
			return IncompleteRebuildAutomaticAggression;
		default:
			result = RetaliateAggression();
			break;
		case 204178:
			result = RedundantScanAutomaticAggression;
			break;
		}
		return result;
	}

	private static WorldRespawnPolicyAssignment ViolentVagabondRespawnPolicy()
	{
		return WorldRespawnPolicyAssignment.Explicit(new RespawnPolicyDefinition
		{
			RespawnPolicyKey = "ordinary.violent-vagabond.450",
			Mode = WorldRespawnMode.FixedDelay,
			FixedDelaySeconds = 450.0,
			RespawnAtOriginalPosition = true,
			ResetHealth = true,
			ResetMovementState = true,
			ResetAggressionState = true,
			DelayStartsAt = RespawnDelayStartsAt.NpcDespawn,
			Evidence = "official-live:20260708-143600;794CD74B>794DF301;449.759588-seconds-after-npc-despawn;1.088-position-delta",
			Confidence = "CAPTURE_BOUNDED",
			Enabled = true
		});
	}

	private static void ValidateViolentVagabondEvidenceBoundary(IEnumerable<OrdinaryEnemyProfile> profiles, IEnumerable<OrdinaryEnemySpawnDefinition> spawns)
	{
		OrdinaryEnemyProfile profile = profiles.Single((OrdinaryEnemyProfile value) => value.MonsterData == 203733);
		CapturedEnemyCombatContract contract = profile.Combat.Contract;
		if (contract == null || !contract.IsCombatReady || contract.AttackModel != CapturedEnemyAttackModel.Specialized || contract.WeaponLowId == 130590 || contract.WeaponHighId == 130590 || contract.SpecialAttackSequence == null || contract.SpecialAttackSequence.OpeningAttack != null || contract.SpecialAttackSequence.RepeatingAttack == null || contract.SpecialAttackSequence.RepeatingAttack.MinDamage != 9 || contract.SpecialAttackSequence.RepeatingAttack.MaxDamage != 12 || contract.SpecialAttackSequence.RepeatingAttack.RechargeSeconds != 4.5802404 || contract.SpecialAttackSequence.RepeatingAttack.AttackInfoAmmoCount != 0 || contract.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponSlot != 6 || contract.SpecialAttackSequence.RepeatingAttack.AttackInfoWeaponInstance != 0 || profile.Aggression.Mode != OrdinaryEnemyAggressionMode.Retaliate || profile.Aggression.AutomaticAggroRadius.HasValue || !profile.Aggression.Chase || profile.Aggression.ReturnToSpawn || profile.Aggression.EvidenceState != OrdinaryEnemyEvidenceState.Observed)
		{
			throw new InvalidOperationException("Violent Vagabond combat/aggression evidence boundary drifted");
		}
		OrdinaryEnemySpawnDefinition[] array = spawns.Where((OrdinaryEnemySpawnDefinition value) => value.ProfileKey == profile.ProfileKey).ToArray();
		if (array.Length != 22 || array.Any((OrdinaryEnemySpawnDefinition value) => value.Disposition != OrdinaryEnemyRuntimeDisposition.Active) || array.Any((OrdinaryEnemySpawnDefinition value) => value.RespawnEvidence != OrdinaryEnemyEvidenceState.Policy || value.RespawnDelaySeconds != 450.0 || value.RespawnPolicy.Mode != WorldRespawnPolicyAssignmentMode.Explicit || value.RespawnPolicy.ExplicitPolicy == null || value.RespawnPolicy.ExplicitPolicy.FixedDelaySeconds != 450.0 || value.RespawnPolicy.ExplicitPolicy.DelayStartsAt != RespawnDelayStartsAt.NpcDespawn))
		{
			throw new InvalidOperationException("Violent Vagabond population/respawn evidence boundary drifted");
		}
	}

	private static OrdinaryEnemySupportNanoProfile SupportNanoFor(int monsterData)
	{
		return monsterData switch
		{
			203728 => OrdinaryEnemySupportNanoProfile.CapturedIncompleteRebuild90405(), 
			203729 => OrdinaryEnemySupportNanoProfile.CapturedFragmentedSoul95447(), 
			204178 => new OrdinaryEnemySupportNanoProfile(121336, 121248, 60.0, 1.400106, 25.590325, 18000, 180.0, 7.5, fallbackToSelf: true, 220, 0, 9, -13, new int[23]
			{
				113, 102, 107, 103, 105, 104, 106, 100, 109, 133,
				110, 112, 130, 114, 115, 116, 108, 128, 122, 129,
				127, 131, 111
			}, OrdinaryEnemyEvidenceState.Policy, "20260709-222339,20260716-033326,20260716-034104,20260716-221358,20260717-214751;primary=121336;triggered-self=121248;duration-centiseconds=18000;primary-modify=+9;triggered-self-modify=-13;nearest-observed-ordinary-target-with-self-fallback"), 
			_ => null, 
		};
	}

	private static OrdinaryEnemyCombatProfile BuildCombatProfile(CapturedEnemyCombatContract contract, int monsterData, Func<int, CapturedEnemyCombatContract> contractResolver = null, Func<int, int, CapturedEnemyCombatContract> sourceContractResolver = null, Func<int, OrdinaryEnemySpawnVariant, CapturedEnemyCombatContract> sourceVariantContractResolver = null, bool capturedCombatEvidenceObserved = false)
	{
		OrdinaryEnemyCombatMode mode = OrdinaryEnemyCombatMode.Unresolved;
		OrdinaryEnemyDamageSource damageSource = OrdinaryEnemyDamageSource.Unresolved;
		bool visibleWeapon = false;
		if (sourceContractResolver != null || sourceVariantContractResolver != null)
		{
			mode = OrdinaryEnemyCombatMode.EquippedRanged;
			damageSource = OrdinaryEnemyDamageSource.WeaponRoll;
			visibleWeapon = true;
		}
		else
		{
			switch (contract.AttackModel)
			{
			case CapturedEnemyAttackModel.FixedAttackInfo:
				mode = OrdinaryEnemyCombatMode.UnarmedMelee;
				damageSource = OrdinaryEnemyDamageSource.CapturedFixed;
				break;
			case CapturedEnemyAttackModel.EquippedWeapon:
				mode = ((monsterData == 26092 || monsterData == 203747) ? OrdinaryEnemyCombatMode.EquippedRanged : OrdinaryEnemyCombatMode.Unresolved);
				damageSource = OrdinaryEnemyDamageSource.WeaponRoll;
				visibleWeapon = true;
				break;
			case CapturedEnemyAttackModel.Specialized:
				mode = OrdinaryEnemyCombatMode.NaturalMelee;
				damageSource = OrdinaryEnemyDamageSource.NaturalAttack;
				break;
			}
		}
		return new OrdinaryEnemyCombatProfile(mode, damageSource, visibleWeapon, contract, (contract.IsCombatReady || sourceContractResolver != null || sourceVariantContractResolver != null || capturedCombatEvidenceObserved) ? OrdinaryEnemyEvidenceState.Observed : OrdinaryEnemyEvidenceState.Unresolved, (monsterData == 26092) ? new double?(1.0) : null, (monsterData == 26092) ? new int?(1) : null, monsterData == 26092, contractResolver, sourceContractResolver, sourceVariantContractResolver);
	}

	private static OrdinaryEnemyLootEntry[] BuildStrictLootEntries(CapturedSubwayStrictLootProfileDefinition strictLootProfile)
	{
		string evidenceReference = string.Join(",", strictLootProfile.EvidenceCaptures);
		return strictLootProfile.Entries.Select((CapturedSubwayLootEvidenceDefinition value, int index) => new OrdinaryEnemyLootEntry(value.LowId, value.HighId, value.Quality, index, 1, 0, value.ObservedBasisPoints, OrdinaryEnemyLootEvidence.ObservedAvailableLoot, OrdinaryEnemyLootLinkageEvidence.ImportedCaptureEvidence, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, value.ObservedCount, value.ObservedCorpses, evidenceReference)).ToArray();
	}

	private static OrdinaryEnemyLootProfile BuildLootProfile(int monsterData, OrdinaryEnemyLootEntry[] entries)
	{
		return BuildLootProfile(monsterData, entries, new CapturedSubwayCorpseEvidenceDefinition[0], null);
	}

	private static OrdinaryEnemyLootProfile BuildLootProfile(int monsterData, OrdinaryEnemyLootEntry[] entries, CapturedSubwayCorpseEvidenceDefinition[] corpseEvidence)
	{
		return BuildLootProfile(monsterData, entries, corpseEvidence, null);
	}

	private static OrdinaryEnemyLootProfile BuildLootProfile(int monsterData, OrdinaryEnemyLootEntry[] entries, CapturedSubwayCorpseEvidenceDefinition[] corpseEvidence, CapturedSubwayStrictLootProfileDefinition strictLootProfile)
	{
		corpseEvidence = corpseEvidence ?? new CapturedSubwayCorpseEvidenceDefinition[0];
		OrdinaryEnemyLootEvidence evidence = ((entries.Length == 0) ? OrdinaryEnemyLootEvidence.Unresolved : ((strictLootProfile != null) ? OrdinaryEnemyLootEvidence.ObservedAvailableLoot : (entries.All((OrdinaryEnemyLootEntry value) => value.Evidence == OrdinaryEnemyLootEvidence.GuaranteedProven) ? OrdinaryEnemyLootEvidence.GuaranteedProven : OrdinaryEnemyLootEvidence.ObservedAvailableLoot)));
		int num = strictLootProfile?.ObservedCompleteInventories ?? entries.Select((OrdinaryEnemyLootEntry value) => value.ObservedCorpses).DefaultIfEmpty(0).Max();
		int observedEmptyInventories = strictLootProfile?.ObservedEmptyInventories ?? ((monsterData == 17657) ? 5 : 0);
		string itemEvidenceReference = string.Join(",", (from value in entries
			select value.EvidenceReference into value
			where !string.IsNullOrWhiteSpace(value)
			select value).Distinct(StringComparer.Ordinal));
		if (monsterData == 17649)
		{
			return new OrdinaryEnemyLootProfile(evidence, entries, OrdinaryEnemyLootPoolMode.WeightedOne, 5, itemPoolComplete: false, 8, 5, itemEvidenceReference, OrdinaryEnemyEvidenceState.Observed, null, null, new OrdinaryEnemyLevelCreditRule[5]
			{
				new OrdinaryEnemyLevelCreditRule(5, 6, 6, 2, "20260709-210452"),
				new OrdinaryEnemyLevelCreditRule(6, 8, 8, 3, "20260709-210452,20260712-153918,20260719-020104"),
				new OrdinaryEnemyLevelCreditRule(8, 10, 10, 4, "20260708-143600,20260709-205921,20260713-033511"),
				new OrdinaryEnemyLevelCreditRule(9, 11, 11, 3, "20260709-220439,20260712-160257,20260713-014714"),
				new OrdinaryEnemyLevelCreditRule(10, 12, 12, 2, "20260709-220439")
			});
		}
		if (corpseEvidence.Length != 0 && monsterData != 30379)
		{
			OrdinaryEnemyLevelCreditRule[] array = (from value in corpseEvidence
				group value by value.EnemyLevel into value
				orderby value.Key
				select value into @group
				select new OrdinaryEnemyLevelCreditRule(@group.Key, @group.Min((CapturedSubwayCorpseEvidenceDefinition value) => value.Credits), @group.Max((CapturedSubwayCorpseEvidenceDefinition value) => value.Credits), @group.Count(), string.Join(",", @group.Select((CapturedSubwayCorpseEvidenceDefinition value) => string.Format(CultureInfo.InvariantCulture, "{0}:{1}>{2}", value.Capture, value.DeadNpcIdentity, value.CorpseIdentity))))).ToArray();
			bool flag = monsterData == 203728;
			bool flag2 = monsterData == 203729;
			if (flag)
			{
				array = (from value in array.Concat(new OrdinaryEnemyLevelCreditRule[2]
					{
						new OrdinaryEnemyLevelCreditRule(20, 124, 124, 0, "policy:floor((13*level-11)/2);captured-levels=17,18,19,21", OrdinaryEnemyEvidenceState.Policy),
						new OrdinaryEnemyLevelCreditRule(22, 137, 137, 0, "policy:floor((13*level-11)/2);captured-levels=17,18,19,21", OrdinaryEnemyEvidenceState.Policy)
					})
					orderby value.EnemyLevel
					select value).ToArray();
			}
			else if (flag2)
			{
				array = (from value in array.Concat(new OrdinaryEnemyLevelCreditRule[2]
					{
						new OrdinaryEnemyLevelCreditRule(19, 118, 118, 0, "policy:floor((13*level-11)/2);captured-levels=17,18,21", OrdinaryEnemyEvidenceState.Policy),
						new OrdinaryEnemyLevelCreditRule(20, 124, 124, 0, "policy:floor((13*level-11)/2);captured-levels=17,18,21", OrdinaryEnemyEvidenceState.Policy)
					})
					orderby value.EnemyLevel
					select value).ToArray();
			}
			bool flag3 = monsterData == 17657;
			return new OrdinaryEnemyLootProfile(evidence, entries, OrdinaryEnemyLootPoolMode.IndependentEntries, 0, entries.Length != 0 && (strictLootProfile?.ItemPoolComplete ?? true), num, observedEmptyInventories, itemEvidenceReference, (!(flag3 || flag || flag2)) ? OrdinaryEnemyEvidenceState.Observed : OrdinaryEnemyEvidenceState.Policy, flag3 ? new int?(23) : null, flag3 ? new int?(79) : null, array);
		}
		return monsterData switch
		{
			17657 => new OrdinaryEnemyLootProfile(evidence, entries, OrdinaryEnemyLootPoolMode.IndependentEntries, 0, itemPoolComplete: true, Math.Max(8, num), 0, itemEvidenceReference, OrdinaryEnemyEvidenceState.Observed, 29, 79, new OrdinaryEnemyLevelCreditRule[0]), 
			30379 => new OrdinaryEnemyLootProfile(evidence, entries, OrdinaryEnemyLootPoolMode.IndependentEntries, 0, strictLootProfile?.ItemPoolComplete ?? false, num, observedEmptyInventories, itemEvidenceReference, OrdinaryEnemyEvidenceState.Policy, 150, 150, new OrdinaryEnemyLevelCreditRule[1]
			{
				new OrdinaryEnemyLevelCreditRule(24, 150, 150, 3, "20260712-223719,20260716-033326,20260716-034104")
			}), 
			_ => new OrdinaryEnemyLootProfile(evidence, entries, OrdinaryEnemyLootPoolMode.IndependentEntries, 0, entries.Length != 0 && (strictLootProfile?.ItemPoolComplete ?? true), num, observedEmptyInventories, itemEvidenceReference, OrdinaryEnemyEvidenceState.Unresolved, null, null, new OrdinaryEnemyLevelCreditRule[0]), 
		};
	}

	private static OrdinaryEnemyCorpseProfile StandardCorpseProfile(int monsterData)
	{
		return StandardCorpseProfile(monsterData, new CapturedSubwayCorpseEvidenceDefinition[0]);
	}

	private static OrdinaryEnemyCorpseProfile StandardCorpseProfile(int monsterData, CapturedSubwayCorpseEvidenceDefinition[] corpseEvidence)
	{
		corpseEvidence = corpseEvidence ?? new CapturedSubwayCorpseEvidenceDefinition[0];
		if (corpseEvidence.Length != 0)
		{
			OrdinaryEnemyCorpsePacketProfile packetProfile = monsterData switch
			{
				17657 => OrdinaryEnemyCorpsePacketProfile.CapturedFilthFlea, 
				26092 => OrdinaryEnemyCorpsePacketProfile.CapturedThief, 
				_ => OrdinaryEnemyCorpsePacketProfile.Generic, 
			};
			int[] array = corpseEvidence.Select((CapturedSubwayCorpseEvidenceDefinition value) => value.CatMesh).Distinct().ToArray();
			if (array.Length != 1)
			{
				throw new InvalidOperationException("Captured ordinary corpse evidence has conflicting CATMesh values: " + monsterData.ToString(CultureInfo.InvariantCulture));
			}
			return new OrdinaryEnemyCorpseProfile(packetProfile, 3.0, 240.0, 3.0, array[0], string.Join(",", corpseEvidence.Select((CapturedSubwayCorpseEvidenceDefinition value) => string.Format(CultureInfo.InvariantCulture, "{0}:{1}>{2}", value.Capture, value.DeadNpcIdentity, value.CorpseIdentity))));
		}
		return monsterData switch
		{
			26092 => CapturedThiefCorpse, 
			17657 => CapturedFilthFleaCorpse, 
			_ => StandardGenericCorpse, 
		};
	}

	private static string SupportedProfileKey(CapturedSubwaySpawnDefinition source)
	{
		return string.Format(CultureInfo.InvariantCulture, "subway.supported.{0}", source.MonsterData);
	}

	private static string OrdinaryProfileKey(string archetypeKey)
	{
		return "subway.ordinary." + archetypeKey;
	}

	private static string SpawnKey(int sourceIdentity)
	{
		return string.Format(CultureInfo.InvariantCulture, "subway.{0:X8}", sourceIdentity);
	}
}
