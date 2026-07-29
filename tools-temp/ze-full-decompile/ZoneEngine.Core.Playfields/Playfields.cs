using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace ZoneEngine.Core.Playfields;

[XmlRoot("Playfields")]
public class Playfields
{
	[XmlIgnore]
	public static readonly Playfields Instance;

	[XmlElement("Playfield")]
	public List<PlayfieldInfo> playfields;

	static Playfields()
	{
		Instance = LoadXml(Path.Combine("XML Data", "Playfields.xml"));
	}

	private Playfields()
	{
	}

	public static void DumpXml(string fileName)
	{
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(Playfields));
		XmlSerializerNamespaces xmlSerializerNamespaces = new XmlSerializerNamespaces();
		xmlSerializerNamespaces.Add(string.Empty, string.Empty);
		TextWriter textWriter = new StreamWriter(fileName);
		xmlSerializer.Serialize(textWriter, Instance, xmlSerializerNamespaces);
		textWriter.Close();
	}

	public static int GetPlayfieldX(int playfieldNumber)
	{
		foreach (PlayfieldInfo playfield in Instance.playfields)
		{
			if (playfield.id == playfieldNumber)
			{
				return playfield.x;
			}
		}
		return 100000;
	}

	public static int GetPlayfieldZ(int playfieldNumber)
	{
		foreach (PlayfieldInfo playfield in Instance.playfields)
		{
			if (playfield.id == playfieldNumber)
			{
				return playfield.z;
			}
		}
		return 100000;
	}

	public static Playfields LoadXml(string fileName)
	{
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(Playfields));
		TextReader textReader = new StreamReader(fileName);
		Playfields result = (Playfields)xmlSerializer.Deserialize(textReader);
		textReader.Close();
		return result;
	}

	public static string PlayfieldIdToPlayfieldName(int playfieldId)
	{
		foreach (PlayfieldInfo playfield in Instance.playfields)
		{
			if (playfield.id == playfieldId)
			{
				return playfield.name;
			}
		}
		return string.Empty;
	}

	public static int PlayfieldNameToPlayfieldId(string playfieldName)
	{
		foreach (PlayfieldInfo playfield in Instance.playfields)
		{
			if (playfield.name == playfieldName)
			{
				return playfield.id;
			}
		}
		return 0;
	}

	public static int ResolveSuppressionGasPercent(int playfieldId)
	{
		List<DistrictInfo> districts = GetDistricts(playfieldId);
		if (districts == null || districts.Count == 0)
		{
			return 75;
		}
		if (districts.Count == 1)
		{
			return districts[0].suppressionGas;
		}
		int suppressionGas = districts[0].suppressionGas;
		for (int i = 1; i < districts.Count; i++)
		{
			if (districts[i].suppressionGas != suppressionGas)
			{
				return 75;
			}
		}
		return suppressionGas;
	}

	public static List<DistrictInfo> GetDistricts(int playfieldId)
	{
		foreach (PlayfieldInfo playfield in Instance.playfields)
		{
			if (playfield.id == playfieldId)
			{
				return playfield.districts ?? new List<DistrictInfo>();
			}
		}
		return DistrictInfo.LoadDistricts(playfieldId);
	}

	public static Dictionary<int, string> PlayfieldNames()
	{
		Dictionary<int, string> dictionary = new Dictionary<int, string>();
		foreach (PlayfieldInfo playfield in Instance.playfields)
		{
			dictionary.Add(playfield.id, playfield.name);
		}
		return dictionary;
	}

	public static bool ValidPlayfield(int playfieldId)
	{
		foreach (PlayfieldInfo playfield in Instance.playfields)
		{
			if (playfield.id == playfieldId)
			{
				return !playfield.disabled;
			}
		}
		return false;
	}

	public static bool ValidPlayfield(string playfieldName)
	{
		foreach (PlayfieldInfo playfield in Instance.playfields)
		{
			if (playfield.name == playfieldName)
			{
				return !playfield.disabled;
			}
		}
		return false;
	}
}
