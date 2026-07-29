using System.Collections.Generic;
using System.Data;
using System.Data.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Vector;
using AORebirth.Database.Dao;
using AORebirth.Database.Entities;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Controllers;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Script;

namespace ZoneEngine.ChatCommands;

public class Npc : AOChatCommand
{
	public override bool CheckCommandArguments(string[] args)
	{
		return args.Length >= 2 && args.Length <= 3;
	}

	public override void CommandHelp(ICharacter character)
	{
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "/npc [save|despawn|delete] with targeted mob\nand /npc knubot <scriptname>", 0, 0));
	}

	public override void ExecuteCommand(ICharacter character, Identity target, string[] args)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Invalid comparison between Unknown and I4
		//IL_035b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0365: Invalid comparison between Unknown and I4
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_047f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0484: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0508: Unknown result type (might be due to invalid IL or missing references)
		//IL_050d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0524: Unknown result type (might be due to invalid IL or missing references)
		//IL_0529: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Expected O, but got Unknown
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_063e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0306: Unknown result type (might be due to invalid IL or missing references)
		//IL_0314: Unknown result type (might be due to invalid IL or missing references)
		//IL_0327: Expected O, but got Unknown
		string text = args[1].ToLower();
		Identity identity;
		if (text == "save")
		{
			if ((int)((Identity)(ref target)).Type != 50000)
			{
				((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Target a mob first.", 0, 0));
				return;
			}
			Character @object = Pool.Instance.GetObject<Character>(((IEntity)((IInstancedEntity)character).Playfield).Identity, target);
			if (@object == null)
			{
				((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Not a NPC?", 0, 0));
				return;
			}
			if (!(((Dynel)@object).Controller is NPCController))
			{
				((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Don't try to remove/save other players please.", 0, 0));
				return;
			}
			identity = ((PooledObject)@object).Identity;
			if (((Identity)(ref identity)).Instance >= 1000000)
			{
				CharacterDao instance = Dao<DBCharacter, CharacterDao>.Instance;
				identity = ((PooledObject)@object).Identity;
				if (((Dao<DBCharacter, CharacterDao>)(object)instance).Get(((Identity)(ref identity)).Instance) == null)
				{
					DBMobSpawn val = new DBMobSpawn();
					identity = ((PooledObject)@object).Identity;
					val.Id = ((Identity)(ref identity)).Instance;
					val.Name = ((Dynel)@object).Name;
					val.Textures0 = 0;
					val.Textures1 = 0;
					val.Textures2 = 0;
					val.Textures3 = 0;
					val.Textures4 = 0;
					identity = ((IEntity)((Dynel)@object).Playfield).Identity;
					val.Playfield = ((Identity)(ref identity)).Instance;
					Coordinate val2 = ((Dynel)@object).Coordinates();
					val.X = val2.x;
					val.Y = val2.y;
					val.Z = val2.z;
					val.HeadingW = ((Dynel)@object).Heading.wf;
					val.HeadingX = ((Dynel)@object).Heading.xf;
					val.HeadingY = ((Dynel)@object).Heading.yf;
					val.HeadingZ = ((Dynel)@object).Heading.zf;
					if (@object.Waypoints.Count > 0)
					{
						List<MobSpawnWaypoint> mobWaypoints = GetMobWaypoints(@object);
						val.Waypoints = new Binary(MessagePackZip.SerializeData<MobSpawnWaypoint>(mobWaypoints));
					}
					if (((Dao<DBMobSpawn, MobSpawnDao>)(object)Dao<DBMobSpawn, MobSpawnDao>.Instance).Exists(val.Id))
					{
						((Dao<DBMobSpawn, MobSpawnDao>)(object)Dao<DBMobSpawn, MobSpawnDao>.Instance).Delete(val.Id, (IDbConnection)null, (IDbTransaction)null);
					}
					Dao<DBMobSpawn, MobSpawnDao>.Instance.Add(val);
					((Dao<DBMobSpawnStat, MobSpawnStatDao>)(object)Dao<DBMobSpawnStat, MobSpawnStatDao>.Instance).Delete((object)new { val.Id, val.Playfield }, (IDbConnection)null, (IDbTransaction)null);
					Dictionary<int, uint> statValues = ((Dynel)@object).Stats.GetStatValues();
					foreach (KeyValuePair<int, uint> item in statValues)
					{
						MobSpawnStatDao instance2 = Dao<DBMobSpawnStat, MobSpawnStatDao>.Instance;
						DBMobSpawnStat val3 = new DBMobSpawnStat();
						identity = ((PooledObject)@object).Identity;
						val3.Id = ((Identity)(ref identity)).Instance;
						identity = ((IEntity)((Dynel)@object).Playfield).Identity;
						val3.Playfield = ((Identity)(ref identity)).Instance;
						val3.Stat = item.Key;
						val3.Value = (int)item.Value;
						instance2.Add(val3);
					}
					goto IL_0344;
				}
			}
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Refusing to save a mob spawn with a player/low-range identity.", 0, 0));
			return;
		}
		goto IL_0344;
		IL_0344:
		if (text == "remove")
		{
			if ((int)((Identity)(ref target)).Type != 50000)
			{
				((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Target a mob first.", 0, 0));
				return;
			}
			ICharacter object2 = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)character).Playfield).Identity, target);
			if (object2 != null && !(((IDynel)object2).Controller is NPCController))
			{
				((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Refusing to remove a mob spawn using a player target.", 0, 0));
				return;
			}
			if (((Dao<DBCharacter, CharacterDao>)(object)Dao<DBCharacter, CharacterDao>.Instance).Get(((Identity)(ref target)).Instance) != null)
			{
				((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Refusing to remove a mob spawn with a player character id.", 0, 0));
				return;
			}
			((Dao<DBMobSpawn, MobSpawnDao>)(object)Dao<DBMobSpawn, MobSpawnDao>.Instance).Delete(((Identity)(ref target)).Instance, (IDbConnection)null, (IDbTransaction)null);
		}
		if (!(text == "knubot"))
		{
			return;
		}
		if (args.Length < 3)
		{
			CommandHelp(character);
			return;
		}
		ICharacter object3 = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)character).Playfield).Identity, target);
		if (object3 == null)
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, $"Target {((Identity)(ref target)).ToString(true)} is no npc.", 0, 0));
			return;
		}
		if (!(((IDynel)object3).Controller is NPCController))
		{
			((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Don't try to attach NPC scripts to players please.", 0, 0));
			return;
		}
		identity = ((IEntity)object3).Identity;
		if (((Identity)(ref identity)).Instance >= 1000000)
		{
			CharacterDao instance3 = Dao<DBCharacter, CharacterDao>.Instance;
			identity = ((IEntity)object3).Identity;
			if (((Dao<DBCharacter, CharacterDao>)(object)instance3).Get(((Identity)(ref identity)).Instance) == null)
			{
				string scriptname = args[2];
				scriptname = ScriptCompiler.Instance.ClassExists(scriptname);
				if (scriptname != "")
				{
					DBMobSpawn val4 = ((Dao<DBMobSpawn, MobSpawnDao>)(object)Dao<DBMobSpawn, MobSpawnDao>.Instance).Get(((Identity)(ref target)).Instance);
					if (val4 == null)
					{
						((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, $"Target npc {((Identity)(ref target)).ToString(true)} is not yet saved to mobspawn table.", 0, 0));
						return;
					}
					val4.KnuBotScriptName = scriptname;
					((Dao<DBMobSpawn, MobSpawnDao>)(object)Dao<DBMobSpawn, MobSpawnDao>.Instance).Save(val4, (object)null, (IDbConnection)null, (IDbTransaction)null);
					((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, $"Saved initialization script '{args[2]}' for spawn {((Identity)(ref target)).ToString(true)}.", 0, 0));
					((NPCController)(object)((IDynel)object3).Controller).SetKnuBot(ScriptCompiler.Instance.CreateKnuBot(scriptname, ((IEntity)object3).Identity));
				}
				else
				{
					((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, $"Script '{args[2]}' does not exist.", 0, 0));
				}
				return;
			}
		}
		((IInstancedEntity)character).Playfield.Publish((object)BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.CreateIM(character, "Refusing to script a mob spawn with a player/low-range identity.", 0, 0));
	}

	private List<MobSpawnWaypoint> GetMobWaypoints(Character mob)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		List<MobSpawnWaypoint> list = new List<MobSpawnWaypoint>();
		foreach (Waypoint waypoint in mob.Waypoints)
		{
			MobSpawnWaypoint val = new MobSpawnWaypoint();
			Identity identity = ((PooledObject)mob).Identity;
			val.Identity = ((Identity)(ref identity)).Instance;
			identity = ((IEntity)((Dynel)mob).Playfield).Identity;
			val.Playfield = ((Identity)(ref identity)).Instance;
			val.WalkMode = (waypoint.Running ? 1 : 0);
			val.X = waypoint.Position.xf;
			val.Y = waypoint.Position.yf;
			val.Z = waypoint.Position.zf;
			list.Add(val);
		}
		return list;
	}

	public override int GMLevelNeeded()
	{
		return 1;
	}

	public override List<string> ListCommands()
	{
		return new List<string> { "npc" };
	}
}
