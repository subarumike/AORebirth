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

        private const int CapturedPrivateCityGuestKeyTerminalInstance = unchecked((int)0x5751538B);

        private const int RuntimePrivateCityGuestKeyTerminalInstance = unchecked((int)0x574B84AB);

        private const int CapturedCityAccessCardTemplateId = 280642;

        private const int CapturedCityAccessCardIdentityType = 0x0000C770;

        private const int CapturedCityAccessCardInstance = 0x006D780D;

        private const int CapturedCityAccessCardOverflowSlot = 0x6F;

        private const int CapturedCityAccessCardStateMachineType = 0x000F424F;

        private const int CapturedCityAccessCardBuildingType = 0x0000C79E;

        private const int CapturedCityAccessCardBuildingInstance = 0x0000177A;

        private const int CapturedCityAccessCardOwnerType = 0x000186A1;

        private const int CapturedCityAccessCardOwnerInstance = 0x0001177A;

        private const uint CapturedCityAccessCardBuildingComplexInstance = 0xC017028F;

        private const int CapturedCityAccessCardTimeExist = 90000;

        private const int CityAccessCardLifetimeMilliseconds = 15 * 60 * 1000;

        private const int CityAccessCardExpiresAtUnixSecondsStat = 0x6B657931;

        private const int CapturedPrivateCityOrganizationInstance = 1370122;

        private const int CapturedOwnedPrivateCityOrganizationInstance = 1970177;

        private const int CapturedCityControllerInstance = 0x009C182E;

        private const int RuntimeCityControllerInstance = 0x009C6010;

        private const int CapturedCityControllerInfoIdentityType = 0x0000C419;

        private const int CapturedCityControllerInfoIdentityInstance = 0x0000C000;

        private const int CapturedCityControllerBuildingType = 0x0000C79E;

        private const int CapturedCityControllerBuildingInstance = 0x0000138A;

        private const int CapturedCityControllerFeedbackCategoryId = 110;

        private const int CapturedCityControllerNoOrganizationMessageId = 8208531;

        private const string CapturedCityControllerNoOrganizationText = "no organization";

        private const string CapturedCityControllerOwnedOrganizationText = "Est. 2024";

        private static readonly object CityAccessCardExpirationSync = new object();

        private static readonly Dictionary<ulong, Timer> CityAccessCardExpirationTimers =
            new Dictionary<ulong, Timer>();

        private static readonly DateTime UnixEpochUtc =
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static int cityAccessCardInstanceSeed =
            Math.Max(CapturedCityAccessCardInstance, unchecked((int)(DateTime.UtcNow.Ticks & 0x3fffffff)));

        private const int GridEnterTerminalTemplateId = 95350;

        private const int GridExitTerminalTemplateId = 95351;

        private const float GridDestinationTerminalClearance = 2.5f;

        private static readonly CapturedGridTerminalRoute[] CapturedGridTerminalRoutes =
        {
            new CapturedGridTerminalRoute(
                567,
                unchecked((int)0xC0010237),
                unchecked((int)0xC00D0098),
                177.7f,
                3.8f,
                181.7f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Newland grid-side exit anchor 2026-06-22;source Terminal:C0010237;PF152 nearest exit Terminal:C00D0098"),
            new CapturedGridTerminalRoute(
                640,
                unchecked((int)0xC0030280),
                unchecked((int)0xC00B0098),
                156.0f,
                3.8f,
                185.1f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Tir grid-side exit anchor 2026-06-22;source Terminal:C0030280;PF152 nearest exit Terminal:C00B0098"),
            new CapturedGridTerminalRoute(
                710,
                unchecked((int)0xC00502C6),
                unchecked((int)0xC0000098),
                165.2f,
                3.8f,
                235.0f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Omni Trade grid-side exit anchor 2026-06-22;source Terminal:C00502C6;PF152 nearest exit Terminal:C0000098"),
            new CapturedGridTerminalRoute(
                540,
                unchecked((int)0xC00A021C),
                unchecked((int)0xC04A0098),
                210.2f,
                3.8f,
                172.8f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Old Athen grid-side exit anchor 2026-06-22;source Terminal:C00A021C;PF152 nearest exit Terminal:C04A0098"),
            new CapturedGridTerminalRoute(
                556,
                unchecked((int)0xC002022C),
                unchecked((int)0xC0510098),
                202.1f,
                3.8f,
                249.8f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Coast of Peace grid-side landing 2026-06-22;source Terminal:C002022C;PF152 nearest exit Terminal:C0510098"),
            new CapturedGridTerminalRoute(
                565,
                unchecked((int)0xC0050235),
                unchecked((int)0xC00C0098),
                169.5f,
                37.4f,
                165.2f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Newland Desert grid-side landing 2026-06-22;source Terminal:C0050235;PF152 nearest exit Terminal:C00C0098"),
            new CapturedGridTerminalRoute(
                635,
                unchecked((int)0xC003027B),
                unchecked((int)0xC0050098),
                188.7f,
                37.4f,
                211.1f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Stret East Bank grid-side landing 2026-06-22;source Terminal:C003027B;PF152 nearest exit Terminal:C0050098"),
            new CapturedGridTerminalRoute(
                646,
                unchecked((int)0xC0040286),
                unchecked((int)0xC00B0098),
                155.4f,
                3.8f,
                185.5f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Tir County grid-side landing 2026-06-22;source Terminal:C0040286;PF152 nearest exit Terminal:C00B0098"),
            new CapturedGridTerminalRoute(
                656,
                unchecked((int)0xC0020290),
                unchecked((int)0xC0520098),
                219.2f,
                3.8f,
                246.4f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Coast of Tranquility grid-side landing 2026-06-22;source Terminal:C0020290;PF152 nearest exit Terminal:C0520098"),
            new CapturedGridTerminalRoute(
                6007,
                unchecked((int)0xC0001777),
                unchecked((int)0xC0580098),
                209.4f,
                3.8f,
                210.5f,
                0.0f,
                0.707108f,
                0.0f,
                0.707105f,
                "user supplied Unicorn Defence Hub grid-side exit anchor 2026-06-22;source Terminal:C0001777;PF152 nearest exit Terminal:C0580098"),
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
                665,
                unchecked((int)0xC0000299),
                unchecked((int)0xC00A0098),
                239.6f,
                37.4f,
                221.6f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Broken Shores grid-side landing 2026-06-22;source Terminal:C0000299;PF152 nearest exit Terminal:C00A0098"),
            new CapturedGridTerminalRoute(
                685,
                unchecked((int)0xC00502AD),
                unchecked((int)0xC0080098),
                215.9f,
                37.4f,
                225.7f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Galway County grid-side landing 2026-06-22;source Terminal:C00502AD;PF152 nearest exit Terminal:C0080098"),
            new CapturedGridTerminalRoute(
                695,
                unchecked((int)0xC00702B7),
                unchecked((int)0xC0040098),
                185.1f,
                37.4f,
                227.4f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Lush Fields Harry's grid-side landing 2026-06-22;source Terminal:C00702B7;PF152 nearest exit Terminal:C0040098"),
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
                705,
                unchecked((int)0xC00302C1),
                unchecked((int)0xC0020098),
                180.2f,
                37.4f,
                248.0f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Omni-1 Entertainment South grid-side landing 2026-06-22;source Terminal:C00302C1;PF152 nearest exit Terminal:C0020098"),
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
                "captures/20260622-003221/events.log:9339-9342,9563-9564;PF152 nearest exit Terminal:C0100098"),
            new CapturedGridTerminalRoute(
                760,
                unchecked((int)0xC00502F8),
                unchecked((int)0xC0060098),
                196.6f,
                37.4f,
                208.1f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied 4 Holes grid-side landing 2026-06-22;source Terminal:C00502F8;PF152 nearest exit Terminal:C0060098"),
            new CapturedGridTerminalRoute(
                800,
                unchecked((int)0xC0040320),
                unchecked((int)0xC04C0098),
                234.4f,
                3.8f,
                198.9f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Borealis grid-side landing 2026-06-22;source Terminal:C0040320;PF152 nearest exit Terminal:C04C0098"),
            new CapturedGridTerminalRoute(
                6101,
                unchecked((int)0xC00017D5),
                unchecked((int)0xC0480098),
                218.3f,
                3.8f,
                190.7f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Three Craters West grid-side landing 2026-06-22;source Terminal:C00017D5;PF152 nearest exit Terminal:C0480098"),
            new CapturedGridTerminalRoute(
                6102,
                unchecked((int)0xC00017D6),
                unchecked((int)0xC0480098),
                218.3f,
                3.8f,
                190.7f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Three Craters East grid-side landing 2026-06-22;source Terminal:C00017D6;PF152 nearest exit Terminal:C0480098")
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
                    else if (this.TryHandlePrivateCityGuestKeyTerminalUse(client, message, target))
                    {
                        break;
                    }
                    else if (this.TryHandleCapturedCityControllerUse(client, message, target))
                    {
                        break;
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

        private bool TryHandlePrivateCityGuestKeyTerminalUse(
            IZoneClient client,
            GenericCmdMessage message,
            Identity target)
        {
            ICharacter character = client.Controller.Character;
            if (character == null
                || character.Playfield == null
                || !IsPrivateCityGuestKeyTerminalTarget(target)
                || !AORebirth.Core.Playfields.Playfield.IsPrivateCityPlayfieldCandidate(character.Playfield.Identity))
            {
                return false;
            }

            Item cityAccessCard;
            int inventorySlot;
            InventoryError inventoryError;
            if (!TryCreateAndPersistCityAccessCard(character, out cityAccessCard, out inventorySlot, out inventoryError))
            {
                ChatTextMessageHandler.Default.Send(
                    character,
                    "Could not generate a guest key. (" + inventoryError + ")");
                this.Acknowledge(character, message);
                return true;
            }

            client.SendCompressed(CreateCapturedCityAccessCardItem(character, cityAccessCard.Identity));
            client.SendCompressed(
                new ContainerAddItemMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Target = new Identity { Type = IdentityType.OverflowWindow, Instance = character.Identity.Instance },
                    TargetPlacement = CapturedCityAccessCardOverflowSlot
                });
            this.Acknowledge(character, message);

            client.Server.Info(
                client,
                "Private city guest key terminal created captured City Access Card character={0} terminal={1} template={2} overflowSlot={3} inventorySlot={4} item={5} lifetimeMs={6} evidence=private_city_capture_20260623_012720 runtime_target=574B84AB persisted=1",
                character.Identity,
                target,
                CapturedCityAccessCardTemplateId,
                CapturedCityAccessCardOverflowSlot,
                inventorySlot,
                cityAccessCard.Identity,
                CityAccessCardLifetimeMilliseconds);

            return true;
        }

        private bool TryHandleCapturedCityControllerUse(
            IZoneClient client,
            GenericCmdMessage message,
            Identity target)
        {
            if (!IsPrivateCityControllerTarget(target))
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (character == null)
            {
                client.Server.Info(
                    client,
                    "CityController use consumed without character target={0} count={1} temp4={2} evidence=live_capture_20260623-015602",
                    target,
                    message.Count,
                    message.Temp4);

                return true;
            }

            if (character.Playfield == null
                || !AORebirth.Core.Playfields.Playfield.IsPrivateCityPlayfieldCandidate(character.Playfield.Identity))
            {
                return false;
            }

            int organizationId = ResolveCharacterOrganizationInstance(character);
            bool hasOrganization = organizationId > 0;
            SendCapturedCityControllerOpenSignals(client, character, organizationId, hasOrganization);
            this.Acknowledge(character, message);

            bool sentNoOrganizationFeedback = !hasOrganization;
            if (sentNoOrganizationFeedback)
            {
                SendCapturedCityControllerSignal(client, character, 6, new byte[] { 0x00, 0x00, 0x00, 0x03 });
                FeedbackMessageHandler.Default.Send(
                    character,
                    CapturedCityControllerFeedbackCategoryId,
                    CapturedCityControllerNoOrganizationMessageId);
            }

            client.Server.Info(
                client,
                "CityController use handled character={0} target={1} org={2} count={3} temp4={4} feedbackSent={5} feedbackCategory={6} feedbackMessage={7} aoTransportSignalSent={8} noCityAdvantages=1 noOwnershipChange=1 evidence=private_city_capture_20260623_012720/private_city_owned_entry_capture_20260623_021643 runtime_target=009C6010",
                character.Identity,
                target,
                organizationId,
                message.Count,
                message.Temp4,
                sentNoOrganizationFeedback,
                CapturedCityControllerFeedbackCategoryId,
                CapturedCityControllerNoOrganizationMessageId,
                hasOrganization ? 5 : 6);

            return true;
        }

        private static bool IsPrivateCityGuestKeyTerminalTarget(Identity target)
        {
            return target.Type == IdentityType.Terminal
                && (target.Instance == CapturedPrivateCityGuestKeyTerminalInstance
                    || target.Instance == RuntimePrivateCityGuestKeyTerminalInstance);
        }

        private static bool IsPrivateCityControllerTarget(Identity target)
        {
            return target.Type == IdentityType.CityController
                && (target.Instance == CapturedCityControllerInstance
                    || target.Instance == RuntimeCityControllerInstance);
        }

        private static void SendCapturedCityControllerOpenSignals(
            IZoneClient client,
            ICharacter character,
            int organizationId,
            bool hasOrganization)
        {
            SendCapturedCityControllerSignal(
                client,
                character,
                5,
                CreateCapturedCityControllerInfoPayload(character, organizationId, hasOrganization));
            SendCapturedCityControllerSignal(client, character, 10, new byte[] { 0x00, 0xE4, 0xE1, 0xC0 });
            SendCapturedCityControllerSignal(
                client,
                character,
                13,
                hasOrganization
                    ? new byte[] { 0x00, 0x27, 0x88, 0x05 }
                    : new byte[] { 0x95, 0xC5, 0xD6, 0xBD });
            SendCapturedCityControllerSignal(
                client,
                character,
                14,
                hasOrganization
                    ? new byte[] { 0x00, 0x00, 0x00, 0x00, 0x95, 0xC5, 0xCC, 0xD7 }
                    : new byte[] { 0x00, 0x00, 0x00, 0x00, 0x95, 0xC5, 0xD6, 0xBD });
            SendCapturedCityControllerSignal(client, character, 15, new byte[] { 0x3F, 0x80, 0x00, 0x00 });
        }

        private static void SendCapturedCityControllerSignal(
            IZoneClient client,
            ICharacter character,
            int signal,
            byte[] payload)
        {
            client.SendCompressed(
                new AOTransportSignalMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Signal = signal,
                    Payload = payload
                });
        }

        private static byte[] CreateCapturedCityControllerInfoPayload(
            ICharacter character,
            int organizationId,
            bool hasOrganization)
        {
            string text = hasOrganization
                ? CapturedCityControllerOwnedOrganizationText
                : CapturedCityControllerNoOrganizationText;
            byte[] textBytes = System.Text.Encoding.ASCII.GetBytes(text);
            var payload = new List<byte>(58 + textBytes.Length);

            AppendInt32(payload, CapturedCityControllerInfoIdentityType);
            AppendInt32(payload, CapturedCityControllerInfoIdentityInstance);
            AppendInt32(
                payload,
                hasOrganization ? organizationId : CapturedOwnedPrivateCityOrganizationInstance);
            AppendInt32(payload, CapturedCityControllerBuildingType);
            AppendInt32(payload, CapturedCityControllerBuildingInstance);
            AppendInt32(payload, (int)character.Identity.Type);
            AppendInt32(payload, character.Identity.Instance);
            AppendInt32(payload, hasOrganization ? 2 : 1);
            AppendInt32(payload, hasOrganization ? 3 : 2);
            AppendInt32(payload, hasOrganization ? 1 : -1);
            AppendInt16(payload, textBytes.Length);
            payload.AddRange(textBytes);

            return payload.ToArray();
        }

        private static void AppendInt32(ICollection<byte> bytes, int value)
        {
            bytes.Add((byte)((value >> 24) & 0xFF));
            bytes.Add((byte)((value >> 16) & 0xFF));
            bytes.Add((byte)((value >> 8) & 0xFF));
            bytes.Add((byte)(value & 0xFF));
        }

        private static void AppendInt16(ICollection<byte> bytes, int value)
        {
            bytes.Add((byte)((value >> 8) & 0xFF));
            bytes.Add((byte)(value & 0xFF));
        }

        private static bool TryCreateAndPersistCityAccessCard(
            ICharacter character,
            out Item cityAccessCard,
            out int inventorySlot,
            out InventoryError inventoryError)
        {
            cityAccessCard = null;
            inventorySlot = -1;
            inventoryError = InventoryError.Invalid;

            if (character == null || character.BaseInventory == null)
            {
                return false;
            }

            IInventoryPage inventoryPage;
            if (!character.BaseInventory.Pages.TryGetValue(character.BaseInventory.StandardPage, out inventoryPage))
            {
                return false;
            }

            inventorySlot = inventoryPage.FindFreeSlot();
            if (inventorySlot == -1)
            {
                inventoryError = InventoryError.InventoryIsFull;
                return false;
            }

            try
            {
                cityAccessCard = CreateCapturedCityAccessCardInventoryItem(character);
            }
            catch
            {
                inventoryError = InventoryError.Invalid;
                return false;
            }

            inventoryError = inventoryPage.Add(inventorySlot, cityAccessCard);
            if (inventoryError != InventoryError.OK)
            {
                return false;
            }

            try
            {
                if (!character.BaseInventory.Write())
                {
                    TryRemoveInventorySlot(inventoryPage, inventorySlot);
                    inventoryError = InventoryError.Invalid;
                    return false;
                }
            }
            catch
            {
                TryRemoveInventorySlot(inventoryPage, inventorySlot);
                inventoryError = InventoryError.Invalid;
                return false;
            }

            RegisterCityAccessCardExpiration(cityAccessCard.Identity);
            return true;
        }

        public static void ProcessCityAccessCardLifetimes(ICharacter character)
        {
            if (character == null || character.BaseInventory == null)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List().ToList())
                {
                    if (!IsCityAccessCard(itemEntry.Value))
                    {
                        continue;
                    }

                    DateTime expiresAtUtc = GetCityAccessCardExpirationUtc(itemEntry.Value);
                    if (expiresAtUtc <= nowUtc)
                    {
                        TryRemoveCityAccessCard(character, itemEntry.Value.Identity.Long(), false);
                        continue;
                    }

                    RegisterCityAccessCardExpiration(itemEntry.Value.Identity, expiresAtUtc);
                }
            }
        }

        private static void TryRemoveInventorySlot(IInventoryPage inventoryPage, int inventorySlot)
        {
            try
            {
                if (inventoryPage != null && inventoryPage[inventorySlot] != null)
                {
                    inventoryPage.Remove(inventorySlot);
                }
            }
            catch
            {
            }
        }

        private static Item CreateCapturedCityAccessCardInventoryItem(ICharacter character)
        {
            var item = new Item(1, CapturedCityAccessCardTemplateId, CapturedCityAccessCardTemplateId)
                       {
                           Identity =
                               new Identity
                               {
                                   Type = (IdentityType)CapturedCityAccessCardIdentityType,
                                   Instance = CreateCityAccessCardInstance()
                               },
                           Flags = 1
                       };

            foreach (GameTuple<CharacterStat, uint> stat in CreateCapturedCityAccessCardStats(
                         ResolvePrivateCityOrganizationInstance(character)))
            {
                item.SetAttribute((int)stat.Value1, unchecked((int)stat.Value2));
            }

            item.MultipleCount = 1;
            item.SetAttribute(
                CityAccessCardExpiresAtUnixSecondsStat,
                GetUnixTimeSecondsUtc(DateTime.UtcNow.AddMilliseconds(CityAccessCardLifetimeMilliseconds)));
            return item;
        }

        private static int CreateCityAccessCardInstance()
        {
            int instance = Interlocked.Increment(ref cityAccessCardInstanceSeed) & 0x7fffffff;
            return instance == 0 ? CreateCityAccessCardInstance() : instance;
        }

        private static void RegisterCityAccessCardExpiration(Identity itemIdentity)
        {
            RegisterCityAccessCardExpiration(
                itemIdentity,
                DateTime.UtcNow.AddMilliseconds(CityAccessCardLifetimeMilliseconds));
        }

        private static void RegisterCityAccessCardExpiration(Identity itemIdentity, DateTime expiresAtUtc)
        {
            if (itemIdentity == null || itemIdentity.Type == IdentityType.None)
            {
                return;
            }

            ulong itemKey = itemIdentity.Long();
            int dueTime = GetTimerDueTimeMilliseconds(expiresAtUtc);
            Timer oldTimer = null;
            lock (CityAccessCardExpirationSync)
            {
                if (CityAccessCardExpirationTimers.TryGetValue(itemKey, out oldTimer))
                {
                    CityAccessCardExpirationTimers.Remove(itemKey);
                }

                CityAccessCardExpirationTimers[itemKey] =
                    new Timer(ExpireCityAccessCard, itemKey, dueTime, Timeout.Infinite);
            }

            if (oldTimer != null)
            {
                oldTimer.Dispose();
            }
        }

        private static void ExpireCityAccessCard(object state)
        {
            ulong itemKey = (ulong)state;
            Timer timer = null;
            lock (CityAccessCardExpirationSync)
            {
                if (CityAccessCardExpirationTimers.TryGetValue(itemKey, out timer))
                {
                    CityAccessCardExpirationTimers.Remove(itemKey);
                }
            }

            if (timer != null)
            {
                timer.Dispose();
            }

            foreach (ICharacter character in Pool.Instance.GetAll<ICharacter>((int)IdentityType.CanbeAffected))
            {
                if (TryRemoveCityAccessCard(character, itemKey, true))
                {
                    return;
                }
            }
        }

        private static bool TryRemoveCityAccessCard(ICharacter character, ulong itemKey, bool notifyClient)
        {
            if (character == null || character.BaseInventory == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List().ToList())
                {
                    if (!IsCityAccessCard(itemEntry.Value, itemKey))
                    {
                        continue;
                    }

                    try
                    {
                        pageEntry.Value.Remove(itemEntry.Key);
                        character.BaseInventory.Write();

                        if (notifyClient && character.Controller != null && character.Controller.Client != null)
                        {
                            CharacterActionMessageHandler.Default.SendDeleteItem(
                                character,
                                pageEntry.Value.Page,
                                itemEntry.Key);
                        }
                    }
                    catch
                    {
                        return false;
                    }

                    return true;
                }
            }

            return false;
        }

        private static bool IsCityAccessCard(IItem item, ulong itemKey)
        {
            return IsCityAccessCard(item)
                   && item.Identity.Long() == itemKey;
        }

        private static bool IsCityAccessCard(IItem item)
        {
            return item != null
                   && item.LowID == CapturedCityAccessCardTemplateId
                   && item.HighID == CapturedCityAccessCardTemplateId
                   && item.Identity != null
                   && item.Identity.Type == (IdentityType)CapturedCityAccessCardIdentityType;
        }

        private static DateTime GetCityAccessCardExpirationUtc(IItem item)
        {
            int expiresAtUnixSeconds = item.GetAttribute(CityAccessCardExpiresAtUnixSecondsStat);
            if (expiresAtUnixSeconds <= 0)
            {
                return DateTime.UtcNow.AddMilliseconds(CityAccessCardLifetimeMilliseconds);
            }

            return UnixEpochUtc.AddSeconds(expiresAtUnixSeconds);
        }

        private static int GetUnixTimeSecondsUtc(DateTime value)
        {
            return (int)Math.Max(0, (value.ToUniversalTime() - UnixEpochUtc).TotalSeconds);
        }

        private static int GetTimerDueTimeMilliseconds(DateTime expiresAtUtc)
        {
            double milliseconds = (expiresAtUtc.ToUniversalTime() - DateTime.UtcNow).TotalMilliseconds;
            if (milliseconds <= 0)
            {
                return 0;
            }

            return milliseconds > int.MaxValue ? int.MaxValue : (int)milliseconds;
        }

        private static SimpleItemFullUpdateMessage CreateCapturedCityAccessCardItem(
            ICharacter character,
            Identity itemIdentity)
        {
            return new SimpleItemFullUpdateMessage
                   {
                       Identity =
                           new Identity
                           {
                               Type = itemIdentity.Type,
                               Instance = itemIdentity.Instance
                           },
                       Unknown = 0,
                       MsgVersion = 0x0B,
                       Identitytype = (int)character.Identity.Type,
                       Instance = character.Identity.Instance,
                       Playfield = character.Playfield.Identity.Instance,
                       Unknown1 = new Identity { Type = (IdentityType)CapturedCityAccessCardStateMachineType, Instance = 0 },
                       Unknown2 = 0x71,
                       Unknown3 = CapturedCityAccessCardOverflowSlot,
                       Stats = CreateCapturedCityAccessCardStats(ResolvePrivateCityOrganizationInstance(character)),
                       Name = string.Empty
                   };
        }

        private static GameTuple<CharacterStat, uint>[] CreateCapturedCityAccessCardStats(int organizationInstance)
        {
            return new[]
                   {
                       CityAccessCardStat(CharacterStat.Flags, 1),
                       CityAccessCardStat(CharacterStat.StaticInstance, CapturedCityAccessCardTemplateId),
                       CityAccessCardStat(CharacterStat.ACGItemLevel, 1),
                       CityAccessCardStat(CharacterStat.ACGItemTemplateID, CapturedCityAccessCardTemplateId),
                       CityAccessCardStat(CharacterStat.ACGItemTemplateID2, CapturedCityAccessCardTemplateId),
                       CityAccessCardStat(CharacterStat.MultipleCount, 1),
                       CityAccessCardStat(CharacterStat.BuildingType, CapturedCityAccessCardBuildingType),
                       CityAccessCardStat(CharacterStat.BuildingInstance, CapturedCityAccessCardBuildingInstance),
                       CityAccessCardStat(CharacterStat.CardOwnerType, CapturedCityAccessCardOwnerType),
                       CityAccessCardStat(CharacterStat.CardOwnerInstance, CapturedCityAccessCardOwnerInstance),
                       CityAccessCardStat(CharacterStat.BuildingComplexInst, CapturedCityAccessCardBuildingComplexInstance),
                       CityAccessCardStat(CharacterStat.AccessKey, 0),
                       CityAccessCardStat(CharacterStat.ExternalPlayfieldInstance, organizationInstance),
                       CityAccessCardStat(CharacterStat.TimeExist, CapturedCityAccessCardTimeExist)
                   };
        }

        private static GameTuple<CharacterStat, uint> CityAccessCardStat(CharacterStat stat, int value)
        {
            return CityAccessCardStat(stat, unchecked((uint)value));
        }

        private static GameTuple<CharacterStat, uint> CityAccessCardStat(CharacterStat stat, uint value)
        {
            return new GameTuple<CharacterStat, uint> { Value1 = stat, Value2 = value };
        }

        private static int ResolvePrivateCityOrganizationInstance(ICharacter character)
        {
            int organizationInstance = ResolveCharacterOrganizationInstance(character);
            return organizationInstance > 0 ? organizationInstance : CapturedPrivateCityOrganizationInstance;
        }

        private static int ResolveCharacterOrganizationInstance(ICharacter character)
        {
            uint baseOrganizationInstance = character.Stats[StatIds.clan].BaseValue;
            if (baseOrganizationInstance > 0 && baseOrganizationInstance <= int.MaxValue)
            {
                return (int)baseOrganizationInstance;
            }

            int organizationInstance = character.Stats[StatIds.clan].Value;
            return organizationInstance > 0 ? organizationInstance : 0;
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

            Function rawTeleportFunction;
            int rawDestinationPlayfieldId;
            int rawDestinationInstance;
            if (!this.TryGetGridTeleportProxy2Destination(
                statelData,
                out rawTeleportFunction,
                out rawDestinationPlayfieldId,
                out rawDestinationInstance))
            {
                rawDestinationInstance = 0;
            }

            StatelData destinationTerminal;
            this.TryGetGridDestinationTerminal(
                CapturedGridPlayfieldId,
                route.DestinationExitTerminalInstance,
                out destinationTerminal);
            ZoneEngine.Core.GridZoneInDiagnostics.RecordGridEntry(
                character,
                statelData,
                destinationTerminal,
                destination,
                "CapturedGridTerminalRoute",
                route.Evidence,
                rawDestinationInstance);

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
            ZoneEngine.Core.GridZoneInDiagnostics.RecordGridEntry(
                character,
                statelData,
                destinationTerminal,
                new Coordinate(destination),
                "GridTeleportProxy2TerminalRoute",
                "playfields.dat Enter The Grid template 95350 TeleportProxy2 -> PF152; destination template 95351",
                destinationInstance);
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

    [MessageHandler(MessageHandlerDirection.All)]
    public class CityControllerWindowCloseMessageHandler :
        BaseMessageHandler<CityControllerWindowCloseMessage, CityControllerWindowCloseMessageHandler>
    {
        private const int CapturedCityControllerCloseWindowInstance = 0x0000C000;

        private const int CapturedOwnedPrivateCityOrganizationInstance = 1970177;

        private const int CapturedCityControllerInfoIdentityType = 0x0000C419;

        private const int CapturedCityControllerInfoIdentityInstance = 0x0000C000;

        private const int CapturedCityControllerBuildingType = 0x0000C79E;

        private const int CapturedCityControllerBuildingInstance = 0x0000138A;

        public override void Receive(MessageWrapper<CityControllerWindowCloseMessage> messageWrapper)
        {
            ICharacter character = messageWrapper.Client.Controller.Character;
            CityControllerWindowCloseMessage message = messageWrapper.MessageBody;
            if (character == null
                || character.Playfield == null
                || message.WindowInstance != CapturedCityControllerCloseWindowInstance
                || !AORebirth.Core.Playfields.Playfield.IsPrivateCityPlayfieldCandidate(character.Playfield.Identity))
            {
                return;
            }

            messageWrapper.Client.SendCompressed(
                new AOTransportSignalMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Signal = 7,
                    Payload = CreateCapturedCityControllerClosePayload(character)
                });

            messageWrapper.Client.Server.Info(
                messageWrapper.Client,
                "CityController window close handled character={0} windowInstance={1} signal=7 evidence=private_city_owned_entry_capture_20260623_021643",
                character.Identity,
                message.WindowInstance);
        }

        private static byte[] CreateCapturedCityControllerClosePayload(ICharacter character)
        {
            int organizationId = ResolveCharacterOrganizationInstance(character);
            var payload = new List<byte>(20);

            AppendInt32(payload, CapturedCityControllerInfoIdentityType);
            AppendInt32(payload, CapturedCityControllerInfoIdentityInstance);
            AppendInt32(payload, organizationId);
            AppendInt32(payload, CapturedCityControllerBuildingType);
            AppendInt32(payload, CapturedCityControllerBuildingInstance);

            return payload.ToArray();
        }

        private static int ResolveCharacterOrganizationInstance(ICharacter character)
        {
            uint baseOrganizationInstance = character.Stats[StatIds.clan].BaseValue;
            if (baseOrganizationInstance > 0 && baseOrganizationInstance <= int.MaxValue)
            {
                return (int)baseOrganizationInstance;
            }

            int organizationInstance = character.Stats[StatIds.clan].Value;
            return organizationInstance > 0 ? organizationInstance : CapturedOwnedPrivateCityOrganizationInstance;
        }

        private static void AppendInt32(ICollection<byte> bytes, int value)
        {
            bytes.Add((byte)((value >> 24) & 0xFF));
            bytes.Add((byte)((value >> 16) & 0xFF));
            bytes.Add((byte)((value >> 8) & 0xFF));
            bytes.Add((byte)(value & 0xFF));
        }
    }
}
