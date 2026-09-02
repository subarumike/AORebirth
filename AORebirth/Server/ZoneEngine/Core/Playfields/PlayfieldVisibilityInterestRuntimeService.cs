namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Controllers;

    internal sealed class PlayfieldVisibilityInterestRuntimeService
    {
        private readonly PlayfieldVisibilityInterestPolicy policy;
        private readonly PlayfieldVisibilityInterestState<ICharacter> state;

        internal PlayfieldVisibilityInterestRuntimeService(
            PlayfieldVisibilityInterestPolicy policy,
            PlayfieldSpatialCharacterIndex spatialIndex)
        {
            if (policy == null)
            {
                throw new ArgumentNullException("policy");
            }

            if (spatialIndex == null)
            {
                throw new ArgumentNullException("spatialIndex");
            }

            this.policy = policy;
            this.state = new PlayfieldVisibilityInterestState<ICharacter>(
                policy,
                spatialIndex.InnerIndex,
                IdentityOf,
                PositionOf,
                CanShareVisibility,
                IsConnectedRecipient,
                IsPinnedVisibility,
                this.ResolveEnterRadius,
                this.ResolveLeaveRadius);
        }

        internal PlayfieldVisibilityInterestPolicy Policy
        {
            get { return this.policy; }
        }

        internal int LastCandidateInspectionCount
        {
            get { return this.state.LastCandidateInspectionCount; }
        }

        internal void Register(ICharacter character)
        {
            this.state.Register(character);
        }

        internal void Unregister(Identity identity)
        {
            this.state.Unregister(identity);
        }

        internal void Synchronize(IEnumerable<ICharacter> characters)
        {
            this.state.Synchronize(characters);
        }

        internal ReadOnlyCollection<ICharacter> SelectInitialCharacters(ICharacter recipient)
        {
            if (recipient == null || recipient.Playfield == null)
            {
                return new List<ICharacter>().AsReadOnly();
            }

            return this.state.SelectInitialValues(recipient);
        }

        internal bool MarkVisibleEntry(ICharacter recipient, ICharacter source)
        {
            return this.state.MarkVisibleEntry(recipient, source);
        }

        internal void CompleteInitialRecipient(ICharacter recipient)
        {
            this.state.CompleteInitialRecipient(recipient);
        }

        internal bool IsInitializedRecipient(Identity recipientIdentity)
        {
            return this.state.IsInitializedRecipient(recipientIdentity);
        }

        internal void ReconcileInitializedRecipients(
            ICharacter changedCharacter,
            Func<ICharacter, ICharacter, bool> enterVisibility,
            Action<ICharacter, Identity> leaveVisibility)
        {
            this.state.ReconcileInitializedRecipients(
                changedCharacter,
                enterVisibility,
                leaveVisibility);
        }

        internal ReadOnlyCollection<ICharacter> VisibleRecipientsForSource(Identity sourceIdentity)
        {
            return this.state.VisibleRecipientsForSource(sourceIdentity);
        }

        internal ReadOnlyCollection<ICharacter> VisibleSourcesForRecipient(Identity recipientIdentity)
        {
            return this.state.VisibleSourcesForRecipient(recipientIdentity);
        }

        internal bool CanReceive(ICharacter source, ICharacter recipient)
        {
            return this.state.CanReceive(source, recipient);
        }

        internal void ForgetRecipient(Identity recipientIdentity)
        {
            this.state.ForgetRecipient(recipientIdentity);
        }

        internal void Clear()
        {
            this.state.Clear();
        }

        private static Identity IdentityOf(ICharacter character)
        {
            return character.Identity;
        }

        private static VisibilityPosition PositionOf(ICharacter character)
        {
            AORebirth.Core.Vector.Coordinate coordinate = character.Coordinates();
            return new VisibilityPosition(coordinate.x, coordinate.y, coordinate.z);
        }

        private static bool CanShareVisibility(ICharacter recipient, ICharacter source)
        {
            return recipient != null
                   && source != null
                   && recipient.Identity != source.Identity
                   && recipient.Playfield != null
                   && source.InPlayfield(recipient.Playfield.Identity);
        }

        private static bool IsConnectedRecipient(ICharacter character)
        {
            return character != null
                   && character.Controller != null
                   && character.Controller.Client != null;
        }

        private static bool IsPinnedVisibility(ICharacter recipient, ICharacter source)
        {
            if (recipient != null
                && source != null
                && source.Stats[StatIds.petmaster].Value > 0
                && source.Stats[StatIds.petmaster].Value == recipient.Identity.Instance)
            {
                return true;
            }

            // Nascence D1/D2: pin living NPCs dungeon-wide for PF map red dots.
            // Door and container updates, not NPC markers, control floor-map reveal.
            if (recipient != null
                && source != null
                && recipient.Playfield != null
                && source.Controller is NPCController
                && source.Stats[StatIds.health].Value > 0
                && (AORebirth.Core.Playfields.NascenceDungeon1Rules.IsDungeonPlayfield(
                        recipient.Playfield.Identity.Instance)
                    || AORebirth.Core.Playfields.NascenceDungeon2Rules.IsDungeonPlayfield(
                        recipient.Playfield.Identity.Instance)
                    || AORebirth.Core.Playfields.NascenceDungeon3Rules.IsDungeonPlayfield(
                        recipient.Playfield.Identity.Instance)
                    || AORebirth.Core.Playfields.NascenceDungeon4Rules.IsDungeonPlayfield(
                        recipient.Playfield.Identity.Instance)))
            {
                return true;
            }

            // Capture 20260823-171238 Havaris is covered by the D1 dungeon-wide marker pin above.

            return false;
        }

        // Gold 20260725-151009: RK mission zone-in SCFU is start-room only.
        // Nascence D1/D2/D3 intentionally retain dungeon-wide NPC markers; their floor fog is
        // controlled by the door/container replay rather than character visibility.
        private const float MissionInstanceEnterRadius = 32.0f;

        private const float MissionInstanceLeaveRadius = 48.0f;

        private const float NascenceDungeon1DungeonWideEnterRadius = 2000.0f;

        private const float NascenceDungeon1DungeonWideLeaveRadius = 2100.0f;

        // D2 capture PF map shows every mob on every floor with no proximity gate.
        // Must stay <= MaximumLeaveRadius or UniformSpatialIndex.Query throws and
        // SelectInitialValues / reconcile abort — mobs only Force-appear in combat (~2s flicker).
        private const float NascenceDungeon2DungeonWideEnterRadius = 2000.0f;

        private const float NascenceDungeon2DungeonWideLeaveRadius = 2100.0f;

        private const float NascenceDungeon3DungeonWideEnterRadius = 2000.0f;

        private const float NascenceDungeon3DungeonWideLeaveRadius = 2100.0f;

        private const float NascenceDungeon4DungeonWideEnterRadius = 2000.0f;

        private const float NascenceDungeon4DungeonWideLeaveRadius = 2100.0f;

        private float ResolveEnterRadius(ICharacter recipient)
        {
            if (recipient != null
                && recipient.Playfield != null
                && AORebirth.Core.Playfields.NascenceDungeon1Rules.IsDungeonPlayfield(
                    recipient.Playfield.Identity.Instance))
            {
                return NascenceDungeon1DungeonWideEnterRadius;
            }

            if (recipient != null
                && recipient.Playfield != null
                && AORebirth.Core.Playfields.NascenceDungeon2Rules.IsDungeonPlayfield(
                    recipient.Playfield.Identity.Instance))
            {
                return NascenceDungeon2DungeonWideEnterRadius;
            }

            if (recipient != null
                && recipient.Playfield != null
                && AORebirth.Core.Playfields.NascenceDungeon3Rules.IsDungeonPlayfield(
                    recipient.Playfield.Identity.Instance))
            {
                return NascenceDungeon3DungeonWideEnterRadius;
            }

            if (recipient != null
                && recipient.Playfield != null
                && AORebirth.Core.Playfields.NascenceDungeon4Rules.IsDungeonPlayfield(
                    recipient.Playfield.Identity.Instance))
            {
                return NascenceDungeon4DungeonWideEnterRadius;
            }

            if (UsesTightAcgVisibility(recipient))
            {
                return MissionInstanceEnterRadius;
            }

            return this.policy.EnterRadius;
        }

        private float ResolveLeaveRadius(ICharacter recipient)
        {
            if (recipient != null
                && recipient.Playfield != null
                && AORebirth.Core.Playfields.NascenceDungeon1Rules.IsDungeonPlayfield(
                    recipient.Playfield.Identity.Instance))
            {
                return NascenceDungeon1DungeonWideLeaveRadius;
            }

            if (recipient != null
                && recipient.Playfield != null
                && AORebirth.Core.Playfields.NascenceDungeon2Rules.IsDungeonPlayfield(
                    recipient.Playfield.Identity.Instance))
            {
                return NascenceDungeon2DungeonWideLeaveRadius;
            }

            if (recipient != null
                && recipient.Playfield != null
                && AORebirth.Core.Playfields.NascenceDungeon3Rules.IsDungeonPlayfield(
                    recipient.Playfield.Identity.Instance))
            {
                return NascenceDungeon3DungeonWideLeaveRadius;
            }

            if (recipient != null
                && recipient.Playfield != null
                && AORebirth.Core.Playfields.NascenceDungeon4Rules.IsDungeonPlayfield(
                    recipient.Playfield.Identity.Instance))
            {
                return NascenceDungeon4DungeonWideLeaveRadius;
            }

            if (UsesTightAcgVisibility(recipient))
            {
                return MissionInstanceLeaveRadius;
            }

            return this.policy.LeaveRadius;
        }

        private static bool UsesTightAcgVisibility(ICharacter recipient)
        {
            if (recipient == null || recipient.Playfield == null)
            {
                return false;
            }

            int pf = recipient.Playfield.Identity.Instance;
            return ZoneEngine.Core.Missions.MissionInstanceService.IsMissionInstancePlayfield(pf);
        }
    }
}
