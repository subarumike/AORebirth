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

    // TODO: Make this to EntityEnvent or something like this
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Requirements;
    using AORebirth.Core.Statels;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;
    using AORebirth.ObjectManager;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Arete.Quests;
    using ZoneEngine.Core.Playfields;

    #endregion

    /// <summary>
    /// </summary>
    [MessageHandler(MessageHandlerDirection.All)]
    public class GenericCmdMessageHandler : BaseMessageHandler<GenericCmdMessage, GenericCmdMessageHandler>
    {
        private static readonly TimeSpan CorpseUseAcknowledgeDelay = TimeSpan.FromMilliseconds(550);

        private static readonly TimeSpan SurgeryClinicSpecialAvailableDelay = TimeSpan.FromMilliseconds(3500);

        private static readonly ISet<int> CapturedSurgeryClinicTemplateIds =
            new HashSet<int>
            {
                43553,
                295742
            };

        private const int SurgeryClinicNanoId = 0x26732;

        private const int SurgeryClinicNanoDuration = 90000;

        private const int SurgeryClinicImplantAccessSeconds = 300;

        private const int SurgeryClinicSpecialStatId = 124;

        private const int SurgeryClinicSpecialLockSeconds = 5;

        private const int SurgeryClinicCreditCost = 300;

        private const int CapturedGridPlayfieldId = 152;

        private const int GridEnterTerminalTemplateId = 95350;

        private const int GridExitTerminalTemplateId = 95351;

        private const float GridDestinationTerminalClearance = 2.5f;

        private static readonly CapturedGridTerminalRoute[] CapturedGridTerminalRoutes =
        {
            new CapturedGridTerminalRoute(
                567,
                unchecked((int)0xC0010237),
                unchecked((int)0xC00D0098),
                177.5012f,
                3.7750f,
                179.1060f,
                0.0f,
                -0.01321465f,
                0.0f,
                0.9999127f,
                "captures/20260622-003221/events.log:140-145,346-347;PF152 nearest exit Terminal:C00D0098"),
            new CapturedGridTerminalRoute(
                655,
                unchecked((int)0xC002028F),
                unchecked((int)0xC04E0098),
                234.3062f,
                3.7750f,
                212.8138f,
                0.0f,
                1.0f,
                0.0f,
                -4.371139E-08f,
                "captures/20260621-091447/events.log:255-256,321-322,645-646;PF152 nearest exit Terminal:C04E0098"),
            new CapturedGridTerminalRoute(
                705,
                unchecked((int)0xC00602C1),
                unchecked((int)0xC0010098),
                174.8353f,
                37.3750f,
                240.2071f,
                0.0f,
                1.0f,
                0.0f,
                -4.371139E-08f,
                "captures/20260622-003221/events.log:2420-2423,2624-2625;PF152 nearest exit Terminal:C0010098"),
            new CapturedGridTerminalRoute(
                730,
                unchecked((int)0xC00002DA),
                unchecked((int)0xC0010098),
                174.8353f,
                37.3750f,
                240.2071f,
                0.0f,
                1.0f,
                0.0f,
                -4.371139E-08f,
                "captures/20260622-003221/events.log:3109-3112,3313-3314;PF152 nearest exit Terminal:C0010098"),
            new CapturedGridTerminalRoute(
                695,
                unchecked((int)0xC00802B7),
                unchecked((int)0xC0030098),
                188.4104f,
                37.37542f,
                234.9863f,
                0.0f,
                0.9952047f,
                0.0f,
                0.09781352f,
                "captures/20260622-003221/events.log:3806-3809,4010-4011;PF152 nearest exit Terminal:C0030098"),
            new CapturedGridTerminalRoute(
                670,
                unchecked((int)0xC003029E),
                unchecked((int)0xC0070098),
                203.2488f,
                37.38467f,
                222.7339f,
                0.0f,
                -0.9641039f,
                0.0f,
                0.2655251f,
                "captures/20260622-003221/events.log:5421-5424,5639-5640;PF152 nearest exit Terminal:C0070098"),
            new CapturedGridTerminalRoute(
                560,
                unchecked((int)0xC0060230),
                unchecked((int)0xC00E0098),
                183.9474f,
                44.0150f,
                150.8788f,
                0.0f,
                0.7062106f,
                0.0f,
                0.7080019f,
                "captures/20260622-003221/events.log:7519-7522,7733-7734;PF152 nearest exit Terminal:C00E0098"),
            new CapturedGridTerminalRoute(
                700,
                unchecked((int)0xC00202BC),
                unchecked((int)0xC0090098),
                224.5596f,
                44.0050f,
                231.8543f,
                0.0f,
                -0.7107669f,
                0.0f,
                0.7034276f,
                "captures/20260622-003221/events.log:8380-8383,8594-8595;PF152 nearest exit Terminal:C0090098"),
            new CapturedGridTerminalRoute(
                505,
                unchecked((int)0xC02301F9),
                unchecked((int)0xC0100098),
                215.8618f,
                43.9950f,
                151.7285f,
                0.0f,
                0.6904334f,
                0.0f,
                0.7233959f,
                "captures/20260622-003221/events.log:9339-9342,9563-9564;PF152 nearest exit Terminal:C0100098")
        };

        private sealed class CapturedGridTerminalRoute
        {
            public CapturedGridTerminalRoute(
                int sourcePlayfieldId,
                int sourceTerminalInstance,
                int destinationExitTerminalInstance,
                float destinationX,
                float destinationY,
                float destinationZ,
                float headingX,
                float headingY,
                float headingZ,
                float headingW,
                string evidence)
            {
                this.SourcePlayfieldId = sourcePlayfieldId;
                this.SourceTerminalInstance = sourceTerminalInstance;
                this.DestinationExitTerminalInstance = destinationExitTerminalInstance;
                this.DestinationX = destinationX;
                this.DestinationY = destinationY;
                this.DestinationZ = destinationZ;
                this.HeadingX = headingX;
                this.HeadingY = headingY;
                this.HeadingZ = headingZ;
                this.HeadingW = headingW;
                this.Evidence = evidence;
            }

            public int SourcePlayfieldId { get; private set; }

            public int SourceTerminalInstance { get; private set; }

            public int DestinationExitTerminalInstance { get; private set; }

            public float DestinationX { get; private set; }

            public float DestinationY { get; private set; }

            public float DestinationZ { get; private set; }

            public float HeadingX { get; private set; }

            public float HeadingY { get; private set; }

            public float HeadingZ { get; private set; }

            public float HeadingW { get; private set; }

            public string Evidence { get; private set; }
        }

        private const string SurgeryClinicFeedback =
            "~&!!!\":!!!)<sHYou have 5 minutes (or until you leave the playfield) to swap implants.";

        private static readonly IDictionary<string, Profession> OfabProfessionVendorRequirements =
            new Dictionary<string, Profession>(StringComparer.OrdinalIgnoreCase)
            {
                { "OFADV", Profession.Adventurer },
                { "OFAGT", Profession.Agent },
                { "OFCRT", Profession.Bureaucrat },
                { "OFDOC", Profession.Doctor },
                { "OFENF", Profession.Enforcer },
                { "OFENG", Profession.Engineer },
                { "OFFIX", Profession.Fixer },
                { "OFKEE", Profession.Keeper },
                { "OFMA", Profession.MartialArtist },
                { "OFNT", Profession.Nanotechnician },
                { "OFPMQ3T", Profession.Metaphysicist },
                { "OFSHD", Profession.Shade },
                { "OFSOL", Profession.Soldier },
                { "OFTRD", Profession.Trader }
            };

        private static readonly IDictionary<Profession, string> ProfessionFeedbackNames =
            new Dictionary<Profession, string>
            {
                { Profession.Adventurer, "Adventurer" },
                { Profession.Agent, "Agent" },
                { Profession.Bureaucrat, "Bureaucrat" },
                { Profession.Doctor, "Doctor" },
                { Profession.Enforcer, "Enforcer" },
                { Profession.Engineer, "Engineer" },
                { Profession.Fixer, "Fixer" },
                { Profession.Keeper, "Keeper" },
                { Profession.MartialArtist, "Martial Artist" },
                { Profession.Metaphysicist, "Meta-Physicist" },
                { Profession.Nanotechnician, "Nano-Technician" },
                { Profession.Shade, "Shade" },
                { Profession.Soldier, "Soldier" },
                { Profession.Trader, "Trader" }
            };

        private const string OfabGmRequirementFeedback = "Your GM capabilities is required to be at least 1!";

        #region Inbound

        /// <summary>
        /// </summary>
        /// <param name="message">
        /// </param>
        /// <param name="client">
        /// </param>
        /// <exception cref="NullReferenceException">
        /// </exception>
        protected override void Read(GenericCmdMessage message, IZoneClient client)
        {
            Identity target = message.Target != null && message.Target.Length > 0
                                  ? message.Target[0]
                                  : Identity.None;
            Identity routedCorpseIdentity;

            client.Server.Info(
                client,
                "GenericCmd action={0}({1}) temp1={2} count={3} temp4={4} user={5} target={6}",
                message.Action,
                (int)message.Action,
                message.Temp1,
                message.Count,
                message.Temp4,
                message.User,
                target);

            switch (message.Action)
            {
                case GenericCmdAction.Get:
                    break;
                case GenericCmdAction.Drop:
                    break;
                case GenericCmdAction.Use:
                    if (RexB18DBoxProgressTracker.TryObserveBoxUse(
                        client.Controller.Character,
                        target))
                    {
                        this.Acknowledge(client.Controller.Character, message);
                    }
                    else if (target.Type == IdentityType.Inventory)
                    {
                        client.Controller.UseItem(target);

                        // Acknowledge action
                        this.Acknowledge(client.Controller.Character, message);
                    }
                    else if (target.Type == IdentityType.ArmorPage || target.Type == IdentityType.SocialPage)
                    {
                        if (client.Controller.TryUseBackpackContainer(target))
                        {
                            this.Acknowledge(client.Controller.Character, message);
                        }
                    }
                    else if (target.Type == IdentityType.Container)
                    {
                        IInventoryPage backpackPage;
                        if (client.Controller.Character.BaseInventory.TryGetBackpackPage(target, out backpackPage))
                        {
                            BackpackContainerActionMessageHandler.Default.SendClose(client.Controller.Character, target);
                            client.Controller.Character.BaseInventory.MarkBackpackClosed(target);
                            this.Acknowledge(client.Controller.Character, message);
                        }
                    }
                    else if (target.Type == IdentityType.Corpse)
                    {
                        bool used = client.Controller.Character.Playfield.TryUseCorpse(
                            client.Controller.Character,
                            target);

                        client.Server.Info(
                            client,
                            "CorpseUse direct target={0} used={1}",
                            target,
                            used);

                        if (used)
                        {
                            this.AcknowledgeCorpseUseDelayed(client.Controller.Character, message, target);
                        }
                    }
                    else if (target.Type == IdentityType.CanbeAffected
                             && this.TryRouteDeadNpcCorpseUse(client, target, out routedCorpseIdentity))
                    {
                        this.AcknowledgeCorpseUseDelayed(client.Controller.Character, message, routedCorpseIdentity);
                    }
                    else if (this.TryHandleCapturedGridTerminalUse(client, target))
                    {
                        break;
                    }
                    else if (this.TryHandleGridEnterTerminalUse(client, target))
                    {
                        break;
                    }
                    else if (this.TryHandleSurgeryClinicTerminalUse(client, message, target))
                    {
                        break;
                    }
                    else
                    {
                        if (Pool.Instance.Contains(target))
                        {
                            // TODO: Call OnUse of the targets controller
                            // Static dynels first
                            IEventHolder temp = null;
                            try
                            {
                                temp =
                                    Pool.Instance.GetObject<IEventHolder>(
                                        client.Controller.Character.Playfield.Identity,
                                        target);
                            }
                            catch (Exception)
                            {
                            }
                            if (temp != null)
                            {
                                var entity = temp as IEntity;
                                if (entity != null)
                                {
                                    Event ev = temp.Events.FirstOrDefault(x => x.EventType == EventType.OnUse);
                                    if (ev != null)
                                    {
                                        ev.Perform(client.Controller.Character, entity);
                                        this.Acknowledge(client.Controller.Character, message);
                                    }
                                    else
                                    {
                                        ev = temp.Events.FirstOrDefault(x => x.EventType == EventType.OnTrade);
                                        if (ev != null)
                                        {
                                            var vendor = entity as Vendor;
                                            if (vendor != null
                                                && this.TryDenyOfabProfessionVendor(client, message, vendor))
                                            {
                                                break;
                                            }

                                            ev.Perform(client.Controller.Character, entity);

                                            TemporaryBag tempBag = new TemporaryBag(
                                                client.Controller.Character.Identity,
                                                new Identity()
                                                {
                                                    Type = IdentityType.TempBag,
                                                    Instance =
                                                Pool.Instance.GetFreeInstance<TemporaryBag>(
                                                    0,
                                                    IdentityType.TempBag)
                                                },
                                                client.Controller.Character.Identity,
                                                target);
                                            client.Controller.Character.ShoppingBag = tempBag;
                                            TradeMessageHandler.Default.Send(client.Controller.Character, tempBag);
                                            this.Acknowledge(client.Controller.Character, message);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Use statel (doors, grid terminals etc)
#if DEBUG
                            string s = string.Format(
                                "Generic Command received:\r\nAction: {0} ({1}){2}Target: {3} {4}",
                                message.Action,
                                (int)message.Action,
                                Environment.NewLine,
                                target.Type,
                                target.ToString(true));
                            ChatTextMessageHandler.Default.Send(client.Controller.Character, s);
#endif
                            client.Controller.UseStatel(target);
                        }
                    }

                    break;
                case GenericCmdAction.UseItemOnItem:
                    IItem item =
                        Pool.Instance.GetObject<IInventoryPage>(
                            new Identity()
                            {
                                Type = (IdentityType)client.Controller.Character.Identity.Instance,
                                Instance = (int)message.Target[0].Type
                            })[message.Target[0].Instance];
                    client.Controller.Character.Stats[StatIds.secondaryitemtemplate].Value = item.LowID;
                    //client.Controller.Character.Stats[StatIds.secondaryitemtype]
                    if (Pool.Instance.Contains(message.Target[1]))
                    {
                        StaticDynel temp =
                            Pool.Instance.GetObject<StaticDynel>(
                                client.Controller.Character.Playfield.Identity,
                                message.Target[1]);
                        if (temp != null)
                        {
                            Event ev = temp.Events.FirstOrDefault(x => x.EventType == EventType.OnUseItemOn);
                            if (ev != null)
                            {
                                ev.Perform(client.Controller.Character, temp);
                            }
                        }
                    }
                    else
                    {
                        client.Controller.UseStatel(message.Target[1], EventType.OnUseItemOn);
                    }
                    break;
            }
        }

        private bool TryRouteDeadNpcCorpseUse(
            IZoneClient client,
            Identity target,
            out Identity routedCorpseIdentity)
        {
            bool routed = client.Controller.Character.Playfield.TryUseDeadNpcCorpse(
                client.Controller.Character,
                target,
                out routedCorpseIdentity);

            client.Server.Info(
                client,
                "CorpseUse deadNpc target={0} routed={1} corpse={2}",
                target,
                routed,
                routedCorpseIdentity);

            return routed;
        }

        private bool TryHandleCapturedGridTerminalUse(
            IZoneClient client,
            Identity target)
        {
            ICharacter character = client.Controller.Character;
            StatelData statelData = this.GetStatelData(character, target);
            CapturedGridTerminalRoute route;

            if (!this.TryGetCapturedGridTerminalRoute(character, target, statelData, out route))
            {
                return false;
            }

            Dynel dynel = character as Dynel;
            if (dynel == null)
            {
                return false;
            }

            character.StopMovement();
            character.Stats[StatIds.externaldoorinstance].BaseValue = 0;
            character.Stats[StatIds.externalplayfieldinstance].BaseValue = 0;
            var destination = new Coordinate(
                route.DestinationX,
                route.DestinationY,
                route.DestinationZ);
            var heading = new AORebirth.Core.Vector.Quaternion(
                route.HeadingX,
                route.HeadingY,
                route.HeadingZ,
                route.HeadingW);
            character.Playfield.Teleport(
                dynel,
                destination,
                heading,
                new Identity { Type = IdentityType.Playfield, Instance = CapturedGridPlayfieldId });

            client.Server.Info(
                client,
                "Captured grid terminal use handled char={0} target={1} sourcePf={2} destPf={3} destExit={4:X8} dest=({5:F3},{6:F3},{7:F3}) evidence={8}",
                character.Identity,
                target,
                route.SourcePlayfieldId,
                CapturedGridPlayfieldId,
                unchecked((uint)route.DestinationExitTerminalInstance),
                destination.x,
                destination.y,
                destination.z,
                route.Evidence);

            return true;
        }

        private bool TryHandleGridEnterTerminalUse(
            IZoneClient client,
            Identity target)
        {
            ICharacter character = client.Controller.Character;
            StatelData statelData = this.GetStatelData(character, target);

            if (!this.IsGridEnterTerminal(character, target, statelData))
            {
                return false;
            }

            Function teleportFunction;
            int destinationPlayfieldId;
            int destinationInstance;
            if (!this.TryGetGridTeleportProxy2Destination(
                statelData,
                out teleportFunction,
                out destinationPlayfieldId,
                out destinationInstance))
            {
                client.Server.Info(
                    client,
                    "Grid enter terminal route skipped; no supported TeleportProxy2 char={0} target={1} sourcePf={2} template={3}",
                    character.Identity,
                    target,
                    statelData.PlayfieldId,
                    statelData.TemplateId);
                return false;
            }

            if (!this.GridTeleportRequirementsPass(character, teleportFunction))
            {
                this.SendGridTerminalRequirementFeedback(character, statelData, teleportFunction);
                client.Server.Info(
                    client,
                    "Grid enter terminal use blocked by requirements char={0} target={1} sourcePf={2} computerLiteracy={3} isfightingme={4}",
                    character.Identity,
                    target,
                    statelData.PlayfieldId,
                    character.Stats[StatIds.computerliteracy].Value,
                    character.Stats[StatIds.isfightingme].Value);
                return true;
            }

            Dynel dynel = character as Dynel;
            if (dynel == null)
            {
                return false;
            }

            StatelData destinationTerminal;
            if (!this.TryGetGridDestinationTerminal(destinationPlayfieldId, destinationInstance, out destinationTerminal))
            {
                this.SendGridTerminalFeedback(character, "Grid terminal route is unavailable.");
                client.Server.Info(
                    client,
                    "Grid enter terminal destination missing char={0} target={1} sourcePf={2} destPf={3} destInstance={4:X8}",
                    character.Identity,
                    target,
                    statelData.PlayfieldId,
                    destinationPlayfieldId,
                    unchecked((uint)destinationInstance));
                return true;
            }

            var heading = new AORebirth.Core.Vector.Quaternion(
                destinationTerminal.HeadingX,
                destinationTerminal.HeadingY,
                destinationTerminal.HeadingZ,
                destinationTerminal.HeadingW);
            AORebirth.Core.Vector.Quaternion.Normalize(heading);

            var destination = new AORebirth.Core.Vector.Vector3(
                destinationTerminal.X,
                destinationTerminal.Y,
                destinationTerminal.Z);
            AORebirth.Core.Vector.Vector3 forward =
                (AORebirth.Core.Vector.Vector3)heading.RotateVector3(AORebirth.Core.Vector.Vector3.AxisZ);
            destination.x += forward.x * GridDestinationTerminalClearance;
            destination.z += forward.z * GridDestinationTerminalClearance;

            character.StopMovement();
            character.Stats[StatIds.externaldoorinstance].BaseValue = 0;
            character.Stats[StatIds.externalplayfieldinstance].BaseValue = 0;
            character.Playfield.Teleport(
                dynel,
                new Coordinate(destination),
                heading,
                new Identity { Type = IdentityType.Playfield, Instance = CapturedGridPlayfieldId });

            client.Server.Info(
                client,
                "Grid enter terminal use handled char={0} target={1} sourcePf={2} destPf={3} destTerminal={4} dest=({5:F3},{6:F3},{7:F3}) evidence={8}",
                character.Identity,
                target,
                statelData.PlayfieldId,
                destinationPlayfieldId,
                destinationTerminal.Identity,
                destination.x,
                destination.y,
                destination.z,
                "playfields.dat Enter The Grid template 95350 TeleportProxy2 -> PF152; destination template 95351");

            return true;
        }

        private bool TryHandleSurgeryClinicTerminalUse(
            IZoneClient client,
            GenericCmdMessage message,
            Identity target)
        {
            ICharacter character = client.Controller.Character;
            StatelData statelData = this.GetStatelData(character, target);

            if (!this.IsCapturedSurgeryClinicTerminal(target, statelData))
            {
                return false;
            }

            int cashBefore = CashStatRules.Clamp(character.Stats[StatIds.cash].BaseValue);
            if (cashBefore < SurgeryClinicCreditCost)
            {
                client.Server.Info(
                    client,
                    "Surgery clinic terminal use blocked by insufficient captured-state support char={0} target={1} cash={2} cost={3}",
                    character.Identity,
                    target,
                    cashBefore,
                    SurgeryClinicCreditCost);
                return false;
            }

            int cashAfter = CashStatRules.Clamp((long)cashBefore - SurgeryClinicCreditCost);
            character.Stats[StatIds.cash].Set((uint)cashAfter);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)cashAfter);
            this.SendSurgeryClinicFeedback(character);
            this.SendSurgeryClinicCastNano(character);
            CharacterActionMessageHandler.Default.SetNanoDuration(
                character,
                character.Identity,
                SurgeryClinicNanoId,
                SurgeryClinicNanoDuration);
            this.GrantSurgeryClinicImplantAccess(character);
            this.SendSurgeryClinicSpecialUsed(character);
            this.Acknowledge(character, message);
            this.SendSurgeryClinicSpecialAvailableDelayed(character);

            client.Server.Info(
                client,
                "Surgery clinic terminal use handled char={0} target={1} statelTemplate={2} cashBefore={3} cashAfter={4} nano={5} duration={6} implantAccessSeconds={7} evidence={8}",
                character.Identity,
                target,
                statelData == null ? 0 : statelData.TemplateId,
                cashBefore,
                cashAfter,
                SurgeryClinicNanoId.ToString("X", CultureInfo.InvariantCulture),
                SurgeryClinicNanoDuration,
                SurgeryClinicImplantAccessSeconds,
                "captures/20260620-213807/events.log:51-52;captures/20260621-062224/events.log:52-71");

            return true;
        }

        private StatelData GetStatelData(ICharacter character, Identity target)
        {
            if (character == null || character.Playfield == null)
            {
                return null;
            }

            AORebirth.Core.Playfields.PlayfieldData playfieldData;
            if (!PlayfieldLoader.PFData.TryGetValue(character.Playfield.Identity.Instance, out playfieldData))
            {
                return null;
            }

            return playfieldData.Statels.FirstOrDefault(
                x => x.Identity.Type == target.Type && x.Identity.Instance == target.Instance);
        }

        private bool TryGetCapturedGridTerminalRoute(
            ICharacter character,
            Identity target,
            StatelData statelData,
            out CapturedGridTerminalRoute route)
        {
            route = null;

            if (character == null || character.Playfield == null)
            {
                return false;
            }

            if (target.Type != IdentityType.Terminal
                || statelData == null
                || statelData.TemplateId != GridEnterTerminalTemplateId)
            {
                return false;
            }

            if (statelData.PlayfieldId != character.Playfield.Identity.Instance
                || statelData.Identity.Type != target.Type
                || statelData.Identity.Instance != target.Instance)
            {
                return false;
            }

            route = CapturedGridTerminalRoutes.FirstOrDefault(
                x => x.SourcePlayfieldId == character.Playfield.Identity.Instance
                     && x.SourceTerminalInstance == target.Instance);

            return route != null;
        }

        private bool IsGridEnterTerminal(
            ICharacter character,
            Identity target,
            StatelData statelData)
        {
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            if (target.Type != IdentityType.Terminal
                || statelData == null
                || statelData.TemplateId != GridEnterTerminalTemplateId)
            {
                return false;
            }

            CapturedGridTerminalRoute route;
            if (this.TryGetCapturedGridTerminalRoute(character, target, statelData, out route))
            {
                return false;
            }

            return statelData.PlayfieldId == character.Playfield.Identity.Instance
                   && statelData.Identity.Type == target.Type
                   && statelData.Identity.Instance == target.Instance;
        }

        private bool TryGetGridTeleportProxy2Destination(
            StatelData statelData,
            out Function teleportFunction,
            out int destinationPlayfieldId,
            out int destinationInstance)
        {
            teleportFunction = null;
            destinationPlayfieldId = 0;
            destinationInstance = 0;

            if (statelData == null)
            {
                return false;
            }

            foreach (Event eventData in statelData.Events.Where(x => x.EventType == EventType.OnUse))
            {
                foreach (Function function in eventData.Functions.Where(
                    x => x.FunctionType == (int)FunctionType.TeleportProxy2))
                {
                    if (function.Arguments.Values.Count < 3)
                    {
                        continue;
                    }

                    int playfieldId = function.Arguments.Values[1].AsInt32();
                    if (playfieldId != CapturedGridPlayfieldId)
                    {
                        continue;
                    }

                    int destinationIndex = function.Arguments.Values[2].AsInt32();
                    teleportFunction = function;
                    destinationPlayfieldId = playfieldId;
                    destinationInstance = unchecked(
                        (int)(0xC0000000u | (uint)playfieldId | ((uint)destinationIndex << 16)));
                    return true;
                }
            }

            return false;
        }

        private bool GridTeleportRequirementsPass(ICharacter character, Function teleportFunction)
        {
            bool result = true;
            for (int i = 0; i < teleportFunction.Requirements.Count; i++)
            {
                Requirement requirement = teleportFunction.Requirements[i];
                if ((i == 0) && (requirement.ChildOperator == Operator.Or))
                {
                    result = false;
                }

                if (requirement.ChildOperator == Operator.Or)
                {
                    result |= requirement.CheckRequirement(character);
                }
                else
                {
                    result &= requirement.CheckRequirement(character);
                }

                if (!result && requirement.ChildOperator != Operator.Or)
                {
                    return false;
                }
            }

            return result;
        }

        private void SendGridTerminalRequirementFeedback(
            ICharacter character,
            StatelData statelData,
            Function teleportFunction)
        {
            int computerLiteracyRequirement;
            if (this.TryGetGreaterThanRequirement(
                teleportFunction,
                StatIds.computerliteracy,
                out computerLiteracyRequirement)
                && character.Stats[StatIds.computerliteracy].Value <= computerLiteracyRequirement)
            {
                this.SendGridTerminalFeedback(
                    character,
                    this.GetGridTerminalSystemText(statelData, "Computer")
                    ?? ("Your skill in Computer Literacy needs to be "
                        + (computerLiteracyRequirement + 1).ToString(CultureInfo.InvariantCulture)
                        + " or better to activate this terminal."));
                return;
            }

            if (this.HasEqualToZeroRequirement(teleportFunction, StatIds.isfightingme)
                && character.Stats[StatIds.isfightingme].Value != 0)
            {
                this.SendGridTerminalFeedback(
                    character,
                    this.GetGridTerminalSystemText(statelData, "combat")
                    ?? "This terminal can not be activated while you are in combat.");
                return;
            }

            this.SendGridTerminalFeedback(character, "Grid terminal requirements are not met.");
        }

        private bool TryGetGreaterThanRequirement(Function function, StatIds statId, out int value)
        {
            foreach (Requirement requirement in function.Requirements)
            {
                if (requirement.Statnumber == (int)statId
                    && requirement.Operator == Operator.GreaterThan)
                {
                    value = requirement.Value;
                    return true;
                }
            }

            value = 0;
            return false;
        }

        private bool HasEqualToZeroRequirement(Function function, StatIds statId)
        {
            return function.Requirements.Any(
                x => x.Statnumber == (int)statId
                     && x.Operator == Operator.EqualTo
                     && x.Value == 0);
        }

        private string GetGridTerminalSystemText(StatelData statelData, string contains)
        {
            if (statelData == null)
            {
                return null;
            }

            foreach (Event eventData in statelData.Events.Where(x => x.EventType == EventType.OnUse))
            {
                foreach (Function function in eventData.Functions.Where(
                    x => x.FunctionType == (int)FunctionType.SystemText))
                {
                    if (function.Arguments.Values.Count == 0)
                    {
                        continue;
                    }

                    string text = function.Arguments.Values[0].AsString();
                    if (text.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return text;
                    }
                }
            }

            return null;
        }

        private bool TryGetGridDestinationTerminal(
            int destinationPlayfieldId,
            int destinationInstance,
            out StatelData destinationTerminal)
        {
            destinationTerminal = null;

            AORebirth.Core.Playfields.PlayfieldData destinationPlayfield;
            if (!PlayfieldLoader.PFData.TryGetValue(destinationPlayfieldId, out destinationPlayfield))
            {
                return false;
            }

            destinationTerminal = destinationPlayfield.Statels.FirstOrDefault(
                x => (x.Identity.Type == IdentityType.Terminal || x.Identity.Type == IdentityType.Door)
                     && x.Identity.Instance == destinationInstance
                     && x.TemplateId == GridExitTerminalTemplateId);

            return destinationTerminal != null;
        }

        private bool IsCapturedSurgeryClinicTerminal(Identity target, StatelData statelData)
        {
            if (target.Type != IdentityType.Terminal)
            {
                return false;
            }

            uint instance = unchecked((uint)target.Instance);
            if (instance == 0xC00204A2u || instance == 0xC00004A2u)
            {
                return true;
            }

            return statelData != null && CapturedSurgeryClinicTemplateIds.Contains(statelData.TemplateId);
        }

        private void GrantSurgeryClinicImplantAccess(ICharacter character)
        {
            Character concreteCharacter = character as Character;
            if (concreteCharacter == null)
            {
                return;
            }

            concreteCharacter.GrantImplantAccess(SurgeryClinicImplantAccessSeconds);
        }

        private void SendSurgeryClinicFeedback(ICharacter character)
        {
            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = SurgeryClinicFeedback,
                    Unknown2 = 0
                },
                character.Identity.Instance);
        }

        private void SendGridTerminalFeedback(ICharacter character, string text)
        {
            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown1 = 0,
                    FormattedMessage = text,
                    Unknown2 = 0
                },
                character.Identity.Instance);
        }

        private void SendSurgeryClinicCastNano(ICharacter character)
        {
            character.Controller.Client.SendCompressed(
                new CastNanoSpellMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    NanoId = SurgeryClinicNanoId,
                    Target = character.Identity,
                    Unknown1 = 1,
                    Caster = character.Identity
                });
        }

        private void SendSurgeryClinicSpecialUsed(ICharacter character)
        {
            character.Controller.Client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.SpecialUsed,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = SurgeryClinicSpecialStatId,
                    Parameter2 = SurgeryClinicSpecialLockSeconds,
                    Unknown2 = 0
                });
        }

        private void SendSurgeryClinicSpecialAvailableDelayed(ICharacter character)
        {
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    Thread.Sleep(SurgeryClinicSpecialAvailableDelay);
                    if (character == null || character.Controller == null || character.Controller.Client == null)
                    {
                        return;
                    }

                    CharacterActionMessageHandler.Default.SendSkillAvailable(
                        character,
                        SurgeryClinicSpecialStatId);
                });
        }

        #endregion

        #region Outbound

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="message">
        /// </param>
        /// <param name="announceToPlayfield">
        /// </param>
        public void Acknowledge(ICharacter character, GenericCmdMessage message, bool announceToPlayfield = false)
        {
            this.Send(character, this.Reply(character, message), announceToPlayfield);
        }

        public void AcknowledgeDenied(
            ICharacter character,
            GenericCmdMessage message,
            bool announceToPlayfield = false)
        {
            this.Send(character, this.Reply(character, message, Identity.None, message.Temp4, 2), announceToPlayfield);
        }

        public void AcknowledgeWithTarget(
            ICharacter character,
            GenericCmdMessage message,
            Identity target,
            bool announceToPlayfield = false)
        {
            this.Send(character, this.Reply(character, message, target), announceToPlayfield);
        }

        public void AcknowledgeCorpseUse(
            ICharacter character,
            GenericCmdMessage message,
            Identity corpse,
            bool announceToPlayfield = false)
        {
            this.Send(character, this.Reply(character, message, corpse, 1), announceToPlayfield);
        }

        private void AcknowledgeCorpseUseDelayed(
            ICharacter character,
            GenericCmdMessage message,
            Identity corpse,
            bool announceToPlayfield = false)
        {
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    Thread.Sleep(CorpseUseAcknowledgeDelay);
                    if (character == null || character.Controller == null || character.Controller.Client == null)
                    {
                        return;
                    }

                    this.AcknowledgeCorpseUse(character, message, corpse, announceToPlayfield);
                });
        }

        /// <summary>
        /// </summary>
        /// <param name="character">
        /// </param>
        /// <param name="message">
        /// </param>
        /// <returns>
        /// </returns>
        private MessageDataFiller Reply(ICharacter character, GenericCmdMessage message)
        {
            return this.Reply(character, message, Identity.None);
        }

        private MessageDataFiller Reply(ICharacter character, GenericCmdMessage message, Identity targetOverride)
        {
            return this.Reply(character, message, targetOverride, message.Temp4);
        }

        private MessageDataFiller Reply(
            ICharacter character,
            GenericCmdMessage message,
            Identity targetOverride,
            int temp4)
        {
            return this.Reply(character, message, targetOverride, temp4, 1);
        }

        private MessageDataFiller Reply(
            ICharacter character,
            GenericCmdMessage message,
            Identity targetOverride,
            int temp4,
            int temp1)
        {
            return x =>
            {
                Identity[] targets = message.Target.ToList().ToArray();
                if (targetOverride != Identity.None && targets.Length > 0)
                {
                    targets[0] = targetOverride;
                }

                x.Identity = message.Identity;
                x.N3MessageType = message.N3MessageType;
                x.Target = targets;
                x.Temp1 = temp1;
                x.Count = message.Count;
                x.Action = message.Action;
                x.Temp4 = temp4;
                x.User = message.User;
                x.Unknown = 0;
            };
        }

        #endregion

        private bool TryDenyOfabProfessionVendor(IZoneClient client, GenericCmdMessage message, Vendor vendor)
        {
            Profession requiredProfession;
            if (string.IsNullOrEmpty(vendor.TemplateHash)
                || !OfabProfessionVendorRequirements.TryGetValue(vendor.TemplateHash, out requiredProfession))
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            Profession characterProfession = (Profession)character.Stats[StatIds.profession].Value;
            if (characterProfession == requiredProfession)
            {
                return false;
            }

            client.Server.Info(
                client,
                "OFAB profession vendor denied character={0} profession={1} required={2} vendor={3} hash={4}",
                character.Identity,
                characterProfession,
                requiredProfession,
                vendor.Identity,
                vendor.TemplateHash);

            this.SendOfabProfessionDeniedFeedback(character, requiredProfession);
            this.AcknowledgeDenied(character, message);
            return true;
        }

        private void SendOfabProfessionDeniedFeedback(ICharacter character, Profession requiredProfession)
        {
            string professionName;
            if (!ProfessionFeedbackNames.TryGetValue(requiredProfession, out professionName))
            {
                professionName = requiredProfession.ToString();
            }

            ChatTextMessageHandler.Default.Send(
                character,
                "This effect can only be utilitized by " + professionName + ".");
            ChatTextMessageHandler.Default.Send(character, OfabGmRequirementFeedback);
        }
    }
}
