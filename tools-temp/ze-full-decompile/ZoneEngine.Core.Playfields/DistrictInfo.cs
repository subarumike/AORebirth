using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace ZoneEngine.Core.Playfields;

public class DistrictInfo
{
	[XmlElement("Name")]
	public string districtName = "Nameless District";

	[XmlAttribute("MaxLevel")]
	public int maxLevel;

	[XmlAttribute("MinLevel")]
	public int minLevel;

	[XmlAttribute("SuppressionGas")]
	public int suppressionGas = 100;

	public static void DumpXML(string fileName, PlayfieldInfo pfInfo)
	{
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<DistrictInfo>), new XmlRootAttribute("Districts"));
		XmlSerializerNamespaces xmlSerializerNamespaces = new XmlSerializerNamespaces();
		xmlSerializerNamespaces.Add(string.Empty, string.Empty);
		MemoryStream memoryStream = new MemoryStream();
		xmlSerializer.Serialize(memoryStream, pfInfo.districts, xmlSerializerNamespaces);
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.LoadXml(Encoding.ASCII.GetString(memoryStream.GetBuffer()));
		memoryStream.Dispose();
		xmlDocument.DocumentElement.SetAttribute("Playfield", pfInfo.id.ToString());
		xmlDocument.Save(fileName);
	}

	public static List<DistrictInfo> LoadDistricts(int pf)
	{
		string path = Path.Combine("XML Data", "Districts");
		path = Path.Combine(path, pf + ".xml");
		if (File.Exists(path))
		{
			return LoadXML(path);
		}
		return new List<DistrictInfo>();
	}

	public static List<DistrictInfo> LoadXML(string fileName)
	{
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<DistrictInfo>), new XmlRootAttribute("Districts"));
		TextReader textReader = new StreamReader(fileName);
		List<DistrictInfo> result = (List<DistrictInfo>)xmlSerializer.Deserialize(textReader);
		textReader.Close();
		return result;
	}
}
