namespace ZoneEngine_New.Core.Playfield
{
    using System;
    using AORebirth.Core.Vector;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Network;

    /// <summary>
    /// Finite, in-process handoff of the existing authoritative Player. Every transition is
    /// serialized by its original session; world changes run on exactly one owning tick.
    /// No transition waits for or enters the other playfield's tick lock.
    /// </summary>
    internal sealed class PlayfieldTransfer
    {
        private enum Phase { Scheduled, Departed, Returning, Arrived, Returned, Abandoned }
        private Phase _phase;
        private Vector3? _origin;
        private Quaternion? _originHeading;
        private readonly Func<bool>? _stillAuthorized;

        public PlayfieldTransfer(ZoneSession session, Player player, Playfield source,
            Playfield destination, Vector3 landing, Quaternion heading, Func<bool>? stillAuthorized = null)
        {
            Session = session; Player = player; Source = source; Destination = destination;
            Landing = new Vector3(landing.x, landing.y, landing.z);
            Heading = new Quaternion(heading.x, heading.y, heading.z, heading.w);
            _stillAuthorized = stillAuthorized;
        }

        public ZoneSession Session { get; }
        public Player Player { get; }
        public Playfield Source { get; }
        public Playfield Destination { get; }
        public Vector3 Landing { get; }
        public Quaternion Heading { get; }

        public void Depart()
        {
            Source.RequireTransferTick();
            // The write-behind failure path may close the session while holding this gate.
            // Take the same aggregate-before-session order; never wait for it under a session lock.
            lock (Player.PersistenceGate)
            lock (Session)
            {
                if (_phase != Phase.Scheduled) return;
                if (!Source.IsAuthoritativePlayer(Player) && ReferenceEquals(Player.Playfield, Source))
                {
                    Source.RemovePartialTransferArrival(Player);
                    Source.AbandonDetachedTransfer(Player, Session);
                    Finish(Phase.Abandoned); return;
                }
                if (Source.IsDisposed || Destination.IsDisposed || !OwnsSession() || !StillAuthorized()
                    || !ReferenceEquals(Player.Playfield, Source))
                { Finish(Phase.Abandoned); return; }
                _origin = new Vector3(Player.Position.x, Player.Position.y, Player.Position.z);
                _originHeading = new Quaternion(Player.Rotation.x, Player.Rotation.y, Player.Rotation.z, Player.Rotation.w);
                Session.State = SessionState.Loading;
                try
                {
                    Source.LeaveTransferredPlayer(Player); // Existing flush precedes unregister/departure.
                    _phase = Phase.Departed;
                    if (!Destination.QueueTransferArrival(this))
                    { _phase = Phase.Returning; Return(); }
                }
                catch
                {
                    // A failed flush has not left the source. A partially completed leave is
                    // restored by the same owner, never by the destination's calling thread.
                    _phase = Phase.Returning;
                    Return();
                    throw;
                }
            }
        }

        public void Arrive()
        {
            Destination.RequireTransferTick();
            lock (Session)
            {
                if (_phase != Phase.Departed) return;
                if (Destination.IsDisposed || !OwnsSession() || !StillAuthorized() || Player.Playfield != null
                    || !Destination.IsAuthoritativePlayer(Player))
                { RequestReturn(); return; }
                try
                {
                    // Resolve/encode both existing messages before changing destination ownership.
                    byte[][] packets = Session.PrepareTransferPackets(Player, Landing, Heading, Destination.Identity.Instance);
                    Destination.ArriveTransferredPlayer(Player, Landing);
                    Player.Rotation = Heading;
                    foreach (byte[] packet in packets) Session.Send(packet);
                    // Keep the same character authority while the client reconnects, but use
                    // the existing reconnect grace so an abandoned redirect cannot live forever.
                    Player.EnterLinkDead(PlayfieldManager.ResolveLinkDeadTimeout());
                    Session.UnbindPlayer();
                    Finish(Phase.Arrived);
                }
                catch (Exception exception)
                {
                    Destination.RemovePartialTransferArrival(Player);
                    Destination.ReportTransferFailure(Player, exception);
                    RequestReturn();
                }
            }
        }

        /// <summary>Transport/disposal path: only changes handoff state and queues owner work.</summary>
        public void RequestReturn()
        {
            lock (Session)
            {
                if (_phase == Phase.Scheduled) { Finish(Phase.Abandoned); return; }
                if (_phase != Phase.Departed) return;
                _phase = Phase.Returning;
                // Source disposal also owns and drains its outgoing set. An unavailable queue
                // is therefore not permission to mutate the source from this thread.
                Source.QueueTransferReturn(this);
            }
        }

        public void Return()
        {
            Source.RequireTransferTick();
            lock (Player.PersistenceGate)
            lock (Session)
            {
                if (_phase != Phase.Returning) return;
                try
                {
                    if (Player.Playfield == null || ReferenceEquals(Player.Playfield, Source))
                    {
                        if (!Source.CanRestoreTransfer(Player))
                        { Source.AbandonDetachedTransfer(Player, Session); Finish(Phase.Abandoned); return; }
                        Source.RestoreTransfer(Player, _origin!, _originHeading!);
                        if (OwnsSession()) Session.State = SessionState.InPlay;
                        if (!Source.IsDisposed && ReferenceEquals(Player.Session, Session)
                            && Session.State == SessionState.InPlay)
                            Source.RefreshReturnedTransfer(Player);
                    }
                    Finish(Phase.Returned);
                }
                catch (Exception exception)
                {
                    // Never release a detached owner for a new login that could overwrite it.
                    // Keep it in the source registry for its ordinary shutdown/logout handling.
                    Source.ReportTransferFailure(Player, exception);
                    Session.Close();
                    Finish(Phase.Abandoned);
                }
            }
        }

        public void SourceShutdown()
        {
            Source.RequireTransferTick();
            lock (Player.PersistenceGate)
            lock (Session)
            {
                if (_phase == Phase.Scheduled) { Finish(Phase.Abandoned); return; }
                if (_phase == Phase.Departed) _phase = Phase.Returning;
                if (_phase == Phase.Returning) Return();
            }
        }

        private bool OwnsSession() => Session.State != SessionState.Closed
            && ReferenceEquals(Session.Player, Player) && ReferenceEquals(Player.Session, Session)
            && !Player.IsPersistenceQuarantined;

        private bool StillAuthorized()
        {
            try { return _stillAuthorized?.Invoke() ?? true; }
            catch (Exception exception) { Source.ReportTransferFailure(Player, exception); return false; }
        }

        private void Finish(Phase phase)
        {
            _phase = phase;
            Source.ForgetOutgoingTransfer(this);
            Destination.ForgetIncomingTransfer(this);
            Session.FinishTransfer(this);
        }
    }
}
