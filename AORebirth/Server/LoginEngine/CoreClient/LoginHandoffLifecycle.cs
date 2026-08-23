namespace LoginEngine.CoreClient
{
    using System;

    using AORebirth.Database.Dao;

    public enum LoginHandoffState
    {
        None,
        PreHandoffLoginOwned,
        HandoffStarted,
        ZoneAccepted,
        CleanupCompleted
    }

    public interface ILoginHandoffOnlineStore
    {
        void SetOnline(int characterId);

        LoginOwnedOnlineCleanupResult TryClearLoginOwnership(int characterId);
    }

    public sealed class CharacterDaoLoginHandoffOnlineStore : ILoginHandoffOnlineStore
    {
        public void SetOnline(int characterId)
        {
            CharacterDao.Instance.SetOnline(characterId);
        }

        public LoginOwnedOnlineCleanupResult TryClearLoginOwnership(int characterId)
        {
            return CharacterOnlineOwnershipGuard.TryClearLoginOwnership(characterId);
        }
    }

    public sealed class LoginHandoffLifecycle
    {
        private readonly object sync = new object();
        private readonly ILoginHandoffOnlineStore store;
        private readonly Action<string> audit;
        private int characterId;
        private LoginHandoffState state;

        public LoginHandoffLifecycle(ILoginHandoffOnlineStore store, Action<string> audit)
        {
            if (store == null)
            {
                throw new ArgumentNullException("store");
            }

            this.store = store;
            this.audit = audit ?? delegate { };
        }

        public int CharacterId
        {
            get
            {
                lock (this.sync)
                {
                    return this.characterId;
                }
            }
        }

        public LoginHandoffState State
        {
            get
            {
                lock (this.sync)
                {
                    return this.state;
                }
            }
        }

        public void MarkOnline(int selectedCharacterId)
        {
            if (selectedCharacterId <= 0)
            {
                throw new ArgumentOutOfRangeException("selectedCharacterId");
            }

            lock (this.sync)
            {
                if (this.state != LoginHandoffState.None)
                {
                    throw new InvalidOperationException("A character handoff is already active for this login session.");
                }

                this.store.SetOnline(selectedCharacterId);
                this.characterId = selectedCharacterId;
                this.state = LoginHandoffState.PreHandoffLoginOwned;
                this.Log("online_marked", "reason=character-selected");
            }
        }

        public void StartHandoff()
        {
            lock (this.sync)
            {
                if (this.state != LoginHandoffState.PreHandoffLoginOwned)
                {
                    throw new InvalidOperationException("Zone handoff cannot start from state " + this.state + ".");
                }

                this.state = LoginHandoffState.HandoffStarted;
                this.Log("handoff_started", "reason=zone-redirection");
            }
        }

        public void RecordZoneAccepted(string reason)
        {
            lock (this.sync)
            {
                if (this.state == LoginHandoffState.None)
                {
                    return;
                }

                this.state = LoginHandoffState.ZoneAccepted;
                this.Log("zone_accepted", "reason=" + SafeReason(reason));
            }
        }

        public LoginOwnedOnlineCleanupResult CleanupLoginOwnership(string reason)
        {
            lock (this.sync)
            {
                if (this.state == LoginHandoffState.None)
                {
                    return LoginOwnedOnlineCleanupResult.Cleared;
                }

                this.Log("session_lost", "reason=" + SafeReason(reason));
                if (this.state == LoginHandoffState.ZoneAccepted)
                {
                    this.Log("cleanup_skipped", "reason=zone-owned");
                    return LoginOwnedOnlineCleanupResult.ZoneOwned;
                }

                if (this.state == LoginHandoffState.CleanupCompleted)
                {
                    this.Log("cleanup_skipped", "reason=already-completed");
                    return LoginOwnedOnlineCleanupResult.Cleared;
                }

                try
                {
                    LoginOwnedOnlineCleanupResult result =
                        this.store.TryClearLoginOwnership(this.characterId);
                    if (result == LoginOwnedOnlineCleanupResult.ZoneOwned)
                    {
                        this.state = LoginHandoffState.ZoneAccepted;
                        this.Log("cleanup_skipped", "reason=zone-ownership-gate-held");
                        return result;
                    }

                    this.state = LoginHandoffState.CleanupCompleted;
                    this.Log("cleanup_completed", "reason=" + SafeReason(reason));
                    return result;
                }
                catch (Exception exception)
                {
                    this.Log("cleanup_failed", "reason=" + SafeReason(reason) + " error=" + exception.GetType().Name);
                    throw;
                }
            }
        }

        private void Log(string eventName, string details)
        {
            this.audit(
                "LOGIN_HANDOFF event=" + eventName
                + " characterId=" + this.characterId
                + " state=" + this.state
                + " " + details);
        }

        private static string SafeReason(string reason)
        {
            return string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason.Replace(' ', '_');
        }
    }
}
