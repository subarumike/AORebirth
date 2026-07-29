using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ZoneEngine.Core.Playfields;

public sealed class CapturedAreteRobotContentProvider
{
	public delegate void ContentLogHandler(bool isError, string message);

	private sealed class CapturedPatrolReplayCsvRow
	{
		public DateTime CapturedUtc { get; set; }

		public float StartX { get; set; }

		public float StartY { get; set; }

		public float StartZ { get; set; }

		public float EndX { get; set; }

		public float EndY { get; set; }

		public float EndZ { get; set; }
	}

	public const string RobotName = "Malfunctioning Cleaning Robot";

	public const int MonsterData = 297023;

	public const string PatrolReplayRelativePath = "Content\\Captured\\Arete\\cleaning_robot_patrol_replay.csv";

	public const string PatrolReplaySourceRelativePath = "AORebirth\\Server\\ZoneEngine\\Content\\Captured\\Arete\\cleaning_robot_patrol_replay.csv";

	public const string EvidenceCapturePatrolReplayRelativePath = "tools-temp\\AOSharpLiveCapture\\bin\\Debug\\captures\\20260719-Rex-Markus-stone\\movement-packets.csv";

	private static readonly CapturedAreteRobotSpawnDefinition[] SpawnDefinitions = new CapturedAreteRobotSpawnDefinition[7]
	{
		new CapturedAreteRobotSpawnDefinition(2038249125, 3596.8118f, 51.745f, 788.2089f, 12, 1, 6, 3596.0552f, 51.745f, 788.4825f),
		new CapturedAreteRobotSpawnDefinition(2035563702, 3596.979f, 51.745f, 783.93585f, 12, 1, 6, 3596.1052f, 51.745f, 783.9248f),
		new CapturedAreteRobotSpawnDefinition(2038289046, 3620.6807f, 51.745f, 784.9009f, 12, 1, 6, 3612.1316f, 52.5f, 787.7304f),
		new CapturedAreteRobotSpawnDefinition(2038247430, 3620.3347f, 40.984997f, 831.18134f, 12, 1, 6, 3623.6228f, 40.860813f, 826.3663f),
		new CapturedAreteRobotSpawnDefinition(2038289099, 3607.1082f, 51.735f, 777.61316f, 12, 1, 6, 3610.7073f, 52.5f, 777.934f),
		new CapturedAreteRobotSpawnDefinition(2038289052, 3610.5754f, 52.135f, 779.04f, 12, 1, 6, 3611.1985f, 52.5f, 778.22504f),
		new CapturedAreteRobotSpawnDefinition(2038289089, 3621.0847f, 51.745f, 784.295f, 12, 1, 6, 3598.0474f, 52.5f, 785.7038f)
	};

	private readonly object replayLock = new object();

	private readonly ContentLogHandler logHandler;

	private readonly string[] patrolReplayPathCandidates;

	private Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]> patrolReplaySegments;

	public CapturedAreteRobotContentProvider()
		: this(null, null)
	{
	}

	public CapturedAreteRobotContentProvider(IEnumerable<string> patrolReplayPathCandidates)
		: this(patrolReplayPathCandidates, null)
	{
	}

	public CapturedAreteRobotContentProvider(ContentLogHandler logHandler)
		: this(null, logHandler)
	{
	}

	private CapturedAreteRobotContentProvider(IEnumerable<string> patrolReplayPathCandidates, ContentLogHandler logHandler)
	{
		this.logHandler = logHandler;
		if (patrolReplayPathCandidates != null)
		{
			this.patrolReplayPathCandidates = new List<string>(patrolReplayPathCandidates).ToArray();
		}
	}

	public CapturedAreteRobotSpawnDefinition[] GetSpawnDefinitions()
	{
		CapturedAreteRobotSpawnDefinition[] array = new CapturedAreteRobotSpawnDefinition[SpawnDefinitions.Length];
		Array.Copy(SpawnDefinitions, array, SpawnDefinitions.Length);
		return array;
	}

	public CapturedAreteRobotPatrolReplaySegment[] GetPatrolReplaySegments(int sourceInstance)
	{
		Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]> patrolReplayRoutes = GetPatrolReplayRoutes();
		CapturedAreteRobotPatrolReplaySegment[] value;
		return patrolReplayRoutes.TryGetValue(sourceInstance, out value) ? value : new CapturedAreteRobotPatrolReplaySegment[0];
	}

	public string FindPatrolReplayPath()
	{
		if (patrolReplayPathCandidates != null)
		{
			for (int i = 0; i < patrolReplayPathCandidates.Length; i++)
			{
				string text = patrolReplayPathCandidates[i];
				if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
				{
					return text;
				}
			}
			return string.Empty;
		}
		DirectoryInfo directoryInfo = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		for (int j = 0; j < 8; j++)
		{
			if (directoryInfo == null)
			{
				break;
			}
			string text2 = Path.Combine(directoryInfo.FullName, "Content\\Captured\\Arete\\cleaning_robot_patrol_replay.csv");
			if (File.Exists(text2))
			{
				return text2;
			}
			text2 = Path.Combine(directoryInfo.FullName, "AORebirth\\Server\\ZoneEngine\\Content\\Captured\\Arete\\cleaning_robot_patrol_replay.csv");
			if (File.Exists(text2))
			{
				return text2;
			}
			directoryInfo = directoryInfo.Parent;
		}
		string text3 = Path.Combine(Directory.GetCurrentDirectory(), "Content\\Captured\\Arete\\cleaning_robot_patrol_replay.csv");
		if (File.Exists(text3))
		{
			return text3;
		}
		text3 = Path.Combine(Directory.GetCurrentDirectory(), "AORebirth\\Server\\ZoneEngine\\Content\\Captured\\Arete\\cleaning_robot_patrol_replay.csv");
		return File.Exists(text3) ? text3 : string.Empty;
	}

	private Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]> GetPatrolReplayRoutes()
	{
		lock (replayLock)
		{
			if (patrolReplaySegments == null)
			{
				patrolReplaySegments = LoadPatrolReplayRoutes();
			}
			return patrolReplaySegments;
		}
	}

	private Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]> LoadPatrolReplayRoutes()
	{
		Dictionary<int, List<CapturedPatrolReplayCsvRow>> dictionary = new Dictionary<int, List<CapturedPatrolReplayCsvRow>>();
		string text = FindPatrolReplayPath();
		if (string.IsNullOrWhiteSpace(text) || !File.Exists(text))
		{
			Log(isError: false, "Captured cleaning robot patrol replay CSV not found; using waypoint fallback.");
			return new Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]>();
		}
		string[] array = File.ReadAllLines(text);
		if (array.Length < 2)
		{
			return new Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]>();
		}
		string[] header = SplitCapturedMovementCsvLine(array[0]);
		int num = FindCsvColumn(header, "CapturedUtc");
		int num2 = FindCsvColumn(header, "MessageType");
		int num3 = FindCsvColumn(header, "SourceInstance");
		int num4 = FindCsvColumn(header, "FollowKind");
		int num5 = FindCsvColumn(header, "CurrentX");
		int num6 = FindCsvColumn(header, "CurrentY");
		int num7 = FindCsvColumn(header, "CurrentZ");
		int num8 = FindCsvColumn(header, "DestinationX");
		int num9 = FindCsvColumn(header, "DestinationY");
		int num10 = FindCsvColumn(header, "DestinationZ");
		if (num < 0 || num2 < 0 || num3 < 0 || num4 < 0 || num5 < 0 || num6 < 0 || num7 < 0 || num8 < 0 || num9 < 0 || num10 < 0)
		{
			Log(isError: true, "Captured cleaning robot patrol replay CSV header is missing required columns.");
			return new Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]>();
		}
		for (int i = 1; i < array.Length; i++)
		{
			string[] array2 = SplitCapturedMovementCsvLine(array[i]);
			if (array2.Length > num10 && string.Equals(array2[num2], "FollowTarget", StringComparison.OrdinalIgnoreCase) && string.Equals(array2[num4], "NpcPath", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(array2[num5]) && !string.IsNullOrWhiteSpace(array2[num8]) && int.TryParse(array2[num3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result) && DateTime.TryParse(array2[num], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result2) && float.TryParse(array2[num5], NumberStyles.Float, CultureInfo.InvariantCulture, out var result3) && float.TryParse(array2[num6], NumberStyles.Float, CultureInfo.InvariantCulture, out var result4) && float.TryParse(array2[num7], NumberStyles.Float, CultureInfo.InvariantCulture, out var result5) && float.TryParse(array2[num8], NumberStyles.Float, CultureInfo.InvariantCulture, out var result6) && float.TryParse(array2[num9], NumberStyles.Float, CultureInfo.InvariantCulture, out var result7) && float.TryParse(array2[num10], NumberStyles.Float, CultureInfo.InvariantCulture, out var result8))
			{
				if (!dictionary.TryGetValue(result, out var value))
				{
					value = (dictionary[result] = new List<CapturedPatrolReplayCsvRow>());
				}
				value.Add(new CapturedPatrolReplayCsvRow
				{
					CapturedUtc = result2,
					StartX = result3,
					StartY = result4,
					StartZ = result5,
					EndX = result6,
					EndY = result7,
					EndZ = result8
				});
			}
		}
		Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]> dictionary2 = new Dictionary<int, CapturedAreteRobotPatrolReplaySegment[]>();
		foreach (KeyValuePair<int, List<CapturedPatrolReplayCsvRow>> item in dictionary)
		{
			dictionary2[item.Key] = BuildPatrolReplay(item.Value);
		}
		return dictionary2;
	}

	private static CapturedAreteRobotPatrolReplaySegment[] BuildPatrolReplay(List<CapturedPatrolReplayCsvRow> rows)
	{
		if (rows == null || rows.Count == 0)
		{
			return new CapturedAreteRobotPatrolReplaySegment[0];
		}
		rows.Sort((CapturedPatrolReplayCsvRow x, CapturedPatrolReplayCsvRow y) => x.CapturedUtc.CompareTo(y.CapturedUtc));
		CapturedAreteRobotPatrolReplaySegment[] array = new CapturedAreteRobotPatrolReplaySegment[rows.Count];
		for (int i = 0; i < rows.Count; i++)
		{
			double delayAfterSeconds = 0.25;
			if (i + 1 < rows.Count)
			{
				delayAfterSeconds = Math.Max(0.01, (rows[i + 1].CapturedUtc - rows[i].CapturedUtc).TotalSeconds);
			}
			array[i] = new CapturedAreteRobotPatrolReplaySegment(delayAfterSeconds, rows[i].StartX, rows[i].StartY, rows[i].StartZ, rows[i].EndX, rows[i].EndY, rows[i].EndZ);
		}
		return array;
	}

	private static int FindCsvColumn(string[] header, string name)
	{
		for (int i = 0; i < header.Length; i++)
		{
			if (string.Equals(header[i], name, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		return -1;
	}

	private static string[] SplitCapturedMovementCsvLine(string line)
	{
		List<string> list = new List<string>();
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = false;
		for (int i = 0; i < line.Length; i++)
		{
			char c = line[i];
			switch (c)
			{
			case '"':
				if (flag && i + 1 < line.Length && line[i + 1] == '"')
				{
					stringBuilder.Append('"');
					i++;
				}
				else
				{
					flag = !flag;
				}
				continue;
			case ',':
				if (!flag)
				{
					list.Add(stringBuilder.ToString());
					stringBuilder.Length = 0;
					continue;
				}
				break;
			}
			stringBuilder.Append(c);
		}
		list.Add(stringBuilder.ToString());
		return list.ToArray();
	}

	private void Log(bool isError, string message)
	{
		if (logHandler != null)
		{
			logHandler(isError, message);
		}
	}
}
