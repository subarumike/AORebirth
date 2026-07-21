#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Controllers;

    #endregion

    /// <summary>
    /// Outbound: server opens KnuBot window.
    /// Inbound: client opens chat with NPC (capture 20260716-Reset-perks / Thrak Veronica —
    /// KnubotOpenChatWindow, not Trade Open).
    /// </summary>
    [MessageHandler(MessageHandlerDirection.All)]
    public class KnuBotOpenChatWindowMessageHandler :
        BaseMessageHandler<KnuBotOpenChatWindowMessage, KnuBotOpenChatWindowMessageHandler>
    {
        public override void Receive(MessageWrapper<KnuBotOpenChatWindowMessage> messageWrapper)
        {
            if (messageWrapper == null || messageWrapper.MessageBody == null || messageWrapper.Client == null
                || messageWrapper.Client.Controller == null
                || messageWrapper.Client.Controller.Character == null)
            {
                return;
            }

            ICharacter player = messageWrapper.Client.Controller.Character;
            Identity npcIdentity = messageWrapper.MessageBody.Target;
            ICharacter npc = Pool.Instance.GetObject<ICharacter>(player.Playfield.Identity, npcIdentity);
            if (npc == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.KnuBot,
                    "KnuBotOpenChatWindow inbound: NPC not found " + npcIdentity.ToString(true));
                return;
            }

            // Content-driven NPCs (Rex / Windcaller / Tailor / Thrak) have no attached KnuBot; client still
            // opens them with KnubotOpenChatWindow (capture 20260718-185306 Veronica).
            if (ContentDrivenNpcDialogueRouter.TryStartDialogueForTarget(player, npcIdentity))
            {
                return;
            }

            NPCController controller = npc.Controller as NPCController;
            if (controller == null || controller.KnuBot == null)
            {
                LogUtil.Debug(
                    DebugInfoDetail.KnuBot,
                    "KnuBotOpenChatWindow inbound: no KnuBot on " + npc.Name);
                return;
            }

            messageWrapper.Client.Server.Info(
                messageWrapper.Client,
                "KnuBotOpenChatWindow inbound player={0} npc={1}",
                player.Identity,
                npc.Identity);

            controller.FaceDialoguePartner(player);
            // Allow re-open even if a previous conversation partner was left set.
            controller.KnuBot.Character = new Utility.WeakReference<ICharacter>(null);
            controller.KnuBot.StartDialog(player);
        }

        public void Send(ICharacter character, Identity knubotTarget)
        {
            this.Send(character, knubotTarget, 1);
        }

        public void Send(ICharacter character, Identity knubotTarget, int unknown2)
        {
            this.Send(character, this.KnuBotOpenWindow(character, knubotTarget, unknown2), false);
        }

        private MessageDataFiller KnuBotOpenWindow(
            ICharacter character,
            Identity knubotTarget,
            int unknown2)
        {
            return x =>
            {
                x.Identity = character.Identity;
                x.Target = knubotTarget;
                x.Unknown1 = 2;
                x.Unknown2 = unknown2;
            };
        }
    }
}
