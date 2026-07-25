namespace ZoneEngine.Core.Missions
{
    #region Usings ...

    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Playfields;
    using AORebirth.ObjectManager;

    using ZoneEngine.Core.Playfields;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.Controllers;

    #endregion

    /// <summary>
    /// RepairMachine objective: accept grants Mission Repair Kit; instance spawns Broken Machine;
    /// UseItemOnItem (or Use while holding the kit) on that machine completes the mission.
    /// </summary>
    internal static class MissionRepairService
    {
        public static bool IsRepairMission(MissionAcceptedStore.AcceptedMission entry)
        {
            return entry != null && entry.MissionIconId == MissionTypeCatalog.RepairMachineIcon;
        }

        public static bool IsRepairOffer(QuestInfo offer)
        {
            return offer != null && offer.MissionIconId == MissionTypeCatalog.RepairMachineIcon;
        }

        public static bool TryHandleUseItemOnItem(IZoneClient client, GenericCmdMessage message)
        {
            if (client == null || message == null || message.Target == null || message.Target.Length < 2)
            {
                return false;
            }

            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null || character.Playfield == null
                || !MissionInstanceService.IsMissionInstancePlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            if (!MissionMachineTracker.IsMissionMachine(message.Target[1]))
            {
                return false;
            }

            IInventoryPage sourcePage =
                Pool.Instance.GetObject<IInventoryPage>(
                    new Identity
                    {
                        Type = (IdentityType)character.Identity.Instance,
                        Instance = (int)message.Target[0].Type
                    });
            if (sourcePage == null)
            {
                return false;
            }

            IItem item = sourcePage[message.Target[0].Instance];
            if (!MissionKeyGrantService.IsRepairTool(item))
            {
                return false;
            }

            return TryCompleteRepair(client, character, message.Target[1], item, "RepairMachine");
        }

        /// <summary>
        /// Plain Use on the Broken Machine while the repair kit is in inventory.
        /// </summary>
        public static bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (client == null || target == null || !MissionMachineTracker.IsMissionMachine(target))
            {
                return false;
            }

            ICharacter character = client.Controller != null ? client.Controller.Character : null;
            if (character == null || character.Playfield == null
                || !MissionInstanceService.IsMissionInstancePlayfield(character.Playfield.Identity.Instance))
            {
                return false;
            }

            IItem kit;
            if (!MissionKeyGrantService.HasRepairTool(character))
            {
                client.Server.Info(client, "Mission repair: Broken Machine requires Mission Repair Kit");
                return true;
            }

            // Prefer the held kit for consume; HasRepairTool already proved one exists.
            if (!TryGetAnyRepairTool(character, out kit))
            {
                return true;
            }

            return TryCompleteRepair(client, character, target, kit, "RepairMachineUse");
        }

        private static bool TryGetAnyRepairTool(ICharacter character, out IItem kit)
        {
            kit = null;
            if (character == null || character.BaseInventory == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, IInventoryPage> pageEntry in character.BaseInventory.Pages)
            {
                foreach (KeyValuePair<int, IItem> itemEntry in pageEntry.Value.List())
                {
                    if (MissionKeyGrantService.IsRepairTool(itemEntry.Value))
                    {
                        kit = itemEntry.Value;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryCompleteRepair(
            IZoneClient client,
            ICharacter character,
            Identity machineIdentity,
            IItem repairItem,
            string reason)
        {
            MissionAcceptedStore.AcceptedMission entry = FindRepairMission(character.Identity.Instance);
            if (entry == null)
            {
                LogUtil.Debug(DebugInfoDetail.Engine, "Mission repair ignored — no RepairMachine accept");
                return false;
            }

            MissionKeyGrantService.TryConsumeRepairTool(client, character, repairItem);
            MissionMachineTracker.Unregister(machineIdentity);

            Playfield playfield = character.Playfield as Playfield;
            if (playfield != null)
            {
                playfield.Despawn(machineIdentity);
            }

            bool completed = MissionCompleteService.TryComplete(client, character, entry, reason);
            MissionDiagnostics.Log(
                "REPAIR machine={0} completed={1} reason={2}",
                machineIdentity,
                completed,
                reason);
            return completed;
        }

        private static MissionAcceptedStore.AcceptedMission FindRepairMission(int characterInstance)
        {
            List<MissionAcceptedStore.AcceptedMission> all = MissionAcceptedStore.GetAll(characterInstance);
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (IsRepairMission(all[i]))
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
