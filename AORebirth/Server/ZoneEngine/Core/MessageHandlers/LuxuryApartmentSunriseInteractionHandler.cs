namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Statels;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Playfields;

    using Coordinate = AORebirth.Core.Vector.Coordinate;
    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    #endregion

    /// <summary>
    /// Capture 20260806-213039 claim: UseItemOnItem claim-key → Door grants
    /// TemplateAction 281570 (Phasefront Classic - Charon) + SIFU 281129 access card,
    /// deletes claim key, teleports in.
    /// Later entry requires unique 281129 in inventory (Use or UseItemOnItem with 281129).
    /// </summary>
    public sealed class LuxuryApartmentSunriseInteractionHandler
    {
        public static readonly LuxuryApartmentSunriseInteractionHandler Default =
            new LuxuryApartmentSunriseInteractionHandler();

        private const byte CapturedOverflowNextFreeSlot = 111;

        private const int CapturedTemplateActionUnknown1 = 1;

        private const int CapturedTemplateActionUnknown2 = 87;

        private const int CapturedAccessCardSifuUnknown2 = 0x71;

        private const int CapturedAccessCardStateMachineType = 1000015;

        // Capture identity type for SimpleItemFullUpdate of 281129 (51056 / 0xC770).
        private const int CapturedAccessCardIdentityType = 0x0000C770;

        private LuxuryApartmentSunriseInteractionHandler()
        {
        }

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (client == null || message == null)
            {
                return false;
            }

            if (target.Type != IdentityType.Door
                && target.Type != IdentityType.MailTerminal
                && target.Type != IdentityType.Terminal)
            {
                return false;
            }

            if (LuxuryApartmentSunriseRules.IsApartmentMailTerminal(target)
                || (target.Type == IdentityType.MailTerminal
                    && characterInApartmentPlayfield(client)))
            {
                return this.TryUseApartmentMailTerminal(client, message);
            }

            if (characterInApartmentPlayfield(client)
                && LuxuryApartmentSunriseRules.IsApartmentBankTerminal(target))
            {
                return this.TryUseApartmentBankTerminal(client, message);
            }

            if (characterInApartmentPlayfield(client)
                && LuxuryApartmentSunriseRules.IsApartmentGridEnterTerminal(target))
            {
                return this.TryUseApartmentGridEnter(client, message);
            }

            if (target.Type != IdentityType.Door)
            {
                return false;
            }

            if (LuxuryApartmentSunriseRules.IsOrbitalApartmentDoor(target))
            {
                // Capture 20260806-220142: plain Use with 281129 in inventory (no item click).
                return this.TryEnterApartment(client, message, Identity.None, false);
            }

            if (target.Instance == LuxuryApartmentSunriseRules.ToRubiKaDoorInstance)
            {
                return this.TryExitLobbyToIcc(client, message);
            }

            if (target.Instance == LuxuryApartmentSunriseRules.ApartmentExitDoorInstance
                || characterInApartmentPlayfield(client))
            {
                return this.TryExitApartmentToLobby(client, message);
            }

            return false;
        }

        private static bool characterInApartmentPlayfield(IZoneClient client)
        {
            ICharacter character = client != null && client.Controller != null
                                       ? client.Controller.Character
                                       : null;
            return character != null
                   && character.Playfield != null
                   && LuxuryApartmentSunriseRules.IsLuxuryApartmentPlayfield(
                       character.Playfield.Identity.Instance);
        }

        public bool TryHandleUseItemOnItem(IZoneClient client, GenericCmdMessage message)
        {
            if (client == null
                || message == null
                || message.Target == null
                || message.Target.Length < 2
                || message.Target[1] == null)
            {
                return false;
            }

            Identity doorTarget = message.Target[1];
            if (!LuxuryApartmentSunriseRules.IsOrbitalApartmentDoor(doorTarget))
            {
                return false;
            }

            Identity source = message.Target[0];
            if (source == null)
            {
                return false;
            }

            if (source.Type != IdentityType.Inventory
                && source.Type != IdentityType.OverflowWindow
                && source.Type != IdentityType.Backpack)
            {
                return false;
            }

            return this.TryEnterApartment(client, message, source, true);
        }

        /// <summary>
        /// Capture 20260806-220142: walk into Orbital Apartment Door with 281129 in inventory
        /// (no GenericCmd) → personal/team apartment entry.
        /// </summary>
        public bool TryProximityEnterFromLobby(ICharacter character)
        {
            if (character == null
                || character.Playfield == null
                || character.Playfield.Identity.Instance != LuxuryApartmentSunriseRules.SunriseStationPlayfieldId
                || !(character.Controller is PlayerController)
                || character.DoNotDoTimers)
            {
                return false;
            }

            if (!InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    character,
                    LuxuryApartmentSunriseRules.CapturedApartmentAccessCardTemplateId))
            {
                return false;
            }

            Dynel dynel = character as Dynel;
            Playfield sourcePlayfield = character.Playfield as Playfield;
            if (dynel == null || sourcePlayfield == null)
            {
                return false;
            }

            return this.CompleteApartmentEntry(null, character, dynel, sourcePlayfield, null);
        }

        private bool TryUseApartmentBankTerminal(IZoneClient client, GenericCmdMessage message)
        {
            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null
                || character.Playfield == null
                || !LuxuryApartmentSunriseRules.IsLuxuryApartmentPlayfield(
                    character.Playfield.Identity.Instance)
                || !(character.Controller is PlayerController))
            {
                if (character != null)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                }

                return true;
            }

            // Capture 20260806-221532: Use Terminal:57C12A72 → IN Bank → ACK.
            character.DoNotDoTimers = false;
            InventoryContainerRuntimeService.Default.OpenBank(character);
            GenericCmdMessageHandler.Default.Acknowledge(character, message);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "LuxuryApartment Sunrise Bank Use char="
                + character.Identity.ToString(true)
                + " evidence=20260806-221532");
            return true;
        }

        private bool TryUseApartmentGridEnter(IZoneClient client, GenericCmdMessage message)
        {
            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null
                || character.Playfield == null
                || !LuxuryApartmentSunriseRules.IsLuxuryApartmentPlayfield(
                    character.Playfield.Identity.Instance)
                || !(character.Controller is PlayerController))
            {
                if (character != null)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                }

                return true;
            }

            Dynel dynel = character as Dynel;
            Playfield sourcePlayfield = character.Playfield as Playfield;
            if (dynel == null || sourcePlayfield == null)
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            // Capture 20260806-221532: ACK then N3Teleport into The Grid.
            character.DoNotDoTimers = false;
            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            Coordinate landing;
            Quaternion heading;
            if (!TryResolveGridLanding(out landing, out heading))
            {
                landing = new Coordinate(198.8429f, 3.775f, 202.2859f);
                heading = new Quaternion(0f, 0.7154163f, 0f, 0.6986985f);
            }

            var envelope = new Vector3(
                (float)character.Position.x,
                (float)character.Position.y,
                (float)character.Position.z);
            var envelopeHeading = new Quaternion(
                character.Rotation.xf,
                character.Rotation.yf,
                character.Rotation.zf,
                character.Rotation.wf);

            character.Stats[StatIds.externaldoorinstance].BaseValue = 0;
            character.Stats[StatIds.externalplayfieldinstance].BaseValue =
                unchecked((uint)character.Playfield.Identity.Instance);

            sourcePlayfield.Teleport(
                dynel,
                landing,
                heading,
                new Identity
                {
                    Type = IdentityType.Playfield,
                    Instance = LuxuryApartmentSunriseRules.CapturedGridPlayfieldId
                },
                transferCharacter => TeleportMessageHandler.Default.SendCapturedApartmentGridEnter(
                    transferCharacter,
                    envelope,
                    envelopeHeading,
                    LuxuryApartmentSunriseRules.CapturedGridPlayfieldId));

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "LuxuryApartment Sunrise Grid enter char="
                + character.Identity.ToString(true)
                + " evidence=20260806-221532");
            return true;
        }

        private static bool TryResolveGridLanding(out Coordinate landing, out Quaternion heading)
        {
            landing = null;
            heading = null;
            PlayfieldData gridPlayfield;
            if (!PlayfieldLoader.PFData.TryGetValue(
                    LuxuryApartmentSunriseRules.CapturedGridPlayfieldId,
                    out gridPlayfield)
                || gridPlayfield == null
                || gridPlayfield.Statels == null)
            {
                return false;
            }

            StatelData exitTerminal = null;
            foreach (StatelData statel in gridPlayfield.Statels)
            {
                if (statel == null
                    || statel.Identity.Type != IdentityType.Terminal
                    || statel.TemplateId != GridTerminalInteractionRules.GridExitTerminalTemplateId)
                {
                    continue;
                }

                exitTerminal = statel;
                break;
            }

            if (exitTerminal == null)
            {
                return false;
            }

            landing = new Coordinate(exitTerminal.X, exitTerminal.Y, exitTerminal.Z);
            heading = new Quaternion(
                exitTerminal.HeadingX,
                exitTerminal.HeadingY,
                exitTerminal.HeadingZ,
                exitTerminal.HeadingW);
            Quaternion.Normalize(heading);

            var forward = (Vector3)heading.RotateVector3(Vector3.AxisZ);
            landing.x += (float)(forward.x * GridTerminalInteractionRules.GridDestinationTerminalClearance);
            landing.z += (float)(forward.z * GridTerminalInteractionRules.GridDestinationTerminalClearance);
            return true;
        }

        private bool TryUseApartmentMailTerminal(IZoneClient client, GenericCmdMessage message)
        {
            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null
                || character.Playfield == null
                || !LuxuryApartmentSunriseRules.IsLuxuryApartmentPlayfield(
                    character.Playfield.Identity.Instance)
                || !(character.Controller is PlayerController))
            {
                if (character != null)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                }

                return true;
            }

            // Same pattern as GMI Market / live MailTerminal: ACK opens client mail UI.
            character.DoNotDoTimers = false;
            GenericCmdMessageHandler.Default.Acknowledge(character, message);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "LuxuryApartment Sunrise MailTerminal Use ACK char="
                + character.Identity.ToString(true)
                + " evidence=20260806-213039");
            return true;
        }

        private bool TryEnterApartment(
            IZoneClient client,
            GenericCmdMessage message,
            Identity sourceSlot,
            bool isUseItemOnItem)
        {
            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null
                || character.Playfield == null
                || character.Playfield.Identity.Instance != LuxuryApartmentSunriseRules.SunriseStationPlayfieldId
                || !(character.Controller is PlayerController))
            {
                if (character != null)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                }

                return true;
            }

            Dynel dynel = character as Dynel;
            Playfield sourcePlayfield = character.Playfield as Playfield;
            if (dynel == null || sourcePlayfield == null)
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            IItem sourceItem = null;
            if (isUseItemOnItem)
            {
                if (!TryResolveInventoryItem(character, sourceSlot, out sourceItem) || sourceItem == null)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                    return true;
                }

                if (IsAccessCard(sourceItem))
                {
                    // Capture later-entry path: UseItemOnItem with 281129 — keep card, enter.
                    return this.CompleteApartmentEntry(client, character, dynel, sourcePlayfield, message);
                }

                // Capture 20260806-213039 claim-key path: grant vehicle + access card, consume key.
                if (!this.TryClaimKeyRewards(client, character, sourceSlot, sourceItem))
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                    return true;
                }

                return this.CompleteApartmentEntry(client, character, dynel, sourcePlayfield, message);
            }

            // Plain Use: require unique access card 281129 in inventory.
            if (!InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    character,
                    LuxuryApartmentSunriseRules.CapturedApartmentAccessCardTemplateId))
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "LuxuryApartment Sunrise Use denied — missing access card 281129 char="
                    + character.Identity.ToString(true));
                return true;
            }

            return this.CompleteApartmentEntry(client, character, dynel, sourcePlayfield, message);
        }

        private bool CompleteApartmentEntry(
            IZoneClient client,
            ICharacter character,
            Dynel dynel,
            Playfield sourcePlayfield,
            GenericCmdMessage message)
        {
            int destinationPlayfieldId;
            int buildingInstance;
            if (!LuxuryApartmentInstanceRuntime.TryResolveEntryDestination(
                    character,
                    out destinationPlayfieldId,
                    out buildingInstance))
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            character.Stats[StatIds.externalplayfieldinstance].BaseValue =
                LuxuryApartmentSunriseRules.SunriseStationPlayfieldId;
            character.Stats[StatIds.externaldoorinstance].BaseValue =
                unchecked((uint)LuxuryApartmentSunriseRules.OrbitalApartmentDoorC000);
            if (message != null)
            {
                GenericCmdMessageHandler.Default.Acknowledge(character, message);
            }

            var landing = new Coordinate(
                LuxuryApartmentSunriseRules.ApartmentLandingX,
                LuxuryApartmentSunriseRules.ApartmentLandingY,
                LuxuryApartmentSunriseRules.ApartmentLandingZ);
            var heading = new Quaternion(
                0f,
                LuxuryApartmentSunriseRules.ApartmentLandingHeadingY,
                0f,
                LuxuryApartmentSunriseRules.ApartmentLandingHeadingW);
            var envelope = new Vector3(
                (float)character.Position.x,
                (float)character.Position.y,
                (float)character.Position.z);
            var envelopeHeading = new Quaternion(
                character.Rotation.xf,
                character.Rotation.yf,
                character.Rotation.zf,
                character.Rotation.wf);

            sourcePlayfield.Teleport(
                dynel,
                landing,
                heading,
                new Identity
                {
                    Type = IdentityType.Playfield,
                    Instance = destinationPlayfieldId
                },
                transferCharacter => TeleportMessageHandler.Default.SendCapturedLuxuryApartmentEntry(
                    transferCharacter,
                    envelope,
                    envelopeHeading,
                    destinationPlayfieldId,
                    buildingInstance));

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "LuxuryApartment Sunrise door entry char=" + character.Identity.ToString(true)
                + " destPf=" + destinationPlayfieldId
                + " building=" + buildingInstance.ToString("X")
                + " evidence=20260806-220142");
            return true;
        }

        private bool TryClaimKeyRewards(
            IZoneClient client,
            ICharacter character,
            Identity sourceSlot,
            IItem sourceItem)
        {
            if (client == null || character == null || sourceItem == null)
            {
                return false;
            }

            if (!ItemLoader.ItemList.ContainsKey(
                    LuxuryApartmentSunriseRules.CapturedPhasefrontClassicCharonTemplateId)
                || !ItemLoader.ItemList.ContainsKey(
                    LuxuryApartmentSunriseRules.CapturedApartmentAccessCardTemplateId))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "LuxuryApartment Sunrise missing item templates 281570/281129");
                return false;
            }

            EnsureAccessCardTemplateAllowsGrant();

            int personalPf;
            int personalBuilding;
            if (!LuxuryApartmentInstanceRuntime.EnsurePersonalApartment(
                    character,
                    out personalPf,
                    out personalBuilding))
            {
                return false;
            }

            // Capture order: FormatFeedback → TemplateAction 281570 → ContainerAdd →
            // SIFU 281129 → ContainerAdd → DeleteItem claim key.
            SendPromotionalVehicleKeyFeedback(character);

            if (!InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    character,
                    LuxuryApartmentSunriseRules.CapturedPhasefrontClassicCharonTemplateId))
            {
                if (!TryGrantPhasefrontClassicCharon(character))
                {
                    return false;
                }
            }

            if (!InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(
                    character,
                    LuxuryApartmentSunriseRules.CapturedApartmentAccessCardTemplateId))
            {
                if (!TryGrantApartmentAccessCard(client, character, personalBuilding))
                {
                    return false;
                }
            }

            ConsumeClaimKey(character, sourceSlot);
            return true;
        }

        private static bool TryGrantPhasefrontClassicCharon(ICharacter character)
        {
            Item item;
            try
            {
                item = new Item(
                    LuxuryApartmentSunriseRules.CapturedPhasefrontClassicCharonQuality,
                    LuxuryApartmentSunriseRules.CapturedPhasefrontClassicCharonTemplateId,
                    LuxuryApartmentSunriseRules.CapturedPhasefrontClassicCharonTemplateId);
                if (item.MultipleCount < 1)
                {
                    item.MultipleCount = 1;
                }
            }
            catch (Exception ex)
            {
                LogUtil.ErrorException(ex);
                return false;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(character, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "LuxuryApartment Sunrise grant 281570 failed status=" + grant.Status);
                return false;
            }

            character.Send(
                new TemplateActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    ItemLowId = LuxuryApartmentSunriseRules.CapturedPhasefrontClassicCharonTemplateId,
                    ItemHighId = LuxuryApartmentSunriseRules.CapturedPhasefrontClassicCharonTemplateId,
                    Quality = LuxuryApartmentSunriseRules.CapturedPhasefrontClassicCharonQuality,
                    Unknown1 = CapturedTemplateActionUnknown1,
                    Unknown2 = CapturedTemplateActionUnknown2,
                    Placement = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Unknown3 = 0,
                    Unknown4 = 0
                });
            character.Send(
                new ContainerAddItemMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Target = new Identity
                             {
                                 Type = IdentityType.OverflowWindow,
                                 Instance = character.Identity.Instance
                             },
                    TargetPlacement = CapturedOverflowNextFreeSlot
                });
            return true;
        }

        private static bool TryGrantApartmentAccessCard(
            IZoneClient client,
            ICharacter character,
            int buildingInstance)
        {
            Item accessCard;
            try
            {
                accessCard = CreateApartmentAccessCardItem(character, buildingInstance);
            }
            catch (Exception ex)
            {
                LogUtil.ErrorException(ex);
                return false;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(character, accessCard);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Engine,
                    "LuxuryApartment Sunrise grant 281129 failed status=" + grant.Status);
                return false;
            }

            client.SendCompressed(
                CreateAccessCardSimpleItemFullUpdate(character, accessCard.Identity, buildingInstance));
            client.SendCompressed(
                new ContainerAddItemMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    SourceContainer = new Identity { Type = IdentityType.OverflowWindow, Instance = 0 },
                    Target = new Identity
                             {
                                 Type = IdentityType.OverflowWindow,
                                 Instance = character.Identity.Instance
                             },
                    TargetPlacement = CapturedOverflowNextFreeSlot
                });
            return true;
        }

        private static Item CreateApartmentAccessCardItem(ICharacter character, int buildingInstance)
        {
            var item = new Item(
                LuxuryApartmentSunriseRules.CapturedAccessCardQuality,
                LuxuryApartmentSunriseRules.CapturedApartmentAccessCardTemplateId,
                LuxuryApartmentSunriseRules.CapturedApartmentAccessCardTemplateId)
                       {
                           Identity =
                               new Identity
                               {
                                   Type = (IdentityType)CapturedAccessCardIdentityType,
                                   Instance = unchecked((int)(Environment.TickCount & 0x7fffffff))
                               },
                           Flags = unchecked((int)LuxuryApartmentSunriseRules.CapturedAccessCardFlags)
                       };

            foreach (GameTuple<CharacterStat, uint> stat in CreateAccessCardStats(character, buildingInstance))
            {
                item.SetAttribute((int)stat.Value1, unchecked((int)stat.Value2));
            }

            item.MultipleCount = 1;
            return item;
        }

        private static GameTuple<CharacterStat, uint>[] CreateAccessCardStats(
            ICharacter character,
            int buildingInstance)
        {
            return new[]
                   {
                       Stat(CharacterStat.Flags, LuxuryApartmentSunriseRules.CapturedAccessCardFlags),
                       Stat(
                           CharacterStat.StaticInstance,
                           (uint)LuxuryApartmentSunriseRules.CapturedApartmentAccessCardTemplateId),
                       Stat(CharacterStat.ACGItemLevel, LuxuryApartmentSunriseRules.CapturedAccessCardQuality),
                       Stat(
                           CharacterStat.ACGItemTemplateID,
                           (uint)LuxuryApartmentSunriseRules.CapturedApartmentAccessCardTemplateId),
                       Stat(
                           CharacterStat.ACGItemTemplateID2,
                           (uint)LuxuryApartmentSunriseRules.CapturedApartmentAccessCardTemplateId),
                       Stat(CharacterStat.MultipleCount, 1),
                       Stat(CharacterStat.BuildingType, (uint)IdentityType.Playfield),
                       Stat(CharacterStat.BuildingInstance, unchecked((uint)buildingInstance)),
                       Stat(CharacterStat.CardOwnerType, (uint)character.Identity.Type),
                       Stat(CharacterStat.CardOwnerInstance, unchecked((uint)character.Identity.Instance)),
                       Stat(
                           CharacterStat.BuildingComplexInst,
                           unchecked((uint)LuxuryApartmentSunriseRules.OrbitalApartmentDoorC000)),
                       Stat(CharacterStat.AccessKey, 0),
                       Stat(
                           CharacterStat.ExternalPlayfieldInstance,
                           (uint)LuxuryApartmentSunriseRules.SunriseStationPlayfieldId)
                   };
        }

        private static SimpleItemFullUpdateMessage CreateAccessCardSimpleItemFullUpdate(
            ICharacter character,
            Identity itemIdentity,
            int buildingInstance)
        {
            return new SimpleItemFullUpdateMessage
                   {
                       Identity = new Identity
                                  {
                                      Type = itemIdentity.Type,
                                      Instance = itemIdentity.Instance
                                  },
                       Unknown = 0,
                       MsgVersion = 0x0B,
                       Identitytype = (int)character.Identity.Type,
                       Instance = character.Identity.Instance,
                       Playfield = character.Playfield.Identity.Instance,
                       Unknown1 =
                           new Identity
                           {
                               Type = (IdentityType)CapturedAccessCardStateMachineType,
                               Instance = 0
                           },
                       Unknown2 = CapturedAccessCardSifuUnknown2,
                       Unknown3 = CapturedOverflowNextFreeSlot,
                       Stats = CreateAccessCardStats(character, buildingInstance),
                       Name = string.Empty
                   };
        }

        private static void ConsumeClaimKey(ICharacter character, Identity sourceSlot)
        {
            try
            {
                character.BaseInventory.RemoveItem((int)sourceSlot.Type, sourceSlot.Instance);
                CharacterActionMessageHandler.Default.SendDeleteItem(
                    character,
                    (int)sourceSlot.Type,
                    sourceSlot.Instance);
            }
            catch (Exception ex)
            {
                LogUtil.ErrorException(ex);
            }
        }

        private static void SendPromotionalVehicleKeyFeedback(ICharacter character)
        {
            if (character == null || character.Controller == null || character.Controller.Client == null)
            {
                return;
            }

            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = LuxuryApartmentSunriseRules.CapturedPromotionalVehicleKeyFeedback,
                    Unknown2 = 0
                });
        }

        private static void EnsureAccessCardTemplateAllowsGrant()
        {
            ItemTemplate template;
            if (!ItemLoader.ItemList.TryGetValue(
                    LuxuryApartmentSunriseRules.CapturedApartmentAccessCardTemplateId,
                    out template)
                || template == null
                || template.Stats == null
                || !template.Stats.ContainsKey(0))
            {
                return;
            }

            int flags = template.Stats[0];
            if ((flags & (int)ItemFlags.Unique) == 0)
            {
                return;
            }

            // Unique blocks retries when a prior copy is already carried; entry still requires the card.
            template.Stats[0] = flags & ~(int)ItemFlags.Unique;
        }

        private static bool TryResolveInventoryItem(
            ICharacter character,
            Identity sourceSlot,
            out IItem item)
        {
            item = null;
            if (character == null || character.BaseInventory == null)
            {
                return false;
            }

            IInventoryPage page;
            if (!character.BaseInventory.Pages.TryGetValue((int)sourceSlot.Type, out page) || page == null)
            {
                return false;
            }

            try
            {
                item = page[sourceSlot.Instance];
            }
            catch
            {
                item = null;
            }

            return item != null;
        }

        private static bool IsAccessCard(IItem item)
        {
            return item != null
                   && item.LowID == LuxuryApartmentSunriseRules.CapturedApartmentAccessCardTemplateId
                   && item.HighID == LuxuryApartmentSunriseRules.CapturedApartmentAccessCardTemplateId;
        }

        private static GameTuple<CharacterStat, uint> Stat(CharacterStat id, uint value)
        {
            return new GameTuple<CharacterStat, uint> { Value1 = id, Value2 = value };
        }

        private bool TryExitLobbyToIcc(IZoneClient client, GenericCmdMessage message)
        {
            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null
                || character.Playfield == null
                || character.Playfield.Identity.Instance != LuxuryApartmentSunriseRules.SunriseStationPlayfieldId
                || !(character.Controller is PlayerController))
            {
                if (character != null)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                }

                return true;
            }

            Dynel dynel = character as Dynel;
            Playfield sourcePlayfield = character.Playfield as Playfield;
            if (dynel == null || sourcePlayfield == null)
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            character.Stats[StatIds.externalplayfieldinstance].BaseValue =
                LuxuryApartmentSunriseRules.SunriseStationPlayfieldId;
            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            var landing = new Coordinate(
                LuxuryApartmentSunriseRules.LobbyEntrySourceX,
                LuxuryApartmentSunriseRules.LobbyEntrySourceY,
                LuxuryApartmentSunriseRules.LobbyEntrySourceZ);
            var heading = new Quaternion(0f, 0.7187735f, 0f, 0.6952443f);

            sourcePlayfield.Teleport(
                dynel,
                landing,
                heading,
                new Identity
                {
                    Type = IdentityType.Playfield,
                    Instance = LuxuryApartmentSunriseRules.IccHqPlayfieldId
                });

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "LuxuryApartment Sunrise To Rubi-Ka exit char=" + character.Identity.ToString(true)
                + " evidence=20260806-202421");
            return true;
        }

        private bool TryExitApartmentToLobby(IZoneClient client, GenericCmdMessage message)
        {
            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null
                || character.Playfield == null
                || !LuxuryApartmentSunriseRules.IsLuxuryApartmentPlayfield(
                    character.Playfield.Identity.Instance)
                || !(character.Controller is PlayerController))
            {
                if (character != null)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                }

                return true;
            }

            Dynel dynel = character as Dynel;
            Playfield sourcePlayfield = character.Playfield as Playfield;
            if (dynel == null || sourcePlayfield == null)
            {
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            character.Stats[StatIds.externalplayfieldinstance].BaseValue =
                LuxuryApartmentSunriseRules.LuxuryApartmentPlayfieldId;
            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            var landing = new Coordinate(
                LuxuryApartmentSunriseRules.ApartmentExitLobbyX,
                LuxuryApartmentSunriseRules.ApartmentExitLobbyY,
                LuxuryApartmentSunriseRules.ApartmentExitLobbyZ);
            var heading = new Quaternion(
                0f,
                LuxuryApartmentSunriseRules.ApartmentExitLobbyHeadingY,
                0f,
                LuxuryApartmentSunriseRules.ApartmentExitLobbyHeadingW);
            var envelope = new Vector3(
                (float)character.Position.x,
                (float)character.Position.y,
                (float)character.Position.z);
            var envelopeHeading = new Quaternion(
                character.Rotation.xf,
                character.Rotation.yf,
                character.Rotation.zf,
                character.Rotation.wf);

            sourcePlayfield.Teleport(
                dynel,
                landing,
                heading,
                new Identity
                {
                    Type = IdentityType.Playfield,
                    Instance = LuxuryApartmentSunriseRules.SunriseStationPlayfieldId
                },
                transferCharacter => TeleportMessageHandler.Default.SendCapturedLuxuryApartmentExit(
                    transferCharacter,
                    envelope,
                    envelopeHeading,
                    LuxuryApartmentSunriseRules.SunriseStationPlayfieldId));

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "LuxuryApartment Sunrise interior exit char=" + character.Identity.ToString(true)
                + " evidence=20260806-210903");
            return true;
        }
    }
}
