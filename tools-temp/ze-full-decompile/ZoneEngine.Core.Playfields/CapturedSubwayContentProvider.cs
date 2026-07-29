using System;
using System.Collections.Generic;
using AORebirth.Core.Playfields;

namespace ZoneEngine.Core.Playfields;

internal sealed class CapturedSubwayContentProvider
{
	public const int SubwayPlayfieldInstance = 127;

	private static readonly HashSet<int> RuntimeQuarantinedSourceInstances = new HashSet<int>();

	private static readonly CapturedSubwaySpawnDefinition[] SpawnDefinitions;

	private static readonly Dictionary<int, CapturedSubwayPatrolReplaySegment[]> PatrolReplaySegments;

	public CapturedSubwaySpawnDefinition[] GetSpawnDefinitions()
	{
		List<CapturedSubwaySpawnDefinition> list = new List<CapturedSubwaySpawnDefinition>();
		CapturedSubwaySpawnDefinition[] spawnDefinitions = SpawnDefinitions;
		foreach (CapturedSubwaySpawnDefinition capturedSubwaySpawnDefinition in spawnDefinitions)
		{
			if (!RuntimeQuarantinedSourceInstances.Contains(capturedSubwaySpawnDefinition.SourceInstance) || SubwayVisibilityDiagnosticSelection.ShouldIncludeQuarantined(capturedSubwaySpawnDefinition.SourceInstance))
			{
				list.Add(capturedSubwaySpawnDefinition);
			}
		}
		return list.ToArray();
	}

	internal CapturedSubwaySpawnDefinition[] GetAllSpawnDefinitions()
	{
		return (CapturedSubwaySpawnDefinition[])SpawnDefinitions.Clone();
	}

	internal static bool IsRuntimeQuarantined(int sourceInstance)
	{
		return RuntimeQuarantinedSourceInstances.Contains(sourceInstance);
	}

	public CapturedSubwayPatrolReplaySegment[] GetPatrolReplaySegments(int sourceInstance)
	{
		if (!PatrolReplaySegments.TryGetValue(sourceInstance, out var value))
		{
			return new CapturedSubwayPatrolReplaySegment[0];
		}
		CapturedSubwayPatrolReplaySegment[] array = new CapturedSubwayPatrolReplaySegment[value.Length];
		Array.Copy(value, array, value.Length);
		return array;
	}

	public CapturedSubwayLootDefinition[] GetLootDefinitions()
	{
		return new CapturedSubwayLootDefinition[19]
		{
			new CapturedSubwayLootDefinition("Thief", 26092, 138, 297055, 297055, 1, 0, 1, 0, 10000, 1, 1, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.GuaranteedProven, "20260710-205400:inventory-updates.csv"),
			new CapturedSubwayLootDefinition("Filth Flea", 17657, 138, 234874, 234874, 1, 0, 1, 0, 556, 1, 18, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, "20260709-210452,20260709-220439"),
			new CapturedSubwayLootDefinition("Filth Flea", 17657, 138, 103110, 103111, 6, 1, 1, 0, 556, 1, 18, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, "20260709-210452,20260709-220439"),
			new CapturedSubwayLootDefinition("Filth Flea", 17657, 138, 101581, 101582, 6, 2, 1, 0, 556, 1, 18, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, "20260709-210452,20260709-220439"),
			new CapturedSubwayLootDefinition("Filth Flea", 17657, 138, 110874, 110875, 6, 3, 1, 0, 556, 1, 18, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, "20260709-210452,20260709-220439"),
			new CapturedSubwayLootDefinition("Filth Flea", 17657, 138, 101507, 101508, 6, 4, 1, 0, 556, 1, 18, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, "20260709-210452,20260709-220439"),
			new CapturedSubwayLootDefinition("Filth Flea", 17657, 138, 202719, 202720, 14, 5, 1, 0, 556, 1, 18, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, "20260709-210452,20260709-220439"),
			new CapturedSubwayLootDefinition("Filth Flea", 17657, 138, 234876, 234876, 1, 6, 1, 0, 556, 1, 18, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, "20260709-210452,20260709-220439"),
			new CapturedSubwayLootDefinition("Filth Flea", 17657, 138, 101761, 101762, 9, 7, 1, 0, 556, 1, 18, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, "20260709-210452,20260709-220439"),
			new CapturedSubwayLootDefinition("Filth Flea", 17657, 138, 110192, 110193, 15, 8, 1, 0, 556, 1, 18, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, "20260709-210452,20260709-220439"),
			new CapturedSubwayLootDefinition("Filth Flea", 17657, 138, 112438, 112439, 5, 9, 1, 0, 556, 1, 18, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, "20260708-004038:SimpleChar:794AD9A9>Corpse:F6E007>InventoryUpdate#8496"),
			new CapturedSubwayLootDefinition("Filth Flea", 17657, 138, 101378, 101379, 4, 10, 1, 0, 556, 1, 18, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, "20260708-004038:SimpleChar:794ADBC4>Corpse:F6E01E>InventoryUpdate#9216"),
			new CapturedSubwayLootDefinition("Filth Flea", 17657, 138, 136652, 136653, 4, 11, 1, 0, 556, 1, 18, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, "20260708-004038:SimpleChar:794ADBC4>Corpse:F6E01E>InventoryUpdate#9216"),
			new CapturedSubwayLootDefinition("Filth Flea", 17657, 138, 111574, 111575, 5, 12, 1, 0, 556, 1, 18, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, "20260708-004038:SimpleChar:794ADC0B>Corpse:F6E002>InventoryUpdate#9996"),
			new CapturedSubwayLootDefinition("Filth Flea", 17657, 138, 111377, 111378, 5, 13, 1, 0, 556, 1, 18, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, "20260712-155528:SimpleChar:795F91B9>Corpse:F6C003>InventoryUpdate#742"),
			new CapturedSubwayLootDefinition("Filth Flea", 17657, 138, 102001, 102002, 4, 14, 1, 0, 556, 1, 18, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ExistingCapturePolicy, "20260712-161506:SimpleChar:795F924E>Corpse:F6C00B>InventoryUpdate#172"),
			new CapturedSubwayLootDefinition("Disobedient Bot", 17649, 138, 234877, 234877, 1, 0, 1, 1, 0, 1, 8, OrdinaryEnemyLootLinkageEvidence.ProvenTransferredEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ProvisionalProjectPolicy, "20260709-210452:SimpleChar:794E807A>Corpse:F6E030>InventoryUpdate#3770>ContainerAddItem#3819"),
			new CapturedSubwayLootDefinition("Disobedient Bot", 17649, 138, 104683, 104684, 10, 0, 1, 1, 0, 1, 8, OrdinaryEnemyLootLinkageEvidence.ProvenTransferredEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ProvisionalProjectPolicy, "20260713-033511:SimpleChar:79607E2C>Corpse:F6C003>InventoryUpdate#1392>ContainerAddItem#1426"),
			new CapturedSubwayLootDefinition("Disobedient Bot", 17649, 138, 113398, 113399, 7, 0, 1, 1, 0, 1, 8, OrdinaryEnemyLootLinkageEvidence.ProvenEnemyCorpseItem, OrdinaryEnemyLootProbabilityEvidence.ProvisionalProjectPolicy, "20260719-020104:SimpleChar:797AD6E4>Corpse:F74004>InventoryUpdate#2383")
		};
	}

	private static CapturedSubwaySpawnDefinition FirstLowerSectionSpawn(CapturedSubwaySpawnDefinition spawn)
	{
		spawn.ContentSection = "FirstLowerSection";
		return spawn;
	}

	private static CapturedSubwaySpawnDefinition CapturedSurveySpawn(CapturedSubwaySpawnDefinition spawn)
	{
		spawn.ContentSection = "Captured20260709Survey";
		return spawn;
	}

	private static CapturedSubwaySpawnDefinition FilthFlea(int sourceInstance, int level, int health, float x, float y, float z, int runSpeed = 22, bool useSpawnAsPatrolStart = false)
	{
		bool useSpawnAsPatrolStart2 = useSpawnAsPatrolStart;
		double? respawnDelaySeconds = 240.0;
		return new CapturedSubwaySpawnDefinition(sourceInstance, "A096", "Filth Flea", 17657, level, health, 130, 0, runSpeed, 138, 268964353, 6, 5, x, y, z, null, null, null, useSpawnAsPatrolStart2, respawnDelaySeconds);
	}

	private static CapturedSubwaySpawnDefinition DiscardedPet(int sourceInstance, int level, int health, float x, float y, float z, int monsterScale = 94, int runSpeed = 33, bool useSpawnAsPatrolStart = false)
	{
		bool useSpawnAsPatrolStart2 = useSpawnAsPatrolStart;
		return new CapturedSubwaySpawnDefinition(sourceInstance, "A120", "Discarded Pet", 17720, level, health, monsterScale, 0, runSpeed, 138, 268980737, 7, 5, x, y, z, null, null, null, useSpawnAsPatrolStart2);
	}

	private static CapturedSubwaySpawnDefinition DisobedientBot(int sourceInstance, int level, int health, float x, float y, float z, int monsterScale = 90, int runSpeed = 33, bool useSpawnAsPatrolStart = false)
	{
		bool useSpawnAsPatrolStart2 = useSpawnAsPatrolStart;
		double? respawnDelaySeconds = 450.0;
		return new CapturedSubwaySpawnDefinition(sourceInstance, "A120", "Disobedient Bot", 17649, level, health, monsterScale, 0, runSpeed, 138, 403182081, 7, 5, x, y, z, null, null, null, useSpawnAsPatrolStart2, respawnDelaySeconds);
	}

	private static CapturedSubwaySpawnDefinition Mugger(int sourceInstance, int level, int health, float x, float y, float z, int monsterScale = 94, int runSpeed = 21)
	{
		return new CapturedSubwaySpawnDefinition(sourceInstance, "A051", "Mugger", 203734, level, health, monsterScale, 40705, runSpeed, 138, 268964353, 1, 6, x, y, z);
	}

	private static CapturedSubwaySpawnDefinition Thief(int sourceInstance, int level, int health, float x, float y, float z, int monsterScale = 93, int runSpeed = 20, float? patrolX = null, float? patrolY = null, float? patrolZ = null, bool useSpawnAsPatrolStart = false, double? respawnDelaySeconds = null, int healthDamage = 0)
	{
		return new CapturedSubwaySpawnDefinition(sourceInstance, "A051", "Thief", 26092, level, health, monsterScale, 40694, runSpeed, 138, 268964353, 1, 6, x, y, z, patrolX, patrolY, patrolZ, useSpawnAsPatrolStart, respawnDelaySeconds, healthDamage);
	}

	private static CapturedSubwaySpawnDefinition ViolentVagabond(int sourceInstance, int level, int health, float x, float y, float z, int monsterScale = 93, int runSpeed = 18, bool useSpawnAsPatrolStart = false)
	{
		bool useSpawnAsPatrolStart2 = useSpawnAsPatrolStart;
		return new CapturedSubwaySpawnDefinition(sourceInstance, "A051", "Violent Vagabond", 203733, level, health, monsterScale, 40676, runSpeed, 3, 268964353, 1, 6, x, y, z, null, null, null, useSpawnAsPatrolStart2);
	}

	static CapturedSubwayContentProvider()
	{
		CapturedSubwaySpawnDefinition[] obj = new CapturedSubwaySpawnDefinition[124]
		{
			CapturedSurveySpawn(DiscardedPet(2035151333, 5, 115, 184.84396f, 107.61483f, 240.56978f, 93, 24)),
			CapturedSurveySpawn(DiscardedPet(2035188673, 7, 160, 195.35136f, 107.61169f, 290.97443f, 94, 32)),
			CapturedSurveySpawn(DiscardedPet(2035453802, 9, 205, 171.85123f, 107.61169f, 304.09885f, 95, 40)),
			CapturedSurveySpawn(DiscardedPet(2035453914, 8, 183, 188.99f, 107.61169f, 309.9072f, 94, 36, useSpawnAsPatrolStart: true)),
			CapturedSurveySpawn(DiscardedPet(2035488726, 5, 115, 178.22032f, 107.61483f, 247.39406f, 93, 24)),
			CapturedSurveySpawn(DiscardedPet(2035526148, 10, 227, 346.52777f, 102.81483f, 161.956f, 95, 44)),
			CapturedSurveySpawn(DiscardedPet(2035526171, 10, 227, 346.46872f, 102.81483f, 165.56929f, 95, 44)),
			CapturedSurveySpawn(DiscardedPet(2035526274, 10, 227, 349.01f, 102.81483f, 168.29759f, 95, 44)),
			CapturedSurveySpawn(DiscardedPet(2035526657, 6, 138, 149.80078f, 107.61483f, 251.29686f, 93, 28)),
			CapturedSurveySpawn(DiscardedPet(2035526972, 8, 183, 149.25511f, 107.61483f, 199.86124f, 94, 36)),
			CapturedSurveySpawn(DiscardedPet(2035527007, 10, 227, 200.48233f, 107.6164f, 161.47556f, 95, 44)),
			CapturedSurveySpawn(DiscardedPet(2035527021, 10, 227, 267.8472f, 102.8164f, 164.07674f, 95, 44)),
			CapturedSurveySpawn(DiscardedPet(2035527023, 10, 227, 268.90552f, 102.8164f, 166.40154f, 95, 44)),
			CapturedSurveySpawn(DiscardedPet(2035527028, 10, 227, 277.9136f, 102.8164f, 165.51718f, 95, 44)),
			CapturedSurveySpawn(DiscardedPet(2035527507, 6, 138, 158.79005f, 107.61483f, 235.16075f, 93, 28)),
			CapturedSurveySpawn(DiscardedPet(2035527526, 5, 115, 158.81708f, 107.61483f, 246.37257f, 93, 24)),
			CapturedSurveySpawn(DiscardedPet(2035527540, 5, 115, 185.50768f, 107.61483f, 241.62752f, 93, 24)),
			CapturedSurveySpawn(DiscardedPet(2035527577, 6, 138, 181.53067f, 107.61483f, 249.83105f, 93, 28)),
			CapturedSurveySpawn(DiscardedPet(2035645449, 9, 205, 183.01f, 107.61169f, 308.6345f, 95, 40)),
			CapturedSurveySpawn(DiscardedPet(2035645478, 7, 160, 192.56523f, 107.61169f, 289.6804f, 94, 32)),
			CapturedSurveySpawn(DiscardedPet(2035645489, 5, 115, 174.19421f, 107.61483f, 242.16644f, 93, 24)),
			CapturedSurveySpawn(DiscardedPet(2035645579, 10, 227, 286.2218f, 107.61169f, 285.7219f, 95, 44)),
			CapturedSurveySpawn(DiscardedPet(2035645607, 8, 183, 161.97876f, 107.61326f, 301.46613f, 94, 36)),
			CapturedSurveySpawn(DiscardedPet(2035645611, 10, 227, 281.3582f, 107.61169f, 284.46725f, 95, 44)),
			CapturedSurveySpawn(DiscardedPet(2035645613, 10, 227, 288.67303f, 107.61169f, 276.39066f, 95, 44)),
			CapturedSurveySpawn(DiscardedPet(2035803153, 10, 227, 201.89015f, 107.6164f, 164.699f, 95, 44)),
			CapturedSurveySpawn(DiscardedPet(2035803301, 6, 138, 144.8586f, 107.61483f, 251.13852f, 93, 28)),
			CapturedSurveySpawn(DiscardedPet(2035803313, 5, 115, 151.49872f, 107.61483f, 237.92157f, 93, 24)),
			CapturedSurveySpawn(DiscardedPet(2035803324, 8, 183, 156.30116f, 107.61483f, 233.5397f, 94, 36)),
			CapturedSurveySpawn(DisobedientBot(2035526174, 10, 227, 333.48615f, 102.414825f, 161.49333f, 95, 34)),
			CapturedSurveySpawn(DisobedientBot(2035526273, 10, 227, 325.24176f, 102.81483f, 163.73727f, 95, 34)),
			CapturedSurveySpawn(DisobedientBot(2035526287, 10, 227, 337.21054f, 102.414825f, 160.9172f, 95, 34)),
			CapturedSurveySpawn(DisobedientBot(2035526408, 10, 227, 334.09982f, 102.414825f, 166.30553f, 95, 34)),
			CapturedSurveySpawn(DisobedientBot(2035526987, 9, 205, 208.74696f, 107.6164f, 165.35898f, 95, 31)),
			CapturedSurveySpawn(DisobedientBot(2035527009, 10, 227, 214.0725f, 107.6164f, 164.6418f, 95, 34)),
			CapturedSurveySpawn(DisobedientBot(2035527017, 9, 205, 216.01f, 107.6164f, 162.70897f, 95, 31)),
			CapturedSurveySpawn(DisobedientBot(2035527535, 9, 205, 114.49927f, 107.61483f, 231.65105f, 95, 31)),
			CapturedSurveySpawn(DisobedientBot(2035527576, 7, 160, 173.61095f, 107.61483f, 232.28839f, 94, 25)),
			CapturedSurveySpawn(DisobedientBot(2035527587, 6, 138, 179.51431f, 107.61483f, 232.11319f, 93, 22)),
			CapturedSurveySpawn(DisobedientBot(2035645542, 7, 160, 151.40912f, 107.61483f, 271.044f, 94, 25, useSpawnAsPatrolStart: true)),
			CapturedSurveySpawn(DisobedientBot(2035803146, 10, 227, 211.50462f, 107.6164f, 166.47296f, 95, 34)),
			CapturedSurveySpawn(FilthFlea(2035487740, 5, 115, 147.95009f, 107.61483f, 229.4221f, 21)),
			CapturedSurveySpawn(FilthFlea(2035488587, 6, 138, 120.68247f, 107.61483f, 241.09883f, 24)),
			CapturedSurveySpawn(FilthFlea(2035488594, 5, 115, 120.437515f, 107.61483f, 238.61601f, 21)),
			CapturedSurveySpawn(FilthFlea(2035488596, 5, 115, 120.61302f, 107.61483f, 237.21764f, 21)),
			CapturedSurveySpawn(FilthFlea(2035488757, 7, 160, 158.91556f, 107.6164f, 162.84361f, 27, useSpawnAsPatrolStart: true)),
			CapturedSurveySpawn(FilthFlea(2035526082, 15, 393, 283.226f, 100.8164f, 212.81714f, 57)),
			CapturedSurveySpawn(FilthFlea(2035526086, 14, 360, 278.98257f, 100.8164f, 212.5821f, 53)),
			CapturedSurveySpawn(FilthFlea(2035526155, 15, 393, 316.3524f, 102.8164f, 218.6188f, 57)),
			CapturedSurveySpawn(FilthFlea(2035526156, 13, 327, 315.67615f, 102.8164f, 220.47012f, 49)),
			CapturedSurveySpawn(FilthFlea(2035526955, 6, 138, 152.82129f, 107.61483f, 203.99f, 24)),
			CapturedSurveySpawn(FilthFlea(2035526956, 5, 115, 148.60043f, 107.61483f, 224.30545f, 21)),
			CapturedSurveySpawn(FilthFlea(2035526959, 8, 183, 145.37485f, 107.61483f, 199.42783f, 31)),
			CapturedSurveySpawn(FilthFlea(2035526960, 7, 160, 149.19594f, 107.61483f, 213.89748f, 27)),
			CapturedSurveySpawn(FilthFlea(2035526966, 8, 183, 146.85692f, 107.61483f, 201.20361f, 31)),
			CapturedSurveySpawn(FilthFlea(2035526974, 5, 115, 148.99f, 107.61483f, 196.13786f, 21)),
			CapturedSurveySpawn(FilthFlea(2035527024, 11, 261, 224.79778f, 107.6164f, 165.96857f, 41)),
			CapturedSurveySpawn(FilthFlea(2035527025, 11, 261, 226.11597f, 107.6164f, 162.99f, 41)),
			CapturedSurveySpawn(FilthFlea(2035527027, 10, 227, 224.22609f, 107.6164f, 163.8984f, 37)),
			CapturedSurveySpawn(FilthFlea(2035527029, 11, 261, 231.02457f, 107.6164f, 163.93681f, 41)),
			CapturedSurveySpawn(FilthFlea(2035527032, 10, 227, 248.08153f, 106.405754f, 164.44235f, 37)),
			CapturedSurveySpawn(FilthFlea(2035527402, 4, 93, 88.50346f, 115.615f, 300.2512f, 17)),
			CapturedSurveySpawn(FilthFlea(2035527406, 5, 115, 86.2133f, 111.615f, 270.39136f, 21)),
			CapturedSurveySpawn(FilthFlea(2035527420, 4, 93, 100.99f, 107.61483f, 238.86769f, 17)),
			CapturedSurveySpawn(FilthFlea(2035527428, 5, 115, 97.40637f, 107.61483f, 257.27744f, 21)),
			CapturedSurveySpawn(FilthFlea(2035527440, 5, 115, 86.80035f, 107.61483f, 250.36943f, 21)),
			CapturedSurveySpawn(FilthFlea(2035527448, 4, 93, 91.535286f, 107.61483f, 248.86052f, 17)),
			CapturedSurveySpawn(FilthFlea(2035527458, 6, 138, 101.78243f, 107.61483f, 236.89037f, 24)),
			CapturedSurveySpawn(FilthFlea(2035527498, 6, 138, 85.88043f, 107.61483f, 258.95575f, 24)),
			CapturedSurveySpawn(FilthFlea(2035527511, 5, 115, 92.79191f, 107.61483f, 257.03732f, 21)),
			CapturedSurveySpawn(FilthFlea(2035527594, 7, 160, 179.49269f, 107.61483f, 252.25995f, 27)),
			CapturedSurveySpawn(FilthFlea(2035527598, 5, 115, 176.86208f, 107.61483f, 249.52832f, 21)),
			CapturedSurveySpawn(FilthFlea(2035527620, 7, 160, 182.377f, 107.61483f, 222.0669f, 27)),
			CapturedSurveySpawn(FilthFlea(2035527622, 5, 115, 190.18114f, 107.61483f, 224.26843f, 21)),
			CapturedSurveySpawn(FilthFlea(2035527628, 5, 115, 177.57327f, 107.61483f, 224.14803f, 21, useSpawnAsPatrolStart: true)),
			CapturedSurveySpawn(FilthFlea(2035526113, 13, 327, 330.57898f, 102.865f, 150.1263f, 49)),
			CapturedSurveySpawn(FilthFlea(2035526119, 11, 261, 328.6433f, 102.965f, 143.93188f, 41)),
			CapturedSurveySpawn(FilthFlea(2035526122, 11, 261, 325.99f, 102.8164f, 148.11964f, 41)),
			CapturedSurveySpawn(FilthFlea(2035526140, 11, 261, 327.13147f, 102.865f, 142.70447f, 41)),
			CapturedSurveySpawn(FilthFlea(2035366535, 12, 294, 351.97552f, 102.81483f, 141.40897f, 45)),
			CapturedSurveySpawn(FilthFlea(2035366543, 12, 294, 351.4564f, 102.81483f, 148.9678f, 45)),
			CapturedSurveySpawn(FilthFlea(2035366575, 13, 327, 348.57153f, 102.81483f, 138.47845f, 49)),
			CapturedSurveySpawn(FilthFlea(2035366594, 13, 327, 350.35043f, 102.81483f, 144.81358f, 49)),
			CapturedSurveySpawn(FilthFlea(2035569187, 13, 327, 325.3251f, 102.81483f, 183.53088f, 49)),
			CapturedSurveySpawn(FilthFlea(2035569191, 11, 261, 324.01f, 102.81483f, 178.83403f, 41)),
			CapturedSurveySpawn(FilthFlea(2035487008, 21, 592, 187.0416f, 73.383026f, 88.03114f, 80)),
			CapturedSurveySpawn(FilthFlea(2035487010, 21, 592, 187.2152f, 73.24139f, 109.88612f, 80)),
			CapturedSurveySpawn(FilthFlea(2035569041, 19, 526, 160.99f, 81.21325f, 70.15537f, 72)),
			CapturedSurveySpawn(FilthFlea(2035527533, 19, 526, 121.509415f, 77.01481f, 126.34852f, 72)),
			CapturedSurveySpawn(FilthFlea(2035527537, 21, 592, 125.11138f, 77.01481f, 128.97948f, 80)),
			CapturedSurveySpawn(FilthFlea(2035527542, 20, 559, 123.63249f, 77.01481f, 126.58586f, 76)),
			CapturedSurveySpawn(FilthFlea(2035569060, 21, 592, 123.01f, 77.01481f, 127.967026f, 80)),
			CapturedSurveySpawn(Mugger(2035526161, 8, 146, 291.3161f, 102.8164f, 250.82439f, 94, 30)),
			CapturedSurveySpawn(Mugger(2035527019, 10, 182, 264.12775f, 103.19651f, 163.2112f, 95, 36)),
			CapturedSurveySpawn(Mugger(2035568852, 5, 92, 167.8636f, 109.10483f, 255.63666f, 93, 20)),
			CapturedSurveySpawn(Mugger(2035569150, 10, 182, 228.21564f, 107.6164f, 163.44533f, 95, 36)),
			CapturedSurveySpawn(Mugger(2035646228, 10, 182, 292.5373f, 107.61169f, 298.02475f, 95, 36)),
			CapturedSurveySpawn(Mugger(2035803590, 9, 164, 152.43741f, 107.61326f, 297.01f, 95, 33)),
			CapturedSurveySpawn(Mugger(2035803591, 8, 146, 153.4413f, 107.61326f, 297.97433f, 94, 30)),
			CapturedSurveySpawn(Mugger(2035803592, 8, 146, 145.38615f, 107.61326f, 289.6806f, 94, 30)),
			CapturedSurveySpawn(Mugger(2035803594, 10, 182, 267.64005f, 107.61169f, 287.82437f, 95, 36)),
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		double? respawnDelaySeconds = 60.0;
		obj[101] = CapturedSurveySpawn(Thief(2035527333, 5, 146, 72.729256f, 115.61483f, 313.1308f, 93, 20, null, null, null, useSpawnAsPatrolStart: true, respawnDelaySeconds, 31));
		obj[102] = CapturedSurveySpawn(ViolentVagabond(2035526218, 10, 182, 198.0572f, 108.416405f, 191.59692f, 95, 27));
		obj[103] = CapturedSurveySpawn(ViolentVagabond(2035526976, 6, 110, 148.6321f, 107.6164f, 189.49127f));
		obj[104] = CapturedSurveySpawn(ViolentVagabond(2035526984, 7, 128, 190.40317f, 107.6164f, 164.9011f, 94, 20));
		obj[105] = CapturedSurveySpawn(ViolentVagabond(2035526985, 6, 110, 171.15405f, 107.6164f, 164.4986f));
		obj[106] = CapturedSurveySpawn(ViolentVagabond(2035526986, 7, 128, 160.53635f, 107.6164f, 165.19084f, 94, 20));
		obj[107] = CapturedSurveySpawn(ViolentVagabond(2035526988, 7, 128, 163.60526f, 107.6164f, 167.14491f, 94, 20));
		obj[108] = CapturedSurveySpawn(ViolentVagabond(2035526996, 8, 146, 198.14859f, 107.6164f, 163.83435f, 94, 23));
		obj[109] = CapturedSurveySpawn(ViolentVagabond(2035527000, 10, 182, 201.0314f, 107.6164f, 183.94325f, 95, 27));
		obj[110] = CapturedSurveySpawn(ViolentVagabond(2035527030, 10, 182, 282.37524f, 102.8164f, 166.22612f, 95, 27));
		obj[111] = CapturedSurveySpawn(ViolentVagabond(2035527497, 7, 128, 90.4653f, 107.61483f, 245.88252f, 94, 20));
		obj[112] = CapturedSurveySpawn(ViolentVagabond(2035527585, 7, 128, 184.87302f, 107.61483f, 245.96968f, 94, 20, useSpawnAsPatrolStart: true));
		obj[113] = CapturedSurveySpawn(ViolentVagabond(2035645612, 10, 182, 273.7663f, 107.61169f, 284.70352f, 95, 27));
		obj[114] = CapturedSurveySpawn(ViolentVagabond(2035761244, 7, 128, 166.46637f, 107.6164f, 165.10306f, 94, 20));
		obj[115] = CapturedSurveySpawn(ViolentVagabond(2035762087, 10, 182, 197.54114f, 108.416405f, 209.09239f, 95, 27));
		obj[116] = CapturedSurveySpawn(ViolentVagabond(2035762088, 10, 182, 199.9471f, 108.416405f, 193.51411f, 95, 27));
		obj[117] = CapturedSurveySpawn(ViolentVagabond(2035802156, 7, 128, 169.27258f, 107.61483f, 244.71405f, 94, 20));
		obj[118] = CapturedSurveySpawn(ViolentVagabond(2035802158, 7, 128, 163.90233f, 107.6164f, 164.68349f, 94, 20));
		obj[119] = CapturedSurveySpawn(ViolentVagabond(2035802403, 6, 110, 149.73949f, 107.61483f, 279.86185f));
		obj[120] = CapturedSurveySpawn(ViolentVagabond(2035803150, 6, 110, 182.84677f, 107.6164f, 165.3118f));
		obj[121] = CapturedSurveySpawn(ViolentVagabond(2035803583, 7, 128, 165.98524f, 107.61326f, 305.1552f, 94, 20));
		obj[122] = CapturedSurveySpawn(ViolentVagabond(2035803588, 7, 128, 153.28094f, 107.61483f, 277.75107f, 94, 20, useSpawnAsPatrolStart: true));
		obj[123] = CapturedSurveySpawn(ViolentVagabond(2035803589, 6, 110, 151.61375f, 107.61483f, 280.14572f));
		SpawnDefinitions = obj;
		PatrolReplaySegments = new Dictionary<int, CapturedSubwayPatrolReplaySegment[]>
		{
			{
				2035645542,
				new CapturedSubwayPatrolReplaySegment[4]
				{
					new CapturedSubwayPatrolReplaySegment(3.250491, 143.6185f, 107.61483f, 268.44336f, 146.634f, 107.61505f, 273.26184f, 24),
					new CapturedSubwayPatrolReplaySegment(5.76904, 145.75583f, 107.61483f, 272.13693f, 155.2044f, 107.61488f, 269.84723f, 24),
					new CapturedSubwayPatrolReplaySegment(2.360521, 153.89844f, 107.61483f, 270.22678f, 153.06241f, 107.61487f, 265.9732f, 24),
					new CapturedSubwayPatrolReplaySegment(6.951918, 153.4961f, 107.61483f, 267.23083f, 142.2362f, 107.61505f, 268.69824f, 24)
				}
			},
			{
				2035803588,
				new CapturedSubwayPatrolReplaySegment[26]
				{
					new CapturedSubwayPatrolReplaySegment(2.149372, 147.40915f, 107.61483f, 276.9853f, 148.06496f, 107.61483f, 281.59012f, 24),
					new CapturedSubwayPatrolReplaySegment(1.310516, 147.68767f, 107.61483f, 280.18646f, 147.15195f, 107.61483f, 278.35f, 24),
					new CapturedSubwayPatrolReplaySegment(1.91102, 147.54613f, 107.61483f, 279.741f, 147.78734f, 107.61483f, 275.66553f, 24),
					new CapturedSubwayPatrolReplaySegment(5.628537, 147.4408f, 107.61483f, 276.889f, 153.60345f, 107.61483f, 269.55487f, 24),
					new CapturedSubwayPatrolReplaySegment(0.849001, 152.81992f, 107.61483f, 270.45496f, 151.23465f, 107.61483f, 269.08676f, 24),
					new CapturedSubwayPatrolReplaySegment(1.330234, 152.57738f, 107.61483f, 269.66922f, 150.05243f, 107.61483f, 271.4642f, 24),
					new CapturedSubwayPatrolReplaySegment(1.660517, 151.07504f, 107.61483f, 270.4944f, 150.03784f, 107.61483f, 274.02997f, 24),
					new CapturedSubwayPatrolReplaySegment(1.560077, 150.31535f, 107.61483f, 272.83472f, 152.75732f, 107.61483f, 274.54236f, 24),
					new CapturedSubwayPatrolReplaySegment(0.599019, 151.55034f, 107.61483f, 273.97775f, 153.41798f, 107.61483f, 275.64813f, 24),
					new CapturedSubwayPatrolReplaySegment(1.480538, 152.52272f, 107.61483f, 274.77194f, 153.23174f, 107.61483f, 278.3087f, 24),
					new CapturedSubwayPatrolReplaySegment(1.351025, 152.99f, 107.61483f, 276.86728f, 153.64958f, 107.61483f, 280.28897f, 24),
					new CapturedSubwayPatrolReplaySegment(1.661019, 153.35567f, 107.61483f, 278.8316f, 152.8666f, 107.61483f, 282.57108f, 24),
					new CapturedSubwayPatrolReplaySegment(2.12069, 153.07341f, 107.61483f, 281.3213f, 149.0787f, 107.61483f, 282.6228f, 24),
					new CapturedSubwayPatrolReplaySegment(2.721522, 150.49724f, 107.61483f, 282.32568f, 145.20001f, 108.60483f, 281.80002f, 24),
					new CapturedSubwayPatrolReplaySegment(1.890045, 146.47588f, 107.61483f, 282.01f, 149.0787f, 107.61483f, 282.6228f, 24),
					new CapturedSubwayPatrolReplaySegment(2.40163, 147.94228f, 107.61483f, 282.388f, 152.8666f, 107.61483f, 282.57108f, 24),
					new CapturedSubwayPatrolReplaySegment(1.55151, 151.5458f, 107.61483f, 282.53842f, 153.64958f, 107.61483f, 280.28897f, 24),
					new CapturedSubwayPatrolReplaySegment(0.949508, 152.93181f, 107.61483f, 281.25616f, 153.23174f, 107.61483f, 278.3087f, 24),
					new CapturedSubwayPatrolReplaySegment(1.939551, 153.19339f, 107.61483f, 279.59927f, 153.41798f, 107.61483f, 275.64813f, 24),
					new CapturedSubwayPatrolReplaySegment(0.669893, 153.34201f, 107.61483f, 276.94302f, 152.75732f, 107.61483f, 274.54236f, 24),
					new CapturedSubwayPatrolReplaySegment(1.710677, 153.1168f, 107.61483f, 275.75333f, 150.03784f, 107.61483f, 274.02997f, 24),
					new CapturedSubwayPatrolReplaySegment(1.16002, 151.23296f, 107.61483f, 274.5704f, 150.05243f, 107.61483f, 271.4642f, 24),
					new CapturedSubwayPatrolReplaySegment(1.65954, 150.43709f, 107.61483f, 272.86328f, 151.23465f, 107.61483f, 269.08676f, 24),
					new CapturedSubwayPatrolReplaySegment(1.490525, 150.88339f, 107.61483f, 270.4369f, 153.60345f, 107.61483f, 269.55487f, 24),
					new CapturedSubwayPatrolReplaySegment(4.569248, 152.21841f, 107.61483f, 269.76935f, 147.78734f, 107.61483f, 275.66553f, 24),
					new CapturedSubwayPatrolReplaySegment(1.990639, 148.57889f, 107.61483f, 274.74878f, 147.15195f, 107.61483f, 278.35f, 24)
				}
			},
			{
				2035527628,
				new CapturedSubwayPatrolReplaySegment[10]
				{
					new CapturedSubwayPatrolReplaySegment(0.894761, 186.28746f, 107.61483f, 224.11531f, 190.76523f, 107.61483f, 220.86206f, 25),
					new CapturedSubwayPatrolReplaySegment(0.410009, 189.8595f, 107.61483f, 221.3813f, 191.24792f, 107.61483f, 219.19702f, 25),
					new CapturedSubwayPatrolReplaySegment(0.714996, 191.0297f, 107.61483f, 219.85571f, 190.76523f, 107.61483f, 220.86206f, 25),
					new CapturedSubwayPatrolReplaySegment(1.145737, 191.1833f, 107.61483f, 219.92708f, 186.89734f, 107.61483f, 223.74706f, 25),
					new CapturedSubwayPatrolReplaySegment(0.694343, 187.64616f, 107.61483f, 223.06082f, 183.36392f, 107.61483f, 225.43213f, 25),
					new CapturedSubwayPatrolReplaySegment(0.450506, 184.23927f, 107.61483f, 225.04248f, 181.13464f, 107.61483f, 225.94124f, 25),
					new CapturedSubwayPatrolReplaySegment(0.874281, 182.06505f, 107.61483f, 225.78598f, 177.69244f, 108.60409f, 224.19263f, 25),
					new CapturedSubwayPatrolReplaySegment(1.330127, 178.43465f, 107.61483f, 224.64552f, 181.13464f, 107.61483f, 225.94124f, 25),
					new CapturedSubwayPatrolReplaySegment(0.419805, 180.40346f, 107.61483f, 225.55597f, 183.36392f, 107.61483f, 225.43213f, 25),
					new CapturedSubwayPatrolReplaySegment(0.874444, 182.44171f, 107.61483f, 225.73627f, 186.89734f, 107.61483f, 223.74706f, 25)
				}
			},
			{
				2035488757,
				new CapturedSubwayPatrolReplaySegment[18]
				{
					new CapturedSubwayPatrolReplaySegment(1.539519, 149.25777f, 107.6164f, 181.12868f, 148.44388f, 107.6164f, 172.39177f, 25),
					new CapturedSubwayPatrolReplaySegment(0.834284, 148.48454f, 107.6164f, 173.36945f, 149.20787f, 107.6164f, 168.5246f, 25),
					new CapturedSubwayPatrolReplaySegment(1.580319, 149.03206f, 107.6164f, 169.26695f, 154.57047f, 107.6164f, 163.03473f, 25),
					new CapturedSubwayPatrolReplaySegment(0.780525, 154.0946f, 107.6164f, 163.49341f, 159.04628f, 107.6164f, 162.81314f, 25),
					new CapturedSubwayPatrolReplaySegment(0.635756, 158.11089f, 107.6164f, 162.72598f, 162.35953f, 107.6164f, 162.74734f, 25),
					new CapturedSubwayPatrolReplaySegment(1.361425, 161.33194f, 107.6164f, 162.74963f, 159.04628f, 107.6164f, 162.81314f, 25),
					new CapturedSubwayPatrolReplaySegment(0.889734, 159.6598f, 107.6164f, 162.80827f, 154.57047f, 107.6164f, 163.03473f, 25),
					new CapturedSubwayPatrolReplaySegment(1.554936, 155.33012f, 107.6164f, 162.98843f, 149.20787f, 107.6164f, 168.5246f, 25),
					new CapturedSubwayPatrolReplaySegment(0.834082, 149.77095f, 107.6164f, 167.88057f, 148.44388f, 107.6164f, 172.39177f, 25),
					new CapturedSubwayPatrolReplaySegment(1.539983, 148.53949f, 107.6164f, 171.3582f, 148.92538f, 107.6164f, 180.10806f, 25),
					new CapturedSubwayPatrolReplaySegment(0.439907, 148.85706f, 107.6164f, 179.29565f, 149.01416f, 107.6164f, 182.18784f, 25),
					new CapturedSubwayPatrolReplaySegment(0.244752, 148.98817f, 107.6164f, 181.4108f, 150.45197f, 107.6164f, 183.31001f, 25),
					new CapturedSubwayPatrolReplaySegment(0.664526, 149.55925f, 107.6164f, 182.59404f, 150.54797f, 107.6164f, 186.31798f, 25),
					new CapturedSubwayPatrolReplaySegment(0.895026, 150.51404f, 107.6164f, 185.57835f, 150.62973f, 108.10638f, 191.21658f, 25),
					new CapturedSubwayPatrolReplaySegment(1.514288, 150.65974f, 107.6164f, 190.20917f, 150.54797f, 107.6164f, 186.31798f, 25),
					new CapturedSubwayPatrolReplaySegment(0.565255, 150.5546f, 107.6164f, 187.28304f, 150.45197f, 107.6164f, 183.31001f, 25),
					new CapturedSubwayPatrolReplaySegment(0.409967, 150.48123f, 107.6164f, 184.15564f, 149.01416f, 107.6164f, 182.18784f, 25),
					new CapturedSubwayPatrolReplaySegment(0.415537, 150.12173f, 107.6164f, 182.90695f, 148.92538f, 107.6164f, 180.10806f, 25)
				}
			},
			{
				2035453914,
				new CapturedSubwayPatrolReplaySegment[24]
				{
					new CapturedSubwayPatrolReplaySegment(2.379826, 179.05276f, 107.61169f, 305.91403f, 174.762f, 107.61169f, 302.95886f, 24),
					new CapturedSubwayPatrolReplaySegment(2.110088, 175.86826f, 107.61169f, 303.72348f, 171.54642f, 108.601685f, 302.84747f, 24),
					new CapturedSubwayPatrolReplaySegment(1.110508, 172.95389f, 107.61169f, 303.0842f, 174.762f, 107.61169f, 302.95886f, 24),
					new CapturedSubwayPatrolReplaySegment(2.720032, 173.27313f, 107.61169f, 303.01f, 177.86801f, 107.61169f, 305.1219f, 24),
					new CapturedSubwayPatrolReplaySegment(2.910029, 176.66801f, 107.61169f, 304.5212f, 181.39429f, 107.61169f, 307.39746f, 24),
					new CapturedSubwayPatrolReplaySegment(1.620021, 180.38205f, 107.61169f, 306.77322f, 183.56003f, 107.61169f, 309.41803f, 24),
					new CapturedSubwayPatrolReplaySegment(2.289555, 182.49895f, 107.61169f, 308.5152f, 186.51175f, 107.61169f, 310.81757f, 24),
					new CapturedSubwayPatrolReplaySegment(1.949692, 185.47765f, 107.61169f, 310.2362f, 189.64706f, 107.61169f, 310.69125f, 24),
					new CapturedSubwayPatrolReplaySegment(1.620035, 188.149f, 107.61169f, 310.61673f, 189.09659f, 107.61169f, 308.88937f, 24),
					new CapturedSubwayPatrolReplaySegment(0.680509, 189.06972f, 107.61169f, 309.07562f, 187.16632f, 107.61169f, 309.37158f, 24),
					new CapturedSubwayPatrolReplaySegment(0.469507, 188.4844f, 107.61169f, 309.16663f, 186.297f, 107.61169f, 310.0088f, 24),
					new CapturedSubwayPatrolReplaySegment(0.190005, 187.56656f, 107.61169f, 309.46014f, 185.82071f, 107.61169f, 309.12384f, 24),
					new CapturedSubwayPatrolReplaySegment(1.330019, 187.26532f, 107.61169f, 309.50507f, 188.58682f, 107.61169f, 308.0366f, 24),
					new CapturedSubwayPatrolReplaySegment(0.24, 187.60646f, 107.61169f, 308.72208f, 188.44159f, 107.61169f, 307.40695f, 24),
					new CapturedSubwayPatrolReplaySegment(0.401001, 187.84586f, 107.61169f, 308.47476f, 188.58682f, 107.61169f, 308.0366f, 24),
					new CapturedSubwayPatrolReplaySegment(1.379053, 188.06f, 107.61169f, 308.2796f, 185.82071f, 107.61169f, 309.12384f, 24),
					new CapturedSubwayPatrolReplaySegment(0.230001, 187.14502f, 107.61169f, 308.50665f, 186.297f, 107.61169f, 310.0088f, 24),
					new CapturedSubwayPatrolReplaySegment(0.410509, 186.88432f, 107.61169f, 308.71646f, 187.16632f, 107.61169f, 309.37158f, 24),
					new CapturedSubwayPatrolReplaySegment(0.880498, 186.76962f, 107.61169f, 308.9421f, 189.09659f, 107.61169f, 308.88937f, 24),
					new CapturedSubwayPatrolReplaySegment(1.679154, 187.69858f, 107.61169f, 309.20593f, 189.64706f, 107.61169f, 310.69125f, 24),
					new CapturedSubwayPatrolReplaySegment(1.380401, 189.5349f, 107.61169f, 310.56833f, 186.51175f, 107.61169f, 310.81757f, 24),
					new CapturedSubwayPatrolReplaySegment(2.249535, 187.89514f, 107.61169f, 310.70352f, 183.56003f, 107.61169f, 309.41803f, 24),
					new CapturedSubwayPatrolReplaySegment(1.949761, 184.68867f, 107.61169f, 309.78635f, 181.39429f, 107.61169f, 307.39746f, 24),
					new CapturedSubwayPatrolReplaySegment(2.660205, 182.53871f, 107.61169f, 308.2798f, 177.86801f, 107.61169f, 305.1219f, 24)
				}
			},
			{
				2035527585,
				new CapturedSubwayPatrolReplaySegment[10]
				{
					new CapturedSubwayPatrolReplaySegment(4.491099, 183.1537f, 107.61483f, 242.17834f, 185.1825f, 107.61483f, 248.96884f, 24),
					new CapturedSubwayPatrolReplaySegment(3.51924, 184.99f, 107.61483f, 247.81754f, 184.62561f, 107.61483f, 242.41577f, 24),
					new CapturedSubwayPatrolReplaySegment(3.679619, 184.71169f, 107.61483f, 243.72133f, 179.04779f, 107.61483f, 241.6514f, 24),
					new CapturedSubwayPatrolReplaySegment(6.129729, 180.1915f, 107.61483f, 241.88873f, 169.74097f, 107.61483f, 242.00235f, 24),
					new CapturedSubwayPatrolReplaySegment(1.755648, 171.11412f, 107.61483f, 241.97795f, 168.77444f, 107.61483f, 245.06696f, 24),
					new CapturedSubwayPatrolReplaySegment(1.780827, 169.50375f, 107.61483f, 243.89479f, 169.1525f, 107.90041f, 247.76314f, 24),
					new CapturedSubwayPatrolReplaySegment(0.850175, 169.20848f, 107.61483f, 246.52873f, 168.77444f, 107.61483f, 245.06696f, 24),
					new CapturedSubwayPatrolReplaySegment(2.399826, 169.01f, 107.61483f, 246.42398f, 169.74097f, 107.61483f, 242.00235f, 24),
					new CapturedSubwayPatrolReplaySegment(5.834848, 169.51761f, 107.61483f, 243.18983f, 179.04779f, 107.61483f, 241.6514f, 24),
					new CapturedSubwayPatrolReplaySegment(3.720079, 177.73146f, 107.61483f, 241.89323f, 184.62561f, 107.61483f, 242.41577f, 24)
				}
			},
			{
				2035527333,
				new CapturedSubwayPatrolReplaySegment[8]
				{
					new CapturedSubwayPatrolReplaySegment(4.548876, 79.98915f, 115.765f, 315.67535f, 71.7964f, 115.61483f, 313.12946f, 24),
					new CapturedSubwayPatrolReplaySegment(1.366818, 72.959435f, 115.61483f, 313.4977f, 70.09366f, 115.61483f, 314.9619f, 24),
					new CapturedSubwayPatrolReplaySegment(3.349639, 71.19964f, 115.61483f, 314.24927f, 69.771515f, 115.61483f, 320.24707f, 24),
					new CapturedSubwayPatrolReplaySegment(3.334295, 70.01f, 115.61483f, 319.04303f, 71.35957f, 115.61483f, 325.12164f, 24),
					new CapturedSubwayPatrolReplaySegment(1.331337, 71.047325f, 115.61483f, 323.88873f, 73.74611f, 115.61483f, 325.72888f, 24),
					new CapturedSubwayPatrolReplaySegment(8.916981, 72.54694f, 115.61483f, 325.0667f, 86.76542f, 115.98234f, 322.63043f, 24),
					new CapturedSubwayPatrolReplaySegment(7.115712, 85.60774f, 115.615f, 322.84103f, 95.61554f, 115.42672f, 316.0174f, 24),
					new CapturedSubwayPatrolReplaySegment(10.41709, 94.4052f, 115.885956f, 316.85834f, 78.698235f, 115.61466f, 315.49176f, 24)
				}
			},
			{
				2035527448,
				new CapturedSubwayPatrolReplaySegment[9]
				{
					new CapturedSubwayPatrolReplaySegment(0.665506, 90.92753f, 107.61483f, 248.66034f, 93.78009f, 107.61483f, 246.55612f, 25),
					new CapturedSubwayPatrolReplaySegment(0.450008, 93.24233f, 107.61483f, 247.87915f, 96.435394f, 107.61483f, 245.37753f, 25),
					new CapturedSubwayPatrolReplaySegment(0.433567, 95.73279f, 107.61483f, 245.78152f, 96.49924f, 107.61483f, 243.00694f, 25),
					new CapturedSubwayPatrolReplaySegment(0.65, 96.66214f, 107.61483f, 244.05574f, 94.30612f, 107.61483f, 240.67899f, 25),
					new CapturedSubwayPatrolReplaySegment(0.932947, 95.30611f, 107.61483f, 241.41331f, 91.19174f, 107.61483f, 239.3239f, 25),
					new CapturedSubwayPatrolReplaySegment(0.617, 92.35083f, 107.61483f, 239.7378f, 88.45444f, 107.61483f, 240.47925f, 25),
					new CapturedSubwayPatrolReplaySegment(0.433049, 89.29336f, 107.61483f, 240.02823f, 86.97269f, 107.61483f, 243.48445f, 25),
					new CapturedSubwayPatrolReplaySegment(0.617011, 87.343765f, 107.61483f, 242.33682f, 87.59735f, 107.61483f, 246.43387f, 25),
					new CapturedSubwayPatrolReplaySegment(1.149506, 87.273026f, 107.61483f, 245.35892f, 91.899994f, 108.60483f, 249.1f, 25)
				}
			},
			{
				2035527511,
				new CapturedSubwayPatrolReplaySegment[12]
				{
					new CapturedSubwayPatrolReplaySegment(0.441025, 94.33628f, 107.61483f, 257.1328f, 95.41315f, 108.60169f, 258.46643f, 24),
					new CapturedSubwayPatrolReplaySegment(0.200505, 94.83473f, 107.61483f, 257.17294f, 95.691956f, 107.61169f, 256.58456f, 24),
					new CapturedSubwayPatrolReplaySegment(0.366001, 95.02447f, 107.61483f, 257.22272f, 94.128174f, 107.61169f, 256.4595f, 24),
					new CapturedSubwayPatrolReplaySegment(0.200507, 95.16589f, 107.61483f, 257.18698f, 94.18887f, 107.61169f, 257.9777f, 24),
					new CapturedSubwayPatrolReplaySegment(0.949525, 95.1729f, 107.61483f, 257.181f, 92.7791f, 107.61169f, 258.24213f, 24),
					new CapturedSubwayPatrolReplaySegment(0.684136, 94.0233f, 107.61483f, 257.70154f, 91.885635f, 107.61169f, 256.91193f, 24),
					new CapturedSubwayPatrolReplaySegment(0.4331, 93.08635f, 107.61483f, 257.54733f, 93.25757f, 107.61169f, 255.79121f, 24),
					new CapturedSubwayPatrolReplaySegment(0.466668, 92.77393f, 107.61483f, 257.12662f, 91.885635f, 107.61169f, 256.91193f, 24),
					new CapturedSubwayPatrolReplaySegment(0.233, 92.657005f, 107.61483f, 256.90042f, 92.7791f, 107.61169f, 258.24213f, 24),
					new CapturedSubwayPatrolReplaySegment(0.900008, 92.52411f, 107.61483f, 256.8148f, 94.18887f, 107.61169f, 257.9777f, 24),
					new CapturedSubwayPatrolReplaySegment(0.0, 93.037186f, 107.61483f, 257.22653f, 94.128174f, 107.61169f, 256.4595f, 24),
					new CapturedSubwayPatrolReplaySegment(0.833564, 93.31615f, 107.61483f, 257.31247f, 95.691956f, 107.61169f, 256.58456f, 24)
				}
			},
			{
				2035488594,
				new CapturedSubwayPatrolReplaySegment[12]
				{
					new CapturedSubwayPatrolReplaySegment(0.250007, 120.37798f, 107.61483f, 238.18799f, 120.35751f, 107.61483f, 238.59804f, 24),
					new CapturedSubwayPatrolReplaySegment(0.367001, 120.46806f, 107.61483f, 238.43642f, 119.14009f, 107.61483f, 237.27914f, 24),
					new CapturedSubwayPatrolReplaySegment(2.199574, 120.26128f, 107.61483f, 238.25618f, 120.19701f, 107.61483f, 234.03085f, 24),
					new CapturedSubwayPatrolReplaySegment(0.883575, 120.14871f, 107.61483f, 235.24371f, 121.03139f, 107.61483f, 232.7997f, 24),
					new CapturedSubwayPatrolReplaySegment(0.65, 120.5315f, 107.61483f, 233.99f, 121.69055f, 107.61483f, 232.02599f, 24),
					new CapturedSubwayPatrolReplaySegment(0.0, 121.0155f, 107.61483f, 233.10223f, 121.3f, 109.10483f, 231.7f, 24),
					new CapturedSubwayPatrolReplaySegment(0.251002, 121.14601f, 107.61483f, 232.84067f, 121.69055f, 107.61483f, 232.02599f, 24),
					new CapturedSubwayPatrolReplaySegment(0.198999, 121.29632f, 107.61483f, 232.56332f, 121.03139f, 107.61483f, 232.7997f, 24),
					new CapturedSubwayPatrolReplaySegment(1.099945, 121.39901f, 107.61483f, 232.37407f, 120.19701f, 107.61483f, 234.03085f, 24),
					new CapturedSubwayPatrolReplaySegment(2.199565, 120.92528f, 107.61483f, 232.96994f, 119.14009f, 107.61483f, 237.27914f, 24),
					new CapturedSubwayPatrolReplaySegment(0.950509, 119.64579f, 107.61483f, 235.99f, 120.35751f, 107.61483f, 238.59804f, 24),
					new CapturedSubwayPatrolReplaySegment(0.683313, 119.87553f, 107.61483f, 237.3192f, 121.137146f, 107.61483f, 239.34732f, 24)
				}
			},
			{
				2035488596,
				new CapturedSubwayPatrolReplaySegment[12]
				{
					new CapturedSubwayPatrolReplaySegment(0.750019, 122.44868f, 107.61483f, 236.74396f, 120.128296f, 107.61483f, 236.93544f, 24),
					new CapturedSubwayPatrolReplaySegment(0.883642, 121.360664f, 107.61483f, 236.61046f, 119.547424f, 107.61483f, 238.47f, 24),
					new CapturedSubwayPatrolReplaySegment(0.899767, 120.38768f, 107.61483f, 237.44711f, 120.09858f, 109.10483f, 239.65303f, 24),
					new CapturedSubwayPatrolReplaySegment(0.233, 120.12573f, 107.61483f, 238.24776f, 119.547424f, 107.61483f, 238.47f, 24),
					new CapturedSubwayPatrolReplaySegment(0.617008, 120.0289f, 107.61483f, 238.47057f, 120.128296f, 107.61483f, 236.93544f, 24),
					new CapturedSubwayPatrolReplaySegment(1.116564, 119.85016f, 107.61483f, 238.36787f, 121.6526f, 107.61483f, 235.7074f, 24),
					new CapturedSubwayPatrolReplaySegment(0.417505, 120.783905f, 107.61483f, 236.8765f, 122.73445f, 107.61483f, 236.43454f, 24),
					new CapturedSubwayPatrolReplaySegment(0.665506, 121.2745f, 107.61483f, 236.59146f, 122.99077f, 107.61483f, 237.32181f, 24),
					new CapturedSubwayPatrolReplaySegment(0.0, 121.89407f, 107.61483f, 236.68996f, 122.52649f, 107.61483f, 238.29227f, 24),
					new CapturedSubwayPatrolReplaySegment(0.200001, 122.13731f, 107.61483f, 236.88277f, 122.99077f, 107.61483f, 237.32181f, 24),
					new CapturedSubwayPatrolReplaySegment(0.250007, 122.34691f, 107.61483f, 237.05751f, 122.73445f, 107.61483f, 236.43454f, 24),
					new CapturedSubwayPatrolReplaySegment(0.683567, 122.60131f, 107.61483f, 237.15074f, 121.6526f, 107.61483f, 235.7074f, 24)
				}
			}
		};
	}
}
