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

namespace ZoneEngine.Core.PacketHandlers
{
    #region Usings ...

    using System;
    using System.Text;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;
    using AORebirth.Core.NPCHandler;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.InternalMessages;
    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Missions;
    using ZoneEngine.Core.Packets;
    using ZoneEngine;
    using ZoneEngine.Core.Perks;
    using ZoneEngine.Core.Playfields;
    using ZoneEngine.Script;

    using Utility;
    using AORebirth.Database.Dao;
    using AORebirth.Stats;
    using System.Collections.Generic;
    using AORebirth.Database.Entities;
    using System.Linq;

    #endregion

    /// <summary>
    /// </summary>
    public class ClientConnected
    {
        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <param name="str">
        /// </param>
        /// <returns>
        /// </returns>
        public static byte[] StrToByteArray(string str)
        {
            var encoding = new ASCIIEncoding();
            return encoding.GetBytes(str);
        }

        /// <summary>
        /// </summary>
        /// <param name="charID">
        /// </param>
        /// <param name="client">
        /// </param>
        public void Read(int charID, ZoneClient client)
        {
            // Don't edit anything in this region
            // unless you are 300% sure you know what you're doing

            // Character is created and read when Client connects in Client.cs->CreateCharacter
            // client.CreateCharacter(charID);
            client.PacketSequencing.BeginSessionReadyBlock(client.SessionLifecycle.EnterReadyBlockForSessionInit);
            client.Server.Info(
                client,
                "Client connected. ID: {0} IP: {1} Character name: {2}",
                client.Controller.Character.Identity.Instance,
                client.ClientAddress,
                client.Controller.Character.Name);

            ActiveNanoRuntimeService.Default.PrepareCharacterForLogin(client.Controller.Character);

            // Saved apartment PF survives logout; in-memory leases do not. Re-bind so
            // PAF building stamp and later lobby re-entry keep the same instance.
            LuxuryApartmentInstanceRuntime.RehydrateLeaseFromLogin(client.Controller.Character);

            // now we have to start sending packets like 
            // character stats, inventory, playfield info
            // and so on. I will put some packets here just 
            // to get us in game. We have to start moving
            // these packets somewhere else and make packet 
            // builders instead of sending (half) hardcoded
            // packets.
            GridZoneInDiagnostics.BeginGridZoneIn(client);

            /* send chat server info to client */
            ChatServerInfoMessageHandler.Default.Send(client.Controller.Character);

            WorldEntrySummary.Begin(client, "zone_login");

            /* send playfield info to client */
            PlayfieldAnarchyFMessageHandler.Default.Send(client.Controller.Character);

            // Capture 20260806-202421: DoorStatusUpdate for apartment exit door immediately after PAF.
            if (client.Controller.Character.Playfield != null
                && LuxuryApartmentSunriseRules.IsLuxuryApartmentPlayfield(
                    client.Controller.Character.Playfield.Identity.Instance))
            {
                DoorStatusUpdateMessageHandler.Default.SendStatus(
                    client.Controller.Character,
                    new Identity
                    {
                        Type = IdentityType.Door,
                        Instance = LuxuryApartmentSunriseRules.ApartmentExitDoorInstance
                    },
                    false);
            }
            // Live 20260725-184103: DoorFullUpdate flood starts immediately after PAF
            // (before SCFU/FullCharacter). Delayed-only replay left the map grey/wrong.
            if (client.Controller.Character.Playfield != null
                && MissionInstanceService.IsMissionInstancePlayfield(
                    client.Controller.Character.Playfield.Identity.Instance))
            {
                if (MissionAcgBindingRuntime.IsBoundLivePlayfield(
                    client.Controller.Character.Playfield.Identity.Instance))
                {
                    MissionAcgRuntimeManager.SendForCharacter(
                        client,
                        client.Controller.Character);
                }
                else
                {
                    MissionInstanceDoorReplay.SendForCharacter(
                        client,
                        client.Controller.Character);
                }

                MissionInstanceService.TryRestampOutdoorReturnFromAccepted(client.Controller.Character);
            }

            // Sparrow Flight CanFly requires expansionplayfield==0 (RK). Set from playfield id.
            if (client.Controller.Character.Playfield != null)
            {
                AdventurerMorphFlightRuntime.SyncExpansionPlayfield(
                    client.Controller.Character,
                    client.Controller.Character.Playfield.Identity.Instance);
            }


            foreach (
Vendor vendor in
Pool.Instance.GetAll<Vendor>(
client.Controller.Character.Playfield.Identity,
(int)IdentityType.VendingMachine))
            {
                VendingMachineFullUpdateMessageHandler.Default.Send(client.Controller.Character, vendor);
            }

            // Debug-only combat test mob spawns are disabled on normal login.
            // Use /command spawn / spawnleet for explicit test spawns.

            /* Live login advertises the character as socially/action-ready. */
            client.Controller.Character.Stats[StatIds.socialstatus].BaseValue = 4;

            // Stat.SendDirect(client, 521, 0, false);

            var identity = new Identity { Type = IdentityType.CanbeAffected, Instance = charID };

            var gameTimeMessage = new GameTimeMessage
                                  {
                                      Identity = identity,
                                      Unknown1 = 30024.0f,
                                      Unknown3 = 185408,
                                      Unknown4 = 80183.3125f
                                  };
            client.SendCompressed(gameTimeMessage);
            client.LastGameTimeSyncUtc = DateTime.UtcNow;

            InitializeActionableState(client);
            SendActionableState(client);
            CharacterActionMessageHandler.Default.SendSkillAvailable(
                client.Controller.Character,
                (int)StatIds.treatment);

            client.SendCompressed(
                new StatMessage
                {
                    Identity = identity,
                    Stats =
                        new[]
                        {
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = CharacterStat.SocialStatus,
                                Value2 = (uint)client.Controller.Character.Stats[StatIds.socialstatus].Value
                            }
                        }
                });


            /* set SocialStatus to 0 */
            // Stat.SendDirect(client, 521, 0, false);

            /* again */
            // Stat.SendDirect(client, 521, 0, false);

            /* visual */
            Playfield currentPlayfield = null;
            client.Controller.Character.CalculateSkills();
            SyncVitalStats(client.Controller.Character);
            client.PacketSequencing.RunSessionReadyFullCharacterSequence(
                () => PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.StagePrivateCityReadyBlockBegin,
                    PlayfieldLifecycleTrace.MessagePrivateCityReadyBlockBegin,
                    identity),
                () => PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.StagePrivateCitySimpleCharFullUpdateBroadcast,
                    PlayfieldLifecycleTrace.MessageSimpleCharFullUpdate,
                    identity),
                () => SimpleCharFullUpdate.SendToPlayfield(client),
                () =>
                {
                    /* inventory, items and all that */
                    GuestKeyGeneratorInteractionHandler.ProcessCityAccessCardLifetimes(client.Controller.Character);
                    Packets.WeaponItemFullUpdate.SendWeaponDefinitions(client.Controller.Character);
                    currentPlayfield = client.Controller.Character.Playfield as Playfield;
                },
                () =>
                {
                    if (currentPlayfield != null)
                    {
                        currentPlayfield.SendPrivateCityPreFullCharacterReadyBlock(client, client.Controller.Character);
                    }
                },
                () => PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.StagePrivateCityFullCharacter,
                    PlayfieldLifecycleTrace.MessageFullCharacter,
                    identity),
                client.SessionLifecycle.EnterFullCharacterBoundaryForSessionInit,
                () =>
                {
                    // Same reason as perks: this client often never sends CharInPlay after zone/relog,
                    // so the mission journal must be restored here or it stays empty.
                    MissionAcgLifecycleService.TryCleanupPendingForCharacter(
                        client,
                        client.Controller.Character);
                    MissionRollFeeService.TryRecoverAndSendForLogin(
                        client,
                        client.Controller.Character);
                    ZoneEngine.Core.Missions.MissionAcceptService.TryResendForLogin(client.Controller.Character);
                    MissionTokenProgressTracker.TryResumePendingClientUpdates(
                        client.Controller.Character);

                    // Gold 080425: Door/Chest FullUpdates land with SCFU before FullCharacter.
                    // Send here (not post-PAF) so the client accepts door meshes + map icons.
                    if (client.Controller.Character.Playfield != null
                        && MissionAcgBindingRuntime.IsBoundLivePlayfield(
                            client.Controller.Character.Playfield.Identity.Instance))
                    {
                        MissionAcgRuntimeManager.ClearSent(client.Controller.Character);
                        MissionAcgRuntimeManager.SendForCharacter(
                            client,
                            client.Controller.Character);
                    }
                    else
                    {
                        ZoneEngine.Core.Missions.MissionInstanceDoorReplay.SendForCharacter(
                            client,
                            client.Controller.Character);
                    }

                    CombatXpRuntimeService.LogXpWireSnapshot(
                        client.Controller.Character,
                        "ClientConnected",
                        "zone-login-before-prepare");
                    CombatXpRuntimeService.PrepareXpStatsForLogin(client.Controller.Character);
                    CombatXpRuntimeService.LogXpWireSnapshot(
                        client.Controller.Character,
                        "ClientConnected",
                        "zone-login-after-prepare-before-fullchar");
                    FullCharacterMessageHandler.Default.Send(client.Controller.Character);
                    // Client often never sends CharInPlay after login; bag UI stays empty until
                    // zone hop. Push InventoryUpdate immediately (and delayed) after FullCharacter.
                    InventoryContainerRuntimeService.Default.ResyncCharacterInventoryToClient(
                        client.Controller.Character);
                    // Client only honors the XP-bar floor (LastSaveXP 372) from a standalone
                    // StatMessage, not from the FullCharacter bulk. Re-send floor stats
                    // (Unknown=1, no cumulative XP, no feedback) so the bar shows progress
                    // instead of the raw cumulative XP after zone/relog.
                    CombatXpRuntimeService.SyncXpBarStatsOnLogin(client.Controller.Character);
                    CombatXpRuntimeService.LogXpWireSnapshot(
                        client.Controller.Character,
                        "ClientConnected",
                        "zone-login-after-fullchar");

                    // Stuck hoverboard/yalm: MonsterData/IsVehicle can persist after NCU cancel
                    // or unequip if MorphState was lost on reboot.
                    AdventurerMorphFlightRuntime.HealOrphanedVehicleMorphOnLogin(
                        client.Controller.Character);

                    // FullCharacter has no perk list yet — re-teach trained perks immediately
                    // (do not wait for CharInPlay; reconnect UI clears on FullCharacter).
                    var loginCharacter = client.Controller.Character as Character;
                    if (loginCharacter != null)
                    {
                        PerkRuntimeService.Default.ResendPerkActions(loginCharacter);
                    }

                    // Thrak garden-key journal entries are capture QFUs — re-emit Active missions after zone/relog.
                    ZoneEngine.Core.Thrak.Quests.ThrakGardenKeyQuestRuntime.TryResendActiveMissionsForLogin(
                        client.Controller.Character);

                    // Arete Rex→Marcus→Flint tip journal (Talk to Flint Novak) — same relog wipe as Thrak.
                    ZoneEngine.Core.Arete.Quests.RexMarcusChainCoordinator.TryResendActiveTipsForLogin(
                        client.Controller.Character);

                    // Sacred Thrak garden key is permanent; restore if already earned and missing.
                    ZoneEngine.Core.Thrak.Quests.ThrakGardenKeyQuestRuntime.TryRestoreGardenKeyIfMissing(
                        client.Controller.Character);
                },
                () =>
                {
                    if (currentPlayfield != null)
                    {
                        currentPlayfield.SendPrivateCityPlayfieldReadyBlock(client, client.Controller.Character);
                    }
                },
                () => PlayfieldLifecycleTrace.Record(
                    PlayfieldLifecycleTrace.FlowPrivateCityReadyInit,
                    PlayfieldLifecycleTrace.StagePrivateCityReadyBlockEnd,
                    PlayfieldLifecycleTrace.MessagePrivateCityReadyBlockEnd,
                    identity));

            ActiveNanoRuntimeService.Default.SchedulePostLoginNanoRestore(client);
            InventoryContainerRuntimeService.Default.SchedulePostLoginInventoryResync(client);

            var specials = new[]
                           {
                               // Capture 20260724-001643 SAW: MAAT 211357/211358, DIIT 42033/42032, BRAW 211401/211402.
                               new SpecialAttack
                               {
                                   Unknown1 = 0x0003399D,
                                   Unknown2 = 0x0003399E,
                                   Unknown3 = 0x00000064,
                                   Unknown4 = "MAAT"
                               },
                               new SpecialAttack
                               {
                                   Unknown1 = 0x0000A431,
                                   Unknown2 = 0x0000A430,
                                   Unknown3 = 0x00000090,
                                   Unknown4 = "DIIT"
                               },
                               new SpecialAttack
                               {
                                   Unknown1 = 0x000339C9,
                                   Unknown2 = 0x000339CA,
                                   Unknown3 = 0x0000008E,
                                   Unknown4 = "BRAW"
                               }
                           };
            var specialAttackWeaponMessage = new SpecialAttackWeaponMessage { Identity = identity, Specials = specials };

            client.SendCompressed(specialAttackWeaponMessage);
            WorldEntrySummary.Complete(client);

            // done


            // spawn all active monsters to client
            // TODO: Implement NonPlayerCharacterHandler
            // NonPlayerCharacterHandler.SpawnMonstersInPlayfieldToClient(client, client.Character.PlayField);

            // TODO: Implement VendorHandler
            // if (VendorHandler.GetNumberofVendorsinPlayfield(client.Character.PlayField) > 0)
            // {
            // Shops 
            // VendorHandler.GetVendorsInPF(client);
            // }

            // Weapon item full updates are sent above immediately after full character sync.

            // TODO: create a better alternative to ProcessTimers
            // client.Character.ProcessTimers(DateTime.Now + TimeSpan.FromMilliseconds(200));
            client.Controller.Character.CalculateSkills();
            SyncVitalStats(client.Controller.Character);
            InventoryContainerRuntimeService.Default.EnsureWeaponVisualMeshes(client.Controller.Character, false);

            if (currentPlayfield != null)
            {
                client.PacketSequencing.RunVisibilityInitializationSequence(
                    () => PlayfieldLifecycleTrace.Record(
                        PlayfieldLifecycleTrace.FlowSamePlayfieldVisibility,
                        PlayfieldLifecycleTrace.StageVisibilityJoinerReady,
                        "ClientConnected",
                        client.Controller.Character.Identity),
                    client.SessionLifecycle.EnterCharInPlayForVisibilityEntry,
                    () => currentPlayfield.AnnouncePlayerVisibility(client.Controller.Character),
                    () =>
                    {
                        currentPlayfield.SendSCFUsToClient(new IMSendPlayerSCFUs { toClient = client });
                        // CellAO sends statues on CharInPlay; this client often never sends CharInPlay.
                        currentPlayfield.SendStaticDynelsToClientAfterExternalPlayfieldArrival(
                            client.Controller.Character);
                    });
            }

            AppearanceUpdateMessageHandler.Default.Send(client.Controller.Character);
            CompleteDeathRespawnCharInPlay(client);
            SendAliveDeadTimerBaseline(client);

            // Daily rewards web UI: publish account Taken board (browser often has no CharacterID).
            try
            {
                DailyLoginRewardRuntime.PublishActiveAccountBoard(client.Controller.Character);
            }
            catch
            {
            }

            // done, so we call a hook.
            // Call all OnConnect script Methods
            ScriptCompiler.Instance.CallMethod("OnConnect", client.Controller.Character);

            // Timers are allowed to update client stats now.
            client.PacketSequencing.CompleteSessionInitialization(
                client.SessionLifecycle.CompleteInPlayForSessionInit);
            client.Controller.Character.DoNotDoTimers = false;
            NewCharacterStartAreaSelectionRuntime.Schedule(client);

            // Do NOT ExchangeOnlinePresence / SCFU-seed remotes here — that spawned
            // visible doubles of the same players in both zones.
            if (Program.ISComClient != null && Program.ISComClient.IsConnected
                && client.Controller.Character.Playfield != null)
            {
                Program.ISComClient.TrySend(
                    new AORebirth.Communication.Messages.ChatCommand
                    {
                        CharacterId = client.Controller.Character.Identity.Instance,
                        ChatCommandString =
                            "#aorebirth-pf " + client.Controller.Character.Playfield.Identity.Instance
                    });
            }
        }

        private static void CompleteDeathRespawnCharInPlay(ZoneClient client)
        {
            ICharacter character = client.Controller.Character;
            if (character.Stats[StatIds.health].Value <= 0 || character.Stats[StatIds.deadtimer].Value != 75)
            {
                return;
            }

            character.Starting = false;
            client.SendCompressed(new CharInPlayMessage { Identity = character.Identity, Unknown = 0x00 });
            LogUtil.Debug(
                DebugInfoDetail.Network,
                string.Format(
                    "Death respawn CharInPlay completion sent target={0} unknown=0 hp={1}/{2} deadTimer={3}",
                    character.Identity,
                    character.Stats[StatIds.health].Value,
                    character.Stats[StatIds.life].Value,
                    character.Stats[StatIds.deadtimer].Value));
        }

        private static void InitializeActionableState(ZoneClient client)
        {
            // Only restore sit for intentional logout-timer reconnects.
            // MoveMode==Sit after grid zoning is a disconnect artifact and must not be restored.
            bool restoreSeatedPosture = client.PreserveLogoutSitOnConnect;

            // Match the captured live alive/actionable baseline.
            SetStat(client, StatIds.state, 0);

            if (restoreSeatedPosture)
            {
                client.Controller.Character.EnterLogoutSitPosture();
                client.PreserveLogoutSitOnConnect = false;
            }
            else
            {
                Character concrete = client.Controller.Character as Character;
                if (concrete != null)
                {
                    concrete.UpdateMoveType(25);
                }

                SetStat(client, StatIds.currentmovementmode, (int)MoveModes.Run);
                SetStat(client, StatIds.prevmovementmode, (int)MoveModes.Run);
                UpsertCharacterStat(
                    client.Controller.Character.Identity.Instance,
                    StatIds.currentmovementmode,
                    (int)MoveModes.Run);
                UpsertCharacterStat(
                    client.Controller.Character.Identity.Instance,
                    StatIds.prevmovementmode,
                    (int)MoveModes.Run);
            }


            // Start adding GM/Expansion in stats
            var character = client.Controller.Character;

            // 1. take a character from the DB (the only valid way for you)
            var characterData = CharacterDao.Instance
                .GetAll(new { })
                .FirstOrDefault(c => c.Id == character.Identity.Instance);

            if (characterData == null)
            {
                Console.WriteLine($"[GM/EXP DEBUG] Character NOT FOUND ID={character.Identity.Instance}");
                return;
            }

            // 2. get login data via Username
            var login = LoginDataDao.Instance.GetByUsername(characterData.Username);

            if (login == null)
            {
                Console.WriteLine($"[GM/EXP DEBUG] LOGIN NOT FOUND for {characterData.Username}");
                return;
            }

            Console.WriteLine($"[GM/EXP DEBUG] CharacterID = {character.Identity.Instance}");
            Console.WriteLine($"[GM/EXP DEBUG] Username = {characterData.Username}");
            Console.WriteLine($"[GM/EXP DEBUG] GM = {login.GM}");
            Console.WriteLine($"[GM/EXP DEBUG] EXP = {login.Expansions}");

            // 3. REGISTRATION IN STATS (CORRECT FOR IStatList - NO Add/Contains)
            SetStat(client, StatIds.gmlevel, login.GM);
            SetStat(client, StatIds.expansion, login.Expansions | 2); // Shadowlands bit — CellAO statue OnUse requires it

            UpsertCharacterStat(character.Identity.Instance, StatIds.gmlevel, login.GM);
            UpsertCharacterStat(character.Identity.Instance, StatIds.expansion, login.Expansions | 2);

            // optional safety reset (if engine ask refresh)
            client.Controller.SendChangedStats();

            // End Here


            SetStat(client, StatIds.currentstate, 0);
            SetStat(client, StatIds.waitstate, 0);
            SetStat(client, StatIds.socialstatus, 4);
            SetStat(client, StatIds.specialcondition, 3);
            SetStat(client, StatIds.actioncategory, 0);
         
        }

        private static void SyncVitalStats(ICharacter character)
        {
            int maxLife = Math.Max(1, character.Stats[StatIds.life].Value);
            int maxNano = Math.Max(0, character.Stats[StatIds.maxnanoenergy].Value);

            if (character.Starting)
            {
                character.Stats[StatIds.health].Value = maxLife;
                character.Stats[StatIds.health].BaseValue = (uint)maxLife;
                character.Stats[StatIds.currentnano].Value = maxNano;
                character.Stats[StatIds.currentnano].BaseValue = (uint)maxNano;
                UpsertCharacterStat(character.Identity.Instance, StatIds.health, maxLife);
                UpsertCharacterStat(character.Identity.Instance, StatIds.currentnano, maxNano);
                return;
            }

            if (character.Stats[StatIds.health].Value > maxLife)
            {
                character.Stats[StatIds.health].Value = maxLife;
                character.Stats[StatIds.health].BaseValue = (uint)maxLife;
                UpsertCharacterStat(character.Identity.Instance, StatIds.health, maxLife);
            }

            if (character.Stats[StatIds.currentnano].Value > maxNano)
            {
                character.Stats[StatIds.currentnano].Value = maxNano;
                character.Stats[StatIds.currentnano].BaseValue = (uint)maxNano;
                UpsertCharacterStat(character.Identity.Instance, StatIds.currentnano, maxNano);
            }
        }

        private static void SetStat(ZoneClient client, StatIds stat, int value)
        {
            client.Controller.Character.Stats[stat].Value = value;
            client.Controller.Character.Stats[stat].BaseValue = (uint)value;
        }

        private static void UpsertCharacterStat(int characterId, StatIds statId, int value)
        {
            DBStats stat = StatDao.Instance
                .GetAll(new { Type = 50000, Instance = characterId, StatId = (int)statId })
                .FirstOrDefault();

            if (stat == null)
            {
                StatDao.Instance.Add(new DBStats
                {
                    Type = 50000,
                    Instance = characterId,
                    StatId = (int)statId,
                    StatValue = value
                });
                return;
            }

            stat.StatValue = value;
            StatDao.Instance.Save(stat);
        }

        private static void SendAliveDeadTimerBaseline(ZoneClient client)
        {
            ICharacter character = client.Controller.Character;
            if (character.Stats[StatIds.health].Value <= 0)
            {
                return;
            }

            SetStat(client, StatIds.deadtimer, 75);
            client.SendCompressed(
                new StatMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Stats =
                        new[]
                        {
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = CharacterStat.DeadTimer,
                                Value2 = (uint)character.Stats[StatIds.deadtimer].Value
                            }
                        }
                });
        }

        private static void SendActionableState(ZoneClient client)
        {
            ICharacter character = client.Controller.Character;
            client.SendCompressed(
                new StatMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Stats =
                        new[]
                        {
                            new GameTuple<CharacterStat, uint>
                            {
                                Value1 = CharacterStat.State,
                                Value2 = (uint)character.Stats[StatIds.state].Value
                            }
                        }
                });
        }

        #endregion
    }
}
