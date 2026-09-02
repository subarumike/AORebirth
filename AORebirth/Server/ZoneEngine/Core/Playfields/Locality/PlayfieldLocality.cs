namespace ZoneEngine.Core.Playfields.Locality
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.GameData;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;

    using Utility.Config;

    using ZoneEngine.Core.Controllers;

    using Config = Utility.Config.ConfigReadWrite;

    internal sealed class PlayfieldLocality
    {
        private readonly Identity playfieldIdentity;
        private readonly IPlayfieldCellLayout layout;
        private readonly PlayfieldLocalityPolicy policy;
        private readonly PlayfieldDynelCellRegistry cells;
        private readonly PlayfieldLocalityVisibility visibility;
        private readonly PlayfieldLocalityPackets packets;
        private readonly PlayfieldCellResourceHub resourceHub;
        private readonly PlayfieldCellLocalityMonitor cellMonitor;
        private readonly PlayfieldCellHeatScheduler heatScheduler;
        private readonly PlayfieldLocalityTickCallbacks tickCallbacks;

        internal PlayfieldLocality(
            Identity playfieldIdentity,
            PlayfieldMetaData metaData,
            PlayfieldVisibilityFanoutRuntimeService visibilityFanout,
            PlayfieldPacketSequencingRuntimeService packetSequences,
            PlayfieldLocalityTickCallbacks tickCallbacks)
        {
            if (playfieldIdentity.Instance <= 0)
            {
                throw new ArgumentOutOfRangeException("playfieldIdentity");
            }

            this.playfieldIdentity = playfieldIdentity;
            this.tickCallbacks = tickCallbacks ?? throw new ArgumentNullException("tickCallbacks");
            this.layout = PlayfieldCellLayoutFactory.Create(playfieldIdentity.Instance, metaData);
            this.policy = PlayfieldLocalityPolicy.FromConfig(
                Config.Instance.CurrentConfig == null ? null : Config.Instance.CurrentConfig.Locality);
            this.cells = new PlayfieldDynelCellRegistry(this.layout);
            this.visibility = new PlayfieldLocalityVisibility(playfieldIdentity, this.layout, this.policy, this.cells);
            this.packets = new PlayfieldLocalityPackets(visibilityFanout, packetSequences, this.visibility);
            this.resourceHub = new PlayfieldCellResourceHub();
            this.resourceHub.AddLoader(new PlaceholderCellSurfaceLoader());
            this.cellMonitor = new PlayfieldCellLocalityMonitor(this.layout, this.policy, this.cells, this.resourceHub);
            this.heatScheduler = new PlayfieldCellHeatScheduler(this.layout, this.policy, this.cells);
        }

        internal IPlayfieldCellLayout Layout
        {
            get { return this.layout; }
        }

        internal void RegisterCharacter(ICharacter character)
        {
            this.cells.Register(character);
        }

        internal bool MoveCharacter(ICharacter character)
        {
            return this.cells.Move(character);
        }

        internal void UnregisterCharacter(Identity identity)
        {
            this.visibility.UnregisterSource(identity);
            this.cells.Unregister(identity);
        }

        internal void ForgetRecipient(Identity recipientIdentity)
        {
            this.visibility.ForgetRecipient(recipientIdentity);
        }

        internal void Clear()
        {
            this.cellMonitor.Clear();
            this.visibility.Clear();
            this.cells.Clear();
        }

        internal void SendExistingCharacterVisibilityToClient(
            ICharacter recipient,
            IEnumerable<ICharacter> characters,
            Action<MessageBody> sendVisibilityMessage)
        {
            this.packets.SendExistingCharacterVisibilityToClient(recipient, characters, sendVisibilityMessage);
        }

        internal void AnnounceJoiningCharacterVisibility(
            ICharacter character,
            Action<ICharacter, MessageBody> sendVisibilityMessage,
            Action<ICharacter, Identity> sendLeaveVisibility)
        {
            this.RegisterCharacter(character);
            this.packets.AnnounceJoiningCharacterVisibility(character, sendVisibilityMessage, sendLeaveVisibility);
        }

        internal void RefreshCharacterVisibility(
            ICharacter character,
            Action<ICharacter, MessageBody> sendVisibilityMessage,
            Action<ICharacter, Identity> sendLeaveVisibility)
        {
            this.packets.AnnounceJoiningCharacterVisibility(character, sendVisibilityMessage, sendLeaveVisibility);
        }

        internal void AnnounceSpawnedCharacterVisibility(
            ICharacter character,
            Identity alreadyVisibleRecipient,
            Action<ICharacter, MessageBody> sendVisibilityMessage,
            Action<ICharacter, Identity> sendLeaveVisibility)
        {
            if (character == null)
            {
                return;
            }

            this.RegisterCharacter(character);
            if (alreadyVisibleRecipient != Identity.None)
            {
                ICharacter recipient = this.ResolveCharacter(alreadyVisibleRecipient);
                if (recipient != null)
                {
                    this.visibility.MarkVisibleEntry(recipient, character);
                }
            }

            this.packets.AnnounceJoiningCharacterVisibility(character, sendVisibilityMessage, sendLeaveVisibility);
        }

        internal bool SendCharacterVisibilityEntry(
            ICharacter source,
            ICharacter recipient,
            Action<MessageBody> sendVisibilityMessage)
        {
            this.RegisterCharacter(source);
            return this.packets.SendCharacterVisibilityEntry(source, recipient, sendVisibilityMessage);
        }

        internal IReadOnlyList<ICharacter> VisibleRecipientsForSource(Identity sourceIdentity)
        {
            return this.visibility.VisibleRecipientsForSource(sourceIdentity);
        }

        internal bool CanReceive(ICharacter source, ICharacter recipient)
        {
            return this.visibility.CanReceive(source, recipient);
        }

        internal bool SharesVisibilityNeighborhood(ICharacter recipient, ICharacter source)
        {
            if (recipient == null || source == null)
            {
                return false;
            }

            if (this.layout.IsIndoor)
            {
                return recipient.Identity != source.Identity;
            }

            int recipientCellId;
            int sourceCellId;
            if (!this.cells.TryGetCellId(recipient, out recipientCellId)
                || !this.cells.TryGetCellId(source, out sourceCellId)
                || recipientCellId < 0
                || sourceCellId < 0)
            {
                return false;
            }

            var neighbors = new List<int>();
            this.cells.CollectNeighborCells(recipientCellId, this.policy.VisibilityNeighborLevel, neighbors);
            return neighbors.Contains(sourceCellId);
        }

        internal void Tick(double deltaTime)
        {
            IEnumerable<ICharacter> players = this.tickCallbacks.GetConnectedPlayers != null
                ? this.tickCallbacks.GetConnectedPlayers()
                : Enumerable.Empty<ICharacter>();
            this.cellMonitor.UpdatePlayers(players);

            IEnumerable<ICharacter> combatHot = this.CollectCombatHotCharacters();
            this.heatScheduler.Tick(
                players,
                combatHot,
                (dynel, dt) => this.ProcessDynelTick(dynel, dt),
                deltaTime);
        }

        private void ProcessDynelTick(ICharacter dynel, double deltaTime)
        {
            if (dynel == null || dynel.Starting)
            {
                return;
            }

            if (this.tickCallbacks.ProcessDeadNpcDespawn != null
                && this.tickCallbacks.ProcessDeadNpcDespawn(dynel))
            {
                return;
            }

            if (dynel.DoNotDoTimers
                && (this.tickCallbacks.HasPendingDeadNpcDespawn == null
                    || !this.tickCallbacks.HasPendingDeadNpcDespawn(dynel.Identity)))
            {
                return;
            }

            if (this.tickCallbacks.ProcessCharacterTick != null)
            {
                this.tickCallbacks.ProcessCharacterTick(dynel, deltaTime);
            }

            if (dynel.Controller is NPCController)
            {
                if (this.tickCallbacks.ProcessNpcPatrolTick != null)
                {
                    this.tickCallbacks.ProcessNpcPatrolTick(dynel);
                }
            }
            else if (this.tickCallbacks.ProcessFollow != null)
            {
                this.tickCallbacks.ProcessFollow(dynel);
            }

            if (dynel.Controller is PlayerController && this.tickCallbacks.ProcessPlayerCollision != null)
            {
                this.tickCallbacks.ProcessPlayerCollision(dynel);
            }
        }

        private IEnumerable<ICharacter> CollectCombatHotCharacters()
        {
            if (this.tickCallbacks.GetAllCharacters == null)
            {
                return Enumerable.Empty<ICharacter>();
            }

            return this.tickCallbacks.GetAllCharacters()
                .Where(
                    c =>
                        c != null
                        && c.Controller is NPCController
                        && c.FightingTarget != Identity.None
                        && c.Stats[AORebirth.Enums.StatIds.health].Value > 0);
        }

        private ICharacter ResolveCharacter(Identity identity)
        {
            if (this.tickCallbacks.GetAllCharacters == null)
            {
                return null;
            }

            return this.tickCallbacks.GetAllCharacters().FirstOrDefault(c => c.Identity == identity);
        }
    }
}
