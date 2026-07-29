using System;
using System.Collections.Concurrent;
using AORebirth.Core.Components;
using AORebirth.Core.Entities;
using AORebirth.Core.Network;
using AORebirth.Enums;
using AORebirth.Interfaces;
using AORebirth.Stats;
using Cell.Core;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Utility;
using ZoneEngine.Core.MessageHandlers;

namespace ZoneEngine.Core;

public static class ShadowlandsGardenSaveRuntimeService
{
	private const int GardenPlayfieldMin = 4676;

	private const int GardenPlayfieldMax = 4699;

	private const float PadRadius = 2.5f;

	private const float TriggerX = 462.4f;

	private const float TriggerZ = 411.8f;

	private const float BindX = 462.3f;

	private const float BindY = 45.4f;

	private const float BindZ = 422.2f;

	private static readonly TimeSpan SaveCooldown = TimeSpan.FromSeconds(30.0);

	private static readonly ConcurrentDictionary<int, bool> OnPadByCharacterId = new ConcurrentDictionary<int, bool>();

	private static readonly ConcurrentDictionary<int, DateTime> LastSaveUtcByCharacterId = new ConcurrentDictionary<int, DateTime>();

	public static void GetGardenSaveSpot(out float x, out float y, out float z)
	{
		x = 462.3f;
		y = 45.4f;
		z = 422.2f;
	}

	public static bool IsGardenPlayfield(int playfieldId)
	{
		return playfieldId >= 4676 && playfieldId <= 4699;
	}

	public static void TryApplyWhenOnSavePad(ICharacter character, string reason)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (character == null || ((IInstancedEntity)character).Playfield == null)
			{
				return;
			}
			Identity identity = ((IEntity)character).Identity;
			int instance = ((Identity)(ref identity)).Instance;
			identity = ((IEntity)((IInstancedEntity)character).Playfield).Identity;
			int instance2 = ((Identity)(ref identity)).Instance;
			if (instance2 <= 0)
			{
				return;
			}
			if (!IsGardenPlayfield(instance2))
			{
				OnPadByCharacterId.TryRemove(instance, out var _);
				return;
			}
			float num = ((IDynel)character).RawCoordinates.X - 462.4f;
			float num2 = ((IDynel)character).RawCoordinates.Z - 411.8f;
			bool flag = num * num + num2 * num2 <= 6.25f;
			OnPadByCharacterId.TryGetValue(instance, out var value2);
			if (!flag)
			{
				if (value2)
				{
					OnPadByCharacterId[instance] = false;
				}
			}
			else
			{
				if (value2)
				{
					return;
				}
				OnPadByCharacterId[instance] = true;
				if (!LastSaveUtcByCharacterId.TryGetValue(instance, out var value3) || !(DateTime.UtcNow - value3 < SaveCooldown))
				{
					LastSaveUtcByCharacterId[instance] = DateTime.UtcNow;
					SaveRespawnPoint(character, instance2);
					uint savedSk;
					uint num3 = CombatXpRuntimeService.ApplyInsuranceTerminalSave(character, out savedSk);
					int value4 = ((IStats)character).Stats[(StatIds)54].Value;
					string text = CombatXpRuntimeService.BuildSaveRewardText(value4, num3, savedSk);
					BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, text, 0, 0);
					BaseMessageHandler<ChatTextMessage, ChatTextMessageHandler>.Default.Send(character, "Character saved", 0, 0);
					IZoneClient val = ((((IDynel)character).Controller != null) ? ((IDynel)character).Controller.Client : null);
					if (val != null && ((IClient)val).Server != null)
					{
						((IClient)val).Server.Info((IClient)(object)val, "Shadowlands garden pad-save char={0} pf={1} xp={2} sk={3} reason={4}", new object[5]
						{
							((IEntity)character).Identity,
							instance2,
							num3,
							savedSk,
							reason ?? string.Empty
						});
					}
				}
			}
		}
		catch (Exception ex)
		{
			LogUtil.Debug((DebugInfoDetail)512, "Shadowlands garden save FAILED: " + ex);
			try
			{
				if (character != null && ((IDynel)character).Controller != null && ((IDynel)character).Controller.Client != null && ((IClient)((IDynel)character).Controller.Client).Server != null)
				{
					((IClient)((IDynel)character).Controller.Client).Server.Info((IClient)(object)((IDynel)character).Controller.Client, "Shadowlands garden save FAILED reason={0} ex={1}", new object[2]
					{
						reason ?? string.Empty,
						ex.Message
					});
				}
			}
			catch
			{
			}
		}
	}

	private static void SaveRespawnPoint(ICharacter character, int playfieldId)
	{
		int val = (int)Math.Round(462.29998779296875);
		int val2 = (int)Math.Round(422.20001220703125);
		((IStats)character).Stats[(StatIds)595].Set((uint)Math.Max(0, playfieldId), false);
		((IStats)character).Stats[(StatIds)596].Set((uint)Math.Max(0, val), false);
		((IStats)character).Stats[(StatIds)597].Set((uint)Math.Max(0, val2), false);
		((IStats)character).Stats[(StatIds)236].Set(100u, false);
		((IStats)character).Stats[(StatIds)49].Set((uint)Math.Max(0, Environment.TickCount), false);
		((IDatabaseObject)((IStats)character).Stats).Write();
		((IStats)character).Stats[(StatIds)595].Changed = false;
		((IStats)character).Stats[(StatIds)596].Changed = false;
		((IStats)character).Stats[(StatIds)597].Changed = false;
		((IStats)character).Stats[(StatIds)236].Changed = false;
		((IStats)character).Stats[(StatIds)49].Changed = false;
	}
}
