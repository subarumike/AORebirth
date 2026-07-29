namespace ZoneEngine.Core.Playfields;

internal static class NascenceCoreHecklerContentProvider
{
	internal const int PlayfieldInstance = 4312;

	internal const int MonsterData = 214982;

	internal const int NpcFamily = 171;

	internal const int MonsterScale = 100;

	internal const int VisualFlags = 31;

	internal const double RespawnDelaySeconds = 600.0;

	internal const string CaptureId = "20260716-071407";

	internal const string TemplateHash = "BART";

	internal const int MinDamage = 106;

	internal const int MaxDamage = 320;

	internal const int CritDamage = 411;

	internal const double RechargeSeconds = 2.0;

	internal const int SpecialAttackWeaponUnknown = 480;

	internal const int PrimaryWeaponInstance = 1145132106;

	private static readonly NascenceCoreHecklerSpawnDefinition[] Spawns = new NascenceCoreHecklerSpawnDefinition[40]
	{
		new NascenceCoreHecklerSpawnDefinition(2036330974, "Heckler of Stones", 80, 1860.2307f, 28.01437f, 1739.6364f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2036330984, "Heckler of Metals", 80, 1689.6503f, 28.500452f, 1785.0535f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2036330987, "Heckler of Earth", 80, 1735.569f, 28.400167f, 1785.2468f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2036330988, "Heckler of Earth", 80, 1737.9418f, 29.018847f, 1767.9429f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2036330989, "Heckler of Earth", 80, 1767.924f, 29.237999f, 1769.7202f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2036330990, "Heckler of Earth", 80, 1811.7034f, 28.546362f, 1756.46f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2036330991, "Heckler of Earth", 80, 1817.5297f, 28.210001f, 1726.774f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2036330998, "Heckler of Stones", 80, 1837.707f, 28.210001f, 1718.9712f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2036330999, "Heckler of Stones", 80, 1854.0144f, 28.210001f, 1700.1052f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2036331000, "Heckler of Stones", 80, 1873.8328f, 28.087164f, 1713.6605f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2036331001, "Heckler of Stones", 80, 1867.3536f, 28.352736f, 1674.8881f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2036331007, "Heckler of Stones", 80, 1892.3938f, 27.87025f, 1647.2289f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2036331009, "Heckler of Metals", 80, 1704.0978f, 28.336145f, 1765.5922f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150140, "Heckler of Earth", 80, 1525.8018f, 28.280605f, 1785.636f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150144, "Heckler of Earth", 80, 1579.7927f, 29.258543f, 1792.4231f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150157, "Heckler of Metals", 80, 1630.5618f, 28.376963f, 1772.5085f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150162, "Heckler of Metals", 80, 1656.1676f, 28.245451f, 1790.8376f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150163, "Heckler of Metals", 80, 1672.0956f, 28.715395f, 1770.736f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150276, "Heckler of Earth", 80, 1727.7806f, 39.48218f, 1419.8068f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150281, "Heckler of Earth", 80, 1747.1752f, 36.476486f, 1429.3867f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150282, "Heckler of Earth", 80, 1747.3234f, 36.269524f, 1441.886f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150287, "Heckler of Earth", 80, 1762.3103f, 34.871925f, 1429.7822f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150290, "Heckler of Earth", 80, 1766.7683f, 34.301533f, 1436.6482f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150302, "Heckler of Earth", 80, 1773.0885f, 34.032715f, 1426.3861f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150312, "Heckler of Earth", 80, 1778.3936f, 32.18948f, 1450.7524f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150315, "Heckler of Earth", 80, 1785.7625f, 32.03211f, 1433.132f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150326, "Heckler of Earth", 80, 1800.2577f, 29.263454f, 1454.294f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150333, "Heckler of Earth", 80, 1817.3789f, 28.347641f, 1459.8298f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150342, "Heckler of Earth", 80, 1800.0278f, 28.401964f, 1475.1279f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150351, "Heckler of Earth", 80, 1839.7664f, 28.021572f, 1503.1819f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150359, "Heckler of Earth", 80, 1848.904f, 28.068722f, 1489.493f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150370, "Heckler of Earth", 80, 1842.1802f, 28.289026f, 1527.4482f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150376, "Heckler of Earth", 80, 1860.3527f, 28.33673f, 1510.8966f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150399, "Heckler of Metals", 80, 1858.5728f, 28.600939f, 1538.4872f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150410, "Heckler of Metals", 80, 1869.9153f, 28.11741f, 1559.3593f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150418, "Heckler of Metals", 80, 1890.5646f, 28.181831f, 1563.3457f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150421, "Heckler of Metals", 80, 1874.8188f, 28.898687f, 1580.4562f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150443, "Heckler of Metals", 80, 1870.3834f, 28.54211f, 1600.3652f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150451, "Heckler of Metals", 80, 1888.7357f, 28.31875f, 1607.7004f, 5733, 285),
		new NascenceCoreHecklerSpawnDefinition(2037150459, "Heckler of Stones", 80, 1871.6256f, 28.45339f, 1627.991f, 5733, 285)
	};

	internal static NascenceCoreHecklerSpawnDefinition[] GetSpawns()
	{
		return (NascenceCoreHecklerSpawnDefinition[])Spawns.Clone();
	}
}
