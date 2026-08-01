namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using AORebirth.Core.Entities;
    using AORebirth.Core.Network;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Missions;

    #endregion

    /// <summary>
    /// Key holder clicks a mission entrance (Rome house doors on pf 735, or any MissionEntrance dynel
    /// outdoors) → private instance. Capture <c>20260718-062936</c>: outdoor enter → N3Teleport →
    /// PLAYFIELD-INIT 1413198.
    /// </summary>
    public sealed class MissionEntranceInteractionHandler
    {
        public static readonly MissionEntranceInteractionHandler Default =
            new MissionEntranceInteractionHandler();

        private MissionEntranceInteractionHandler()
        {
        }

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            if (!MissionInstanceService.EntryEnabled || client == null || client.Controller == null)
            {
                return false;
            }

            ICharacter character = client.Controller.Character;
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            // Inside instance: Door/MissionEntrance use → exit to outdoor marker.
            // Also accept use while standing at the interior exit door (spawn) — some
            // clients target Building/Statel instead of Door when mesh replay is partial.
            if (MissionInstanceService.IsMissionInstancePlayfield(character.Playfield.Identity.Instance))
            {
                // Generated ACG exits are handled earlier by exact owner + PF2 + runtime
                // identity. Missing runtime state is a rejection, never permission to invoke
                // the legacy near-door fallback.
                if (MissionAcgBindingRuntime.ClaimsGeneratedLivePlayfield(
                    character.Playfield.Identity.Instance))
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                    return true;
                }

                bool doorTarget = MissionInstanceService.IsMissionExitDoorTarget(target);
                bool nearExit = MissionInstanceService.IsNearInteriorExitDoor(character, 8.0, 10.0);
                if (!doorTarget && !nearExit)
                {
                    return false;
                }

                if (!MissionInstanceService.TryExitMissionInstance(client))
                {
                    return false;
                }

                GenericCmdMessageHandler.Default.Acknowledge(character, message);
                return true;
            }

            bool generatedExteriorClaim =
                MissionAcgBindingRuntime.HasOwnedExteriorMarker(
                    character.Identity.Instance,
                    character.Playfield.Identity.Instance,
                    character.RawCoordinates.X,
                    character.RawCoordinates.Y,
                    character.RawCoordinates.Z,
                    10.0,
                    14.0)
                || MissionInstanceService.HasGeneratedAcceptedExteriorClaim(
                    character,
                    target,
                    10.0,
                    14.0);

            if (!MissionInstanceService.IsAcceptedMissionEntranceUse(character, target))
            {
                if (generatedExteriorClaim)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                    return true;
                }

                return false;
            }

            // Rome doors are only valid on Rome Blue unless they also match an outdoor MissionEntrance use.
            if (MissionInstanceService.IsRomeEntranceDoor(target.Instance)
                && character.Playfield.Identity.Instance != MissionInstanceService.RomeBluePlayfieldInstance
                && target.Type != IdentityType.MissionEntrance
                && !MissionInstanceService.IsNearAcceptedMarker(character, 10.0, 14.0))
            {
                if (generatedExteriorClaim)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                    return true;
                }

                return false;
            }

            if (!MissionKeyGrantService.HasMissionKey(character))
            {
                if (generatedExteriorClaim)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                    return true;
                }

                return false;
            }

            if (!MissionInstanceService.TryEnterMissionInstance(client, target))
            {
                if (generatedExteriorClaim)
                {
                    GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                    return true;
                }

                return false;
            }

            GenericCmdMessageHandler.Default.Acknowledge(character, message);
            return true;
        }
    }
}
