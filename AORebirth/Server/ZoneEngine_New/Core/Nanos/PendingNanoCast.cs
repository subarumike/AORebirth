namespace ZoneEngine_New.Core.Nanos
{
    using System;

    using SmokeLounge.AOtomation.Messaging.GameData;

    /// <summary>
    /// A cast bar in flight. Completion is a UTC deadline so a long tick cannot stretch it,
    /// and the cast is discarded (never completed early) when it is interrupted.
    /// </summary>
    public sealed class PendingNanoCast
    {
        public PendingNanoCast(
            NanoSpell spell,
            Identity target,
            int nanoCost,
            int attackTimeCentiseconds,
            DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(spell);

            Spell = spell;
            Target = target;
            NanoCost = nanoCost;
            AttackTimeCentiseconds = attackTimeCentiseconds;
            CompletesAtUtc = nowUtc.AddMilliseconds(attackTimeCentiseconds * 10L);
        }

        public NanoSpell Spell { get; }

        public Identity Target { get; }

        /// <summary>Cost resolved at cast start; the same amount is charged on completion.</summary>
        public int NanoCost { get; }

        public int AttackTimeCentiseconds { get; }

        public DateTime CompletesAtUtc { get; }

        public bool IsReady(DateTime nowUtc) => nowUtc >= CompletesAtUtc;
    }
}
