namespace ZoneEngine.Core.Functions.GameFunctions
{
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Nanos;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    using ZoneEngine.Core.MessageHandlers;

    /// <summary>
    /// FunctionType.CastNano (53051) — used heavily by perk action items.
    /// Instant apply path (no cast bar / upload gate) for perk/item scripted casts.
    /// Does not persist into UploadedNanos / nano programs window.
    /// </summary>
    internal class castnano : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.CastNano;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            Character character = self as Character;
            if (character == null || arguments == null || arguments.Length < 1)
            {
                return false;
            }

            int nanoId = arguments[0].AsInt32();
            return ApplyInstantNano(character, target as IInstancedEntity, nanoId);
        }

        /// <summary>
        /// Shared by CastNano / TeamCastNano / AreaCastNano / Undefined(53240) perk wrappers.
        /// </summary>
        internal static bool ApplyInstantNano(Character character, IInstancedEntity preferredTarget, int nanoId)
        {
            if (character == null || nanoId <= 0)
            {
                return false;
            }

            NanoFormula nano;
            if (!NanoLoader.NanoList.TryGetValue(nanoId, out nano))
            {
                LogUtil.Debug(
                    DebugInfoDetail.GameFunctions,
                    "CastNano missing nanoId=" + nanoId);
                return false;
            }

            Character recipient = preferredTarget as Character;
            if (recipient == null)
            {
                recipient = character;
                if (character.SelectedTarget.Instance != 0
                    && character.SelectedTarget.Instance != character.Identity.Instance
                    && character.Playfield != null)
                {
                    Character selected =
                        character.Playfield.FindByIdentity<Character>(character.SelectedTarget);
                    if (selected != null)
                    {
                        recipient = selected;
                    }
                }
            }

            Identity targetIdentity = recipient.Identity;

            // FX only — never add perk casts to nano programs.
            CastNanoSpellMessageHandler.Default.Send(character, nanoId, targetIdentity);
            CharacterActionMessageHandler.Default.FinishNanoCasting(
                character,
                CharacterActionType.FinishNanoCasting,
                Identity.None,
                1,
                nanoId);

            // Nested marker nanos (0 events, e.g. 209827 "Affected by Channel Rage" Attr8=1000)
            // must not create a separate 10s NCU entry — the parent tier nano owns duration.
            bool hasScript = nano.Events != null && nano.Events.Count > 0;
            if (hasScript)
            {
                // Always bind SelectedTarget to the TeamCastNano/CastNano recipient for nested OnUse.
                // Ambient Restoration (302365 → 300495..300498, capture 20260722-keeper-exect-nano)
                // uses Hit target=3; without binding self, heals land on the selected enemy instead.
                // Also required for AreaCastNano nested TauntNpc (Mongo Slam 100194).
                Identity previousSelected = character.SelectedTarget;
                try
                {
                    if (recipient != null && recipient.Identity.Instance != 0)
                    {
                        character.SetTarget(recipient.Identity);
                    }

                    NanoEventRuntimeService.Default.ExecuteOnUseEvents(character, nano);
                }
                finally
                {
                    character.SetTarget(previousSelected);
                }
            }

            int duration = nano.getItemAttribute(8);
            if (duration > 0 && hasScript && !NanoEventRuntimeService.Default.HasOffensiveHitOnUse(nano))
            {
                CharacterActionMessageHandler.Default.SetNanoDuration(
                    character,
                    targetIdentity,
                    nanoId,
                    duration);
            }

            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    "CastNano instant caster={0} nano={1} recipient={2} duration={3} scripted={4}",
                    character.Identity,
                    nanoId,
                    targetIdentity,
                    duration,
                    hasScript));
            return true;
        }
    }
}
