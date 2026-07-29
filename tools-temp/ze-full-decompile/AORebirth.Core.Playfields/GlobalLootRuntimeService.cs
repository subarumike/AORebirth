using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Items;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core;

namespace AORebirth.Core.Playfields;

internal sealed class GlobalLootRuntimeService
{
	private const string CleaningRobotProfileKey = "captured.arete.cleaning-robot";

	private const int CleaningRobotMonsterData = 297023;

	private const int CleaningRobotCredits = 5;

	private const string DockerProfileKey = "captured.arete.32v-docker";

	private const int DockerMonsterData = 17649;

	private const string WasteCollectorProfileKey = "captured.arete.waste-collector";

	private const int WasteCollectorMonsterData = 17714;

	private const string GarbageFleaProfileKey = "captured.arete.garbage-flea";

	private const int GarbageFleaMonsterData = 17657;

	private const int CapturedAbmouthCredits = 587;

	private const int CapturedInfectorCredits = 150;

	private const int CapturedEumenidesCredits = 186;

	private const string CapturedVergilProfileKey = "subway.127.boss.vergil-aeneid";

	private const int CapturedVergilMonsterData = 203748;

	private const string CapturedAbmouthLootEvidence = "official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved";

	private const string CapturedVergilLootEvidence = "official-live-captures 20260712-232711/234401/20260716-034433; three exact observed corpse snapshots with linked credits 610/587/563; 20260716-034433 inventory linked by normalized corpse identity F69001; snapshot probabilities and wider pool unresolved";

	private const string CapturedEumenidesLootEvidence = "official-live-captures 20260717-214751/20260717-215250; two exact identity-linked Eumenides corpse snapshots, each with 186 credits and three item rows; 20260717-220340 adds exact local-name/identity-linked item membership for two already-existing Remains of Eumenides corpses but no CorpseFullUpdate, credits, dead-NPC link, or playfield context, so those rows are not promoted as atomic runtime snapshots; snapshot probabilities and wider pool unresolved";

	private readonly object sync = new object();

	private readonly object productionRandomSync = new object();

	private readonly Random productionRandom = new Random();

	private readonly LootTableRegistry registry;

	private readonly LootGenerationService generator;

	private bool databaseLoaded;

	private CombatLootTableEntry[] databaseEntries = new CombatLootTableEntry[0];

	private CombatLootTableEntry[] debugEntries = new CombatLootTableEntry[0];

	internal LootTableRegistry Registry => registry;

	internal GlobalLootRuntimeService()
	{
		registry = new LootTableRegistry((int value) => ItemLoader.ItemList.ContainsKey(value));
		generator = new LootGenerationService(registry, new LootAssignmentResolver());
	}

	internal LootGenerationResult Generate(ICharacter target, int playfieldId)
	{
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		if (target == null)
		{
			throw new ArgumentNullException("target");
		}
		LootGenerationContext lootGenerationContext = BuildContext(target, playfieldId);
		try
		{
			EnsureDefinitions(target, lootGenerationContext);
		}
		catch (LootDefinitionValidationException ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, "Global loot definition rejected: " + ex.Message);
		}
		int seed;
		lock (productionRandomSync)
		{
			seed = productionRandom.Next();
		}
		lootGenerationContext.Seed = seed;
		LootGenerationResult lootGenerationResult = generator.Generate(lootGenerationContext, new SeededLootRandomSource(seed));
		if (DiagnosticsEnabled())
		{
			LogUtil.Debug((DebugInfoDetail)128, string.Format(CultureInfo.InvariantCulture, "GlobalLoot target={0} profile={1} tables={2} assignments={3} items={4} credits={5} unresolved={6}/{7}", ((IEntity)target).Identity, lootGenerationContext.EnemyProfileKey, string.Join(",", lootGenerationResult.AppliedTableKeys), string.Join(",", lootGenerationResult.AppliedAssignmentKeys), lootGenerationResult.Items.Count, lootGenerationResult.Credits, lootGenerationResult.LootUnresolved, lootGenerationResult.CreditsUnresolved));
		}
		return lootGenerationResult;
	}

	internal LootGenerationResult GenerateDeterministic(LootGenerationContext context, int seed)
	{
		context.Seed = seed;
		return generator.Generate(context, new SeededLootRandomSource(seed));
	}

	private LootGenerationContext BuildContext(ICharacter target, int playfieldId)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		Identity identity = ((IEntity)target).Identity;
		CapturedEncounterRuntimeDefinition definition;
		bool flag = CapturedEncounterRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out definition);
		identity = ((IEntity)target).Identity;
		OrdinaryEnemyRuntimeDefinition definition2;
		bool flag2 = OrdinaryEnemyRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out definition2);
		bool flag3 = PetCombatRules.IsPlayerOwnedPet(target);
		int value = ((IStats)target).Stats[(StatIds)359].Value;
		bool flag4 = !flag3 && value == 203748;
		LootGenerationContext obj = new LootGenerationContext
		{
			EnemyProfileKey = (flag4 ? "subway.127.boss.vergil-aeneid" : (flag ? definition.ProfileKey : (flag3 ? "owned-summon" : (flag2 ? definition2.Profile.ProfileKey : LegacyProfileKey(target)))))
		};
		identity = ((IEntity)target).Identity;
		obj.EnemyIdentityInstance = ((Identity)(ref identity)).Instance;
		obj.MonsterData = value;
		obj.FamilyKey = (flag4 ? "subway.127.named-boss" : (flag ? ("encounter." + definition.EncounterKey) : (flag2 ? definition2.Profile.FamilyKey : ("legacy." + ((IStats)target).Stats[(StatIds)455].Value))));
		obj.Level = ((IStats)target).Stats[(StatIds)54].Value;
		obj.PlayfieldId = playfieldId;
		obj.SpawnKey = (flag ? definition.SpawnKey : (flag2 ? definition2.Spawn.SpawnKey : null));
		obj.EncounterKey = (flag ? definition.EncounterKey : null);
		obj.IsBoss = flag4 || (flag && definition.IsBoss);
		obj.IsOwnedSummon = flag3 && !flag;
		return obj;
	}

	private void EnsureDefinitions(ICharacter target, LootGenerationContext context)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		if (context.IsOwnedSummon)
		{
			return;
		}
		lock (sync)
		{
			if (string.Equals(context.EnemyProfileKey, "subway.127.boss.vergil-aeneid", StringComparison.Ordinal))
			{
				EnsureCapturedVergil();
				return;
			}
			Identity identity = ((IEntity)target).Identity;
			if (CapturedEncounterRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition))
			{
				EnsureCapturedEncounter(definition);
				return;
			}
			if (context.MonsterData == 297023)
			{
				EnsureCleaningRobot();
				context.EnemyProfileKey = "captured.arete.cleaning-robot";
				return;
			}
			if (context.MonsterData == 17649)
			{
				EnsureAlexAreaDocker();
				context.EnemyProfileKey = "captured.arete.32v-docker";
				return;
			}
			if (context.MonsterData == 17657)
			{
				EnsureAlexAreaGarbageFlea();
				context.EnemyProfileKey = "captured.arete.garbage-flea";
				return;
			}
			if (context.MonsterData == 17714)
			{
				EnsureAlexAreaWasteCollector();
				context.EnemyProfileKey = "captured.arete.waste-collector";
				return;
			}
			identity = ((IEntity)target).Identity;
			if (OrdinaryEnemyRuntimeRegistry.TryGet(((Identity)(ref identity)).Instance, out var definition2))
			{
				EnsureOrdinary(definition2.Profile, context.Level);
				return;
			}
			EnsureDatabaseLoaded();
			EnsureLegacyTarget(target, context.EnemyProfileKey);
		}
	}

	private void EnsureCapturedEncounter(CapturedEncounterRuntimeDefinition encounter)
	{
		string text = "captured." + encounter.ProfileKey;
		if (!registry.ContainsTable(text))
		{
			bool flag = encounter.IsBoss && string.Equals(encounter.ProfileKey, "subway.127.boss.abmouth-supremus", StringComparison.Ordinal);
			bool flag2 = string.Equals(encounter.ProfileKey, "subway.127.encounter.abmouth-infector", StringComparison.Ordinal);
			bool flag3 = string.Equals(encounter.ProfileKey, "subway.127.named.eumenides", StringComparison.Ordinal);
			if (flag || flag2 || flag3)
			{
				ObservedCorpseSnapshotDefinition[] observedCorpseSnapshots = ((!flag) ? ((!flag3) ? new ObservedCorpseSnapshotDefinition[0] : new ObservedCorpseSnapshotDefinition[2]
				{
					ObservedCorpseSnapshot("official-live-captures 20260717-214751/20260717-215250; two exact identity-linked Eumenides corpse snapshots, each with 186 credits and three item rows; 20260717-220340 adds exact local-name/identity-linked item membership for two already-existing Remains of Eumenides corpses but no CorpseFullUpdate, credits, dead-NPC link, or playfield context, so those rows are not promoted as atomic runtime snapshots; snapshot probabilities and wider pool unresolved", "capture.20260717-214751", 186, ObservedCorpseSnapshotEntry("official-live-captures 20260717-214751/20260717-215250; two exact identity-linked Eumenides corpse snapshots, each with 186 credits and three item rows; 20260717-220340 adds exact local-name/identity-linked item membership for two already-existing Remains of Eumenides corpses but no CorpseFullUpdate, credits, dead-NPC link, or playfield context, so those rows are not promoted as atomic runtime snapshots; snapshot probabilities and wider pool unresolved", "capture.20260717-214751", 163430, 163431, 22, 1), ObservedCorpseSnapshotEntry("official-live-captures 20260717-214751/20260717-215250; two exact identity-linked Eumenides corpse snapshots, each with 186 credits and three item rows; 20260717-220340 adds exact local-name/identity-linked item membership for two already-existing Remains of Eumenides corpses but no CorpseFullUpdate, credits, dead-NPC link, or playfield context, so those rows are not promoted as atomic runtime snapshots; snapshot probabilities and wider pool unresolved", "capture.20260717-214751", 301714, 301714, 1, 1), ObservedCorpseSnapshotEntry("official-live-captures 20260717-214751/20260717-215250; two exact identity-linked Eumenides corpse snapshots, each with 186 credits and three item rows; 20260717-220340 adds exact local-name/identity-linked item membership for two already-existing Remains of Eumenides corpses but no CorpseFullUpdate, credits, dead-NPC link, or playfield context, so those rows are not promoted as atomic runtime snapshots; snapshot probabilities and wider pool unresolved", "capture.20260717-214751", 287146, 287146, 200, 1)),
					ObservedCorpseSnapshot("official-live-captures 20260717-214751/20260717-215250; two exact identity-linked Eumenides corpse snapshots, each with 186 credits and three item rows; 20260717-220340 adds exact local-name/identity-linked item membership for two already-existing Remains of Eumenides corpses but no CorpseFullUpdate, credits, dead-NPC link, or playfield context, so those rows are not promoted as atomic runtime snapshots; snapshot probabilities and wider pool unresolved", "capture.20260717-215250", 186, ObservedCorpseSnapshotEntry("official-live-captures 20260717-214751/20260717-215250; two exact identity-linked Eumenides corpse snapshots, each with 186 credits and three item rows; 20260717-220340 adds exact local-name/identity-linked item membership for two already-existing Remains of Eumenides corpses but no CorpseFullUpdate, credits, dead-NPC link, or playfield context, so those rows are not promoted as atomic runtime snapshots; snapshot probabilities and wider pool unresolved", "capture.20260717-215250", 301715, 301715, 1, 1), ObservedCorpseSnapshotEntry("official-live-captures 20260717-214751/20260717-215250; two exact identity-linked Eumenides corpse snapshots, each with 186 credits and three item rows; 20260717-220340 adds exact local-name/identity-linked item membership for two already-existing Remains of Eumenides corpses but no CorpseFullUpdate, credits, dead-NPC link, or playfield context, so those rows are not promoted as atomic runtime snapshots; snapshot probabilities and wider pool unresolved", "capture.20260717-215250", 160051, 160050, 16, 1), ObservedCorpseSnapshotEntry("official-live-captures 20260717-214751/20260717-215250; two exact identity-linked Eumenides corpse snapshots, each with 186 credits and three item rows; 20260717-220340 adds exact local-name/identity-linked item membership for two already-existing Remains of Eumenides corpses but no CorpseFullUpdate, credits, dead-NPC link, or playfield context, so those rows are not promoted as atomic runtime snapshots; snapshot probabilities and wider pool unresolved", "capture.20260717-215250", 287146, 287146, 200, 1))
				}) : new ObservedCorpseSnapshotDefinition[2]
				{
					ObservedCorpseSnapshot("official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved", "capture.20260712-232137", 587, ObservedCorpseSnapshotEntry("official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved", "capture.20260712-232137", 136622, 136623, 30, 1), ObservedCorpseSnapshotEntry("official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved", "capture.20260712-232137", 202717, 202718, 28, 1), ObservedCorpseSnapshotEntry("official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved", "capture.20260712-232137", 107933, 107934, 23, 1), ObservedCorpseSnapshotEntry("official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved", "capture.20260712-232137", 85693, 27389, 30, 1), ObservedCorpseSnapshotEntry("official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved", "capture.20260712-232137", 287146, 287146, 200, 1)),
					ObservedCorpseSnapshot("official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved", "capture.20260716-220400", 587, ObservedCorpseSnapshotEntry("official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved", "capture.20260716-220400", 202741, 202742, 32, 1), ObservedCorpseSnapshotEntry("official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved", "capture.20260716-220400", 202734, 202735, 32, 1), ObservedCorpseSnapshotEntry("official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved", "capture.20260716-220400", 202717, 202718, 32, 1), ObservedCorpseSnapshotEntry("official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved", "capture.20260716-220400", 85723, 85722, 32, 1), ObservedCorpseSnapshotEntry("official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved", "capture.20260716-220400", 123968, 123970, 25, 1), ObservedCorpseSnapshotEntry("official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved", "capture.20260716-220400", 287146, 287146, 200, 1))
				});
				LootTableDefinition lootTableDefinition = new LootTableDefinition();
				lootTableDefinition.LootTableKey = text;
				lootTableDefinition.DisplayName = encounter.DisplayName + " captured corpse";
				lootTableDefinition.TableType = (flag ? LootTableType.Boss : LootTableType.EnemyType);
				lootTableDefinition.RollGroups = new LootGroupDefinition[0];
				lootTableDefinition.ObservedCorpseSnapshots = observedCorpseSnapshots;
				lootTableDefinition.CreditsPolicy = ((flag || flag3) ? new CreditsPolicyDefinition
				{
					Mode = CreditsPolicyMode.Unresolved,
					Evidence = LootEvidenceConfidence.Unresolved
				} : CreditsRange(150, 150, LootEvidenceConfidence.ProvenCapture));
				lootTableDefinition.QualityPolicy = ((flag || flag3) ? "captured-observed-corpse-snapshots" : "unresolved");
				lootTableDefinition.Evidence = (flag ? "official-live-captures 20260712-232137/20260716-220400; two exact Abmouth corpse snapshots with linked 587 credits; 20260716-220400 inventory generation rebound after corpse identity F69001 reuse; snapshot probabilities and wider pool unresolved" : (flag3 ? "official-live-captures 20260717-214751/20260717-215250; two exact identity-linked Eumenides corpse snapshots, each with 186 credits and three item rows; 20260717-220340 adds exact local-name/identity-linked item membership for two already-existing Remains of Eumenides corpses but no CorpseFullUpdate, credits, dead-NPC link, or playfield context, so those rows are not promoted as atomic runtime snapshots; snapshot probabilities and wider pool unresolved" : (encounter.Evidence + "; item pool unresolved")));
				lootTableDefinition.Confidence = ((flag || flag3) ? LootEvidenceConfidence.ObservedAvailableLoot : LootEvidenceConfidence.Unresolved);
				lootTableDefinition.ItemPoolUnresolved = true;
				lootTableDefinition.Enabled = true;
				LootTableDefinition lootTableDefinition2 = lootTableDefinition;
				registry.RegisterTable(lootTableDefinition2);
				registry.RegisterAssignment(new LootAssignmentDefinition
				{
					AssignmentKey = text,
					TargetType = (flag ? LootAssignmentTargetType.Boss : LootAssignmentTargetType.EnemyType),
					TargetKey = encounter.ProfileKey,
					LootTableKey = text,
					PlayfieldId = 127,
					EncounterKey = encounter.EncounterKey,
					Priority = 0,
					Conditions = new string[0],
					Evidence = lootTableDefinition2.Evidence,
					Confidence = lootTableDefinition2.Confidence,
					Enabled = true
				});
			}
		}
	}

	private void EnsureCapturedVergil()
	{
		string text = "captured.subway.127.boss.vergil-aeneid";
		if (!registry.ContainsTable(text))
		{
			registry.RegisterTable(BuildCapturedVergilLootTable());
			registry.RegisterAssignment(new LootAssignmentDefinition
			{
				AssignmentKey = text,
				TargetType = LootAssignmentTargetType.Boss,
				TargetKey = "subway.127.boss.vergil-aeneid",
				LootTableKey = text,
				PlayfieldId = 127,
				Priority = 0,
				Conditions = new string[0],
				Evidence = "official-live-captures 20260712-232711/234401/20260716-034433; three exact observed corpse snapshots with linked credits 610/587/563; 20260716-034433 inventory linked by normalized corpse identity F69001; snapshot probabilities and wider pool unresolved",
				Confidence = LootEvidenceConfidence.ObservedAvailableLoot,
				Enabled = true
			});
		}
	}

	internal static LootTableDefinition BuildCapturedVergilLootTable()
	{
		string lootTableKey = "captured.subway.127.boss.vergil-aeneid";
		LootTableDefinition lootTableDefinition = new LootTableDefinition();
		lootTableDefinition.LootTableKey = lootTableKey;
		lootTableDefinition.DisplayName = "Vergil Aeneid captured corpse snapshots";
		lootTableDefinition.TableType = LootTableType.Boss;
		lootTableDefinition.RollGroups = new LootGroupDefinition[0];
		lootTableDefinition.ObservedCorpseSnapshots = new ObservedCorpseSnapshotDefinition[3]
		{
			ObservedCorpseSnapshot("capture.20260712-232711", 610, ObservedCorpseSnapshotEntry("capture.20260712-232711", 301713, 301713, 1, 1), ObservedCorpseSnapshotEntry("capture.20260712-232711", 202743, 202744, 32, 1), ObservedCorpseSnapshotEntry("capture.20260712-232711", 287146, 287146, 200, 1)),
			ObservedCorpseSnapshot("capture.20260712-234401", 587, ObservedCorpseSnapshotEntry("capture.20260712-234401", 301714, 301714, 1, 1), ObservedCorpseSnapshotEntry("capture.20260712-234401", 123571, 123572, 23, 1), ObservedCorpseSnapshotEntry("capture.20260712-234401", 287146, 287146, 200, 1)),
			ObservedCorpseSnapshot("capture.20260716-034433", 563, ObservedCorpseSnapshotEntry("capture.20260716-034433", 202734, 202735, 33, 1), ObservedCorpseSnapshotEntry("capture.20260716-034433", 301715, 301715, 1, 1), ObservedCorpseSnapshotEntry("capture.20260716-034433", 160051, 160050, 24, 1), ObservedCorpseSnapshotEntry("capture.20260716-034433", 21605, 21605, 1, 100), ObservedCorpseSnapshotEntry("capture.20260716-034433", 287146, 287146, 200, 1))
		};
		lootTableDefinition.CreditsPolicy = new CreditsPolicyDefinition
		{
			Mode = CreditsPolicyMode.Unresolved,
			Evidence = LootEvidenceConfidence.Unresolved
		};
		lootTableDefinition.QualityPolicy = "captured-observed-corpse-snapshots";
		lootTableDefinition.Evidence = "official-live-captures 20260712-232711/234401/20260716-034433; three exact observed corpse snapshots with linked credits 610/587/563; 20260716-034433 inventory linked by normalized corpse identity F69001; snapshot probabilities and wider pool unresolved";
		lootTableDefinition.Confidence = LootEvidenceConfidence.ObservedAvailableLoot;
		lootTableDefinition.ItemPoolUnresolved = true;
		lootTableDefinition.Enabled = true;
		return lootTableDefinition;
	}

	private static ObservedCorpseSnapshotDefinition ObservedCorpseSnapshot(string snapshotKey, int credits, params LootEntryDefinition[] entries)
	{
		return ObservedCorpseSnapshot("official-live-captures 20260712-232711/234401/20260716-034433; three exact observed corpse snapshots with linked credits 610/587/563; 20260716-034433 inventory linked by normalized corpse identity F69001; snapshot probabilities and wider pool unresolved", snapshotKey, credits, entries);
	}

	private static ObservedCorpseSnapshotDefinition ObservedCorpseSnapshot(string evidence, string snapshotKey, int credits, params LootEntryDefinition[] entries)
	{
		ObservedCorpseSnapshotDefinition observedCorpseSnapshotDefinition = new ObservedCorpseSnapshotDefinition();
		observedCorpseSnapshotDefinition.SnapshotKey = snapshotKey;
		observedCorpseSnapshotDefinition.Credits = credits;
		observedCorpseSnapshotDefinition.Entries = entries ?? new LootEntryDefinition[0];
		observedCorpseSnapshotDefinition.Evidence = LootEvidenceConfidence.ProvenCapture;
		observedCorpseSnapshotDefinition.SelectionProbabilityEvidence = LootEvidenceConfidence.Unresolved;
		observedCorpseSnapshotDefinition.EvidenceReference = evidence + "; " + snapshotKey;
		return observedCorpseSnapshotDefinition;
	}

	private static LootEntryDefinition ObservedCorpseSnapshotEntry(string snapshotKey, int itemTemplateId, int highItemTemplateId, int quality, int quantity)
	{
		return ObservedCorpseSnapshotEntry("official-live-captures 20260712-232711/234401/20260716-034433; three exact observed corpse snapshots with linked credits 610/587/563; 20260716-034433 inventory linked by normalized corpse identity F69001; snapshot probabilities and wider pool unresolved", snapshotKey, itemTemplateId, highItemTemplateId, quality, quantity);
	}

	private static LootEntryDefinition ObservedCorpseSnapshotEntry(string evidence, string snapshotKey, int itemTemplateId, int highItemTemplateId, int quality, int quantity)
	{
		return new LootEntryDefinition
		{
			SelectionKey = snapshotKey,
			ItemTemplateId = itemTemplateId,
			HighItemTemplateId = highItemTemplateId,
			FixedQuality = quality,
			MinimumQuality = quality,
			MaximumQuality = quality,
			MinimumQuantity = quantity,
			MaximumQuantity = quantity,
			Weight = 0,
			DropChanceBasisPoints = 0,
			UniquePerCorpse = true,
			Semantics = LootSemantics.ObservedAvailable,
			Evidence = LootEvidenceConfidence.ObservedAvailableLoot,
			EvidenceReference = evidence + "; " + snapshotKey,
			ProbabilityEvidence = "unresolved"
		};
	}

	private void EnsureOrdinary(OrdinaryEnemyProfile profile, int targetLevel)
	{
		string text = ((profile.Loot.LevelCreditRules.Length != 0) ? (".level." + targetLevel.ToString(CultureInfo.InvariantCulture)) : string.Empty);
		string text2 = "ordinary." + profile.ProfileKey + text;
		string assignmentKey = "ordinary." + profile.ProfileKey + text;
		if (!registry.ContainsTable(text2))
		{
			OrdinaryEnemyLootTableAdapterResult ordinaryEnemyLootTableAdapterResult = OrdinaryEnemyLootTableAdapter.Build(profile, targetLevel, text2, assignmentKey);
			registry.RegisterTableAndAssignment(ordinaryEnemyLootTableAdapterResult.Table, ordinaryEnemyLootTableAdapterResult.Assignment);
		}
	}

	private void EnsureCleaningRobot()
	{
		if (registry.ContainsTable("captured.arete.cleaning-robot"))
		{
			return;
		}
		int[][] array = new int[18][]
		{
			new int[1] { 42620 },
			new int[0],
			new int[2] { 36779, 84142 },
			new int[0],
			new int[1] { 297289 },
			new int[0],
			new int[0],
			new int[2] { 70558, 155685 },
			new int[2] { 297289, 150306 },
			new int[0],
			new int[1] { 155666 },
			new int[0],
			new int[1] { 70564 },
			new int[1] { 155666 },
			new int[1] { 155687 },
			new int[1] { 70565 },
			new int[1] { 155684 },
			new int[0]
		};
		List<LootEntryDefinition> list = new List<LootEntryDefinition>();
		int num = 0;
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].Length == 0)
			{
				num++;
				continue;
			}
			int[] array2 = array[i];
			foreach (int itemId in array2)
			{
				list.Add(FixedEntry(itemId, 1, "outcome." + i, 1));
			}
		}
		registry.RegisterTable(new LootTableDefinition
		{
			LootTableKey = "captured.arete.cleaning-robot",
			DisplayName = "Malfunctioning Cleaning Robot captured outcomes",
			TableType = LootTableType.EnemyType,
			RollGroups = new LootGroupDefinition[1]
			{
				new LootGroupDefinition
				{
					LootGroupKey = "captured-outcome",
					RollMode = LootRollMode.WeightedOne,
					RollCount = 1,
					EmptyWeight = num,
					DropChanceBasisPoints = 10000,
					Entries = list.ToArray(),
					Conditions = new string[0]
				}
			},
			CreditsPolicy = CreditsRange(5, 5, LootEvidenceConfidence.ProvenCapture),
			QualityPolicy = "captured-fixed",
			Evidence = "live-capture-20260629-142800",
			Confidence = LootEvidenceConfidence.ProvenCapture,
			Enabled = true
		});
		registry.RegisterAssignment(new LootAssignmentDefinition
		{
			AssignmentKey = "captured.arete.cleaning-robot",
			TargetType = LootAssignmentTargetType.EnemyType,
			TargetKey = "captured.arete.cleaning-robot",
			LootTableKey = "captured.arete.cleaning-robot",
			Priority = 0,
			Evidence = "live-capture-20260629-142800",
			Confidence = LootEvidenceConfidence.ProvenCapture,
			Enabled = true,
			Conditions = new string[0]
		});
	}

	private void EnsureAlexAreaDocker()
	{
		if (!registry.ContainsTable("captured.arete.32v-docker"))
		{
			registry.RegisterTable(new LootTableDefinition
			{
				LootTableKey = "captured.arete.32v-docker",
				DisplayName = "32-V Docker captured outcomes",
				TableType = LootTableType.EnemyType,
				RollGroups = new LootGroupDefinition[1]
				{
					new LootGroupDefinition
					{
						LootGroupKey = "captured-outcome",
						RollMode = LootRollMode.WeightedOne,
						RollCount = 1,
						EmptyWeight = 2,
						DropChanceBasisPoints = 10000,
						Entries = new LootEntryDefinition[2]
						{
							FixedEntry(248318, 1, "docker.248318", 1),
							new LootEntryDefinition
							{
								SelectionKey = "docker.70560",
								ItemTemplateId = 70560,
								HighItemTemplateId = 85688,
								FixedQuality = 3,
								MinimumQuality = 3,
								MaximumQuality = 3,
								MinimumQuantity = 1,
								MaximumQuantity = 1,
								Weight = 1,
								DropChanceBasisPoints = 10000,
								Semantics = LootSemantics.WeightedDocumented,
								Evidence = LootEvidenceConfidence.ProvenCapture,
								EvidenceReference = "20260720-080123"
							}
						},
						Conditions = new string[0]
					}
				},
				CreditsPolicy = CreditsRange(4, 4, LootEvidenceConfidence.ProvenCapture),
				QualityPolicy = "captured-fixed",
				Evidence = "20260720-080123",
				Confidence = LootEvidenceConfidence.ProvenCapture,
				Enabled = true
			});
			registry.RegisterAssignment(new LootAssignmentDefinition
			{
				AssignmentKey = "captured.arete.32v-docker",
				TargetType = LootAssignmentTargetType.EnemyType,
				TargetKey = "captured.arete.32v-docker",
				LootTableKey = "captured.arete.32v-docker",
				Priority = 0,
				Evidence = "20260720-080123",
				Confidence = LootEvidenceConfidence.ProvenCapture,
				Enabled = true,
				Conditions = new string[0]
			});
		}
	}

	private void EnsureAlexAreaWasteCollector()
	{
		if (!registry.ContainsTable("captured.arete.waste-collector"))
		{
			registry.RegisterTable(new LootTableDefinition
			{
				LootTableKey = "captured.arete.waste-collector",
				DisplayName = "Waste Collector captured credits",
				TableType = LootTableType.EnemyType,
				RollGroups = new LootGroupDefinition[0],
				CreditsPolicy = CreditsRange(4, 5, LootEvidenceConfidence.Inferred),
				QualityPolicy = "captured-fixed",
				Evidence = "20260720-080123",
				Confidence = LootEvidenceConfidence.Inferred,
				Enabled = true
			});
			registry.RegisterAssignment(new LootAssignmentDefinition
			{
				AssignmentKey = "captured.arete.waste-collector",
				TargetType = LootAssignmentTargetType.EnemyType,
				TargetKey = "captured.arete.waste-collector",
				LootTableKey = "captured.arete.waste-collector",
				Priority = 0,
				Evidence = "20260720-080123",
				Confidence = LootEvidenceConfidence.Inferred,
				Enabled = true,
				Conditions = new string[0]
			});
		}
	}

	private void EnsureAlexAreaGarbageFlea()
	{
		if (!registry.ContainsTable("captured.arete.garbage-flea"))
		{
			registry.RegisterTable(new LootTableDefinition
			{
				LootTableKey = "captured.arete.garbage-flea",
				DisplayName = "Garbage Flea captured outcomes",
				TableType = LootTableType.EnemyType,
				RollGroups = new LootGroupDefinition[1]
				{
					new LootGroupDefinition
					{
						LootGroupKey = "captured-outcome",
						RollMode = LootRollMode.WeightedOne,
						RollCount = 1,
						EmptyWeight = 1,
						DropChanceBasisPoints = 10000,
						Entries = new LootEntryDefinition[1] { FixedEntry(248322, 1, "flea.248322", 1) },
						Conditions = new string[0]
					}
				},
				CreditsPolicy = CreditsRange(5, 11, LootEvidenceConfidence.ProvenCapture),
				QualityPolicy = "captured-fixed",
				Evidence = "20260720-080123",
				Confidence = LootEvidenceConfidence.ProvenCapture,
				Enabled = true
			});
			registry.RegisterAssignment(new LootAssignmentDefinition
			{
				AssignmentKey = "captured.arete.garbage-flea",
				TargetType = LootAssignmentTargetType.EnemyType,
				TargetKey = "captured.arete.garbage-flea",
				LootTableKey = "captured.arete.garbage-flea",
				Priority = 0,
				Evidence = "20260720-080123",
				Confidence = LootEvidenceConfidence.ProvenCapture,
				Enabled = true,
				Conditions = new string[0]
			});
		}
	}

	private void EnsureDatabaseLoaded()
	{
		if (!databaseLoaded)
		{
			debugEntries = CombatTestLootCatalog.BuildEntries();
			try
			{
				databaseEntries = CombatMobLootCatalog.BuildEntries(((Dao<DBMobTemplate, MobTemplateDao>)(object)Dao<DBMobTemplate, MobTemplateDao>.Instance).GetAll((object)null).ToArray(), ((Dao<DBMobDroptable, MobDroptableDao>)(object)Dao<DBMobDroptable, MobDroptableDao>.Instance).GetAll((object)null).ToArray());
			}
			catch (Exception ex)
			{
				databaseEntries = new CombatLootTableEntry[0];
				LogUtil.Debug((DebugInfoDetail)512, "Global loot DB adapter load failed: " + ex.Message);
			}
			databaseLoaded = true;
		}
	}

	private void EnsureLegacyTarget(ICharacter target, string profileKey)
	{
		if (registry.ContainsTable(profileKey))
		{
			return;
		}
		CombatLootTableEntry[] array = debugEntries.Where((CombatLootTableEntry x) => x.Matches(((INamedEntity)target).Name, ((IStats)target).Stats[(StatIds)359].Value, ((IStats)target).Stats[(StatIds)455].Value)).ToArray();
		CombatLootTableEntry[] array2 = databaseEntries.Where((CombatLootTableEntry x) => x.Matches(((INamedEntity)target).Name, ((IStats)target).Stats[(StatIds)359].Value, ((IStats)target).Stats[(StatIds)455].Value)).ToArray();
		CombatLootTableEntry[] array3 = ((array.Length != 0) ? array : array2);
		int minimumCredits;
		int maximumCredits;
		bool flag = CombatCorpseRules.TryGetObservedCreditRange(((INamedEntity)target).Name, ((IStats)target).Stats[(StatIds)359].Value, out minimumCredits, out maximumCredits);
		if (array3.Length != 0 || flag)
		{
			List<LootGroupDefinition> list = new List<LootGroupDefinition>();
			for (int i = 0; i < array3.Length; i++)
			{
				CombatLootTableEntry combatLootTableEntry = array3[i];
				LootEntryDefinition[] entries = LegacyEntries(combatLootTableEntry);
				list.Add(new LootGroupDefinition
				{
					LootGroupKey = "db.slot." + combatLootTableEntry.Slot + "." + i,
					RollMode = LootRollMode.WeightedOne,
					RollCount = 1,
					DropChanceBasisPoints = combatLootTableEntry.EffectiveDropChanceBasisPoints,
					Entries = entries,
					Conditions = new string[0]
				});
			}
			registry.RegisterTable(new LootTableDefinition
			{
				LootTableKey = profileKey,
				DisplayName = ((INamedEntity)target).Name + " legacy DB loot",
				TableType = LootTableType.EnemyType,
				RollGroups = list.ToArray(),
				CreditsPolicy = (flag ? CreditsRange(minimumCredits, maximumCredits, LootEvidenceConfidence.ProvenRepository) : new CreditsPolicyDefinition
				{
					Mode = CreditsPolicyMode.Unresolved,
					Evidence = LootEvidenceConfidence.Unresolved
				}),
				QualityPolicy = "legacy-range-check",
				Evidence = ((array.Length != 0) ? "combat-test-catalog" : "mobtemplate/mobdroptable"),
				Confidence = LootEvidenceConfidence.ProvenRepository,
				Enabled = true
			});
			registry.RegisterAssignment(new LootAssignmentDefinition
			{
				AssignmentKey = profileKey,
				TargetType = LootAssignmentTargetType.EnemyType,
				TargetKey = profileKey,
				LootTableKey = profileKey,
				Priority = 0,
				Evidence = ((array.Length != 0) ? "combat-test-catalog" : "mobtemplate/mobdroptable"),
				Confidence = LootEvidenceConfidence.ProvenRepository,
				Enabled = true,
				Conditions = new string[0]
			});
		}
	}

	private static LootEntryDefinition[] LegacyEntries(CombatLootTableEntry match)
	{
		if (match.ItemTemplates != null && match.ItemTemplates.Length != 0)
		{
			return match.ItemTemplates.Select((CombatLootItemTemplate x) => new LootEntryDefinition
			{
				ItemTemplateId = x.LowId,
				HighItemTemplateId = x.HighId,
				MinimumQuality = Math.Max(1, x.MinQuality),
				MaximumQuality = Math.Max(Math.Max(1, x.MinQuality), x.MaxQuality),
				MinimumQuantity = 1,
				MaximumQuantity = 1,
				Weight = 1,
				DropChanceBasisPoints = 10000,
				Semantics = LootSemantics.WeightedDocumented,
				Evidence = LootEvidenceConfidence.ProvenRepository,
				EvidenceReference = x.DropGroupHash
			}).ToArray();
		}
		return (match.ItemTemplateIds ?? new int[0]).Select((int x) => FixedEntry(x, Math.Max(1, match.Quality), null, 1)).ToArray();
	}

	private static LootEntryDefinition FixedEntry(int itemId, int quality, string selectionKey, int weight)
	{
		return new LootEntryDefinition
		{
			SelectionKey = selectionKey,
			ItemTemplateId = itemId,
			HighItemTemplateId = itemId,
			FixedQuality = quality,
			MinimumQuality = quality,
			MaximumQuality = quality,
			MinimumQuantity = 1,
			MaximumQuantity = 1,
			Weight = weight,
			DropChanceBasisPoints = 10000,
			Semantics = LootSemantics.WeightedDocumented,
			Evidence = LootEvidenceConfidence.ProvenCapture,
			EvidenceReference = "live-capture-20260629-142800"
		};
	}

	private static CreditsPolicyDefinition CreditsRange(int minimum, int maximum, LootEvidenceConfidence evidence)
	{
		return new CreditsPolicyDefinition
		{
			Mode = ((minimum == maximum) ? CreditsPolicyMode.Fixed : CreditsPolicyMode.Range),
			MinimumCredits = minimum,
			MaximumCredits = maximum,
			Evidence = evidence
		};
	}

	private static CreditsPolicyDefinition CreditsObservedSet(params int[] outcomes)
	{
		int[] array = (from value in (outcomes ?? new int[0]).Distinct()
			orderby value
			select value).ToArray();
		if (array.Length == 0)
		{
			return new CreditsPolicyDefinition
			{
				Mode = CreditsPolicyMode.Unresolved,
				Evidence = LootEvidenceConfidence.Unresolved
			};
		}
		return new CreditsPolicyDefinition
		{
			Mode = CreditsPolicyMode.ObservedSet,
			MinimumCredits = array[0],
			MaximumCredits = array[array.Length - 1],
			ObservedCredits = array,
			Evidence = LootEvidenceConfidence.ObservedAvailableLoot
		};
	}

	private static string LegacyProfileKey(ICharacter target)
	{
		return string.Format(CultureInfo.InvariantCulture, "legacy.{0}.{1}.{2}", ((IStats)target).Stats[(StatIds)359].Value, ((IStats)target).Stats[(StatIds)455].Value, (((INamedEntity)target).Name ?? "unnamed").Replace(' ', '-').ToLowerInvariant());
	}

	private static bool DiagnosticsEnabled()
	{
		return string.Equals(Environment.GetEnvironmentVariable("AO_REBIRTH_LOOT_DIAGNOSTICS"), "1", StringComparison.Ordinal);
	}
}
