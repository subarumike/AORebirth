using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using AORebirth.Core.Entities;
using AORebirth.Core.Events;
using AORebirth.Core.Functions;
using AORebirth.Core.Playfields;
using AORebirth.Core.Statels;
using AORebirth.Core.Vector;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using MsgPack;
using SmokeLounge.AOtomation.Messaging.GameData;
using Utility;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.ChatCommands;

public class tptoproxy : AOChatCommand
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
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_0354: Unknown result type (might be due to invalid IL or missing references)
		//IL_0359: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0543: Unknown result type (might be due to invalid IL or missing references)
		//IL_056c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0579: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a7: Expected O, but got Unknown
		//IL_05a7: Expected O, but got Unknown
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_020e: Expected O, but got Unknown
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		//IL_023d: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0264: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Expected I4, but got Unknown
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0424: Unknown result type (might be due to invalid IL or missing references)
		//IL_0429: Unknown result type (might be due to invalid IL or missing references)
		//IL_0461: Unknown result type (might be due to invalid IL or missing references)
		//IL_0481: Unknown result type (might be due to invalid IL or missing references)
		//IL_048e: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_04cc: Expected O, but got Unknown
		//IL_04cc: Expected O, but got Unknown
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		IEnumerable<DBTeleport> all = ((Dao<DBTeleport, TeleportDao>)(object)Dao<DBTeleport, TeleportDao>.Instance).GetAll((object)null);
		List<StatelData> list = new List<StatelData>();
		Function val = null;
		foreach (PlayfieldData value in PlayfieldLoader.PFData.Values)
		{
			bool flag = false;
			foreach (StatelData sd in value.Statels)
			{
				val = null;
				flag = false;
				MessagePackObject val2;
				if (sd.Events.Count > 0)
				{
					foreach (Event @event in sd.Events)
					{
						foreach (Function function in @event.Functions)
						{
							if (function.FunctionType == 53082)
							{
								flag = true;
								val = function;
								val2 = function.Arguments.Values[1];
								if (((MessagePackObject)(ref val2)).AsInt32() < 0)
								{
									val = null;
									flag = false;
								}
								break;
							}
						}
						if (flag)
						{
							break;
						}
					}
				}
				Identity val6;
				if (flag && !all.Any(delegate(DBTeleport x)
				{
					//IL_000c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0011: Unknown result type (might be due to invalid IL or missing references)
					//IL_0014: Unknown result type (might be due to invalid IL or missing references)
					//IL_0019: Invalid comparison between I4 and Unknown
					//IL_0027: Unknown result type (might be due to invalid IL or missing references)
					//IL_002c: Unknown result type (might be due to invalid IL or missing references)
					int statelType2 = x.statelType;
					Identity identity6 = sd.Identity;
					int result2;
					if (statelType2 == (int)((Identity)(ref identity6)).Type)
					{
						uint statelInstance2 = x.statelInstance;
						identity6 = sd.Identity;
						if (statelInstance2 == (uint)((Identity)(ref identity6)).Instance)
						{
							result2 = ((x.playfield == sd.PlayfieldId) ? 1 : 0);
							goto IL_004c;
						}
					}
					result2 = 0;
					goto IL_004c;
					IL_004c:
					return (byte)result2 != 0;
				}))
				{
					Dictionary<int, PlayfieldData> pFData = PlayfieldLoader.PFData;
					val2 = val.Arguments.Values[1];
					PlayfieldData val3 = pFData[((MessagePackObject)(ref val2)).AsInt32()];
					if (val3.Statels.Count(delegate(StatelData x)
					{
						//IL_0001: Unknown result type (might be due to invalid IL or missing references)
						//IL_0006: Unknown result type (might be due to invalid IL or missing references)
						//IL_0009: Unknown result type (might be due to invalid IL or missing references)
						//IL_0013: Invalid comparison between Unknown and I4
						Identity identity5 = x.Identity;
						return (int)((Identity)(ref identity5)).Type == 51016;
					}) == 1)
					{
						StatelData val4 = val3.Statels.First(delegate(StatelData x)
						{
							//IL_0001: Unknown result type (might be due to invalid IL or missing references)
							//IL_0006: Unknown result type (might be due to invalid IL or missing references)
							//IL_0009: Unknown result type (might be due to invalid IL or missing references)
							//IL_0013: Invalid comparison between Unknown and I4
							Identity identity4 = x.Identity;
							return (int)((Identity)(ref identity4)).Type == 51016;
						});
						DBTeleport val5 = new DBTeleport();
						val5.playfield = sd.PlayfieldId;
						val5.statelType = 51016;
						val6 = sd.Identity;
						val5.statelInstance = (uint)((Identity)(ref val6)).Instance;
						val5.destinationPlayfield = val4.PlayfieldId;
						val6 = val4.Identity;
						val5.destinationType = (int)((Identity)(ref val6)).Type;
						val6 = val4.Identity;
						val5.destinationInstance = BitConverter.ToUInt32(BitConverter.GetBytes(((Identity)(ref val6)).Instance), 0);
						IEnumerable<DBTeleport> where = ((Dao<DBTeleport, TeleportDao>)(object)Dao<DBTeleport, TeleportDao>.Instance).GetWhere((object)new { val5.playfield, val5.statelType, val5.statelInstance }, (IDbConnection)null, (IDbTransaction)null);
						foreach (DBTeleport item in where)
						{
							((Dao<DBTeleport, TeleportDao>)(object)Dao<DBTeleport, TeleportDao>.Instance).Delete(item.Id, (IDbConnection)null, (IDbTransaction)null);
						}
						((Dao<DBTeleport, TeleportDao>)(object)Dao<DBTeleport, TeleportDao>.Instance).Add(val5, (IDbConnection)null, (IDbTransaction)null, true);
						flag = false;
					}
				}
				if (!flag || all.Any(delegate(DBTeleport x)
				{
					//IL_000c: Unknown result type (might be due to invalid IL or missing references)
					//IL_0011: Unknown result type (might be due to invalid IL or missing references)
					//IL_0014: Unknown result type (might be due to invalid IL or missing references)
					//IL_0019: Invalid comparison between I4 and Unknown
					//IL_0027: Unknown result type (might be due to invalid IL or missing references)
					//IL_002c: Unknown result type (might be due to invalid IL or missing references)
					int statelType = x.statelType;
					Identity identity3 = sd.Identity;
					int result;
					if (statelType == (int)((Identity)(ref identity3)).Type)
					{
						uint statelInstance = x.statelInstance;
						identity3 = sd.Identity;
						if (statelInstance == (uint)((Identity)(ref identity3)).Instance)
						{
							result = ((x.playfield == sd.PlayfieldId) ? 1 : 0);
							goto IL_004c;
						}
					}
					result = 0;
					goto IL_004c;
					IL_004c:
					return (byte)result != 0;
				}))
				{
					continue;
				}
				Dictionary<int, PlayfieldData> pFData2 = PlayfieldLoader.PFData;
				val2 = val.Arguments.Values[1];
				PlayfieldData val7 = pFData2[((MessagePackObject)(ref val2)).AsInt32()];
				StatelData val8 = null;
				if (val7.Statels.Count(delegate(StatelData x)
				{
					//IL_0001: Unknown result type (might be due to invalid IL or missing references)
					//IL_0006: Unknown result type (might be due to invalid IL or missing references)
					//IL_0009: Unknown result type (might be due to invalid IL or missing references)
					//IL_0013: Invalid comparison between Unknown and I4
					Identity identity2 = x.Identity;
					return (int)((Identity)(ref identity2)).Type == 51016;
				}) > 0)
				{
					val8 = val7.Statels.First(delegate(StatelData x)
					{
						//IL_0001: Unknown result type (might be due to invalid IL or missing references)
						//IL_0006: Unknown result type (might be due to invalid IL or missing references)
						//IL_0009: Unknown result type (might be due to invalid IL or missing references)
						//IL_0013: Invalid comparison between Unknown and I4
						Identity identity = x.Identity;
						return (int)((Identity)(ref identity)).Type == 51016;
					});
					string text = sd.PlayfieldId.ToString();
					val6 = sd.Identity;
					LogUtil.Debug((DebugInfoDetail)512, text + " " + ((Identity)(ref val6)).ToString(true));
					IStat obj = ((IStats)character).Stats[(StatIds)193];
					val6 = sd.Identity;
					obj.BaseValue = (uint)((Identity)(ref val6)).Instance;
					((IStats)character).Stats[(StatIds)192].BaseValue = (uint)sd.PlayfieldId;
					IPlayfield playfield = ((IInstancedEntity)character).Playfield;
					Dynel val9 = (Dynel)character;
					Coordinate val10 = new Coordinate(val8.X, val8.Y + 1f, val8.Z);
					Quaternion heading = ((IDynel)character).Heading;
					val6 = default(Identity);
					val2 = val.Arguments.Values[0];
					((Identity)(ref val6)).Type = (IdentityType)((MessagePackObject)(ref val2)).AsInt32();
					((Identity)(ref val6)).Instance = val8.PlayfieldId;
					playfield.Teleport(val9, val10, (IQuaternion)(object)heading, val6);
				}
				else
				{
					string text2 = sd.PlayfieldId.ToString();
					val6 = sd.Identity;
					LogUtil.Debug((DebugInfoDetail)512, text2 + " " + ((Identity)(ref val6)).ToString(true));
					((IStats)character).Stats[(StatIds)193].BaseValue = 0u;
					((IStats)character).Stats[(StatIds)192].BaseValue = 0u;
					IPlayfield playfield2 = ((IInstancedEntity)character).Playfield;
					Dynel val11 = (Dynel)character;
					Coordinate val12 = new Coordinate(sd.X, sd.Y, sd.Z);
					Quaternion heading2 = ((IDynel)character).Heading;
					val6 = default(Identity);
					((Identity)(ref val6)).Type = (IdentityType)51101;
					((Identity)(ref val6)).Instance = sd.PlayfieldId;
					playfield2.Teleport(val11, val12, (IQuaternion)(object)heading2, val6);
				}
				return;
			}
		}
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		return new List<string> { "tpt", "tp2" };
	}
}
