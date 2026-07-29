using System;

namespace ZoneEngine.Core.Missions;

internal static class MissionTypeCatalog
{
	internal const int KillPersonIcon = 11330;

	internal const int FindPersonIcon = 11335;

	internal const int FindItemIconA = 11329;

	internal const int FindItemIconB = 11337;

	internal const int RepairMachineIcon = 11342;

	internal static int ArchetypeIndex(MissionRollType type)
	{
		return type switch
		{
			MissionRollType.KillPerson => 0, 
			MissionRollType.FindPerson => 1, 
			MissionRollType.FindItem => 2, 
			MissionRollType.RepairMachine => 3, 
			_ => 0, 
		};
	}

	internal static int IconId(MissionRollType type, int salt)
	{
		return type switch
		{
			MissionRollType.KillPerson => 11330, 
			MissionRollType.FindPerson => 11335, 
			MissionRollType.FindItem => ((salt & 1) == 0) ? 11329 : 11337, 
			MissionRollType.RepairMachine => 11342, 
			_ => 11335, 
		};
	}

	internal static MissionRollType TypeFromIcon(int missionIconId)
	{
		switch (missionIconId)
		{
		case 11330:
			return MissionRollType.KillPerson;
		case 11335:
			return MissionRollType.FindPerson;
		default:
			if (missionIconId != 11337)
			{
				if (missionIconId == 11342)
				{
					return MissionRollType.RepairMachine;
				}
				return MissionRollType.FindPerson;
			}
			goto case 11329;
		case 11329:
			return MissionRollType.FindItem;
		}
	}

	internal static string ShortTitle(MissionRollType type)
	{
		return type switch
		{
			MissionRollType.KillPerson => "Kill person mission", 
			MissionRollType.FindPerson => "Find person mission", 
			MissionRollType.FindItem => "Find item mission", 
			MissionRollType.RepairMachine => "Repair machine mission", 
			_ => "Mission", 
		};
	}

	internal static string TypeName(MissionRollType type)
	{
		return type switch
		{
			MissionRollType.KillPerson => "KillPerson", 
			MissionRollType.FindPerson => "FindPerson", 
			MissionRollType.FindItem => "FindItem", 
			MissionRollType.RepairMachine => "RepairMachine", 
			_ => "Unknown", 
		};
	}

	internal static MissionRollType[] NextMix(Random rng)
	{
		MissionRollType[] array = new MissionRollType[5];
		MissionRollType[] array2 = new MissionRollType[8]
		{
			MissionRollType.KillPerson,
			MissionRollType.KillPerson,
			MissionRollType.FindPerson,
			MissionRollType.FindPerson,
			MissionRollType.FindItem,
			MissionRollType.FindItem,
			MissionRollType.RepairMachine,
			MissionRollType.RepairMachine
		};
		for (int num = array2.Length - 1; num > 0; num--)
		{
			int num2 = rng.Next(num + 1);
			MissionRollType missionRollType = array2[num];
			array2[num] = array2[num2];
			array2[num2] = missionRollType;
		}
		for (int i = 0; i < 5; i++)
		{
			array[i] = array2[i];
		}
		if (CountDistinct(array) < 2)
		{
			array[0] = MissionRollType.KillPerson;
			array[1] = MissionRollType.FindPerson;
			array[2] = MissionRollType.FindItem;
			array[3] = MissionRollType.RepairMachine;
			array[4] = MissionRollType.FindPerson;
		}
		for (int num3 = 4; num3 > 0; num3--)
		{
			int num4 = rng.Next(num3 + 1);
			MissionRollType missionRollType2 = array[num3];
			array[num3] = array[num4];
			array[num4] = missionRollType2;
		}
		return array;
	}

	private static int CountDistinct(MissionRollType[] mix)
	{
		int num = 0;
		for (int i = 0; i < mix.Length; i++)
		{
			num |= 1 << (int)mix[i];
		}
		int num2 = 0;
		while (num != 0)
		{
			num2 += num & 1;
			num >>= 1;
		}
		return num2;
	}
}
