using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Functions;
using AORebirth.Core.Network;
using AORebirth.Core.Playfields;
using AORebirth.Core.Vector;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.ObjectManager;
using AORebirth.Stats;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.Arete.Dialogue;
using ZoneEngine.Core.Functions;
using ZoneEngine.Core.InternalMessages;
using ZoneEngine.Core.KnuBot;
using ZoneEngine.Core.MessageHandlers;
using ZoneEngine.Core.Playfields;

namespace ZoneEngine.Core.Controllers;

public class NPCController : IController, IDisposable
{
	private struct NpcMotionSegment
	{
		public Vector3 Start;

		public Vector3 End;

		public DateTime StartedUtc;

		public bool Active;
	}

	public BaseKnuBot KnuBot = null;

	private Identity followIdentity = Identity.None;

	private Vector3 followCoordinates = new Vector3();

	private NpcMotionSegment followMotionSegment;

	private double followStopDistance;

	private DateTime lastMotionPacketUtc = DateTime.MinValue;

	private Vector3 lastMotionPacketDestination = new Vector3();

	private NpcPatrolReplaySegment[] capturedPatrolReplaySegments = new NpcPatrolReplaySegment[0];

	private int capturedPatrolReplayIndex;

	private bool capturedPatrolReplayUsesRuntimeStart;

	private bool capturedPatrolReplayBatchesZeroDelaySegments;

	private bool capturedPatrolReplayUsesRuntimeStartOnce;

	private DateTime nextCapturedPatrolReplayUtc = DateTime.MinValue;

	private bool hasMotionPacket;

	private bool suppressMotionSegmentUpdates;

	private CharacterState state = (CharacterState)0;

	private int activeWaypoint = 0;

	private const double MaxNpcFollowSpeedPerSecond = 6.0;

	private const double MaxPlayerChaseProjectionDistance = 3.0;

	private const double MinVisibleFollowUpdateSeconds = 0.35;

	private const double MinVisibleFollowTargetDelta = 1.0;

	private const double CoordinateFollowArrivalDistance = 0.3;

	private const double WalkFollowSpeedPerSecond = 1.5;

	public NpcAiProfile AiProfile { get; set; } = NpcAiProfile.Passive;


	public CharacterState State
	{
		get
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			return state;
		}
		set
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			state = value;
		}
	}

	public ICharacter Character { get; set; }

	public IZoneClient Client
	{
		get
		{
			return null;
		}
		set
		{
			throw new Exception("NPC's dont have a client. Faulty code tries to use it!!");
		}
	}

	public bool SaveToDatabase => false;

	private static string FormatVector(Vector3 vector)
	{
		return string.Format(CultureInfo.InvariantCulture, "{0:0.00},{1:0.00},{2:0.00}", vector.x, vector.y, vector.z);
	}

	private static Vector3 GetFollowTargetPosition(ICharacter target)
	{
		if (((IDynel)target).Controller is PlayerController)
		{
			Vector3 start = Vector3.op_Implicit(((IDynel)target).RawCoordinates);
			Vector3 coordinate = ((IDynel)target).Coordinates().coordinate;
			return MoveToward(start, coordinate, 3.0);
		}
		return ((IDynel)target).Coordinates().coordinate;
	}

	private void LogChase(string phase, Vector3 start, Vector3 destination)
	{
	}

	private static Vector3 MoveToward(Vector3 start, Vector3 destination, double maxDistance)
	{
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected O, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected O, but got Unknown
		double num = start.Distance2D(destination);
		if (num < 0.001 || maxDistance <= 0.0)
		{
			return new Vector3(start.x, start.y, start.z);
		}
		double num2 = Math.Min(num, maxDistance);
		double num3 = num2 / num;
		return new Vector3(start.x + (destination.x - start.x) * num3, start.y + (destination.y - start.y) * num3, start.z + (destination.z - start.z) * num3);
	}

	private static ICharacter GetCharacterFromPool(Identity parent, Identity identity)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		IEntity @object = Pool.Instance.GetObject(parent, identity);
		return (ICharacter)(object)((@object is ICharacter) ? @object : null);
	}

	private void ResetFollowPosition()
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		followMotionSegment = default(NpcMotionSegment);
		followStopDistance = 0.0;
		hasMotionPacket = false;
		suppressMotionSegmentUpdates = false;
		lastMotionPacketUtc = DateTime.MinValue;
		lastMotionPacketDestination = new Vector3();
	}

	private Vector3 CurrentMotionSegmentPosition(DateTime now)
	{
		if (!followMotionSegment.Active)
		{
			return ((IDynel)Character).Coordinates().coordinate;
		}
		double num = Math.Max(0.0, (now - followMotionSegment.StartedUtc).TotalSeconds);
		return MoveToward(followMotionSegment.Start, followMotionSegment.End, CurrentFollowSpeedPerSecond() * num);
	}

	private double CurrentFollowSpeedPerSecond()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Invalid comparison between Unknown and I4
		if (((object)(Identity)(ref followIdentity)).Equals((object)Identity.None) && (int)Character.MoveMode == 2)
		{
			return 1.5;
		}
		return 6.0;
	}

	private Vector3 UpdateMotionSegmentPosition(DateTime now)
	{
		Vector3 val = CurrentMotionSegmentPosition(now);
		((IDynel)Character).Coordinates(val);
		return val;
	}

	private void FaceToward(Vector3 start, Vector3 destination)
	{
		TryFaceToward(start, destination, out var _);
	}

	private bool TryFaceToward(Vector3 start, Vector3 destination, out Vector3 normalizedDirection)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected O, but got Unknown
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		normalizedDirection = new Vector3();
		if (start.Distance2D(destination) < 0.001)
		{
			return false;
		}
		Vector3 val = destination - start;
		val.y = 0.0;
		normalizedDirection = val.Normalize();
		((IDynel)Character).Heading = (Quaternion)Quaternion.GenerateRotationFromDirectionVector((IVector3)(object)normalizedDirection);
		return true;
	}

	private Vector3 BuildVisibleFollowDestination(Vector3 start, Vector3 targetPosition)
	{
		if (followStopDistance <= 0.0)
		{
			return targetPosition;
		}
		double num = start.Distance2D(targetPosition);
		if (num <= followStopDistance)
		{
			return start;
		}
		return MoveToward(start, targetPosition, num - followStopDistance);
	}

	private bool IsCapturedIdlePatrolReplay()
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		int result;
		if ((int)state == 4 && ((object)(Identity)(ref followIdentity)).Equals((object)Identity.None) && Character != null)
		{
			Identity fightingTarget = ((ITargetingEntity)Character).FightingTarget;
			if (((object)(Identity)(ref fightingTarget)).Equals((object)Identity.None))
			{
				result = (HasCapturedPatrolReplay() ? 1 : 0);
				goto IL_005d;
			}
		}
		result = 0;
		goto IL_005d;
		IL_005d:
		return (byte)result != 0;
	}

	public void SetCapturedPatrolReplaySegments(NpcPatrolReplaySegment[] segments)
	{
		SetCapturedPatrolReplaySegments(segments, useRuntimeStart: false, batchZeroDelaySegments: false);
	}

	public void SetCapturedPatrolReplaySegments(NpcPatrolReplaySegment[] segments, bool useRuntimeStart)
	{
		SetCapturedPatrolReplaySegments(segments, useRuntimeStart, batchZeroDelaySegments: false);
	}

	public void SetCapturedPatrolReplaySegments(NpcPatrolReplaySegment[] segments, bool useRuntimeStart, bool batchZeroDelaySegments)
	{
		SetCapturedPatrolReplaySegments(segments, useRuntimeStart, batchZeroDelaySegments, useRuntimeStartOnce: false);
	}

	public void SetCapturedPatrolReplaySegments(NpcPatrolReplaySegment[] segments, bool useRuntimeStart, bool batchZeroDelaySegments, bool useRuntimeStartOnce)
	{
		capturedPatrolReplaySegments = segments ?? new NpcPatrolReplaySegment[0];
		capturedPatrolReplayIndex = 0;
		capturedPatrolReplayUsesRuntimeStart = useRuntimeStart;
		capturedPatrolReplayBatchesZeroDelaySegments = batchZeroDelaySegments;
		capturedPatrolReplayUsesRuntimeStartOnce = useRuntimeStartOnce;
		nextCapturedPatrolReplayUtc = DateTime.MinValue;
	}

	private bool HasCapturedPatrolReplay()
	{
		return capturedPatrolReplaySegments != null && capturedPatrolReplaySegments.Length != 0;
	}

	public bool TryGetCapturedPatrolReplayProjection(out Vector3 currentPosition, out Vector3 destination)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected O, but got Unknown
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Expected O, but got Unknown
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Expected O, but got Unknown
		currentPosition = new Vector3();
		destination = new Vector3();
		if (!IsCapturedIdlePatrolReplay() || !hasMotionPacket || !followMotionSegment.Active)
		{
			return false;
		}
		currentPosition = CurrentMotionSegmentPosition(DateTime.UtcNow);
		destination = new Vector3((double)followMotionSegment.End.xf, (double)followMotionSegment.End.yf, (double)followMotionSegment.End.zf);
		return true;
	}

	private bool TrySendCapturedPatrolReplay()
	{
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Expected O, but got Unknown
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Expected O, but got Unknown
		if (!IsCapturedIdlePatrolReplay())
		{
			return false;
		}
		DateTime utcNow = DateTime.UtcNow;
		if (nextCapturedPatrolReplayUtc != DateTime.MinValue && utcNow < nextCapturedPatrolReplayUtc)
		{
			return true;
		}
		int num = 0;
		while (num < capturedPatrolReplaySegments.Length)
		{
			if (capturedPatrolReplayIndex >= capturedPatrolReplaySegments.Length)
			{
				capturedPatrolReplayIndex = 0;
			}
			NpcPatrolReplaySegment npcPatrolReplaySegment = capturedPatrolReplaySegments[capturedPatrolReplayIndex];
			if (npcPatrolReplaySegment.MoveMode == 25)
			{
				Run();
			}
			else
			{
				Walk();
			}
			Vector3 val = new Vector3((double)npcPatrolReplaySegment.StartX, (double)npcPatrolReplaySegment.StartY, (double)npcPatrolReplaySegment.StartZ);
			Vector3 val2 = ((capturedPatrolReplayUsesRuntimeStart || capturedPatrolReplayUsesRuntimeStartOnce) ? UpdateMotionSegmentPosition(utcNow) : val);
			Vector3 val3 = (followCoordinates = new Vector3((double)npcPatrolReplaySegment.EndX, (double)npcPatrolReplaySegment.EndY, (double)npcPatrolReplaySegment.EndZ));
			((IDynel)Character).Coordinates(val2);
			FaceToward(val2, val3);
			BaseMessageHandler<FollowTargetMessage, FollowTargetMessageHandler>.Default.Send(Character, Vector3.op_Implicit(val2), Vector3.op_Implicit(val3));
			SetMotionSegment(val2, val3, utcNow);
			lastMotionPacketUtc = utcNow;
			lastMotionPacketDestination = val3;
			hasMotionPacket = true;
			capturedPatrolReplayUsesRuntimeStartOnce = false;
			capturedPatrolReplayIndex = (capturedPatrolReplayIndex + 1) % capturedPatrolReplaySegments.Length;
			num++;
			if (!capturedPatrolReplayBatchesZeroDelaySegments || npcPatrolReplaySegment.DelayAfterSeconds > 0.0)
			{
				nextCapturedPatrolReplayUtc = utcNow + TimeSpan.FromSeconds(Math.Max(0.01, npcPatrolReplaySegment.DelayAfterSeconds));
				return true;
			}
		}
		nextCapturedPatrolReplayUtc = utcNow + TimeSpan.FromSeconds(0.01);
		return true;
	}

	public void SnapshotCurrentMotionPosition()
	{
		if (Character != null && hasMotionPacket)
		{
			((IDynel)Character).Coordinates(UpdateMotionSegmentPosition(DateTime.UtcNow));
		}
	}

	private void SetMotionSegment(Vector3 start, Vector3 destination, DateTime now)
	{
		followMotionSegment = new NpcMotionSegment
		{
			Start = start,
			End = destination,
			StartedUtc = now,
			Active = true
		};
	}

	private bool ShouldSendMotionSegmentUpdate(Vector3 currentPosition, Vector3 targetPosition, DateTime now)
	{
		if (!followMotionSegment.Active || !hasMotionPacket)
		{
			return true;
		}
		if ((now - lastMotionPacketUtc).TotalSeconds < 0.35)
		{
			return false;
		}
		Vector3 val = BuildVisibleFollowDestination(currentPosition, targetPosition);
		if (lastMotionPacketDestination.Distance2D(val) >= 1.0)
		{
			return true;
		}
		double num = Math.Max(followStopDistance, 0.3);
		return currentPosition.Distance2D(followMotionSegment.End) < 0.3 && currentPosition.Distance2D(targetPosition) > num + 1.0;
	}

	private void SendMotionSegmentFollow(string phase, Vector3 start, Vector3 targetPosition, DateTime now)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = BuildVisibleFollowDestination(start, targetPosition);
		if (!((object)(Identity)(ref followIdentity)).Equals((object)Identity.None))
		{
			Run();
		}
		((IDynel)Character).Coordinates(start);
		FaceToward(start, val);
		LogChase(phase, start, val);
		BaseMessageHandler<FollowTargetMessage, FollowTargetMessageHandler>.Default.Send(Character, Vector3.op_Implicit(start), Vector3.op_Implicit(val));
		SetMotionSegment(start, val, now);
		lastMotionPacketUtc = now;
		lastMotionPacketDestination = val;
		hasMotionPacket = true;
	}

	private bool TryCompleteCoordinateFollow(Vector3 current, Vector3 targetPosition)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		if (!((object)(Identity)(ref followIdentity)).Equals((object)Identity.None) || current.Distance2D(targetPosition) > 0.3)
		{
			return false;
		}
		StopMovement();
		((IDynel)Character).Coordinates(targetPosition);
		StopFollow();
		return true;
	}

	public void SuppressMotionSegmentUpdates(bool suppress)
	{
		suppressMotionSegmentUpdates = suppress;
	}

	private void SendWantedDirection(Vector3 direction)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected O, but got Unknown
		//IL_0069: Expected O, but got Unknown
		((IInstancedEntity)Character).Playfield.Publish((object)new IMSendAOtomationMessageToPlayfield
		{
			Body = (MessageBody)new SetWantedDirectionMessage
			{
				Identity = ((IEntity)Character).Identity,
				Unknown = 0,
				DirectinVector = new Vector3
				{
					X = direction.xf,
					Y = direction.yf,
					Z = direction.zf
				}
			}
		});
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	public bool LookAt(Identity target)
	{
		throw new NotImplementedException();
	}

	public bool UseStatel(Identity identity, EventType eventType = 0)
	{
		throw new NotImplementedException();
	}

	public void SendChatText(string text)
	{
		throw new NotImplementedException();
	}

	public bool CastNano(int nanoId, Identity target)
	{
		throw new NotImplementedException();
	}

	public bool Search()
	{
		throw new NotImplementedException();
	}

	public bool Sneak()
	{
		throw new NotImplementedException();
	}

	public bool ChangeVisualFlag(int visualFlag)
	{
		throw new NotImplementedException();
	}

	public bool Move(int moveType, Coordinate newCoordinates, Quaternion heading)
	{
		throw new NotImplementedException();
	}

	public bool ContainerAddItem(int sourceContainerType, int sourcePlacement, Identity target, int targetPlacement)
	{
		throw new NotImplementedException();
	}

	public bool Follow(Identity target)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		return Follow(target, 0.0);
	}

	public bool Follow(Identity target, double stopDistance)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		followIdentity = target;
		ICharacter characterFromPool = GetCharacterFromPool(((IEntity)((IInstancedEntity)Character).Playfield).Identity, target);
		if (characterFromPool == null)
		{
			StopFollow();
			return false;
		}
		DateTime utcNow = DateTime.UtcNow;
		Vector3 val = UpdateMotionSegmentPosition(utcNow);
		ResetFollowPosition();
		followIdentity = target;
		followStopDistance = Math.Max(0.0, stopDistance);
		Vector3 val2 = (followCoordinates = GetFollowTargetPosition(characterFromPool));
		if (followStopDistance > 0.0 && val.Distance2D(val2) <= followStopDistance)
		{
			((IDynel)Character).Coordinates(val);
			FaceToward(val, val2);
			return true;
		}
		Run();
		FaceToward(val, val2);
		SendMotionSegmentFollow("coordinate-follow", val, val2, utcNow);
		return true;
	}

	public bool Stand()
	{
		throw new NotImplementedException();
	}

	public bool SocialAction(SocialAction action, byte parameter1, byte parameter2, byte parameter3, byte parameter4, int parameter5)
	{
		throw new NotImplementedException();
	}

	public bool Trade(Identity target)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		if (ContentDrivenNpcDialogueRouter.TryStartDialogue(Character, target))
		{
			return true;
		}
		if (KnuBot != null)
		{
			ICharacter @object = Pool.Instance.GetObject<ICharacter>(((IEntity)((IInstancedEntity)Character).Playfield).Identity, target);
			if (@object == null)
			{
				return false;
			}
			FaceDialoguePartner(@object);
			KnuBot.Character = new WeakReference<ICharacter>((ICharacter)null);
			return KnuBot.StartDialog(@object);
		}
		return false;
	}

	public bool FaceDialoguePartner(ICharacter source)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		if (source == null || Character == null || ((IInstancedEntity)Character).Playfield == null)
		{
			return false;
		}
		if (((IInstancedEntity)source).Playfield != null)
		{
			Identity identity = ((IEntity)((IInstancedEntity)source).Playfield).Identity;
			if (((object)(Identity)(ref identity)).Equals((object)((IEntity)((IInstancedEntity)Character).Playfield).Identity))
			{
				if (!TryFaceToward(Vector3.op_Implicit(((IDynel)Character).RawCoordinates), Vector3.op_Implicit(((IDynel)source).RawCoordinates), out var normalizedDirection))
				{
					return false;
				}
				SendWantedDirection(normalizedDirection);
				identity = ((IEntity)Character).Identity;
				string text = ((Identity)(ref identity)).ToString(true);
				identity = ((IEntity)source).Identity;
				LogUtil.Debug((DebugInfoDetail)4096, "NPC dialogue facing npc=" + text + " source=" + ((Identity)(ref identity)).ToString(true));
				return true;
			}
		}
		return false;
	}

	public bool UseItem(Identity itemPosition)
	{
		throw new NotImplementedException();
	}

	public bool TryUseBackpackContainer(Identity itemPosition)
	{
		return false;
	}

	public bool DeleteItem(int container, int slotNumber)
	{
		throw new NotImplementedException();
	}

	public bool SplitItemStack(Identity targetItem, int stackCount)
	{
		throw new NotImplementedException();
	}

	public bool JoinItemStack(Identity sourceItem, Identity targetItem)
	{
		throw new NotImplementedException();
	}

	public bool CombineItems(Identity sourceItem, Identity targetItem)
	{
		throw new NotImplementedException();
	}

	public bool TradeSkillSourceChanged(int inventoryPageId, int slotNumber)
	{
		throw new NotImplementedException();
	}

	public bool TradeSkillTargetChanged(int inventoryPageId, int slotNumber)
	{
		throw new NotImplementedException();
	}

	public bool TradeSkillBuildPressed(Identity targetItem)
	{
		throw new NotImplementedException();
	}

	public bool ChatCommand(string command, Identity target)
	{
		throw new NotImplementedException();
	}

	public bool Logout()
	{
		throw new NotImplementedException();
	}

	public void LogoffCharacter()
	{
	}

	public bool Login()
	{
		throw new NotImplementedException();
	}

	public bool StopLogout()
	{
		throw new NotImplementedException();
	}

	public bool GetTargetInfo(Identity target)
	{
		throw new NotImplementedException();
	}

	public bool TeamInvite(Identity target)
	{
		throw new NotImplementedException();
	}

	public bool TeamKickMember(Identity target)
	{
		throw new NotImplementedException();
	}

	public bool TeamLeave()
	{
		throw new NotImplementedException();
	}

	public bool TransferTeamLeadership(Identity target)
	{
		throw new NotImplementedException();
	}

	public bool TeamJoinRequest(Identity target)
	{
		throw new NotImplementedException();
	}

	public bool TeamJoinReply(bool accept, Identity requester)
	{
		throw new NotImplementedException();
	}

	public bool TeamJoinAccepted(Identity newTeamMember)
	{
		throw new NotImplementedException();
	}

	public bool TeamJoinRejected(Identity rejectingIdentity)
	{
		throw new NotImplementedException();
	}

	public void SendChangedStats()
	{
		Dictionary<int, uint> dictionary = new Dictionary<int, uint>();
		Dictionary<int, uint> dictionary2 = new Dictionary<int, uint>();
		((IStats)Character).Stats.GetChangedStats(dictionary2, dictionary);
		dictionary2.Clear();
		BaseMessageHandler<StatMessage, StatMessageHandler>.Default.SendBulk(Character, dictionary2, dictionary);
	}

	public void CallFunction(Function function, IEntity caller)
	{
		FunctionCollection.Instance.CallFunction(function.FunctionType, (INamedEntity)(object)Character, caller, (IInstancedEntity)(object)Character, function.Arguments.Values.ToArray());
	}

	public void MoveTo(Vector3 destination)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Expected O, but got Unknown
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Expected O, but got Unknown
		Vector3 val = Vector3.op_Implicit(destination);
		Vector3 coordinate = ((IDynel)Character).Coordinates().coordinate;
		DateTime utcNow = DateTime.UtcNow;
		followIdentity = Identity.None;
		if (coordinate.Distance2D(val) < 0.30000001192092896)
		{
			StopMovement();
			StopFollow();
			((IDynel)Character).RawCoordinates = destination;
			BaseMessageHandler<FollowTargetMessage, FollowTargetMessageHandler>.Default.Send(Character, destination);
		}
		else
		{
			val -= coordinate;
			val.y = 0.0;
			((IDynel)Character).Heading = (Quaternion)Quaternion.GenerateRotationFromDirectionVector((IVector3)(object)val.Normalize());
			SendMotionSegmentFollow("moveto", coordinate, Vector3.op_Implicit(destination), utcNow);
			Coordinate val2 = new Coordinate(destination);
			followCoordinates = val2.coordinate;
		}
	}

	public void DoFollow()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Expected O, but got Unknown
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		if (TrySendCapturedPatrolReplay())
		{
			return;
		}
		Vector3 followTargetPosition = followCoordinates;
		if (!((object)(Identity)(ref followIdentity)).Equals((object)Identity.None))
		{
			ICharacter characterFromPool = GetCharacterFromPool(((IEntity)((IInstancedEntity)Character).Playfield).Identity, followIdentity);
			if (characterFromPool == null)
			{
				if (PetCombatRules.IsPlayerOwnedPet(Character))
				{
					PetCommandService.ReturnPetToOwner(Character);
				}
				else
				{
					StopFollow();
				}
				return;
			}
			followTargetPosition = GetFollowTargetPosition(characterFromPool);
		}
		if (followTargetPosition.Distance2D(new Vector3()) < 0.009999999776482582)
		{
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		Vector3 val = UpdateMotionSegmentPosition(utcNow);
		followCoordinates = followTargetPosition;
		if (TryCompleteCoordinateFollow(val, followTargetPosition))
		{
			return;
		}
		if (followStopDistance > 0.0 && val.Distance2D(followTargetPosition) <= followStopDistance)
		{
			((IDynel)Character).Coordinates(val);
			FaceToward(val, followTargetPosition);
			return;
		}
		FaceToward(val, followTargetPosition);
		if (!suppressMotionSegmentUpdates && ShouldSendMotionSegmentUpdate(val, followTargetPosition, utcNow))
		{
			SendMotionSegmentFollow("coordinate-update", val, followTargetPosition, utcNow);
		}
	}

	public void StartPatrolling()
	{
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Expected O, but got Unknown
		if (TrySendCapturedPatrolReplay())
		{
			return;
		}
		Waypoint val = FindNextWaypoint();
		if (val != null)
		{
			if (val.Running)
			{
				Run();
			}
			else
			{
				Walk();
			}
			followCoordinates = val.Position;
			DateTime utcNow = DateTime.UtcNow;
			Vector3 coordinate = ((IDynel)Character).Coordinates().coordinate;
			Vector3 val2 = coordinate - val.Position;
			val2.y = 0.0;
			((IDynel)Character).Heading = (Quaternion)Quaternion.GenerateRotationFromDirectionVector((IVector3)(object)val2).Normalize();
			LogUtil.Debug((DebugInfoDetail)1, "Direction: " + ((object)((IDynel)Character).Heading).ToString());
			SendMotionSegmentFollow("patrol-start", coordinate, val.Position, utcNow);
			LogUtil.Debug((DebugInfoDetail)1, "Walking to: " + (object)followCoordinates);
		}
	}

	public bool IsFollowing()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return !((object)(Identity)(ref followIdentity)).Equals((object)Identity.None) || followCoordinates.x != 0.0 || followCoordinates.y != 0.0 || followCoordinates.z != 0.0;
	}

	public bool IsFollowing(Identity target)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return ((object)(Identity)(ref followIdentity)).Equals((object)target);
	}

	public void StopFollowForCombatRange(Vector3 targetPosition)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Expected O, but got Unknown
		Vector3 val = UpdateMotionSegmentPosition(DateTime.UtcNow);
		((IDynel)Character).Coordinates(val);
		FaceToward(val, targetPosition);
		LogChase("combat-stop", val, targetPosition);
		BaseMessageHandler<FollowTargetMessage, FollowTargetMessageHandler>.Default.Send(Character, Vector3.op_Implicit(val));
		followIdentity = Identity.None;
		lock (followCoordinates)
		{
			followCoordinates = new Vector3();
		}
		ResetFollowPosition();
	}

	public void StopFollowForCapturedCombatRange(Vector3 targetPosition, Vector3 movementDestination)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Expected O, but got Unknown
		//IL_00b2: Expected O, but got Unknown
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Expected O, but got Unknown
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Expected O, but got Unknown
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Expected O, but got Unknown
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Expected O, but got Unknown
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Expected O, but got Unknown
		//IL_024d: Expected O, but got Unknown
		Vector3 val = UpdateMotionSegmentPosition(DateTime.UtcNow);
		((IDynel)Character).Coordinates(val);
		FaceToward(val, targetPosition);
		LogChase("captured-combat-stop", val, targetPosition);
		AnnounceCapturedCombatMessage((MessageBody)new FollowTargetMessage
		{
			Identity = ((IEntity)Character).Identity,
			Unknown = 0,
			Info = (FollowInfo)new FollowTargetInfo
			{
				MoveType = 25,
				Target = Identity.None,
				Dummy = 0,
				Dummy1 = 1073741824,
				X = val.xf,
				Y = val.yf,
				Z = val.zf
			}
		});
		AnnounceCapturedCombatMessage((MessageBody)new StopMovingCmdMessage
		{
			Identity = ((IEntity)Character).Identity,
			Unknown = 0,
			Unknown1 = 1,
			Unknown2 = 514,
			Unknown3 = 1
		});
		AnnounceCapturedCombatMessage((MessageBody)new SetPosMessage
		{
			Identity = ((IEntity)Character).Identity,
			Unknown = 0,
			Coordinates = new Vector3
			{
				X = val.xf,
				Y = val.yf,
				Z = val.zf
			},
			Unknown1 = 1,
			Unknown2 = 0,
			Unknown3 = 0
		});
		followIdentity = Identity.None;
		lock (followCoordinates)
		{
			followCoordinates = new Vector3((double)movementDestination.xf, (double)(movementDestination.yf + 0.5f), (double)movementDestination.zf);
		}
		ResetFollowPosition();
		if (!(val.Distance2D(movementDestination) < 0.01))
		{
			Run();
			AnnounceCapturedCombatMessage((MessageBody)new FollowTargetMessage
			{
				Identity = ((IEntity)Character).Identity,
				Unknown = 0,
				Info = (FollowInfo)new FollowCoordinateInfo
				{
					CurrentCoordinates = Vector3.op_Implicit(val),
					EndCoordinates = Vector3.op_Implicit(followCoordinates),
					CoordinateCount = 2,
					MoveMode = 25,
					FollowInfoType = 1
				}
			});
			DateTime utcNow = DateTime.UtcNow;
			SetMotionSegment(val, followCoordinates, utcNow);
			lastMotionPacketUtc = utcNow;
			lastMotionPacketDestination = followCoordinates;
			hasMotionPacket = true;
		}
	}

	private void AnnounceCapturedCombatMessage(MessageBody body)
	{
		if (((IInstancedEntity)Character).Playfield is Playfield playfield)
		{
			playfield.Announce(body);
			return;
		}
		((IInstancedEntity)Character).Playfield.Publish((object)new IMSendAOtomationMessageToPlayfield
		{
			Body = body
		});
	}

	public void StopFollowForCapturedCombatRange(Vector3 targetPosition)
	{
		StopFollowForCapturedCombatRange(targetPosition, targetPosition);
	}

	public void Run()
	{
		Character.UpdateMoveType((byte)25);
	}

	public void StopMovement()
	{
		Character.UpdateMoveType((byte)2);
	}

	public void Walk()
	{
		Character.UpdateMoveType((byte)24);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing && Client != null)
		{
			Client = null;
		}
	}

	private Waypoint FindNextWaypoint()
	{
		Waypoint val = null;
		if (Character.Waypoints.Count < 2)
		{
			return null;
		}
		if (Character.Waypoints.Count <= activeWaypoint)
		{
			activeWaypoint = 0;
		}
		int count = Character.Waypoints.Count;
		do
		{
			activeWaypoint = (activeWaypoint + 1) % count;
			val = Character.Waypoints[activeWaypoint];
		}
		while (val.Position.Distance2D(((IDynel)Character).Coordinates().coordinate) < 0.20000000298023224);
		return val;
	}

	public void StartMovement()
	{
		Character.UpdateMoveType((byte)1);
	}

	~NPCController()
	{
		LogUtil.Debug((DebugInfoDetail)1024, "NPC Controller finished");
		LogUtil.Debug((DebugInfoDetail)1024, new StackTrace().ToString());
		Dispose(disposing: false);
	}

	public bool Move(int moveType, Coordinate newCoordinates, Quaternion heading)
	{
		return false;
	}

	public void Move()
	{
	}

	public void StopFollow()
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		followIdentity = Identity.None;
		ResetFollowPosition();
		lock (followCoordinates)
		{
			followCoordinates = new Vector3();
		}
	}

	public void SetKnuBot(BaseKnuBot knubot)
	{
		KnuBot = knubot;
		AiProfile = NpcAiProfile.Social;
	}
}
