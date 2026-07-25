namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;

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
            return recipient != null
                   && source != null
                   && source.Stats[StatIds.petmaster].Value > 0
                   && source.Stats[StatIds.petmaster].Value == recipient.Identity.Instance;
        }

        // Gold 20260725-151009: zone-in SCFU wave is start-room only; far NPCs stream later.
        // Default 80m enter lights almost the whole L7 mish → PF Map fully explored (mobs as
        // "seen" positions). Keep mobs, but only stream nearby ones like live.
        private const float MissionInstanceEnterRadius = 32.0f;

        private const float MissionInstanceLeaveRadius = 48.0f;

        private float ResolveEnterRadius(ICharacter recipient)
        {
            if (recipient != null
                && recipient.Playfield != null
                && ZoneEngine.Core.Missions.MissionInstanceService.IsMissionInstancePlayfield(
                    recipient.Playfield.Identity.Instance))
            {
                return MissionInstanceEnterRadius;
            }

            return this.policy.EnterRadius;
        }

        private float ResolveLeaveRadius(ICharacter recipient)
        {
            if (recipient != null
                && recipient.Playfield != null
                && ZoneEngine.Core.Missions.MissionInstanceService.IsMissionInstancePlayfield(
                    recipient.Playfield.Identity.Instance))
            {
                return MissionInstanceLeaveRadius;
            }

            return this.policy.LeaveRadius;
        }
    }
}
