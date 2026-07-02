namespace ZoneEngine.Core
{
    #region Usings ...

    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    #endregion

    public enum ZoneClientSessionPhase
    {
        Connected,
        CharacterLoading,
        PlayfieldLoading,
        ReadyBlock,
        FullCharacterBoundary,
        CharInPlay,
        InPlay,
        Zoning,
        Disconnecting
    }

    public sealed class ZoneClientSessionLifecycleCoordinator
    {
        private readonly List<ZoneClientSessionPhase> phaseHistory =
            new List<ZoneClientSessionPhase>();

        public ZoneClientSessionLifecycleCoordinator()
        {
            this.TransitionTo(ZoneClientSessionPhase.Connected);
        }

        public ZoneClientSessionPhase Phase { get; private set; }

        public ReadOnlyCollection<ZoneClientSessionPhase> PhaseHistory
        {
            get
            {
                return this.phaseHistory.AsReadOnly();
            }
        }

        public void BeginCharacterLoading()
        {
            this.TransitionTo(ZoneClientSessionPhase.CharacterLoading);
        }

        public void BeginPlayfieldLoading()
        {
            this.TransitionTo(ZoneClientSessionPhase.PlayfieldLoading);
        }

        public void BeginReadyBlock()
        {
            this.TransitionTo(ZoneClientSessionPhase.ReadyBlock);
        }

        public void BeginFullCharacterBoundary()
        {
            this.TransitionTo(ZoneClientSessionPhase.FullCharacterBoundary);
        }

        public void MarkCharInPlay()
        {
            this.TransitionTo(ZoneClientSessionPhase.CharInPlay);
        }

        public void MarkInPlay()
        {
            this.TransitionTo(ZoneClientSessionPhase.InPlay);
        }

        public void BeginZoning()
        {
            this.TransitionTo(ZoneClientSessionPhase.Zoning);
        }

        public void BeginDisconnecting()
        {
            this.TransitionTo(ZoneClientSessionPhase.Disconnecting);
        }

        private void TransitionTo(ZoneClientSessionPhase phase)
        {
            if (this.phaseHistory.Count > 0 && this.Phase == phase)
            {
                return;
            }

            this.Phase = phase;
            this.phaseHistory.Add(phase);
        }
    }
}
