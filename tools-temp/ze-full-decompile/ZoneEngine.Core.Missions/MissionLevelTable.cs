using System;
using System.Globalization;
using System.IO;

namespace ZoneEngine.Core.Missions;

internal static class MissionLevelTable
{
	internal const int SliderPositions = 11;

	private const int MinLevel = 1;

	private const int MaxLevel = 220;

	private static readonly object InitLock = new object();

	private static int[][] qualityByLevel;

	private static int[] tokensByLevel;

	public static int GetMissionQuality(int characterLevel, int sliderIndex)
	{
		EnsureLoaded();
		if (qualityByLevel == null)
		{
			return 1;
		}
		int num = Clamp(characterLevel, 1, 220);
		int num2 = Clamp(sliderIndex, 0, 10);
		int[] array = qualityByLevel[num - 1];
		if (array == null)
		{
			return 1;
		}
		return array[num2];
	}

	public static int GetTokenReward(int characterLevel)
	{
		EnsureLoaded();
		if (tokensByLevel == null)
		{
			return 0;
		}
		int num = Clamp(characterLevel, 1, 220);
		return tokensByLevel[num - 1];
	}

	private static int Clamp(int value, int min, int max)
	{
		if (value < min)
		{
			return min;
		}
		return (value > max) ? max : value;
	}

	private static void EnsureLoaded()
	{
		if (qualityByLevel != null)
		{
			return;
		}
		lock (InitLock)
		{
			if (qualityByLevel != null)
			{
				return;
			}
			int[][] array = new int[220][];
			int[] array2 = new int[220];
			string text = FindDataFile("MissionLevels.csv");
			if (text == null || !File.Exists(text))
			{
				return;
			}
			string[] array3 = File.ReadAllLines(text);
			foreach (string text2 in array3)
			{
				string text3 = ((text2 == null) ? string.Empty : text2.Trim());
				if (text3.Length == 0 || text3.StartsWith("Level", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				string[] array4 = text3.Split(',');
				if (array4.Length < 13 || !int.TryParse(array4[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || result < 1 || result > 220)
				{
					continue;
				}
				int[] array5 = new int[11];
				bool flag = true;
				for (int j = 0; j < 11; j++)
				{
					if (!int.TryParse(array4[1 + j], NumberStyles.Integer, CultureInfo.InvariantCulture, out array5[j]))
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					int.TryParse(array4[12], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2);
					array[result - 1] = array5;
					array2[result - 1] = result2;
				}
			}
			tokensByLevel = array2;
			qualityByLevel = array;
		}
	}

	private static string FindDataFile(string fileName)
	{
		string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
		string[] array = new string[4]
		{
			Path.Combine(baseDirectory, "XML Data", fileName),
			Path.Combine(baseDirectory, fileName),
			Path.Combine(Directory.GetCurrentDirectory(), "XML Data", fileName),
			Path.Combine(Directory.GetCurrentDirectory(), fileName)
		};
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (File.Exists(text))
			{
				return text;
			}
		}
		return null;
	}
}
