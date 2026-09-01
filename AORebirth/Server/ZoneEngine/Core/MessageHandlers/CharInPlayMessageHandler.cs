#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Threading;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;
    using ZoneEngine.Core;
    using ZoneEngine.Core.Mail;
    using ZoneEngine.Core.Packets;
    using ZoneEngine.Core.Perks;
    using ZoneEngine.Core.Playfields;

    #endregion

    /// <summary>
    /// </summary>
    [MessageHandler(MessageHandlerDirection.InboundOnly)]
    public class CharInPlayMessageHandler : BaseMessageHandler<CharInPlayMessage, CharInPlayMessageHandler>
    {
        /// <summary>
        /// </summary>
        public CharInPlayMessageHandler()
        {
            this.UpdateCharacterStatsOnReceive = true;
        }

        #region Inbound

        /// <summary>
        /// </summary>
        /// <param name="message">
        /// </param>
        /// <param name="client">
        /// </param>
        protected override void Read(CharInPlayMessage message, IZoneClient client)
        {
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                PlayfieldLifecycleTrace.StageCharInPlayReceived,
                PlayfieldLifecycleTrace.MessageCharInPlay,
                client.Controller.Character.Identity);
            LogUtil.Debug(
                DebugInfoDetail.NetworkMessages,
                string.Format(
                    "Client CharInPlay received character={0} unknown={1}",
                    client.Controller.Character.Identity,
                    message.Unknown));
            client.Controller.Character.DoNotDoTimers = true;
            Thread.Sleep(1000);
            // client got all the needed data and
            // wants to enter the world. After we
            // reply to this, the character will really be in game
            var announce = new CharInPlayMessage { Identity = client.Controller.Character.Identity, Unknown = 0x00 };
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                PlayfieldLifecycleTrace.StageCharInPlayAnnounce,
                PlayfieldLifecycleTrace.MessageCharInPlay,
                client.Controller.Character.Identity);
            client.Controller.Character.Playfield.Announce(announce);

            // Player is in game now, starting is over, set stats normally now
            client.Controller.Character.Starting = false;
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                PlayfieldLifecycleTrace.StageCharInPlayReady,
                "CharacterReady",
                client.Controller.Character.Identity);
            client.Controller.Character.Stats.ClearChangedFlags();

            // Needed fix, so gmlevel will be loaded
            client.Controller.Character.Stats[StatIds.gmlevel].Value =
                client.Controller.Character.Stats[StatIds.gmlevel].Value;
            client.Controller.Character.Stats[StatIds.expansion].Value =
                client.Controller.Character.Stats[StatIds.expansion].Value;
            client.Controller.Character.Stats[StatIds.healinterval].Value = 0;
            client.Controller.Character.Stats[StatIds.healdelta].Value = 0;
            client.Controller.Character.Stats[StatIds.nanointerval].Value = 0;
            client.Controller.Character.Stats[StatIds.nanodelta].Value = 0;

            // Extra to calculate IP
            client.Controller.Character.Stats[StatIds.ip].Value = 0;

            client.Controller.SendChangedStats();

            // Mobs get sent whenever player enters playfield, BUT (!) they are NOT synchronized, because the mobs don't save stuff yet.
            // for instance: the waypoints the mob went through will NOT be saved and therefore when you re-enter the PF, it will AGAIN
            // walk the same waypoints.
            // TODO: Fix it
            /*foreach (MobType mob in NPCPool.Mobs)
            {
                // TODO: Make cache - use pf indexing somehow.
                if (mob.pf == client.Character.pf)
                {
                    mob.SendToClient(client);
                }
            }*/

            foreach (WeatherEntry w in WeatherSettings.Instance.WeatherList)
            {
                WeatherControlMessageHandler.Default.Send(client.Controller.Character, w);
            }

            List<StaticDynel> list =
                new List<StaticDynel>(
                    Pool.Instance.GetAll<StaticDynel>(client.Controller.Character.Playfield.Identity));
            client.Server.Info(
                client,
                "StaticDynelSnapshot pf={0} count={1}",
                client.Controller.Character.Playfield.Identity.Instance,
                list.Count);
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                PlayfieldLifecycleTrace.StageStaticDynelSnapshot,
                "StaticDynelSnapshot",
                client.Controller.Character.Identity);
            foreach (StaticDynel sd in list)
            {
                SimpleItemFullUpdateMessageHandler.Default.Send(client.Controller.Character, sd);
            }

            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                PlayfieldLifecycleTrace.StageWeaponDefinitions,
                "WeaponItemFullUpdate",
                client.Controller.Character.Identity);
            WeaponItemFullUpdate.SendWeaponDefinitions(client.Controller.Character);
            // Link backpacks as Container dynels so mail Item field gets Feedback_MailNoChests.
            InventoryContainerRuntimeService.Default.PublishMailBlockedContainerLinks(
                client.Controller.Character);
            Playfield.ArmPostZoneCollisionGrace(client.Controller.Character);

            // In-memory mail pending while offline → show envelope on enter world.
            MailRuntimeService.SyncUnreadMailEnvelope(client.Controller.Character);
            // GMI web withdraw requests (capture 20260715-143838) → Omni-Trade mail.
            ZoneEngine.Core.GMI.GmiRuntimeService.ProcessPendingWithdrawals(client.Controller.Character);

            // Re-sync trained perks + Perk Actions after relog (also done after FullCharacter).
            var inPlayCharacter = client.Controller.Character as Character;
            if (inPlayCharacter != null)
            {
                inPlayCharacter.ReloadTrainedPerksFromDatabase();
                PerkRuntimeService.Default.ResendPerkActions(inPlayCharacter);
            }

            // Backup path — primary resync is ClientConnected after FullCharacter (CharInPlay often missing).
            ZoneEngine.Core.Missions.MissionAcgLifecycleService.TryCleanupPendingForCharacter(
                client,
                client.Controller.Character);
            bool missionWindowResent =
                ZoneEngine.Core.Missions.MissionAcceptService.TryResendForLogin(client.Controller.Character);
            ZoneEngine.Core.Missions.MissionTokenProgressTracker
                .TryResumePendingClientUpdates(client.Controller.Character);

            bool thrakMissionResent =
                ZoneEngine.Core.Thrak.Quests.ThrakGardenKeyQuestRuntime.TryResendActiveMissionsForLogin(
                    client.Controller.Character);

            bool dojaMissionResent =
                ZoneEngine.Core.Doja.DojaChipQuestRuntime.TryResendActiveMissionsForLogin(
                    client.Controller.Character);

            bool rosenblattMissionResent =
                ZoneEngine.Core.Nascence.Quests.RosenblattHiathlinQuestRuntime.TryResendActiveMissionsForLogin(
                    client.Controller.Character);

            bool rosenblattPapagenaMissionResent =
                ZoneEngine.Core.Nascence.Quests.RosenblattPapagenaQuestRuntime.TryResendActiveMissionsForLogin(
                    client.Controller.Character);
            ZoneEngine.Core.Nascence.Quests.RosenblattPapagenoQuestRuntime.TryResendActiveMissionsForLogin(
                client.Controller.Character);
            ZoneEngine.Core.Nascence.Quests.RosenblattCascadingSpiritQuestRuntime.TryResendActiveMissionsForLogin(
                client.Controller.Character);
            ZoneEngine.Core.Nascence.Quests.RosenblattSpinetoothQuestRuntime.TryResendActiveMissionsForLogin(
                client.Controller.Character);
            ZoneEngine.Core.Nascence.Quests.RosenblattDemonicQuestRuntime.TryResendActiveMissionsForLogin(
                client.Controller.Character);
            bool nascenceLifeRodriguezMissionResent =
                ZoneEngine.Core.Nascence.Quests.NascenceLifeRodriguezQuestRuntime.TryResendActiveMissionsForLogin(
                    client.Controller.Character);
            bool nascenceLifeFalkerMissionResent =
                ZoneEngine.Core.Nascence.Quests.NascenceLifeJoshuaFalkerQuestRuntime.TryResendActiveMissionsForLogin(
                    client.Controller.Character);
            bool nascenceLifeDonnaMissionResent =
                ZoneEngine.Core.Nascence.Quests.NascenceLifeDonnaRedQuestRuntime.TryResendActiveMissionsForLogin(
                    client.Controller.Character);
            bool nascenceAbanFalaMissionResent =
                ZoneEngine.Core.Nascence.Quests.NascenceAbanFalaQuestRuntime.TryResendActiveMissionsForLogin(
                    client.Controller.Character);

            bool areteTipResent =
                ZoneEngine.Core.Arete.Quests.RexMarcusChainCoordinator.TryResendActiveTipsForLogin(
                    client.Controller.Character);

            // Sacred Thrak garden key is permanent; restore if quest/account already earned it.
            ZoneEngine.Core.Thrak.Quests.ThrakGardenKeyQuestRuntime.TryRestoreGardenKeyIfMissing(
                client.Controller.Character);
            ZoneEngine.Core.Nascence.Quests.NascenceAbanFalaQuestRuntime.TryRestoreAbanGardenKeyIfMissing(
                client.Controller.Character);

            int pfInstance = client.Controller.Character.Playfield != null
                                 ? client.Controller.Character.Playfield.Identity.Instance
                                 : 0;
            client.Server.Info(
                client,
                "CharInPlay mission-window resync resent={0} thrak={1} doja={2} areteTips={3}",
                missionWindowResent,
                thrakMissionResent,
                dojaMissionResent,
                areteTipResent);

            ZoneEngine.Core.Missions.MissionDiagnostics.Log(
                "CHARINPLAY char={0} pf={1} windowResent={2} thrakResent={3} dojaResent={4} rosenblattResent={5}",
                client.Controller.Character.Identity.Instance,
                pfInstance,
                missionWindowResent,
                thrakMissionResent,
                dojaMissionResent,
                rosenblattMissionResent);

            // Mission interiors: re-send exact instance-local captured objects after CharInPlay.
            if (client.Controller.Character.Playfield != null
                && ZoneEngine.Core.Missions.MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(
                    client.Controller.Character.Playfield.Identity.Instance))
            {
                ZoneEngine.Core.Missions.MissionAcgRuntimeManager.ClearSent(
                    client.Controller.Character);
                ZoneEngine.Core.Missions.MissionAcgRuntimeManager.SendForCharacter(
                    client,
                    client.Controller.Character);
            }
            else if (client.Controller.Character.Playfield != null
                     && AORebirth.Core.Playfields.NascenceDungeon1Rules.IsDungeonPlayfield(
                         client.Controller.Character.Playfield.Identity.Instance))
            {
                AORebirth.Core.Playfields.NascenceDungeon1SearchRuntime.ClearForCharacter(
                    client.Controller.Character);
                AORebirth.Core.Playfields.NascenceDungeon1DoorReplay.SendForCharacter(
                    client,
                    client.Controller.Character);
            }
            else if (client.Controller.Character.Playfield != null
                     && AORebirth.Core.Playfields.NascenceDungeon2Rules.IsDungeonPlayfield(
                         client.Controller.Character.Playfield.Identity.Instance))
            {
                AORebirth.Core.Playfields.NascenceDungeon2SearchRuntime.ClearForCharacter(
                    client.Controller.Character);
                AORebirth.Core.Playfields.NascenceDungeon2DoorReplay.SendForCharacter(
                    client,
                    client.Controller.Character);
            }
            else if (client.Controller.Character.Playfield != null
                     && AORebirth.Core.Playfields.NascenceDungeon3Rules.IsDungeonPlayfield(
                         client.Controller.Character.Playfield.Identity.Instance))
            {
                AORebirth.Core.Playfields.NascenceDungeon3SearchRuntime.ClearForCharacter(
                    client.Controller.Character);
                AORebirth.Core.Playfields.NascenceDungeon3DoorReplay.SendForCharacter(
                    client,
                    client.Controller.Character);
            }
            else if (client.Controller.Character.Playfield != null
                     && AORebirth.Core.Playfields.NascenceDungeon4Rules.IsDungeonPlayfield(
                         client.Controller.Character.Playfield.Identity.Instance))
            {
                AORebirth.Core.Playfields.NascenceDungeon4SearchRuntime.ClearForCharacter(
                    client.Controller.Character);
                AORebirth.Core.Playfields.NascenceDungeon4DoorReplay.SendForCharacter(
                    client,
                    client.Controller.Character);
            }
            else
            {
                ZoneEngine.Core.Missions.MissionInstanceDoorReplay.SendForCharacter(
                    client,
                    client.Controller.Character);
            }

            client.Controller.Character.DoNotDoTimers = false;
            PlayfieldLifecycleTrace.Record(
                PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                PlayfieldLifecycleTrace.StageTimersEnabled,
                "TimersEnabled",
                client.Controller.Character.Identity);
        }

        #endregion
    }
}
