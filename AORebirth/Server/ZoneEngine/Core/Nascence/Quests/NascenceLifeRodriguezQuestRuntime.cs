namespace ZoneEngine.Core.Nascence.Quests
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Inventory;
    using AORebirth.Core.Items;
    using AORebirth.Core.Playfields;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core;
    using ZoneEngine.Core.Arete.Dialogue;
    using ZoneEngine.Core.Controllers;
    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Capture-backed Scientist Drake Rodriguez bracer / Donna Red quest runtime
    /// (20260822-221109 + proximity auto-dialog/bracer 20260825-155929).
    /// </summary>
    internal static class NascenceLifeRodriguezQuestRuntime
    {
        private static readonly object ProximityGate = new object();

        // Characters currently inside Rodriguez proximity (enter-edge opens dialog once per visit).
        private static readonly HashSet<int> CharactersInProximity = new HashSet<int>();

        internal static bool IsMissionActive(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(
                source.Identity.Instance,
                NascenceLifeRodriguezInteractionRules.QuestId);
            return mission != null && mission.State == MissionLifecycleState.Active;
        }

        internal static bool IsMissionCompleted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            MissionStateRecord mission = MissionRuntime.Service.GetMission(
                source.Identity.Instance,
                NascenceLifeRodriguezInteractionRules.QuestId);
            return mission != null && mission.State == MissionLifecycleState.Completed;
        }

        internal static MissionOperationResult AcceptQuest(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.Unresolved,
                           Message = "nascence-life-rodriguez-runtime-unavailable"
                       };
            }

            string questId = NascenceLifeRodriguezInteractionRules.QuestId;
            int characterId = source.Identity.Instance;

            if (IsMissionActive(source))
            {
                NascenceLifeRodriguezPacketSender.TrySendQuestFullUpdate(source);
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "nascence-life-rodriguez-already-active"
                       };
            }

            if (IsMissionCompleted(source) || IsRewardGranted(source))
            {
                return new MissionOperationResult
                       {
                           Status = MissionOperationStatus.AlreadyApplied,
                           Message = "nascence-life-rodriguez-already-completed"
                       };
            }

            MissionOperationResult offer = MissionRuntime.Service.OfferMission(characterId, questId);
            if (!IsClientEmitSuccess(offer) && IsPersistenceFailure(offer))
            {
                return offer;
            }

            MissionOperationResult accepted = MissionRuntime.Service.AcceptMission(characterId, questId);
            if (IsClientEmitSuccess(accepted))
            {
                NascenceLifeRodriguezPacketSender.TrySendQuestFullUpdate(source);
            }

            return accepted;
        }

        /// <summary>
        /// Capture 20260825-155929: bracer grant on dialogue open (same moment as KnubotOpenChatWindow).
        /// </summary>
        internal static bool TryGrantBracerOnDialogueOpen(ICharacter source)
        {
            return TryGrantBracerIfNeeded(source);
        }

        /// <summary>
        /// Capture 20260825-155929: no client Use — server opens dialog + grants bracer when player
        /// enters ~5m of Rodriguez. Dialog reopens on each re-entry; bracer only if not unique-owned.
        /// </summary>
        internal static void TickProximityBracerGrant(Playfield playfield, IEnumerable<ICharacter> characters)
        {
            if (playfield == null
                || characters == null
                || playfield.Identity.Instance != NascenceLifeRodriguezInteractionRules.JobeResearchPlayfieldId)
            {
                return;
            }

            ICharacter rodriguez = FindRodriguez(playfield);
            if (rodriguez == null)
            {
                return;
            }

            foreach (ICharacter character in characters)
            {
                if (character == null
                    || character.Controller == null
                    || !(character.Controller is PlayerController)
                    || character.Stats[StatIds.health].Value <= 0)
                {
                    continue;
                }

                int characterId = character.Identity.Instance;
                bool inRange = IsWithinRodriguezProximity(character, rodriguez);
                bool wasInRange;
                lock (ProximityGate)
                {
                    wasInRange = CharactersInProximity.Contains(characterId);
                    if (inRange)
                    {
                        CharactersInProximity.Add(characterId);
                    }
                    else
                    {
                        CharactersInProximity.Remove(characterId);
                    }
                }

                if (!inRange || wasInRange)
                {
                    continue;
                }

                // Enter-edge: open dialog (grant runs inside dialog start side-effect).
                ContentDrivenNpcDialogueRouter.TryStartDialogue(rodriguez, character.Identity);
            }
        }

        private static bool TryGrantBracerIfNeeded(ICharacter source)
        {
            if (source == null
                || source.Controller == null
                || source.Controller.Client == null)
            {
                return false;
            }

            if (CharacterHasBracer(source))
            {
                MarkBracerGranted(source);
                return true;
            }

            if (!ItemLoader.ItemList.ContainsKey(NascenceLifeRodriguezInteractionRules.BracerItemId))
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_LIFE_RODRIGUEZ bracer missing ItemLoader id="
                    + NascenceLifeRodriguezInteractionRules.BracerItemId);
                return false;
            }

            Item item;
            try
            {
                item = new Item(
                    NascenceLifeRodriguezInteractionRules.BracerQuality,
                    NascenceLifeRodriguezInteractionRules.BracerItemId,
                    NascenceLifeRodriguezInteractionRules.BracerItemId);
            }
            catch (Exception ex)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_LIFE_RODRIGUEZ bracer create failed: " + ex.Message);
                return false;
            }

            QuestRewardInventoryGrantResult grant =
                InventoryContainerRuntimeService.Default.TryGrantQuestRewardItem(source, item);
            if (grant.Status != QuestRewardInventoryGrantStatus.Success)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Error,
                    "NASCENCE_LIFE_RODRIGUEZ bracer inventory grant failed status="
                    + grant.Status
                    + " invErr="
                    + grant.InventoryError);
                return false;
            }

            if (!NascenceLifeRodriguezPacketSender.TrySendBracerGrant(source))
            {
                return false;
            }

            MarkBracerGranted(source);
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                "NASCENCE_LIFE_RODRIGUEZ bracer granted char=" + source.Identity.Instance.ToString("X8"));
            return true;
        }

        internal static bool TryResendActiveMissionsForLogin(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            if (!IsMissionActive(source))
            {
                return false;
            }

            return NascenceLifeRodriguezPacketSender.TrySendQuestFullUpdate(source);
        }

        private static ICharacter FindRodriguez(Playfield playfield)
        {
            foreach (ICharacter candidate in playfield.EnumerateActiveCharacters())
            {
                if (candidate != null
                    && candidate.Controller is NPCController
                    && NascenceLifeRodriguezInteractionRules.IsRodriguezName(candidate.Name))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsWithinRodriguezProximity(ICharacter player, ICharacter rodriguez)
        {
            AORebirth.Core.Vector.Vector3 playerCoord = player.Coordinates().coordinate;
            AORebirth.Core.Vector.Vector3 npcCoord = rodriguez.Coordinates().coordinate;
            float radius = NascenceLifeRodriguezInteractionRules.RodriguezProximityRadiusMeters;
            float radiusSq = radius * radius;
            double dx = npcCoord.x - playerCoord.x;
            double dz = npcCoord.z - playerCoord.z;
            return dx * dx + dz * dz <= radiusSq;
        }

        private static bool CharacterHasBracer(ICharacter source)
        {
            if (source == null || source.BaseInventory == null)
            {
                return false;
            }

            int itemId = NascenceLifeRodriguezInteractionRules.BracerItemId;
            if (InventoryContainerRuntimeService.Default.CharacterHasItemInCarriedInventory(source, itemId))
            {
                return true;
            }

            IInventoryPage weaponPage;
            if (source.BaseInventory.Pages.TryGetValue((int)IdentityType.WeaponPage, out weaponPage)
                && weaponPage != null)
            {
                foreach (KeyValuePair<int, IItem> entry in weaponPage.List())
                {
                    if (entry.Value != null
                        && (entry.Value.LowID == itemId || entry.Value.HighID == itemId))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsBracerGranted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       NascenceLifeRodriguezInteractionRules.QuestId,
                       NascenceLifeRodriguezInteractionRules.BracerGrantedFlag) != null;
        }

        private static void MarkBracerGranted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return;
            }

            MissionRuntime.Service.SetFlag(
                source.Identity.Instance,
                NascenceLifeRodriguezInteractionRules.QuestId,
                NascenceLifeRodriguezInteractionRules.BracerGrantedFlag,
                "1");
        }

        private static bool IsRewardGranted(ICharacter source)
        {
            if (source == null || !MissionRuntime.IsInitialized)
            {
                return false;
            }

            return MissionRuntime.Service.GetFlag(
                       source.Identity.Instance,
                       NascenceLifeRodriguezInteractionRules.QuestId,
                       NascenceLifeRodriguezInteractionRules.RewardGrantedFlag) != null;
        }

        private static bool IsClientEmitSuccess(MissionOperationResult result)
        {
            return result != null
                   && (result.Status == MissionOperationStatus.Applied
                       || result.Status == MissionOperationStatus.AlreadyApplied);
        }

        private static bool IsPersistenceFailure(MissionOperationResult result)
        {
            return result != null
                   && result.Status != MissionOperationStatus.Applied
                   && result.Status != MissionOperationStatus.AlreadyApplied
                   && result.Status != MissionOperationStatus.Unresolved;
        }
    }
}
