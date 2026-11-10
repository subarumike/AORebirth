namespace ZoneEngine_New.Core.Organizations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AORebirth.Interfaces.Persistence.Characters;
    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine_New.Core.Characters;
    using ZoneEngine_New.Core.Configuration;
    using ZoneEngine_New.Core.Data;
    using ZoneEngine_New.Core.Entities;
    using ZoneEngine_New.Core.Helpers;
    using ZoneEngine_New.Core.Playfield;

    /// <summary>
    /// Individual org tax. Each non-leader member is charged the org tax every <c>taxTickSeconds</c> of personal /played time.
    /// An unpaid charge warns the member for <c>taxPeriodSeconds</c> real seconds; an unpaid warning suspends the member and
    /// adds 125% of the tax to a cumulative debt. The suspension ends by paying the debt into the organization bank.
    /// Offline members are not charged: their deadlines are processed at their next online tick.
    /// </summary>
    public sealed class OrgTaxTickService
    {
        /// <summary>Organization bank cap (rules.txt).</summary>
        public const long BankCap = 3_999_999_999;

        const long DebtNumerator = 5;
        const long DebtDenominator = 4;

        // Catalogue ids (Text.json): the warning, suspension and deduction notices are category 110; debt paid is 508.
        const int MemberCategory = 508;
        const int TaxNoticeCategory = 110;
        const int TaxUnpaidWarningMessage = 84220094;
        const int TaxSuspendedMessage = 168308756;
        const int TaxDeductedMessage = 108633128;
        const int DebtPaidMessage = 236096388;
        const int DebtStatusMessage = 182058216;

        sealed class MemberTax
        {
            public int CharacterId;
            public int OrganizationId;
            public string Name = string.Empty;
            public long NextTaxPlayed;
            public long Debt;
            public OrgTaxStage Stage;
            public long StageEndsUnix;
            public DateTime NextCheckUtc;
        }

        /// <summary>Remaining real seconds of an active suspension and the debt that ends it.</summary>
        public readonly record struct SuspensionStatus(long SecondsLeft, long Debt);

        readonly IOrganizationRepository _organizations;
        readonly CharacterPlayedClock _playedClock;
        readonly GameplaySettings _settings;
        readonly Lazy<PlayfieldManager> _playfields;
        readonly Dictionary<int, MemberTax> _members = new();
        readonly object _gate = new();

        public OrgTaxTickService(IOrganizationRepository organizations, CharacterPlayedClock playedClock,
            GameplaySettings settings, Lazy<PlayfieldManager> playfields)
        {
            _organizations = organizations;
            _playedClock = playedClock;
            _settings = settings;
            _playfields = playfields;
        }

        /// <summary>Processes the deadlines of one online member. Called from the playfield tick and throttled to once per second.</summary>
        public void Tick(Player player, DateTime nowUtc)
        {
            if (player.Organization is not { } org || OrgMembershipService.IsLeader(player, org) || player.IsPersistenceQuarantined)
                return;

            lock (_gate)
            {
                MemberTax member = Load(player, org);
                if (nowUtc < member.NextCheckUtc)
                    return;

                member.NextCheckUtc = nowUtc.AddSeconds(1);
                ProcessExpiry(player, org, member, nowUtc);
                ProcessSchedule(player, org, member, nowUtc);
            }
        }

        /// <summary>Unpaid debt of the member (0 when none). Used by /org debt.</summary>
        public long DebtOf(Player player)
        {
            if (player.Organization is not { } org)
                return 0;

            lock (_gate)
                return Load(player, org).Debt;
        }

        /// <summary>Active suspension of the member, or null when the member is not suspended.</summary>
        public SuspensionStatus? SuspensionOf(Player player, DateTime nowUtc)
        {
            if (player.Organization is not { } org)
                return null;

            lock (_gate)
            {
                MemberTax member = Load(player, org);
                ProcessExpiry(player, org, member, nowUtc);
                if (member.Stage != OrgTaxStage.Suspended)
                    return null;

                return new SuspensionStatus(Math.Max(0, member.StageEndsUnix - ToUnix(nowUtc)), member.Debt);
            }
        }

        /// <summary>
        /// Pays the member's debt from a /org bank add before the rest of the deposit. Returns the amount applied to the debt;
        /// 0 when there is no debt or the cash transfer was refused.
        /// </summary>
        public long RepayDebt(Player player, long amount)
        {
            if (player.Organization is not { } org || amount <= 0)
                return 0;

            lock (_gate)
            {
                MemberTax member = Load(player, org);
                if (member.Debt <= 0)
                    return 0;

                long applied = Math.Min(amount, member.Debt);
                if (!TryCharge(player, org, applied))
                    return 0;

                member.Debt -= applied;
                Save(member);
                return applied;
            }
        }

        /// <summary>Drops the cached state of a character who left, was kicked or whose organization was disbanded (its row is deleted by the repository).</summary>
        public void Forget(int characterId)
        {
            lock (_gate)
                _members.Remove(characterId);
        }

        MemberTax Load(Player player, OrganizationRecord org)
        {
            int characterId = player.Identity.Instance;
            if (_members.TryGetValue(characterId, out MemberTax? cached) && cached.OrganizationId == org.Id)
                return cached;

            long taxTick = _settings.Org.TaxTickSeconds;
            PersistedOrgMemberTaxData? stored = _organizations.GetMemberTax(characterId);
            MemberTax member;
            if (stored is null || stored.OrganizationId != org.Id)
            {
                member = new MemberTax
                {
                    NextTaxPlayed = _playedClock.Played(characterId) + taxTick,
                };
            }
            else
            {
                member = new MemberTax
                {
                    NextTaxPlayed = stored.NextTaxPlayed,
                    Debt = stored.Debt,
                    Stage = stored.Stage,
                    StageEndsUnix = stored.StageEndsUtc,
                };
            }

            member.CharacterId = characterId;
            member.OrganizationId = org.Id;
            member.Name = player.Name;
            _members[characterId] = member;
            if (stored is null || stored.OrganizationId != org.Id)
                Save(member);

            return member;
        }

        void ProcessExpiry(Player player, OrganizationRecord org, MemberTax member, DateTime nowUtc)
        {
            long nowUnix = ToUnix(nowUtc);
            long period = _settings.Org.TaxPeriodSeconds;

            if (member.Stage == OrgTaxStage.Suspended && nowUnix >= member.StageEndsUnix)
            {
                if (TryCharge(player, org, member.Debt))
                {
                    if (member.Debt > 0)
                        ClientFeedback.SendFormattedInCategory(player, MemberCategory, DebtPaidMessage);

                    member.Debt = 0;
                    member.Stage = OrgTaxStage.None;
                    member.StageEndsUnix = 0;
                }
                else
                {
                    member.StageEndsUnix = nowUnix + period;
                    SendTaxNotice(player, TaxSuspendedMessage, org.Tax);
                }

                Save(member);
            }
            else if (member.Stage == OrgTaxStage.Warned && nowUnix >= member.StageEndsUnix)
            {
                // A debt accrued by ticks during the ultimatum keeps the member unpaid; the suspension settles it.
                if (member.Debt == 0 && TryCharge(player, org, org.Tax))
                {
                    member.Stage = OrgTaxStage.None;
                    member.StageEndsUnix = 0;
                }
                else
                {
                    member.Stage = OrgTaxStage.Suspended;
                    member.Debt += TaxDebt(org.Tax);
                    SendDebtStatus(player, member.Debt);
                    member.StageEndsUnix = nowUnix + period;
                    SendTaxNotice(player, TaxSuspendedMessage, org.Tax);
                }

                Save(member);
            }
        }

        void ProcessSchedule(Player player, OrganizationRecord org, MemberTax member, DateTime nowUtc)
        {
            long played = _playedClock.Played(member.CharacterId);
            if (played < member.NextTaxPlayed)
                return;

            if (org.Tax > 0)
            {
                // AOR choice (not captured): a tick during an open ultimatum or suspension adds the debt, not a second charge.
                if (member.Stage is OrgTaxStage.Suspended or OrgTaxStage.Warned)
                {
                    member.Debt += TaxDebt(org.Tax);
                    SendDebtStatus(player, member.Debt);
                }
                else if (!TryCharge(player, org, org.Tax))
                {
                    member.Stage = OrgTaxStage.Warned;
                    member.StageEndsUnix = ToUnix(nowUtc) + _settings.Org.TaxPeriodSeconds;
                    SendTaxNotice(player, TaxUnpaidWarningMessage, org.Tax);
                }
                else
                {
                    SendTaxNotice(player, TaxDeductedMessage, org.Tax);
                }
            }

            long taxTick = _settings.Org.TaxTickSeconds;
            long next = member.NextTaxPlayed + taxTick;
            member.NextTaxPlayed = next > played ? next : played + taxTick;
            Save(member);
        }

        /// <summary>Moves the amount from the member's cash to the organization bank. Silent on success; false when refused.</summary>
        bool TryCharge(Player player, OrganizationRecord org, long amount)
        {
            if (amount <= 0)
                return true;

            player.FlushDirtyStats();
            PersistedOrganizationBankResult result = _organizations.TransferBank(org.Id, player.Identity.Instance, true,
                amount, BankCap, CashLimit.Max - 1);
            if (result.Outcome != OrganizationBankOutcome.Done)
                return false;

            player.Stats.Set(CharacterStat.Cash, (int)result.Cash, StatDetail.Base, dirty: false);
            player.Session?.Send(new StatMessage
            {
                Identity = player.Identity,
                Stats =
                [
                    new GameTuple<CharacterStat, uint>
                    {
                        Value1 = CharacterStat.Cash,
                        Value2 = (uint)result.Cash
                    }
                ]
            });

            foreach (Player online in _playfields.Value.SnapshotPlayers().Where(p => p.Organization?.Id == org.Id))
            {
                if (online.Organization is { } current)
                    online.Organization = current with { Bank = result.Bank };
            }

            return true;
        }

        static long TaxDebt(long tax) => tax * DebtNumerator / DebtDenominator;

        static long ToUnix(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();

        void Save(MemberTax member)
        {
            _organizations.SaveMemberTax(member.CharacterId, new PersistedOrgMemberTaxData
            {
                OrganizationId = member.OrganizationId,
                CharacterName = member.Name,
                NextTaxPlayed = member.NextTaxPlayed,
                Debt = member.Debt,
                Stage = member.Stage,
                StageEndsUtc = member.StageEndsUnix,
            });
        }

        // Message ids of the notices above are category 110 (Text.json); the tax argument is an integer.
        static void SendDebtStatus(Player player, long debt)
            => ClientFeedback.SendFormattedInCategory(player, MemberCategory, DebtStatusMessage, MessageInt(debt));

        static void SendTaxNotice(Player player, int messageId, long tax)
            => ClientFeedback.SendFormattedInCategory(player, TaxNoticeCategory, messageId, MessageInt(tax));

        internal static int MessageInt(long value) => (int)Math.Clamp(value, 0, int.MaxValue);
    }
}
