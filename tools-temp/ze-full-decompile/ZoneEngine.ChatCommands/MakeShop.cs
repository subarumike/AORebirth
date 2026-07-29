using System.Collections.Generic;
using System.Data;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Statels;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.ChatCommands;

public class MakeShop : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		return true;
	}

	public override void CommandHelp(ICharacter character)
	{
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Expected O, but got Unknown
		Vendor v = Pool.Instance.GetObject<Vendor>(((IEntity)((IInstancedEntity)character).Playfield).Identity, target);
		if (v != null)
		{
			Identity identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			StatelData val = PlayfieldLoader.PFData[instance].Statels.FirstOrDefault(delegate(StatelData x)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				//IL_000f: Unknown result type (might be due to invalid IL or missing references)
				Identity identity2 = x.Identity;
				return ((object)(Identity)(ref identity2)).Equals((object)v.OriginalIdentity);
			});
			if (val != null)
			{
				identity = val.Identity;
				int num = (((Identity)(ref identity)).Instance >> 16) & 0xFF;
				identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
				int id = num | (((Identity)(ref identity)).Instance << 16);
				DBVendor val2 = new DBVendor();
				val2.Id = id;
				val2.Playfield = instance;
				val2.X = val.X;
				val2.Y = val.Y;
				val2.Z = val.Z;
				val2.HeadingX = val.HeadingX;
				val2.HeadingY = val.HeadingY;
				val2.HeadingZ = val.HeadingZ;
				val2.HeadingW = val.HeadingW;
				val2.Name = "New shop, please fill me";
				val2.TemplateId = val.TemplateId;
				val2.Hash = "";
				((Dao<DBVendor, VendorDao>)(object)Dao<DBVendor, VendorDao>.Instance).Delete(val2.Id, (IDbConnection)null, (IDbTransaction)null);
				((Dao<DBVendor, VendorDao>)(object)Dao<DBVendor, VendorDao>.Instance).Add(val2, (IDbConnection)null, (IDbTransaction)null, false);
			}
		}
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		return new List<string> { "makeshop" };
	}
}
