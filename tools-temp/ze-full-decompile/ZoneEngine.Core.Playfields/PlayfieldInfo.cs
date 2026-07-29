using System.Collections.Generic;
using System.Xml.Serialization;

namespace ZoneEngine.Core.Playfields;

public class PlayfieldInfo
{
	[XmlAttribute("disabled")]
	public bool disabled;

	[XmlIgnore]
	public List<DistrictInfo> districts;

	[XmlAttribute("expansion")]
	public int expansion;

	[XmlElement("Name")]
	public string name = string.Empty;

	[XmlAttribute("x")]
	public int x = 100000;

	[XmlAttribute("xscale")]
	public float xscale = 1f;

	[XmlAttribute("z")]
	public int z = 100000;

	[XmlAttribute("zscale")]
	public float zscale = 1f;

	private int _id;

	[XmlAttribute("id")]
	public int id
	{
		get
		{
			return _id;
		}
		set
		{
			_id = value;
			districts = DistrictInfo.LoadDistricts(_id);
		}
	}
}
