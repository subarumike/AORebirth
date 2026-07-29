using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Items;
using MsgPack;

namespace ZoneEngine.Core.Perks;

public static class PerkCatalog
{
	private static readonly object Sync = new object();

	private static Dictionary<int, PerkDefinition> byPacketId;

	private static Dictionary<int, PerkDefinition> byActionHash;

	public static IEnumerable<PerkDefinition> All
	{
		get
		{
			EnsureLoaded();
			return byPacketId.Values;
		}
	}

	public static bool TryGet(int packetId, out PerkDefinition definition)
	{
		EnsureLoaded();
		if (!byPacketId.TryGetValue(packetId, out definition))
		{
			return false;
		}
		if (!definition.GrantsPerkAction)
		{
			TryResolveActionFromItem(definition);
		}
		return true;
	}

	public static bool TryGetByActionHash(int actionHash, out PerkDefinition definition)
	{
		EnsureLoaded();
		if (byActionHash != null && byActionHash.TryGetValue(actionHash, out definition))
		{
			return true;
		}
		definition = null;
		return false;
	}

	private static void EnsureLoaded()
	{
		if (byPacketId != null)
		{
			return;
		}
		lock (Sync)
		{
			if (byPacketId == null)
			{
				byPacketId = new Dictionary<int, PerkDefinition>();
				byActionHash = new Dictionary<int, PerkDefinition>();
				LoadPerksXml();
				LoadPerkActionsCsv();
			}
		}
	}

	private static void LoadPerksXml()
	{
		string text = FindDataFile("Perks.xml");
		if (text == null || !File.Exists(text))
		{
			return;
		}
		XDocument xDocument = XDocument.Load(text);
		foreach (XElement item in xDocument.Descendants("Perk"))
		{
			string text2 = (string)item.Attribute("PacketID");
			string text3 = (string)item.Attribute("AOID");
			string name = ((string)item.Attribute("Name")) ?? string.Empty;
			if (!string.IsNullOrEmpty(text2) && int.TryParse(text2, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				int result2 = 0;
				if (!string.IsNullOrEmpty(text3))
				{
					int.TryParse(text3, NumberStyles.Integer, CultureInfo.InvariantCulture, out result2);
				}
				byPacketId[result] = new PerkDefinition
				{
					PacketId = result,
					Aoid = result2,
					Name = name
				};
			}
		}
	}

	private static void LoadPerkActionsCsv()
	{
		string text = FindDataFile("PerkActions.csv");
		if (text == null || !File.Exists(text))
		{
			return;
		}
		string[] array = File.ReadAllLines(text);
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i];
			if ((i == 0 && text2.StartsWith("PacketId", StringComparison.OrdinalIgnoreCase)) || string.IsNullOrWhiteSpace(text2))
			{
				continue;
			}
			string[] array2 = SplitCsv(text2);
			if (array2.Length >= 5 && int.TryParse(array2[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && int.TryParse(array2[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2) && int.TryParse(array2[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result3) && int.TryParse(array2[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result4))
			{
				string fourChars = array2[3];
				int num = Hash(fourChars);
				if (!byPacketId.TryGetValue(result, out var value))
				{
					value = new PerkDefinition
					{
						PacketId = result,
						Aoid = result2,
						Name = ((array2.Length > 5) ? Unquote(array2[5]) : ("Perk " + result))
					};
					byPacketId[result] = value;
				}
				value.ActionTemplateId = result3;
				value.ActionHash = num;
				value.ActionSlotIdOverride = result4;
				if (value.Aoid == 0)
				{
					value.Aoid = result2;
				}
				byActionHash[num] = value;
			}
		}
	}

	private static void TryResolveActionFromItem(PerkDefinition def)
	{
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		if (def == null || def.Aoid <= 0 || ItemLoader.ItemList == null || ItemLoader.ItemList.Count == 0 || !ItemLoader.ItemList.TryGetValue(def.Aoid, out var value) || value.Events == null)
		{
			return;
		}
		foreach (Event @event in value.Events)
		{
			foreach (Function function in @event.Functions)
			{
				if (function.FunctionType != 53182 || function.Arguments == null || function.Arguments.Values == null || function.Arguments.Values.Count < 4)
				{
					continue;
				}
				string text = AsString(function.Arguments.Values[1]);
				int num = AsInt(function.Arguments.Values[3]);
				if (string.IsNullOrEmpty(text) || text.Length != 4 || num <= 0)
				{
					continue;
				}
				int num2 = AsInt(function.Arguments.Values[0]);
				def.ActionTemplateId = num;
				def.ActionHash = Hash(text);
				if (num2 > 0)
				{
					def.ActionSlotIdOverride = num2;
				}
				byActionHash[def.ActionHash.Value] = def;
				return;
			}
		}
	}

	private static int AsInt(MessagePackObject o)
	{
		if (((MessagePackObject)(ref o)).IsTypeOf(typeof(int)) == true)
		{
			return ((MessagePackObject)(ref o)).AsInt32();
		}
		if (((MessagePackObject)(ref o)).IsTypeOf(typeof(string)) == true && int.TryParse(((MessagePackObject)(ref o)).AsStringUtf8(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return 0;
	}

	private static string AsString(MessagePackObject o)
	{
		if (((MessagePackObject)(ref o)).IsTypeOf(typeof(string)) == true)
		{
			return ((MessagePackObject)(ref o)).AsStringUtf8();
		}
		return string.Empty;
	}

	private static string[] SplitCsv(string line)
	{
		List<string> list = new List<string>();
		bool flag = false;
		StringBuilder stringBuilder = new StringBuilder();
		foreach (char c in line)
		{
			switch (c)
			{
			case '"':
				flag = !flag;
				continue;
			case ',':
				if (!flag)
				{
					list.Add(stringBuilder.ToString());
					stringBuilder.Clear();
					continue;
				}
				break;
			}
			stringBuilder.Append(c);
		}
		list.Add(stringBuilder.ToString());
		return list.ToArray();
	}

	private static string Unquote(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return s;
		}
		return s.Trim().Trim('"').Replace("''", "\"");
	}

	private static string FindDataFile(string fileName)
	{
		string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
		string[] array = new string[3]
		{
			Path.Combine(baseDirectory, "XML Data", fileName),
			Path.Combine(baseDirectory, fileName),
			Path.Combine(Directory.GetCurrentDirectory(), "XML Data", fileName)
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

	private static int Hash(string fourChars)
	{
		if (fourChars == null || fourChars.Length != 4)
		{
			throw new ArgumentException("Action hash must be 4 ASCII chars.", "fourChars");
		}
		return (int)(((uint)fourChars[0] << 24) | ((uint)fourChars[1] << 16) | ((uint)fourChars[2] << 8) | fourChars[3]);
	}
}
