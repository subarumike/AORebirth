namespace ZoneEngine_New.Core.MessageHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Inventory;
    using ZoneEngine_New.Core.Teams;
    using ZoneEngine_New.Core.Nanos;
    using ZoneEngine_New.Core.Missions;
    using ZoneEngine_New.Core.Movement;
    using ZoneEngine_New.Core.Network;
    using ZoneEngine_New.Core.Playfield;
    using ZoneEngine_New.Core.Playfield.Locality;
    using ZoneEngine_New.Core.Quests;

    using MsgQuaternion = SmokeLounge.AOtomation.Messaging.GameData.Quaternion;
    using MsgVector3 = SmokeLounge.AOtomation.Messaging.GameData.Vector3;

    public sealed class CharacterActionMessageHandler : IMessageHandler<CharacterActionMessage>
    {
        private readonly InventoryActionService _inventoryActions;
        private readonly TeamService _teams;
        private readonly Tradeskills.TradeskillService? _tradeskills;
        private readonly Lazy<DailyLogin.DailyLoginClaimBridge>? _dailyLogin;
        private readonly CharacterPlayedClock? _playedClock;

        public CharacterActionMessageHandler(InventoryActionService inventoryActions, TeamService teams,
            Tradeskills.TradeskillService? tradeskills = null, Lazy<DailyLogin.DailyLoginClaimBridge>? dailyLogin = null,
            CharacterPlayedClock? playedClock = null)
        {
            _inventoryActions = inventoryActions;
            _teams = teams;
            _tradeskills = tradeskills;
            _dailyLogin = dailyLogin;
            _playedClock = playedClock;
        }

        public Type MessageBodyType => typeof(CharacterActionMessage);

        public void Handle(MessageBody body, IZoneSession session)
        {
            Handle((CharacterActionMessage)body, session);
        }

        /// <summary>
        /// Client skill reset (Parameter2 = skill stat id). Live on success (capture 2026-10-14T00:07:33Z): Stat RP, then
        /// Stat IP + the skill's base, then FormatFeedback "%d IP have been returned to you for the reset of %s!"; on a
        /// refusal only the Feedback line. The skill name goes as a text reference the client resolves to the same name
        /// live sends as plain text.
        /// </summary>
        static void ResetSkill(Player player, IZoneSession session, int statId)
        {
            // The skill window's "Reset all skills" sends skill 0 (GUI.dll 100f9c21: N3Msg_ResetSkill(0) on Yes).
            if (statId == 0)
            {
                FullIpReset(player, session);
                return;
            }

            var skill = (CharacterStat)statId;
            if (!SkillTraining.TryReset(player, skill, player.SkillCatalog, out int refund, out SkillTraining.ResetRefusal refusal))
            {
                SendResetRefusal(player, refusal);
                LogUtil.Debug(DebugInfoDetail.Network, string.Format(CultureInfo.InvariantCulture,
                    "Skill reset refused character={0} skill={1} reason={2}", player.Identity.Instance, statId, refusal));
                return;
            }

            session.Send(OwnStats(player, (CharacterStat.RP, player.Stats.GetOrZero(CharacterStat.RP, StatDetail.Base))));
            session.Send(OwnStats(player,
                (CharacterStat.IP, Math.Max(0, player.AvailableIp)),
                (skill, player.Stats.GetOrZero(skill, StatDetail.Base))));
            ClientFeedback.SendFormatted(player, ClientFeedback.SkillResetIpReturned, refund,
                new ClientFeedback.TextReference(ClientFeedback.StatNameCategory, statId));
            player.FlushDirtyStats();
            LogUtil.Debug(DebugInfoDetail.Network, string.Format(CultureInfo.InvariantCulture,
                "Skill reset character={0} skill={1} refund={2} ip={3} rp={4}", player.Identity.Instance, skill, refund,
                player.AvailableIp, player.Stats.GetOrZero(CharacterStat.RP, StatDetail.Base)));
        }

        /// <summary>
        /// Full IP Reset, answered like a single reset: Stat for what it spent (SpecialCondition for the free reset,
        /// otherwise FullIPRPoints), then Stat IP + every lowered ability and skill, then the client's own finish line
        /// 110/218510731 "Your Full IP Reset has finished. You now have %d IP." (no retail capture of this reset yet).
        /// </summary>
        static void FullIpReset(Player player, IZoneSession session)
        {
            StatCollection stats = player.Stats;
            bool freeReset = (stats.GetOrZero(CharacterStat.SpecialCondition, StatDetail.Base) & SkillTraining.FreeFullResetUsedBit) == 0;
            if (!SkillTraining.TryFullReset(player, player.SkillCatalog, out int refund, out List<CharacterStat> changed,
                    out SkillTraining.ResetRefusal refusal))
            {
                SendResetRefusal(player, refusal);
                LogUtil.Debug(DebugInfoDetail.Network, string.Format(CultureInfo.InvariantCulture,
                    "Full IP reset refused character={0} reason={1}", player.Identity.Instance, refusal));
                return;
            }

            CharacterStat spent = freeReset ? CharacterStat.SpecialCondition : CharacterStat.FullIPRPoints;
            session.Send(OwnStats(player, (spent, stats.GetOrZero(spent, StatDetail.Base))));
            var values = new (CharacterStat Stat, int Value)[changed.Count + 1];
            values[0] = (CharacterStat.IP, Math.Max(0, player.AvailableIp));
            for (int i = 0; i < changed.Count; i++)
                values[i + 1] = (changed[i], stats.GetOrZero(changed[i], StatDetail.Base));
            session.Send(OwnStats(player, values));
            ClientFeedback.SendFormatted(player, ClientFeedback.FullIpResetFinished, Math.Max(0, player.AvailableIp));
            player.FlushDirtyStats();
            LogUtil.Debug(DebugInfoDetail.Network, string.Format(CultureInfo.InvariantCulture,
                "Full IP reset character={0} free={1} refund={2} lowered={3} ip={4} fullResetPoints={5}",
                player.Identity.Instance, freeReset, refund, changed.Count, player.AvailableIp,
                stats.GetOrZero(CharacterStat.FullIPRPoints, StatDetail.Base)));
        }

        /// <summary>Every refused reset gets a line, so the skill window's reset never looks dead.</summary>
        static void SendResetRefusal(Player player, SkillTraining.ResetRefusal refusal)
        {
            switch (refusal)
            {
                case SkillTraining.ResetRefusal.MapNavigation:
                    ClientFeedback.Send(player, ClientFeedback.CantResetMapNavigation);
                    break;
                case SkillTraining.ResetRefusal.NoResetPoint:
                    ClientFeedback.Send(player, ClientFeedback.NeedResetPoint);
                    break;
                case SkillTraining.ResetRefusal.ItemsEquipped:
                    ClientFeedback.Send(player, ClientFeedback.UnequipBeforeSkillReset);
                    break;
                case SkillTraining.ResetRefusal.NothingSpent:
                    ClientFeedback.Send(player, ClientFeedback.NoIpSpentOnSkill);
                    break;
                case SkillTraining.ResetRefusal.NoFullReset:
                    ClientFeedback.SendFormatted(player, ClientFeedback.PlainText, "You have no Full IP Reset available.");
                    break;
                default:
                    // NotResettable and anything newer: the client's own "You are only allowed to reset abilities and skills."
                    ClientFeedback.SendFormatted(player, ClientFeedback.PlainText,
                        new ClientFeedback.TextReference(ClientFeedback.GuiTextCategory, ClientFeedback.OnlyAbilitiesAndSkillsResettable));
                    break;
            }
        }

        static StatMessage OwnStats(Player player, params (CharacterStat Stat, int Value)[] stats)
        {
            var tuples = new GameTuple<CharacterStat, uint>[stats.Length];
            for (int i = 0; i < stats.Length; i++)
                tuples[i] = new GameTuple<CharacterStat, uint> { Value1 = stats[i].Stat, Value2 = unchecked((uint)stats[i].Value) };
            return new StatMessage { Identity = player.Identity, Stats = tuples };
        }

        public void Handle(CharacterActionMessage message, IZoneSession session)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(session);

            if (session.State != SessionState.InPlay)
                return;

            Player? player = session.Player;
            if (player == null || !ReferenceEquals(player.Session, session) || player.IsPersistenceQuarantined)
                return;

            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "CharacterAction action={0}({1}) target={2} p1={3} p2={4} character={5}",
                    message.Action,
                    (int)message.Action,
                    message.Target,
                    message.Parameter1,
                    message.Parameter2,
                    player.Identity.Instance));

            if (_teams.TryHandle(player, message))
                return;

            switch (message.Action)
            {
                case CharacterActionType.Die:
                case CharacterActionType.DeathRespawn:
                    player.RequestRespawn();
                    break;

                case CharacterActionType.StandUp:
                    // Sit posture arrives via CharDCMove (MoveType SwitchToSit), not CharacterAction.
                    ApplyStand(player);
                    break;

                case CharacterActionType.SitDown:
                    // Daily claim Action=263 Target=self, captured 20260806-063619 and recovered from feec603.
                    // Actual posture uses movement/ChangeAnimationAndStance, not this historical enum label.
                    if (message.Target != player.Identity)
                        break;
                    try { _dailyLogin?.Value.HandleClaim(player, session.AccountName, DateTime.UtcNow); }
                    catch (Exception exception)
                    {
                        LogUtil.Debug(DebugInfoDetail.Engine, "Daily Login bridge unavailable: " + exception.Message);
                        Commands.GmCommandFeedback.Send(session, player, "Daily Login is unavailable. No claim has been lost; please try again later.");
                    }
                    break;

                case CharacterActionType.StartSneak: //TODO: Wire in hiding when sneaking
                    if (player.IsBeingFought)
                    {
                        ClientFeedback.Send(player, ClientFeedback.FailedToHideWhileFighting);
                        break;
                    }

                    if (player.Motor.ApplyAction(MovementAction.SwitchToSneak))
                        AnnounceAction(player, CharacterActionType.StartedSneaking);
                    break;

                case CharacterActionType.StopSneaking: //TODO: Wire in cooldown on sneak
                    player.Motor.ApplyAction(MovementAction.LeaveSneak);
                    AnnounceAction(player, CharacterActionType.StopSneaking);
                    break;

                case CharacterActionType.CastNano:
                    // Client cast request: Parameter2 = nano id, Target = recipient.
                    NanoRuntime.TryStartCast(player, message.Parameter2, message.Target, DateTime.UtcNow);
                    break;

                case CharacterActionType.InterruptNanoCasting:
                    NanoRuntime.InterruptCast(player);
                    break;

                case CharacterActionType.RemoveFriendlyNano:
                    CancelNano(player, message);
                    break;

                case CharacterActionType.ChangeVisualFlag:
                    // Client pad/helmet/social toggles; Parameter2 carries the whole flag word.
                    player.TryApplyVisualFlags(message.Parameter2);
                    break;

                case CharacterActionType.DisableXP:
                    // EventFeedback 0x3c sends the complete AutoAttackFlags word in Parameter2.
                    if (message.Identity == player.Identity)
                        player.SetXpGainOptions(message.Parameter2);
                    break;

                case CharacterActionType.DeleteItem:
                case CharacterActionType.Split:
                case CharacterActionType.JoinItems:
                    _inventoryActions.Handle(player, message);
                    break;

                // Tradeskill window: Parameter1 = container type (0 clears), Parameter2 = slot; Build carries the
                // chosen QL in Target.Instance.
                case CharacterActionType.TradeskillSourceChanged:
                    _tradeskills?.SourceChanged(player, message.Parameter1, message.Parameter2);
                    break;

                case CharacterActionType.TradeskillTargetChanged:
                    _tradeskills?.TargetChanged(player, message.Parameter1, message.Parameter2);
                    break;

                case CharacterActionType.TradeskillBuildPressed:
                    _tradeskills?.BuildPressed(player, message.Target.Instance);
                    break;

                case CharacterActionType.UseItemOnItem:
                {
                    // An inventory item dropped on another (CellAO's handler): Target = the dragged item,
                    // Parameter1/Parameter2 = the container type and slot it was dropped on.
                    var droppedOn = new Identity { Type = (IdentityType)message.Parameter1, Instance = message.Parameter2 };
                    var outcome = _tradeskills?.TryCombineDrag(player, message.Target, droppedOn);
                    if (outcome == Tradeskills.TradeskillService.CombineOutcome.NoRecipe)
                        Tradeskills.TradeskillService.NoCombination(player);
                    player.Logger.Info(string.Format(CultureInfo.InvariantCulture,
                        "CharacterAction UseItemOnItem character={0} source={1}:{2} target={3}:{4} tradeskill={5}",
                        player.Identity.Instance, message.Target.Type, message.Target.Instance, droppedOn.Type, droppedOn.Instance,
                        outcome?.ToString() ?? "unavailable"));
                    break;
                }

                case CharacterActionType.Reload:
                    // Reload button / hotkey (Gamecode.dll special action 0x6E → 100697c0): names no ammo or weapon, so
                    // every worn gun tops up from the first stack of its ammo.
                    WeaponAmmo.ReloadAll(player);
                    break;

                case CharacterActionType.UsePerk:
                    // Perk Actions button (Gamecode.dll 0x1004277c): Parameter2 = the button's action hash.
                    player.Playfield?.GetService<ItemUseService>()?.TryBeginPerkAction(player, message.Parameter2);
                    break;

                case CharacterActionType.TrainPerk:
                    // Client train request: Parameter2 = perk id. The accepted request goes back unchanged;
                    // client case 0x48 (Gamecode.dll 0x100536d7) then adds the perk to its own map.
                    player.TryTrainPerk(message.Parameter2, () => session.Send(message));
                    break;

                case CharacterActionType.UntrainPerk:
                    // Client untrain request: Parameter2 = perk id. The accepted request goes back unchanged;
                    // client case 0x49 (Gamecode.dll 0x10053362) then removes the perk from its map.
                    player.TryUntrainPerk(message.Parameter2, () => session.Send(message));
                    break;

                case CharacterActionType.ResetSkill:
                    ResetSkill(player, session, message.Parameter2);
                    break;

                case CharacterActionType.InfoRequest:
                {
                    Playfield? playfield = player.Playfield;
                    if (playfield == null || message.Identity != player.Identity)
                        break;

                    if (message.Target.Instance == 0)
                        break;

                    if (!playfield.GetRequiredService<DynelRegistry>().TryGet(message.Target, out Dynel? dynel)
                        || dynel is not Character target)
                        break;

                    session.Send(target.BuildInfoPacket());
                    // Retail Find Person completion follows inspection of the exact, visible objective NPC.
                    if (target is NpcCharacter npc
                        && playfield.GetRequiredService<PlayfieldLocality>().IsVisibleTo(npc, player)
                        && playfield.GetService<QuestService>() is QuestService quests)
                        quests.OnNpcInspected(player, npc);
                    break;
                }

                case CharacterActionType.Logout:
                {
                    // No /camp out of a fight: attacking something or being attacked refuses it.
                    if (player.IsInCombat)
                    {
                        ClientFeedback.Send(player, ClientFeedback.CantLogoutInFight);
                        break;
                    }

                    session.Send(
                        new StartLogoutMessage
                        {
                            Identity = player.Identity
                        });

                    Playfield? logoutPlayfield = player.Playfield;
                    if (logoutPlayfield != null)
                        logoutPlayfield.GetRequiredService<SpawnService>().LogoutPlayer(player);
                    else
                        session.Close();
                    break;
                }

                case CharacterActionType.PlayedTime:
                {
                    long played = _playedClock?.Played(player.Identity.Instance) ?? 0;
                    session.Send(CharacterPlayedBornText.Played(player.Identity, player.Name ?? string.Empty, played));
                    break;
                }

                case CharacterActionType.BornDate:
                {
                    // Without a recorded creation time there is no date to show, so the reply stays silent.
                    long? createdAt = _playedClock?.CreatedAtUnix(player.Identity.Instance);
                    if (createdAt is long created)
                        session.Send(CharacterPlayedBornText.Born(player.Identity, player.Name ?? string.Empty, created));
                    break;
                }

                default:
                    player.Logger.Warn(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Deserialized but unhandled CharacterAction action={0}({1}) character={2}",
                            message.Action,
                            (int)message.Action,
                            player.Identity.Instance));
                    break;
            }
        }

        /// <summary>
        /// NCU cancel from the client. Live cancel often arrives as Target=None with the nano id
        /// in Parameter2; Target.Type=NanoProgram is accepted when present. No single-buff guess.
        /// </summary>
        static void CancelNano(Player player, CharacterActionMessage message)
        {
            int nanoId = ResolveCancelNanoId(message);
            if (nanoId <= 0)
            {
                player.Logger.Warn(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "RemoveFriendlyNano without a nano id: target={0} p1={1} p2={2} character={3}",
                        message.Target,
                        message.Parameter1,
                        message.Parameter2,
                        player.Identity.Instance));
                return;
            }

            NanoRuntime.TryCancelBuff(player, nanoId);
        }

        static int ResolveCancelNanoId(CharacterActionMessage message)
        {
            if (message.Target.Type == IdentityType.NanoProgram && message.Target.Instance > 0)
                return message.Target.Instance;

            return message.Parameter2 > 0 ? message.Parameter2 : 0;
        }

        static void ApplyStand(Player player)
        {
            // StandUp ends sit, and also sleep and lounge (entered via SocialActionCmd 68/69).
            MovementAction leave = player.Motor.State switch
            {
                MovementState.Sleep => MovementAction.LeaveSleep,
                MovementState.Lounge => MovementAction.LeaveLounge,
                _ => MovementAction.LeaveSit
            };
            player.Motor.ApplyAction(leave);

            Cell? cell = player.Cell;
            if (cell == null)
                return;

            AnnounceAction(player, CharacterActionType.StandUp);
            cell.Announce(CreatePostureMove(player, (byte)leave));
        }

        static void AnnounceAction(Player player, CharacterActionType action)
        {
            Cell? cell = player.Cell;
            if (cell == null)
                return;

            cell.Announce(
                new CharacterActionMessage
                {
                    Identity = player.Identity,
                    Unknown = 0x00,
                    Action = action,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = 0,
                    Parameter2 = 0,
                    Unknown2 = 0
                });
        }

        static CharDCMoveMessage CreatePostureMove(Player player, byte moveType)
        {
            return new CharDCMoveMessage
            {
                Identity = player.Identity,
                Unknown = 0x00,
                MoveType = moveType,
                Heading = new MsgQuaternion
                {
                    X = player.Rotation.xf,
                    Y = player.Rotation.yf,
                    Z = player.Rotation.zf,
                    W = player.Rotation.wf
                },
                Coordinates = new MsgVector3
                {
                    X = player.Position.xf,
                    Y = player.Position.yf,
                    Z = player.Position.zf
                },
                Unknown1 = 0,
                AuxA = 0,
                AuxB = 0
            };
        }
    }
}
