namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;
    using System.Threading;

    using AORebirth.Core.Combat;
    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;

    #endregion

    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class CharSecSpecAttackMessageHandler :
        BaseMessageHandler<CharSecSpecAttackMessage, CharSecSpecAttackMessageHandler>
    {
        private const int SimpleCharFullUpdateIsImmuneFlag = 0x00800000;

        protected override void Read(CharSecSpecAttackMessage message, IZoneClient client)
        {
            if (client == null || client.Controller == null || client.Controller.Character == null)
            {
                return;
            }

            Character character = client.Controller.Character as Character;
            if (character == null)
            {
                return;
            }

            int specialStatId = message.Stat;
            Identity targetId = message.Target;

            client.Server.Info(
                client,
                "CharSecSpecAttack stat={0} target={1}",
                specialStatId,
                targetId);

            if (!CharacterSpecialAttackRules.IsSupportedSpecial(specialStatId))
            {
                client.Server.Info(client, "CharSecSpecAttack ignored: unsupported special={0}", specialStatId);
                return;
            }

            ICharacter target = Pool.Instance.GetObject<ICharacter>(character.Playfield.Identity, targetId);
            if (target == null
                || ContentDrivenNpcDialogueRouter.ShouldSuppressCombat(target)
                || IsImmuneTarget(target)
                || !PlayerVersusPlayerCombatRules.CanEngagePlayerVersusPlayerCombat(character, target))
            {
                client.Server.Info(client, "CharSecSpecAttack ignored: invalid/immune target.");
                return;
            }

            string missionSpatialFailure;
            if (!MissionAcgSpatialRuntime.TryValidateCombatPair(
                    character,
                    target,
                    out missionSpatialFailure))
            {
                client.Server.Info(client, "CharSecSpecAttack ignored: mission spatial validation failed.");
                return;
            }

            CharacterSpecialAttackResult specialResult =
                character.ProcessSpecialAttack(target, (StatIds)specialStatId);
            if (specialResult.Outcome != StrikeOutcome.Applied)
            {
                client.Server.Info(client, "CharSecSpecAttack failed: outcome={0}", specialResult.Outcome);
                return;
            }

            Playfield playfield = character.Playfield as Playfield;
            if (playfield == null)
            {
                return;
            }

            int lockSeconds = CharacterSpecialAttackRules.ResolveLockSeconds(specialStatId);

            playfield.Announce(
                new CharSecSpecAttackMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Target = targetId,
                    Stat = specialStatId
                });

            client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.SpecialUsed,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = specialStatId,
                    Parameter2 = lockSeconds,
                    Unknown2 = 0
                });

            playfield.Announce(
                new SpecialAttackInfo
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Unknown1 = specialResult.EquipSlot,
                    Unknown2 = specialResult.Damage,
                    Unknown3 = specialResult.AmmoCount,
                    Target = targetId,
                    Unknown4 = specialStatId,
                    Unknown5 = 0
                });

            ScheduleSpecialAvailable(character, specialStatId, lockSeconds);
        }

        private static void ScheduleSpecialAvailable(ICharacter character, int specialStatId, int lockSeconds)
        {
            int delayMs = Math.Max(1, lockSeconds) * 1000;
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    Thread.Sleep(delayMs);
                    if (character == null || character.Controller == null || character.Controller.Client == null)
                    {
                        return;
                    }

                    CharacterActionMessageHandler.Default.SendSkillAvailable(character, specialStatId);
                });
        }

        private static bool IsImmuneTarget(ICharacter target)
        {
            return target != null
                   && (target.Stats[StatIds.flags].Value & SimpleCharFullUpdateIsImmuneFlag)
                   == SimpleCharFullUpdateIsImmuneFlag;
        }
    }
}
