using System;

namespace AORebirth.Core.Playfields;

internal static class CapturedSubwayCombatCatalog
{
	private const int DerangedShopperMonsterData = 203736;

	private const int DerangedShopperSourceInstance = 2035762471;

	private const int IncompleteRebuildMonsterData = 203728;

	private const int FragmentedSoulMonsterData = 203729;

	private const int LooterMonsterData = 203745;

	private const int MuggerMonsterData = 203734;

	private const int RedundantScanMonsterData = 204178;

	private const int WorkmanStrikerMonsterData = 203854;

	private static readonly int[] MuggerSourceInstances = new int[9] { 2035526161, 2035527019, 2035568852, 2035569150, 2035646228, 2035803590, 2035803591, 2035803592, 2035803594 };

	private static readonly int[] IncompleteRebuildSourceInstances = new int[10] { 2035569008, 2035569010, 2035569015, 2035569025, 2035569032, 2035569084, 2035569089, 2035569099, 2035569149, 2035569217 };

	private static readonly int[] RedundantScanSourceInstances = new int[4] { 2035527557, 2035569087, 2035569092, 2035569107 };

	private static readonly int[] FragmentedSoulSourceInstances = new int[10] { 2035569002, 2035569007, 2035569018, 2035569034, 2035569035, 2035569038, 2035569066, 2035569070, 2035569224, 2035569511 };

	internal static CapturedEnemyCombatContract For(string name, int monsterData)
	{
		return For(name, monsterData, null);
	}

	internal static CapturedEnemyCombatContract For(string name, int monsterData, int? level)
	{
		switch (monsterData)
		{
		case 203726:
			return CapturedEnemyCombatContract.EquippedWeaponWithEmptySpecialAttackContext("20260709-222339 plus 20260717-214612/214751/215250: Eumenides owner-linked 123267/123268 weapons are observed at QL20 and QL17; runtime retains QL20 because the respawn selection rule is unresolved; initial empty-special context is 143/143/143/143/0, with two captured misses; immediate attack start, 0.233124-second movement transition, 5.199992-second first hit, 21 observed normal local-player hits 25..45, and 4.311321-second median interval across 17 intervals; weapon owns runtime damage and recharge", 123267, 123268, 20, 6, 0, 0, 0.001, 0.233124, 5.199992, 0.0, sendStopFightOnDeath: false, 19, 0, 143, 143, 143, 143, 0, requiresDamageLineOfSight: true);
		case 203748:
			return CapturedEnemyCombatContract.EquippedWeaponWithEmptySpecialAttackContext("20260712-232711/234401: Vergil Aeneid QL23 Cast-Off E-Beamer 122123; 23-25 player damage, 23-34 all-target damage, captured attack-start/first-hit timing, and weapon-owned roll/cadence", 122123, 122123, 23, 6, 0, 0, 0.646433, 0.001, 2.78741, 0.0, sendStopFightOnDeath: true, 19, 0, 167, 167, 167, 167, 0, requiresDamageLineOfSight: true);
		case 155962:
		{
			CapturedEnemyCombatAttackDefinition attack = new CapturedEnemyCombatAttackDefinition(74, 96, 0, 8.0, 6.3, usesEquippedWeapon: false, -1, 1, 0, 3, 1481592922, sendAttackInfo: true);
			CapturedEnemyCombatAttackDefinition attack2 = new CapturedEnemyCombatAttackDefinition(115, 126, 0, 8.0, 6.3, usesEquippedWeapon: false, -1, 0, 0, 3, 1145392727, sendAttackInfo: true);
			return CapturedEnemyCombatContract.CapturedParallelAttackSequence("20260712-224840/232137: Abmouth XOPZ paired stream, DENW stream, and captured SIW context", new CapturedEnemyParallelAttackSequenceDefinition(new CapturedEnemyParallelAttackStreamDefinition[3]
			{
				new CapturedEnemyParallelAttackStreamDefinition(0.0, attack),
				new CapturedEnemyParallelAttackStreamDefinition(1.476528, attack2),
				new CapturedEnemyParallelAttackStreamDefinition(3.425454, attack)
			}, new CapturedEnemySpecialAttackDefinition[2]
			{
				new CapturedEnemySpecialAttackDefinition(203781, 203782, 1481592922, "XOPZ"),
				new CapturedEnemySpecialAttackDefinition(203778, 203779, 1145392727, "DENW")
			}, 167, 167, 167, 167, 0));
		}
		case 31909:
			return CapturedEnemyCombatContract.CapturedSpecialSequence("20260712-224840/232137: Abmouth-owned Infector DMXF attacks, 21-26 player damage, and 3.7-second cadence", new CapturedEnemySpecialAttackSequenceDefinition(2.2, null, new CapturedEnemyCombatAttackDefinition(21, 26, 0, 8.0, 3.7, usesEquippedWeapon: false, -1, 0, 0, 3, 1145919558, sendAttackInfo: true), new CapturedEnemySpecialAttackDefinition[1]
			{
				new CapturedEnemySpecialAttackDefinition(201062, 201063, 1145919558, "DMXF")
			}, 107, 107, 107, 107, 100));
		case 17657:
			return CapturedEnemyCombatContract.CapturedSpecialSequence("20260708-004038 and 20260709-193914: Filth Flea normal slot rolls with criticals excluded", new CapturedEnemySpecialAttackSequenceDefinition(3.65, new CapturedEnemyCombatAttackDefinition(14, 24, 0, 8.0, 1.58, usesEquippedWeapon: false, -1, 1, 0, 3, 1162887496, sendAttackInfo: true), new CapturedEnemyCombatAttackDefinition(3, 10, 0, 8.0, 2.8, usesEquippedWeapon: false, -1, 0, 0, 3, 1096439123, sendAttackInfo: true), new CapturedEnemySpecialAttackDefinition[2]
			{
				new CapturedEnemySpecialAttackDefinition(201059, 201060, 1162887496, "EPAH"),
				new CapturedEnemySpecialAttackDefinition(201056, 201057, 1096439123, "AZUS")
			}, 33, 33, 33, 33, 0));
		case 17720:
			return CapturedEnemyCombatContract.FixedAttack("20260708-143600 and 20260709-210452: 37 normal local-player Discarded Pet SIW1 hits span 9..18; four 30..33 criticals remain report-only; 30 same-source landed-hit intervals span 4.609299..5.950416 seconds with conventional median 5.089763; AttackInfo uses ammo -1, slot 0, unknown 0, and instance SIW1; raw SpecialAttackWeapon first four fields are exact by level while the varying fifth field remains unresolved and is not synthesized", 9, 18, 5.089763, 0, 0, 1397315377, -1);
		case 17649:
			return ForDisobedientBot(level);
		case 30379:
			return CapturedEnemyCombatContract.CapturedParallelAttackSequence("20260709-222339 and 20260716-033326/034104: Bloodcreeper proactive dual Skinspider Bite/Spit natural attacks, 21-41 rolled damage, and independent captured hand cadence", new CapturedEnemyParallelAttackSequenceDefinition(new CapturedEnemyParallelAttackStreamDefinition[2]
			{
				new CapturedEnemyParallelAttackStreamDefinition(3.057708, new CapturedEnemyCombatAttackDefinition(21, 41, 0, 8.0, 7.389908, usesEquippedWeapon: false, -1, 1, 0, 3, 1397446450, sendAttackInfo: true)),
				new CapturedEnemyParallelAttackStreamDefinition(6.088742, new CapturedEnemyCombatAttackDefinition(21, 35, 0, 8.0, 7.50984, usesEquippedWeapon: false, -1, 0, 0, 3, 1397446449, sendAttackInfo: true))
			}, new CapturedEnemySpecialAttackDefinition[2]
			{
				new CapturedEnemySpecialAttackDefinition(121094, 121095, 1397446450, "SKW2"),
				new CapturedEnemySpecialAttackDefinition(121091, 121092, 1397446449, "SKW1")
			}, 131, 131, 131, 131, 37));
		case 203734:
			return CapturedEnemyCombatContract.Unresolved("Mugger combat requires an exact captured source identity; aggregate weapon fallback is forbidden", retaliationObserved: true);
		case 26092:
			return CapturedEnemyCombatContract.EquippedWeaponWithEmptySpecialAttackContext("20260711-170337 packets 301-654: Thief attack start, movement transition, three landed projectile hits, and six-second repeat cadence; 2026-07-12 private validation proved the weapon context renders projectile damage", 121567, 121567, 1, 6, 0, 0, 1.409765, 0.219999, 11.409643, 6.0, sendStopFightOnDeath: true, -1, 0, 32, 32, 32, 32, 0);
		case 203733:
			return CapturedEnemyCombatContract.CapturedSpecialSequence("Official-live captures 20260719-010047 and 20260719-020104 prove repeated Violent Vagabond attack attempts, all misses, a 4.5802404-second corpus cadence, AttackInfo 0/6/0/0, and SpecialAttackWeapon 32/35/29/31/0. Landed damage is unavailable because the Vagabonds could not hit the test character, so the private-project playability policy uses the adjacent same-level Subway Mugger normal range of 9..12. QL1 template 130590 is Red Wine and remains excluded from combat.", new CapturedEnemySpecialAttackSequenceDefinition(4.5802404, null, new CapturedEnemyCombatAttackDefinition(9, 12, 0, 8.0, 4.5802404, usesEquippedWeapon: false, 0, 6, 0, 3, 0, sendAttackInfo: true), new CapturedEnemySpecialAttackDefinition[0], 32, 35, 29, 31, 0));
		default:
			return CapturedEnemyCombatContract.Unresolved("No captured combat contract for " + name + " monsterData=" + monsterData, retaliationObserved: false);
		}
	}

	private static CapturedEnemyCombatContract ForDisobedientBot(int? level)
	{
		int specialAttackWeaponUnknown = 0;
		int num;
		switch (level)
		{
		case 5:
			num = 30;
			specialAttackWeaponUnknown = 22;
			break;
		case 6:
			num = 35;
			break;
		case 7:
			num = 40;
			break;
		case 8:
			num = 45;
			break;
		case 9:
			num = 49;
			break;
		case 10:
			num = 54;
			break;
		default:
			return CapturedEnemyCombatContract.Unresolved("Disobedient Bot SIW1 attack context is unresolved for level " + (level.HasValue ? level.Value.ToString() : "unknown"), retaliationObserved: true);
		}
		return CapturedEnemyCombatContract.CapturedSpecialSequence("20260708-143600, 20260709-205921/210452/220439, 20260712-153918, 20260713-014714/033511, and 20260719-020104: 15 Disobedient Bot SIW1 normal local-player hits span 6-15 damage; three other-player hits and two player-owned Killer-pet hits remain separate; focused raw packets prove a 3.270444-second first hit and 5.973723-second repeat attempt cadence; SpecialAttackWeapon contexts are capture-backed for levels 5, 6, 8, 9, and 10, including the level-5 terminal value 22, with level 7 explicitly using the bounded 35/45 midpoint policy", new CapturedEnemySpecialAttackSequenceDefinition(3.270444, null, new CapturedEnemyCombatAttackDefinition(6, 15, 0, 8.0, 5.973723, usesEquippedWeapon: false, -1, 0, 0, 3, 1397315377, sendAttackInfo: true), new CapturedEnemySpecialAttackDefinition[1]
		{
			new CapturedEnemySpecialAttackDefinition(144742, 144743, 1397315377, "SIW1")
		}, num, num, num, num, specialAttackWeaponUnknown));
	}

	private static CapturedEnemyCombatContract ForWorkmanStriker(CapturedSubwayOrdinaryArchetypeDefinition archetype, int sourceInstance)
	{
		return ForSourceSpecificWeaponArchetype(archetype, sourceInstance, "Workman Striker");
	}

	private static CapturedEnemyCombatContract ForLooter(CapturedSubwayOrdinaryArchetypeDefinition archetype, int sourceInstance)
	{
		return ForSourceSpecificWeaponArchetype(archetype, sourceInstance, "Looter");
	}

	private static CapturedEnemyCombatContract ForIncompleteRebuild(CapturedSubwayOrdinaryArchetypeDefinition archetype, int sourceInstance)
	{
		CapturedSubwaySourceWeaponEvidenceDefinition[] array = ((archetype == null) ? new CapturedSubwaySourceWeaponEvidenceDefinition[0] : archetype.SourceWeaponEvidence);
		CapturedSubwayCombatEvidenceDefinition capturedSubwayCombatEvidenceDefinition = archetype?.Combat;
		if (capturedSubwayCombatEvidenceDefinition == null || !capturedSubwayCombatEvidenceDefinition.Observed || capturedSubwayCombatEvidenceDefinition.ObservedRows != 2 || capturedSubwayCombatEvidenceDefinition.MinDamage != 17 || capturedSubwayCombatEvidenceDefinition.MaxDamage != 35 || capturedSubwayCombatEvidenceDefinition.WeaponSlot != 6 || capturedSubwayCombatEvidenceDefinition.AttackInfoUnknown != 0 || capturedSubwayCombatEvidenceDefinition.WeaponInstance != 0 || !HasCompleteIncompleteRebuildSourceWeaponEvidence(array))
		{
			return CapturedEnemyCombatContract.Unresolved("Incomplete Rebuild combat requires the exact two normal 17..35 local-player hits and one owner-linked weapon tuple for each of the ten current sources", capturedSubwayCombatEvidenceDefinition?.Observed ?? false);
		}
		CapturedSubwaySourceWeaponEvidenceDefinition capturedSubwaySourceWeaponEvidenceDefinition = null;
		int num = 0;
		CapturedSubwaySourceWeaponEvidenceDefinition[] array2 = array;
		foreach (CapturedSubwaySourceWeaponEvidenceDefinition capturedSubwaySourceWeaponEvidenceDefinition2 in array2)
		{
			if (capturedSubwaySourceWeaponEvidenceDefinition2.SourceInstance == sourceInstance)
			{
				capturedSubwaySourceWeaponEvidenceDefinition = capturedSubwaySourceWeaponEvidenceDefinition2;
				num++;
			}
		}
		if (num != 1 || capturedSubwaySourceWeaponEvidenceDefinition == null)
		{
			return CapturedEnemyCombatContract.Unresolved($"Incomplete Rebuild source 0x{sourceInstance:X8} requires exactly one owner-linked captured weapon tuple; found {num}", retaliationObserved: true);
		}
		return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo($"{capturedSubwaySourceWeaponEvidenceDefinition.EvidenceCaptures}: Incomplete Rebuild source 0x{sourceInstance:X8} owner-linked QL{capturedSubwaySourceWeaponEvidenceDefinition.Quality} weapon {capturedSubwaySourceWeaponEvidenceDefinition.LowId}/{capturedSubwaySourceWeaponEvidenceDefinition.HighId}; two normal local-player hits span 17..35 and one captured miss shares ammo 9, slot 6, unknown 0, and weapon instance 0; item owns runtime damage and recharge; no empty SIW or captured attack-start/stop context", capturedSubwaySourceWeaponEvidenceDefinition.LowId, capturedSubwaySourceWeaponEvidenceDefinition.HighId, capturedSubwaySourceWeaponEvidenceDefinition.Quality, 6, 9, 6, 0, 0);
	}

	private static CapturedEnemyCombatContract ForIncompleteRebuild(CapturedSubwayOrdinaryArchetypeDefinition archetype, int sourceInstance, OrdinaryEnemySpawnVariant variant, CapturedSubwayGenerationVariantDefinition[] generationEvidence)
	{
		CapturedSubwayCombatEvidenceDefinition capturedSubwayCombatEvidenceDefinition = archetype?.Combat;
		bool flag = capturedSubwayCombatEvidenceDefinition != null && capturedSubwayCombatEvidenceDefinition.Observed && capturedSubwayCombatEvidenceDefinition.ObservedRows == 2 && capturedSubwayCombatEvidenceDefinition.MinDamage == 17 && capturedSubwayCombatEvidenceDefinition.MaxDamage == 35 && capturedSubwayCombatEvidenceDefinition.WeaponSlot == 6 && capturedSubwayCombatEvidenceDefinition.AttackInfoUnknown == 0 && capturedSubwayCombatEvidenceDefinition.WeaponInstance == 0;
		OrdinaryEnemySpawnWeaponLoadout ordinaryEnemySpawnWeaponLoadout = variant?.WeaponLoadout;
		string failure = string.Empty;
		if (!flag || archetype == null || !HasCompleteIncompleteRebuildSourceWeaponEvidence(archetype.SourceWeaponEvidence) || Array.IndexOf(IncompleteRebuildSourceInstances, sourceInstance) < 0 || !OrdinaryEnemyAtomicGenerationEvidenceValidator.TryValidateSelectedVariant(203728, sourceInstance, variant, generationEvidence, out failure))
		{
			return CapturedEnemyCombatContract.Unresolved("Incomplete Rebuild combat requires one exact reviewed atomic level/stat/weapon generation for the selected source", flag);
		}
		return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo($"{ordinaryEnemySpawnWeaponLoadout.Evidence}: Incomplete Rebuild source 0x{sourceInstance:X8} selected captured L{variant.Level} QL{ordinaryEnemySpawnWeaponLoadout.Quality} weapon {ordinaryEnemySpawnWeaponLoadout.LowId}/{ordinaryEnemySpawnWeaponLoadout.HighId} as one atomic generation; two normal local-player hits span 17..35 and one captured miss shares ammo 9, slot 6, unknown 0, and weapon instance 0; item owns runtime damage and recharge; uniform selection over distinct captured generations is private policy", ordinaryEnemySpawnWeaponLoadout.LowId, ordinaryEnemySpawnWeaponLoadout.HighId, ordinaryEnemySpawnWeaponLoadout.Quality, 6, 9, 6, 0, 0);
	}

	private static CapturedEnemyCombatContract ForDerangedShopper(CapturedSubwayOrdinaryArchetypeDefinition archetype, int sourceInstance)
	{
		CapturedSubwaySourceWeaponEvidenceDefinition[] array = ((archetype == null) ? new CapturedSubwaySourceWeaponEvidenceDefinition[0] : archetype.SourceWeaponEvidence);
		if (sourceInstance != 2035762471 || array == null || array.Length != 1 || array[0].SourceInstance != 2035762471 || array[0].LowId != 125454 || array[0].HighId != 125455 || array[0].Quality != 8)
		{
			return CapturedEnemyCombatContract.Unresolved($"Deranged Shopper source 0x{sourceInstance:X8} requires the one exact owner-linked QL8 125454/125455 tuple", archetype != null && archetype.Combat != null && archetype.Combat.Observed);
		}
		return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo(array[0].EvidenceCaptures + ": Deranged Shopper source 0x79574527 owner-linked QL8 weapon 125454/125455; eight normal local-player hits span 9..15, one 27-point critical is report-only, and one captured miss preserves ammo -1, slot 6, and unknown 0; item owns runtime damage, damage bonus, and recharge; captured AttackInfo carries only ammo -1, slot 6, unknown 0, and weapon instance 0; no empty SIW or captured attack-start/stop context", array[0].LowId, array[0].HighId, array[0].Quality, 6, -1, 6, 0, 0);
	}

	private static CapturedEnemyCombatContract ForRedundantScan(CapturedSubwayOrdinaryArchetypeDefinition archetype, int sourceInstance)
	{
		CapturedSubwaySourceWeaponEvidenceDefinition[] array = ((archetype == null) ? new CapturedSubwaySourceWeaponEvidenceDefinition[0] : archetype.SourceWeaponEvidence);
		bool retaliationObserved = archetype != null && archetype.Combat != null && archetype.Combat.Observed;
		if (!HasCompleteRedundantScanSourceWeaponEvidence(array))
		{
			return CapturedEnemyCombatContract.Unresolved("Redundant Scan combat requires one exact owner-linked weapon tuple for each of the four current sources", retaliationObserved);
		}
		CapturedSubwaySourceWeaponEvidenceDefinition capturedSubwaySourceWeaponEvidenceDefinition = null;
		int num = 0;
		CapturedSubwaySourceWeaponEvidenceDefinition[] array2 = array;
		foreach (CapturedSubwaySourceWeaponEvidenceDefinition capturedSubwaySourceWeaponEvidenceDefinition2 in array2)
		{
			if (capturedSubwaySourceWeaponEvidenceDefinition2.SourceInstance == sourceInstance)
			{
				capturedSubwaySourceWeaponEvidenceDefinition = capturedSubwaySourceWeaponEvidenceDefinition2;
				num++;
			}
		}
		if (num != 1 || capturedSubwaySourceWeaponEvidenceDefinition == null)
		{
			return CapturedEnemyCombatContract.Unresolved($"Redundant Scan source 0x{sourceInstance:X8} requires exactly one owner-linked captured weapon tuple; found {num}", retaliationObserved);
		}
		return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo($"{capturedSubwaySourceWeaponEvidenceDefinition.EvidenceCaptures}: Redundant Scan source 0x{sourceInstance:X8} owner-linked QL{capturedSubwaySourceWeaponEvidenceDefinition.Quality} weapon {capturedSubwaySourceWeaponEvidenceDefinition.LowId}/{capturedSubwaySourceWeaponEvidenceDefinition.HighId}; one normal local-player hit is 19; item owns runtime damage and recharge; captured AttackInfo carries only ammo 17, slot 6, unknown 0, and weapon instance 0; no fixed damage, empty SIW, or captured attack-start/stop context", capturedSubwaySourceWeaponEvidenceDefinition.LowId, capturedSubwaySourceWeaponEvidenceDefinition.HighId, capturedSubwaySourceWeaponEvidenceDefinition.Quality, 6, 17, 6, 0, 0);
	}

	private static CapturedEnemyCombatContract ForFragmentedSoul(CapturedSubwayOrdinaryArchetypeDefinition archetype, int sourceInstance, OrdinaryEnemySpawnVariant variant, CapturedSubwayGenerationVariantDefinition[] generationEvidence)
	{
		CapturedSubwayCombatEvidenceDefinition capturedSubwayCombatEvidenceDefinition = archetype?.Combat;
		bool flag = capturedSubwayCombatEvidenceDefinition != null && capturedSubwayCombatEvidenceDefinition.Observed && capturedSubwayCombatEvidenceDefinition.ObservedRows == 2 && capturedSubwayCombatEvidenceDefinition.MinDamage == 18 && capturedSubwayCombatEvidenceDefinition.MaxDamage == 23 && capturedSubwayCombatEvidenceDefinition.WeaponSlot == 6 && capturedSubwayCombatEvidenceDefinition.AttackInfoUnknown == 0 && capturedSubwayCombatEvidenceDefinition.WeaponInstance == 0;
		OrdinaryEnemySpawnWeaponLoadout ordinaryEnemySpawnWeaponLoadout = variant?.WeaponLoadout;
		string failure = string.Empty;
		if (!flag || archetype == null || Array.IndexOf(FragmentedSoulSourceInstances, sourceInstance) < 0 || !OrdinaryEnemyAtomicGenerationEvidenceValidator.TryValidateSelectedVariant(203729, sourceInstance, variant, generationEvidence, out failure))
		{
			return CapturedEnemyCombatContract.Unresolved("Fragmented Soul combat requires one exact reviewed atomic level/stat/weapon generation for the selected source", flag);
		}
		return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo($"{ordinaryEnemySpawnWeaponLoadout.Evidence}: Fragmented Soul source 0x{sourceInstance:X8} selected captured L{variant.Level} QL{ordinaryEnemySpawnWeaponLoadout.Quality} weapon {ordinaryEnemySpawnWeaponLoadout.LowId}/{ordinaryEnemySpawnWeaponLoadout.HighId} as one atomic generation; two normal local-player hits span 18..23 with ammo 24, slot 6, unknown 0, and weapon instance 0; item owns runtime damage and recharge; uniform selection over distinct captured generations is private policy", ordinaryEnemySpawnWeaponLoadout.LowId, ordinaryEnemySpawnWeaponLoadout.HighId, ordinaryEnemySpawnWeaponLoadout.Quality, 6, 24, 6, 0, 0);
	}

	private static CapturedEnemyCombatContract ForRedundantScan(CapturedSubwayOrdinaryArchetypeDefinition archetype, int sourceInstance, OrdinaryEnemySpawnVariant variant, CapturedSubwayGenerationVariantDefinition[] generationEvidence)
	{
		CapturedSubwayCombatEvidenceDefinition capturedSubwayCombatEvidenceDefinition = archetype?.Combat;
		bool flag = capturedSubwayCombatEvidenceDefinition != null && capturedSubwayCombatEvidenceDefinition.Observed && capturedSubwayCombatEvidenceDefinition.ObservedRows == 1 && capturedSubwayCombatEvidenceDefinition.MinDamage == 19 && capturedSubwayCombatEvidenceDefinition.MaxDamage == 19 && capturedSubwayCombatEvidenceDefinition.WeaponSlot == 6 && capturedSubwayCombatEvidenceDefinition.AttackInfoUnknown == 0 && capturedSubwayCombatEvidenceDefinition.WeaponInstance == 0;
		OrdinaryEnemySpawnWeaponLoadout ordinaryEnemySpawnWeaponLoadout = variant?.WeaponLoadout;
		string failure = string.Empty;
		if (!flag || archetype == null || !HasCompleteRedundantScanSourceWeaponEvidence(archetype.SourceWeaponEvidence) || Array.IndexOf(RedundantScanSourceInstances, sourceInstance) < 0 || !OrdinaryEnemyAtomicGenerationEvidenceValidator.TryValidateSelectedVariant(204178, sourceInstance, variant, generationEvidence, out failure))
		{
			return CapturedEnemyCombatContract.Unresolved("Redundant Scan combat requires one exact reviewed atomic level/stat/weapon generation for the selected source", flag);
		}
		return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo($"{ordinaryEnemySpawnWeaponLoadout.Evidence}: Redundant Scan source 0x{sourceInstance:X8} selected captured L{variant.Level} QL{ordinaryEnemySpawnWeaponLoadout.Quality} weapon {ordinaryEnemySpawnWeaponLoadout.LowId}/{ordinaryEnemySpawnWeaponLoadout.HighId} as one atomic generation; one normal local-player hit is 19; item owns runtime damage and recharge; captured AttackInfo carries only ammo 17, slot 6, unknown 0, and weapon instance 0; uniform selection over distinct captured generations is private policy", ordinaryEnemySpawnWeaponLoadout.LowId, ordinaryEnemySpawnWeaponLoadout.HighId, ordinaryEnemySpawnWeaponLoadout.Quality, 6, 17, 6, 0, 0);
	}

	private static CapturedEnemyCombatContract ForSourceSpecificWeaponArchetype(CapturedSubwayOrdinaryArchetypeDefinition archetype, int sourceInstance, string displayName)
	{
		CapturedSubwaySourceWeaponEvidenceDefinition capturedSubwaySourceWeaponEvidenceDefinition = null;
		int num = 0;
		CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence = archetype.SourceWeaponEvidence;
		foreach (CapturedSubwaySourceWeaponEvidenceDefinition capturedSubwaySourceWeaponEvidenceDefinition2 in sourceWeaponEvidence)
		{
			if (capturedSubwaySourceWeaponEvidenceDefinition2.SourceInstance == sourceInstance)
			{
				capturedSubwaySourceWeaponEvidenceDefinition = capturedSubwaySourceWeaponEvidenceDefinition2;
				num++;
			}
		}
		if (num != 1 || capturedSubwaySourceWeaponEvidenceDefinition == null)
		{
			return CapturedEnemyCombatContract.Unresolved($"{displayName} source 0x{sourceInstance:X8} requires exactly one owner-linked captured weapon tuple; found {num}", archetype.Combat != null && archetype.Combat.Observed);
		}
		return CapturedEnemyCombatContract.EquippedWeapon($"{capturedSubwaySourceWeaponEvidenceDefinition.EvidenceCaptures}: {displayName} source 0x{sourceInstance:X8} owner-linked QL{capturedSubwaySourceWeaponEvidenceDefinition.Quality} weapon {capturedSubwaySourceWeaponEvidenceDefinition.LowId}/{capturedSubwaySourceWeaponEvidenceDefinition.HighId}; item owns normal damage and recharge; no fixed damage, special-attack, or captured AttackInfo context", capturedSubwaySourceWeaponEvidenceDefinition.LowId, capturedSubwaySourceWeaponEvidenceDefinition.HighId, capturedSubwaySourceWeaponEvidenceDefinition.Quality, 6);
	}

	internal static CapturedEnemyCombatContract ForSupportedSourceWeapon(string name, int monsterData, CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence, int sourceInstance)
	{
		if (!string.Equals(name, "Mugger", StringComparison.Ordinal) || monsterData != 203734)
		{
			return CapturedEnemyCombatContract.Unresolved($"Unsupported source-specific weapon profile {name} monsterData={monsterData}", retaliationObserved: false);
		}
		if (!HasCompleteMuggerSourceWeaponEvidence(sourceWeaponEvidence))
		{
			return CapturedEnemyCombatContract.Unresolved("Mugger combat requires one exact QL1 121567/121567 owner-linked weapon tuple for each of the nine current sources", retaliationObserved: true);
		}
		CapturedSubwaySourceWeaponEvidenceDefinition capturedSubwaySourceWeaponEvidenceDefinition = null;
		int num = 0;
		foreach (CapturedSubwaySourceWeaponEvidenceDefinition capturedSubwaySourceWeaponEvidenceDefinition2 in sourceWeaponEvidence)
		{
			if (capturedSubwaySourceWeaponEvidenceDefinition2.SourceInstance == sourceInstance)
			{
				capturedSubwaySourceWeaponEvidenceDefinition = capturedSubwaySourceWeaponEvidenceDefinition2;
				num++;
			}
		}
		if (num != 1 || capturedSubwaySourceWeaponEvidenceDefinition == null)
		{
			return CapturedEnemyCombatContract.Unresolved($"Mugger source 0x{sourceInstance:X8} requires exactly one owner-linked captured weapon tuple; found {num}", retaliationObserved: true);
		}
		return CapturedEnemyCombatContract.EquippedWeaponWithCapturedAttackInfo($"{capturedSubwaySourceWeaponEvidenceDefinition.EvidenceCaptures}: Mugger source 0x{sourceInstance:X8} owner-linked QL1 weapon 121567/121567; 38 normal local-player hits span 9..12, three 21-point criticals are report-only, and the median interval is 5.816469 seconds; item owns runtime damage, damage bonus, and recharge; captured AttackInfo carries only ammo -1, slot 6, unknown 0, and weapon instance 0; no empty SIW or captured attack-start/stop context", capturedSubwaySourceWeaponEvidenceDefinition.LowId, capturedSubwaySourceWeaponEvidenceDefinition.HighId, capturedSubwaySourceWeaponEvidenceDefinition.Quality, 6, -1, 6, 0, 0);
	}

	private static bool HasCompleteMuggerSourceWeaponEvidence(CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence)
	{
		if (sourceWeaponEvidence == null || sourceWeaponEvidence.Length != MuggerSourceInstances.Length)
		{
			return false;
		}
		int[] muggerSourceInstances = MuggerSourceInstances;
		foreach (int num in muggerSourceInstances)
		{
			int num2 = 0;
			foreach (CapturedSubwaySourceWeaponEvidenceDefinition capturedSubwaySourceWeaponEvidenceDefinition in sourceWeaponEvidence)
			{
				if (capturedSubwaySourceWeaponEvidenceDefinition.SourceInstance == num && capturedSubwaySourceWeaponEvidenceDefinition.LowId == 121567 && capturedSubwaySourceWeaponEvidenceDefinition.HighId == 121567 && capturedSubwaySourceWeaponEvidenceDefinition.Quality == 1)
				{
					num2++;
				}
			}
			if (num2 != 1)
			{
				return false;
			}
		}
		return true;
	}

	private static bool HasCompleteRedundantScanSourceWeaponEvidence(CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence)
	{
		if (sourceWeaponEvidence == null || sourceWeaponEvidence.Length != RedundantScanSourceInstances.Length)
		{
			return false;
		}
		int[] redundantScanSourceInstances = RedundantScanSourceInstances;
		foreach (int expectedSource in redundantScanSourceInstances)
		{
			int num = 0;
			foreach (CapturedSubwaySourceWeaponEvidenceDefinition evidence in sourceWeaponEvidence)
			{
				if (IsExactRedundantScanSourceWeapon(evidence, expectedSource))
				{
					num++;
				}
			}
			if (num != 1)
			{
				return false;
			}
		}
		return true;
	}

	private static bool HasCompleteIncompleteRebuildSourceWeaponEvidence(CapturedSubwaySourceWeaponEvidenceDefinition[] sourceWeaponEvidence)
	{
		if (sourceWeaponEvidence == null || sourceWeaponEvidence.Length != IncompleteRebuildSourceInstances.Length)
		{
			return false;
		}
		int[] incompleteRebuildSourceInstances = IncompleteRebuildSourceInstances;
		foreach (int expectedSource in incompleteRebuildSourceInstances)
		{
			int num = 0;
			foreach (CapturedSubwaySourceWeaponEvidenceDefinition evidence in sourceWeaponEvidence)
			{
				if (IsExactIncompleteRebuildSourceWeapon(evidence, expectedSource))
				{
					num++;
				}
			}
			if (num != 1)
			{
				return false;
			}
		}
		return true;
	}

	private static bool IsExactIncompleteRebuildSourceWeapon(CapturedSubwaySourceWeaponEvidenceDefinition evidence, int expectedSource)
	{
		if (evidence == null || evidence.SourceInstance != expectedSource)
		{
			return false;
		}
		switch (expectedSource)
		{
		case 2035569008:
		case 2035569015:
		case 2035569084:
			return evidence.LowId == 122653 && evidence.HighId == 122654 && evidence.Quality == 18;
		case 2035569010:
			return evidence.LowId == 122653 && evidence.HighId == 122654 && evidence.Quality == 14;
		case 2035569032:
			return evidence.LowId == 122653 && evidence.HighId == 122654 && evidence.Quality == 17;
		case 2035569025:
		case 2035569149:
		case 2035569217:
			return evidence.LowId == 122654 && evidence.HighId == 122654 && evidence.Quality == 20;
		case 2035569089:
			return evidence.LowId == 122655 && evidence.HighId == 122655 && evidence.Quality == 21;
		case 2035569099:
			return evidence.LowId == 122655 && evidence.HighId == 122656 && evidence.Quality == 24;
		default:
			return false;
		}
	}

	private static bool IsExactRedundantScanSourceWeapon(CapturedSubwaySourceWeaponEvidenceDefinition evidence, int expectedSource)
	{
		if (evidence == null || evidence.SourceInstance != expectedSource)
		{
			return false;
		}
		return expectedSource switch
		{
			2035527557 => evidence.LowId == 122027 && evidence.HighId == 122027 && evidence.Quality == 20, 
			2035569087 => evidence.LowId == 122026 && evidence.HighId == 122027 && evidence.Quality == 14, 
			2035569092 => evidence.LowId == 122028 && evidence.HighId == 122029 && evidence.Quality == 25, 
			2035569107 => evidence.LowId == 122026 && evidence.HighId == 122027 && evidence.Quality == 16, 
			_ => false, 
		};
	}

	private static CapturedEnemyCombatContract ForMeldedPatterns(CapturedSubwayOrdinaryArchetypeDefinition archetype)
	{
		CapturedSubwayCombatEvidenceDefinition combat = archetype.Combat;
		bool flag = archetype.EvidenceCaptures != null && Array.IndexOf(archetype.EvidenceCaptures, "20260716-034559") >= 0;
		bool flag2 = combat != null && combat.Observed && combat.ObservedRows == 7 && combat.MinDamage == 21 && combat.MaxDamage == 34 && combat.WeaponSlot == 6;
		if (!flag || !flag2)
		{
			return CapturedEnemyCombatContract.Unresolved("Melded Patterns equipped-weapon context requires focused capture 20260716-034559 and its seven normal 21..34 local-player hits", combat?.Observed ?? false);
		}
		return CapturedEnemyCombatContract.EquippedWeapon("20260716-034559: Melded Patterns QL20 Irreparable Sleekblaster Minor 121817/121818; seven normal local-player hits span 21..34 and no critical was observed; weapon owns runtime damage and recharge", 121817, 121818, 20, 6);
	}

	internal static CapturedEnemyCombatContract ForOrdinary(CapturedSubwayOrdinaryArchetypeDefinition archetype)
	{
		if (archetype != null && (archetype.MonsterData == 203736 || archetype.MonsterData == 203728 || archetype.MonsterData == 203729 || archetype.MonsterData == 203854 || archetype.MonsterData == 203745 || archetype.MonsterData == 204178))
		{
			return CapturedEnemyCombatContract.Unresolved(archetype.Name + " combat requires an exact captured source identity; aggregate weapon fallback is forbidden", archetype.Combat != null && archetype.Combat.Observed);
		}
		if (archetype != null && archetype.MonsterData == 203747)
		{
			return ForMeldedPatterns(archetype);
		}
		if (archetype != null && archetype.MonsterData == 30379)
		{
			return For(archetype.Name, archetype.MonsterData);
		}
		CapturedSubwayCombatEvidenceDefinition combat = archetype.Combat;
		if (combat == null || !combat.Observed)
		{
			return CapturedEnemyCombatContract.Unresolved("Generated ordinary archetype has no observed AttackInfo: " + archetype.Name, retaliationObserved: false);
		}
		if (!combat.RuntimeReady)
		{
			return CapturedEnemyCombatContract.Unresolved("Generated ordinary archetype has report-only AttackInfo evidence without a runtime-ready damage range and cadence: " + archetype.Name, retaliationObserved: true);
		}
		return CapturedEnemyCombatContract.FixedAttack(string.Join(",", archetype.EvidenceCaptures), combat.MinDamage, combat.MaxDamage, combat.RechargeSeconds, combat.WeaponSlot, combat.AttackInfoUnknown, combat.WeaponInstance);
	}

	internal static CapturedEnemyCombatContract ForOrdinary(CapturedSubwayOrdinaryArchetypeDefinition archetype, int sourceInstance)
	{
		if (archetype != null && archetype.MonsterData == 203736)
		{
			return ForDerangedShopper(archetype, sourceInstance);
		}
		if (archetype != null && archetype.MonsterData == 203854)
		{
			return ForWorkmanStriker(archetype, sourceInstance);
		}
		if (archetype != null && archetype.MonsterData == 203728)
		{
			return ForIncompleteRebuild(archetype, sourceInstance);
		}
		if (archetype != null && archetype.MonsterData == 203745)
		{
			return ForLooter(archetype, sourceInstance);
		}
		if (archetype != null && archetype.MonsterData == 204178)
		{
			return ForRedundantScan(archetype, sourceInstance);
		}
		return ForOrdinary(archetype);
	}

	internal static CapturedEnemyCombatContract ForOrdinary(CapturedSubwayOrdinaryArchetypeDefinition archetype, int sourceInstance, OrdinaryEnemySpawnVariant variant, CapturedSubwayGenerationVariantDefinition[] generationEvidence)
	{
		if (archetype != null && archetype.MonsterData == 203728)
		{
			return ForIncompleteRebuild(archetype, sourceInstance, variant, generationEvidence);
		}
		if (archetype != null && archetype.MonsterData == 203729)
		{
			return ForFragmentedSoul(archetype, sourceInstance, variant, generationEvidence);
		}
		return (archetype != null && archetype.MonsterData == 204178) ? ForRedundantScan(archetype, sourceInstance, variant, generationEvidence) : ForOrdinary(archetype, sourceInstance);
	}
}
