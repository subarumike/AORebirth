using System;
using System.Collections.Generic;
using System.Linq;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core.Controllers;

internal static class TeamRuntime
{
	private static readonly object Sync = new object();

	private static readonly Dictionary<int, Identity> PendingInvites = new Dictionary<int, Identity>();

	private static readonly Dictionary<int, List<Identity>> TeamMembers = new Dictionary<int, List<Identity>>();

	private static readonly Dictionary<int, int> CharacterTeams = new Dictionary<int, int>();

	private static int nextTeamId = 1;

	public static bool Invite(ICharacter inviter, Identity targetIdentity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		ICharacter val = ResolveOnlineCharacter(inviter, targetIdentity);
		if (val != null)
		{
			Identity identity = ((IEntity)val).Identity;
			if (!((object)(Identity)(ref identity)).Equals((object)((IEntity)inviter).Identity))
			{
				lock (Sync)
				{
					Dictionary<int, Identity> pendingInvites = PendingInvites;
					identity = ((IEntity)val).Identity;
					pendingInvites[((Identity)(ref identity)).Instance] = ((IEntity)inviter).Identity;
				}
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(inviter, "Team invite sent to " + ((INamedEntity)val).Name + ".", 0, 0);
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(val, ((INamedEntity)inviter).Name + " invited you to a team. Use /team accept or /team decline.", 0, 0);
				identity = ((IEntity)inviter).Identity;
				string text = ((Identity)(ref identity)).ToString(true);
				identity = ((IEntity)val).Identity;
				LogUtil.Debug((DebugInfoDetail)128, "Team invite pending inviter=" + text + " target=" + ((Identity)(ref identity)).ToString(true));
				return true;
			}
		}
		BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(inviter, "Team invite target is not available.", 0, 0);
		return false;
	}

	public static bool Reply(ICharacter character, bool accept, Identity requester)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		Identity value;
		lock (Sync)
		{
			Dictionary<int, Identity> pendingInvites = PendingInvites;
			Identity identity = ((IEntity)character).Identity;
			if (!pendingInvites.TryGetValue(((Identity)(ref identity)).Instance, out value))
			{
				value = requester;
			}
			Dictionary<int, Identity> pendingInvites2 = PendingInvites;
			identity = ((IEntity)character).Identity;
			pendingInvites2.Remove(((Identity)(ref identity)).Instance);
		}
		if (((object)(Identity)(ref value)).Equals((object)Identity.None))
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "No pending team invite.", 0, 0);
			return false;
		}
		ICharacter val = ResolveOnlineCharacter(character, value);
		if (val == null)
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "The team inviter is no longer available.", 0, 0);
			return false;
		}
		if (!accept)
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Team invite declined.", 0, 0);
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(val, ((INamedEntity)character).Name + " declined your team invite.", 0, 0);
			return true;
		}
		Join(val, character);
		return true;
	}

	public static bool AcceptDirect(ICharacter leader, Identity newMemberIdentity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		ICharacter val = ResolveOnlineCharacter(leader, newMemberIdentity);
		if (val == null)
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(leader, "Team member is not available.", 0, 0);
			return false;
		}
		Join(leader, val);
		return true;
	}

	public static bool RejectDirect(ICharacter inviter, Identity rejectingIdentity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		ICharacter val = ResolveOnlineCharacter(inviter, rejectingIdentity);
		if (val != null)
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(inviter, ((INamedEntity)val).Name + " declined your team invite.", 0, 0);
		}
		return true;
	}

	public static bool Leave(ICharacter character)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		int value;
		List<Identity> list;
		lock (Sync)
		{
			Dictionary<int, int> characterTeams = CharacterTeams;
			Identity identity = ((IEntity)character).Identity;
			if (!characterTeams.TryGetValue(((Identity)(ref identity)).Instance, out value))
			{
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "You are not in a team.", 0, 0);
				return false;
			}
			list = TeamMembers[value];
			list.RemoveAll(delegate(Identity x)
			{
				//IL_000d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0012: Unknown result type (might be due to invalid IL or missing references)
				int instance = ((Identity)(ref x)).Instance;
				Identity identity2 = ((IEntity)character).Identity;
				return instance == ((Identity)(ref identity2)).Instance;
			});
			Dictionary<int, int> characterTeams2 = CharacterTeams;
			identity = ((IEntity)character).Identity;
			characterTeams2.Remove(((Identity)(ref identity)).Instance);
			if (list.Count == 0)
			{
				TeamMembers.Remove(value);
			}
		}
		ApplyTeamStats(character, 0, 1);
		BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "You left the team.", 0, 0);
		NotifyMembers(list, ((INamedEntity)character).Name + " left the team.");
		UpdateTeamMemberStats(value);
		return true;
	}

	public static bool Kick(ICharacter leader, Identity targetIdentity)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		ICharacter val = ResolveOnlineCharacter(leader, targetIdentity);
		if (val == null)
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(leader, "Team kick target is not available.", 0, 0);
			return false;
		}
		lock (Sync)
		{
			Dictionary<int, int> characterTeams = CharacterTeams;
			Identity identity = ((IEntity)leader).Identity;
			if (!characterTeams.TryGetValue(((Identity)(ref identity)).Instance, out var value))
			{
				goto IL_00a2;
			}
			Dictionary<int, int> characterTeams2 = CharacterTeams;
			identity = ((IEntity)val).Identity;
			if (!characterTeams2.ContainsKey(((Identity)(ref identity)).Instance))
			{
				goto IL_00a2;
			}
			Dictionary<int, int> characterTeams3 = CharacterTeams;
			identity = ((IEntity)val).Identity;
			if (characterTeams3[((Identity)(ref identity)).Instance] != value)
			{
				goto IL_00a2;
			}
			goto end_IL_0037;
			IL_00a2:
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(leader, ((INamedEntity)val).Name + " is not in your team.", 0, 0);
			return false;
			end_IL_0037:;
		}
		Leave(val);
		BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(leader, ((INamedEntity)val).Name + " was removed from the team.", 0, 0);
		return true;
	}

	public static bool TryHandleChatCommand(ICharacter character, string[] args)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		if (args == null || args.Length < 2)
		{
			BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Team commands: /team invite <name>, /team accept, /team decline, /team leave.", 0, 0);
			return true;
		}
		string text = args[1].ToLowerInvariant();
		if (text == "accept")
		{
			return Reply(character, accept: true, Identity.None);
		}
		if (text == "decline" || text == "reject")
		{
			return Reply(character, accept: false, Identity.None);
		}
		if (text == "leave")
		{
			return Leave(character);
		}
		if (text == "invite" && args.Length >= 3)
		{
			ICharacter val = FindOnlineCharacterByName(character, args[2]);
			if (val == null)
			{
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Could not find online character " + args[2] + ".", 0, 0);
				return false;
			}
			return Invite(character, ((IEntity)val).Identity);
		}
		BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Unknown team command.", 0, 0);
		return false;
	}

	private static void Join(ICharacter leader, ICharacter newMember)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		Identity identity;
		int value;
		List<Identity> list;
		lock (Sync)
		{
			Dictionary<int, int> characterTeams = CharacterTeams;
			identity = ((IEntity)leader).Identity;
			if (!characterTeams.TryGetValue(((Identity)(ref identity)).Instance, out value))
			{
				value = nextTeamId++;
				Dictionary<int, int> characterTeams2 = CharacterTeams;
				identity = ((IEntity)leader).Identity;
				characterTeams2[((Identity)(ref identity)).Instance] = value;
				TeamMembers[value] = new List<Identity> { ((IEntity)leader).Identity };
			}
			list = TeamMembers[value];
			if (!list.Any(delegate(Identity x)
			{
				//IL_000d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0012: Unknown result type (might be due to invalid IL or missing references)
				int instance = ((Identity)(ref x)).Instance;
				Identity identity2 = ((IEntity)newMember).Identity;
				return instance == ((Identity)(ref identity2)).Instance;
			}))
			{
				list.Add(((IEntity)newMember).Identity);
			}
			Dictionary<int, int> characterTeams3 = CharacterTeams;
			identity = ((IEntity)newMember).Identity;
			characterTeams3[((Identity)(ref identity)).Instance] = value;
		}
		UpdateTeamMemberStats(value);
		NotifyMembers(list, ((INamedEntity)newMember).Name + " joined the team.");
		string[] obj = new string[6]
		{
			"Team joined teamId=",
			value.ToString(),
			" leader=",
			null,
			null,
			null
		};
		identity = ((IEntity)leader).Identity;
		obj[3] = ((Identity)(ref identity)).ToString(true);
		obj[4] = " member=";
		identity = ((IEntity)newMember).Identity;
		obj[5] = ((Identity)(ref identity)).ToString(true);
		LogUtil.Debug((DebugInfoDetail)128, string.Concat(obj));
	}

	private static void UpdateTeamMemberStats(int teamId)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		List<Identity> value;
		lock (Sync)
		{
			if (!TeamMembers.TryGetValue(teamId, out value))
			{
				return;
			}
			value = value.ToList();
		}
		int count = value.Count;
		foreach (Identity item in value)
		{
			ICharacter @object = Pool.Instance.GetObject<ICharacter>(item);
			if (@object != null)
			{
				ApplyTeamStats(@object, teamId, count);
			}
		}
	}

	private static void ApplyTeamStats(ICharacter character, int teamId, int memberCount)
	{
		((IStats)character).Stats[(StatIds)6].Value = teamId;
		((IStats)character).Stats[(StatIds)6].BaseValue = (uint)teamId;
		((IStats)character).Stats[(StatIds)587].Value = memberCount;
		((IStats)character).Stats[(StatIds)587].BaseValue = (uint)memberCount;
		((IDynel)character).Controller.SendChangedStats();
	}

	private static void NotifyMembers(List<Identity> members, string text)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if (members == null)
		{
			return;
		}
		foreach (Identity item in members.ToList())
		{
			ICharacter @object = Pool.Instance.GetObject<ICharacter>(item);
			if (@object != null)
			{
				BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(@object, text, 0, 0);
			}
		}
	}

	private static ICharacter ResolveOnlineCharacter(ICharacter reference, Identity identity)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		if (reference == null || ((IInstancedEntity)reference).Playfield == null)
		{
			return null;
		}
		return Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)reference).Playfield).Identity, identity) ?? Pool.Instance.GetObject<ICharacter>(identity);
	}

	private static ICharacter FindOnlineCharacterByName(ICharacter reference, string name)
	{
		if (reference == null || string.IsNullOrWhiteSpace(name))
		{
			return null;
		}
		return Pool.Instance.GetAll<ICharacter>(50000).FirstOrDefault((ICharacter x) => x != null && ((IDynel)x).Controller is PlayerController && string.Equals(((INamedEntity)x).Name, name, StringComparison.OrdinalIgnoreCase));
	}
}
