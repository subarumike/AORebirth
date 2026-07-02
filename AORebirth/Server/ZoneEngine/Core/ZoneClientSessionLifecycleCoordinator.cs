namespace ZoneEngine.Core
{
    #region Usings ...

    using System;
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

        public string PhaseTraceName
        {
            get
            {
                return GetTraceName(this.Phase);
            }
        }

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

        public void EnterReadyBlockForSessionInit()
        {
            this.TransitionTo(ZoneClientSessionPhase.ReadyBlock);
        }

        public void EnterFullCharacterBoundaryForSessionInit()
        {
            this.TransitionTo(ZoneClientSessionPhase.FullCharacterBoundary);
        }

        public void EnterCharInPlayForVisibilityEntry()
        {
            this.TransitionTo(ZoneClientSessionPhase.CharInPlay);
        }

        public void CompleteInPlayForSessionInit()
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

        public bool CanTransitionTo(ZoneClientSessionPhase phase)
        {
            if (this.phaseHistory.Count == 0)
            {
                return phase == ZoneClientSessionPhase.Connected;
            }

            return IsAllowedTransition(this.Phase, phase);
        }

        public static string GetTraceName(ZoneClientSessionPhase phase)
        {
            return "ZoneClientSession." + phase;
        }

        private void TransitionTo(ZoneClientSessionPhase phase)
        {
            if (this.phaseHistory.Count > 0 && this.Phase == phase)
            {
                return;
            }

            if (!this.CanTransitionTo(phase))
            {
                throw new InvalidOperationException(
                    "Invalid ZoneClient session transition from "
                    + GetTraceName(this.Phase)
                    + " to "
                    + GetTraceName(phase)
                    + ".");
            }

            this.Phase = phase;
            this.phaseHistory.Add(phase);
        }

        private static bool IsAllowedTransition(ZoneClientSessionPhase from, ZoneClientSessionPhase to)
        {
            if (from == to)
            {
                return true;
            }

            if (to == ZoneClientSessionPhase.Disconnecting)
            {
                return true;
            }

            switch (from)
            {
                case ZoneClientSessionPhase.Connected:
                    return to == ZoneClientSessionPhase.CharacterLoading;
                case ZoneClientSessionPhase.CharacterLoading:
                    return to == ZoneClientSessionPhase.PlayfieldLoading;
                case ZoneClientSessionPhase.PlayfieldLoading:
                    return to == ZoneClientSessionPhase.ReadyBlock;
                case ZoneClientSessionPhase.ReadyBlock:
                    return to == ZoneClientSessionPhase.FullCharacterBoundary;
                case ZoneClientSessionPhase.FullCharacterBoundary:
                    return to == ZoneClientSessionPhase.CharInPlay;
                case ZoneClientSessionPhase.CharInPlay:
                    return to == ZoneClientSessionPhase.InPlay;
                case ZoneClientSessionPhase.InPlay:
                    return to == ZoneClientSessionPhase.Zoning;
                case ZoneClientSessionPhase.Zoning:
                    return to == ZoneClientSessionPhase.PlayfieldLoading
                           || to == ZoneClientSessionPhase.ReadyBlock;
                default:
                    return false;
            }
        }
    }
}
