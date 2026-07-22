namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Items;
    using AORebirth.Core.Network;
    using AORebirth.Core.Statels;
    using AORebirth.Enums;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Functions;
    using ZoneEngine.Core.Playfields;
    using ZoneEngine.Core.Arete.Quests;

    #endregion

    /// <summary>
    /// Insurance Terminal → SaveChar (53032).
    /// GitHub savechar.cs alone is not enough: current playfields.dat / staticdynels have no OnUse
    /// SaveChar events, so UseStatel is a no-op ("nothing happens"). Dedicated handler like
    /// SurgeryClinic. Capture: 20260716-141512 Terminal:C005028F.
    /// </summary>
    public sealed class InsuranceTerminalInteractionHandler
    {
        public static readonly InsuranceTerminalInteractionHandler Default =
            new InsuranceTerminalInteractionHandler();

        /// <summary>Omni-Trade Insurance (live capture 20260716-141512).</summary>
        private const int OmniTradeInsuranceTerminalInstance = unchecked((int)0xC005028F);

        /// <summary>Itemnames Id 261415-261424 named "Insurance".</summary>
        private static readonly int[] InsuranceTemplateIds =
        {
            261415, 261416, 261417, 261418, 261419,
            261420, 261421, 261422, 261423, 261424
        };

        private static readonly MessagePackObject[] NoArguments = new MessagePackObject[0];

        private static HashSet<int> cachedSaveCharTemplateIds;

        private InsuranceTerminalInteractionHandler()
        {
        }

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (client == null || message == null || target == null
                || target.Type != IdentityType.Terminal)
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            StatelData statelData = GetStatelData(character, target);
            int templateId = statelData != null ? statelData.TemplateId : 0;
            if (!this.IsInsuranceTerminal(target, templateId, statelData))
            {
                return false;
            }

            character.DoNotDoTimers = false;

            bool saved = FunctionCollection.Instance.CallFunction(
                (int)FunctionType.SaveChar,
                character,
                character,
                character,
                NoArguments);

            GenericCmdMessageHandler.Default.Acknowledge(character, message);

            client.Server.Info(
                client,
                "Insurance terminal Use handled char={0} pf={1} target={2} template={3} saveCharOk={4}",
                character.Identity,
                character.Playfield.Identity.Instance,
                target,
                templateId,
                saved);

            return true;
        }

        private bool IsInsuranceTerminal(Identity target, int templateId, StatelData statelData)
        {
            if (target.Instance == OmniTradeInsuranceTerminalInstance)
            {
                return true;
            }

            // Never steal Stationary Automated Surgery Clinic Uses (Arete + private).
            if (SurgeryClinicInteractionRules.IsCapturedSurgeryClinicTerminal(target, templateId))
            {
                return false;
            }

            // Capture 20260721-finish: Exit Arete Landing → ICC HQ (not Insurance SaveChar).
            // playfields.dat Terminal:C0001999 + live capture Terminal:574187C3 (tpl 297303).
            if (target.Instance == VaughnHammondQuestRuntime.ExitAreteLandingTerminalInstance
                || target.Instance == VaughnHammondQuestRuntime.ExitAreteLandingPlayfieldStatelInstance
                || templateId == 297303)
            {
                return false;
            }

            if (templateId != 0)
            {
                if (InsuranceTemplateIds.Contains(templateId))
                {
                    return true;
                }

                if (TemplateHasSaveChar(templateId))
                {
                    return true;
                }
            }

            return StatelHasSaveChar(statelData);
        }

        private static bool TemplateHasSaveChar(int templateId)
        {
            EnsureSaveCharTemplateCache();
            return cachedSaveCharTemplateIds.Contains(templateId);
        }

        private static void EnsureSaveCharTemplateCache()
        {
            if (cachedSaveCharTemplateIds != null)
            {
                return;
            }

            var ids = new HashSet<int>();
            if (ItemLoader.ItemList != null)
            {
                foreach (KeyValuePair<int, ItemTemplate> pair in ItemLoader.ItemList)
                {
                    if (pair.Value != null && EventsHaveSaveChar(pair.Value.Events))
                    {
                        ids.Add(pair.Key);
                    }
                }
            }

            foreach (int known in InsuranceTemplateIds)
            {
                ids.Add(known);
            }

            cachedSaveCharTemplateIds = ids;
        }

        private static bool StatelHasSaveChar(StatelData statelData)
        {
            return statelData != null && EventsHaveSaveChar(statelData.Events);
        }

        private static bool EventsHaveSaveChar(IEnumerable<Event> events)
        {
            if (events == null)
            {
                return false;
            }

            foreach (Event ev in events)
            {
                if (ev == null || ev.EventType != EventType.OnUse || ev.Functions == null)
                {
                    continue;
                }

                foreach (AORebirth.Core.Functions.Function fn in ev.Functions)
                {
                    if (fn != null && fn.FunctionType == (int)FunctionType.SaveChar)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static StatelData GetStatelData(ICharacter character, Identity target)
        {
            int playfieldId = character.Playfield.Identity.Instance;
            if (!PlayfieldLoader.PFData.ContainsKey(playfieldId))
            {
                return null;
            }

            return PlayfieldLoader.PFData[playfieldId].Statels.FirstOrDefault(
                x => x.Identity.Type == target.Type && x.Identity.Instance == target.Instance);
        }
    }
}
