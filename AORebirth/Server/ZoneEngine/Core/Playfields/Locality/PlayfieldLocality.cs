namespace ZoneEngine.Core.Playfields.Locality
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.GameData;
    using AORebirth.Core.Vector;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;

    using Utility;
    using Utility.Config;

    using ZoneEngine.Core.Controllers;

    using Config = Utility.Config.ConfigReadWrite;

    internal sealed class PlayfieldLocalityPolicy
    {
        private const int DefaultVisibilityNeighborLevel = 2;
        private const int DefaultHotNeighborLevel = 1;
        private const int DefaultWarmNeighborLevel = 2;
        private const int DefaultCellSleepTimeSeconds = 30;

        private PlayfieldLocalityPolicy(
            bool enableCellHeatScheduling,
            int visibilityNeighborLevel,
            int hotNeighborLevel,
            int warmNeighborLevel,
            int cellSleepTimeSeconds)
        {
            EnableCellHeatScheduling = enableCellHeatScheduling;
            VisibilityNeighborLevel = visibilityNeighborLevel;
            HotNeighborLevel = hotNeighborLevel;
            WarmNeighborLevel = warmNeighborLevel;
            CellSleepTimeSeconds = cellSleepTimeSeconds;
        }

        internal bool EnableCellHeatScheduling { get; private set; }

        internal int VisibilityNeighborLevel { get; private set; }

        internal int HotNeighborLevel { get; private set; }

        internal int WarmNeighborLevel { get; private set; }

        internal int CellSleepTimeSeconds { get; private set; }

        internal static PlayfieldLocalityPolicy FromConfig(LocalitySettings settings)
        {
            bool enableCellHeatScheduling = settings != null && settings.EnableCellHeatScheduling;
            int visibility = DefaultVisibilityNeighborLevel;
            int hot = DefaultHotNeighborLevel;
            int warm = DefaultWarmNeighborLevel;
            int sleep = DefaultCellSleepTimeSeconds;

            if (settings != null)
            {
                if (settings.VisibilityNeighborLevel > 0)
                {
                    visibility = settings.VisibilityNeighborLevel;
                }

                if (settings.HotNeighborLevel > 0)
                {
                    hot = settings.HotNeighborLevel;
                }

                if (settings.WarmNeighborLevel > 0)
                {
                    warm = settings.WarmNeighborLevel;
                }

                if (settings.CellSleepTime > 0)
                {
                    sleep = settings.CellSleepTime;
                }
            }

            if (hot > warm)
            {
                hot = DefaultHotNeighborLevel;
                warm = DefaultWarmNeighborLevel;
            }

            if (warm > visibility)
            {
                warm = Math.Min(warm, visibility);
                if (hot > warm)
                {
                    hot = DefaultHotNeighborLevel;
                    warm = DefaultWarmNeighborLevel;
                }
            }

            return new PlayfieldLocalityPolicy(enableCellHeatScheduling, visibility, hot, warm, sleep);
        }
    }


    //TODO: Remove this and have the dynel own it's own mechanics.
    internal sealed class PlayfieldLocalityTickCallbacks
    {
        internal Func<Identity, bool> HasPendingDeadNpcDespawn { get; set; }

        internal Func<ICharacter, bool> ProcessDeadNpcDespawn { get; set; }

        internal Action<ICharacter, double> ProcessCharacterTick { get; set; }

        internal Action<ICharacter> ProcessNpcPatrolTick { get; set; }

        internal Action<ICharacter> ProcessFollow { get; set; }

        internal Action<ICharacter> ProcessPlayerCollision { get; set; }

        internal Action<ICharacter> ProcessPlayerCellChanged { get; set; }
    }

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
        private readonly PlayfieldDynelRegistry dynelRegistry;

        internal PlayfieldLocality(
            Identity playfieldIdentity,
            PlayfieldMetaData metaData,
            PlayfieldVisibilityFanoutRuntimeService visibilityFanout,
            PlayfieldPacketSequencingRuntimeService packetSequences,
            PlayfieldDynelRegistry dynelRegistry,
            PlayfieldLocalityTickCallbacks tickCallbacks)
        {
            if (playfieldIdentity.Instance <= 0)
            {
                throw new ArgumentOutOfRangeException("playfieldIdentity");
            }

            this.playfieldIdentity = playfieldIdentity;
            this.dynelRegistry = dynelRegistry ?? throw new ArgumentNullException("dynelRegistry");
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

        internal void RegisterDynel(IDynel dynel)
        {
            this.cells.Register(dynel);
        }

        internal void RegisterStaticDynel(StaticDynel staticDynel)
        {
            this.cells.Register(staticDynel);
        }

        internal bool MoveCharacter(ICharacter character)
        {
            if (character == null)
            {
                return false;
            }

            int oldCellId;
            int newCellId;
            bool cellChanged = this.cells.Move(character, out oldCellId, out newCellId);
            if (cellChanged && character.Controller is PlayerController)
            {
                this.LogPlayerCellChange(character, oldCellId, newCellId);
            }

            return cellChanged;
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

        internal bool SharesVisibilityNeighborhood(ICharacter recipient, Identity sourceIdentity)
        {
            if (recipient == null)
            {
                return false;
            }

            if (this.layout.IsIndoor)
            {
                return recipient.Identity != sourceIdentity;
            }

            int recipientCellId;
            int sourceCellId;
            if (!this.cells.TryGetCellId(recipient, out recipientCellId)
                || !this.cells.TryGetCellId(sourceIdentity, out sourceCellId)
                || recipientCellId < 0
                || sourceCellId < 0)
            {
                return false;
            }

            var neighbors = new List<int>();
            this.cells.CollectNeighborCells(recipientCellId, this.policy.VisibilityNeighborLevel, neighbors);
            return neighbors.Contains(sourceCellId);
        }

        internal bool SharesVisibilityNeighborhood(ICharacter recipient, Coordinate sourceCoordinate)
        {
            if (recipient == null || sourceCoordinate == null)
            {
                return false;
            }

            if (this.layout.IsIndoor)
            {
                return true;
            }

            int recipientCellId;
            int sourceCellId;
            if (!this.cells.TryGetCellId(recipient, out recipientCellId)
                || !this.layout.TryGetCellId(sourceCoordinate, out sourceCellId)
                || recipientCellId < 0
                || sourceCellId < 0)
            {
                return false;
            }

            var neighbors = new List<int>();
            this.cells.CollectNeighborCells(recipientCellId, this.policy.VisibilityNeighborLevel, neighbors);
            return neighbors.Contains(sourceCellId);
        }

        internal void Tick(
            double deltaTime,
            Action<ICharacter, MessageBody> sendVisibilityMessage,
            Action<ICharacter, Identity> sendLeaveVisibility)
        {
            foreach (IDynel dynel in this.dynelRegistry.Dynels())
            {
                if (dynel == null || !dynel.Transform.PositionChangedSinceLastTick)
                {
                    continue;
                }

                int oldCellId;
                int newCellId;
                bool cellChanged;
                ICharacter character = dynel as ICharacter;
                if (character != null)
                {
                    cellChanged = this.MoveCharacter(character);
                }
                else
                {
                    cellChanged = this.cells.Move(dynel, out oldCellId, out newCellId);
                }

                dynel.Transform.AcknowledgePositionChange();
                if (cellChanged && character != null)
                {
                    this.packets.AnnounceJoiningCharacterVisibility(
                        character,
                        sendVisibilityMessage,
                        sendLeaveVisibility);
                    if (character.Controller is PlayerController
                        && this.tickCallbacks.ProcessPlayerCellChanged != null)
                    {
                        this.tickCallbacks.ProcessPlayerCellChanged(character);
                    }
                }
            }

            IEnumerable<ICharacter> players = this.dynelRegistry.Players();
            this.cellMonitor.UpdatePlayers(players);

            if (!this.policy.EnableCellHeatScheduling)
            {
                IEnumerable<ICharacter> characters = this.dynelRegistry.Characters();
                foreach (ICharacter character in characters.ToList())
                {
                    this.ProcessDynelTick(character, deltaTime);
                }

                return;
            }

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
            return this.dynelRegistry.Characters()
                .Where(
                    c =>
                        c != null
                        && c.Controller is NPCController
                        && c.FightingTarget != Identity.None
                        && c.Stats[AORebirth.Enums.StatIds.health].Value > 0);
        }

        private ICharacter ResolveCharacter(Identity identity)
        {
            return this.dynelRegistry.Characters().FirstOrDefault(c => c.Identity == identity);
        }

        private void LogPlayerCellChange(ICharacter player, int oldCellId, int newCellId)
        {
            if (!LogUtil.HasDetail(DebugInfoDetail.Locality) || player == null)
            {
                return;
            }

            AORebirth.Core.Vector.Vector3 position = player.Position;
            LogUtil.Debug(
                DebugInfoDetail.Locality,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Playfield {0} player {1}/{2} cell {3} -> {4} pos=({5:F1},{6:F1},{7:F1})",
                    this.layout.PlayfieldId,
                    player.Identity,
                    player.Name ?? string.Empty,
                    this.FormatCellLabel(oldCellId),
                    this.FormatCellLabel(newCellId),
                    position == null ? 0f : position.xf,
                    position == null ? 0f : position.yf,
                    position == null ? 0f : position.zf));

            if (!this.policy.EnableCellHeatScheduling && !this.layout.IsIndoor)
            {
                this.heatScheduler.RefreshHeatDiagnostics(
                    this.dynelRegistry.Players(),
                    this.CollectCombatHotCharacters());
            }
        }

        private string FormatCellLabel(int cellId)
        {
            if (cellId < 0)
            {
                return "non-local";
            }

            if (this.layout.IsIndoor)
            {
                return cellId.ToString(CultureInfo.InvariantCulture);
            }

            this.layout.GetCellCoords(cellId, out int ix, out int iz);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} ({1},{2})",
                cellId,
                ix,
                iz);
        }
    }
}
