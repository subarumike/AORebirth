namespace ZoneEngine_New.Core.Nanos
{
    /// <summary>Why a cast attempt was refused. <see cref="None"/> means the cast may start.</summary>
    public enum NanoCastRefusal
    {
        None,
        CasterDead,
        AlreadyCasting,
        Recharging,
        NotUploaded,
        RequirementsNotMet,
        NotEnoughNano,
        InvalidTarget,
        TargetDead,
    }

    /// <summary>
    /// The cast gate, as a pure decision over already-resolved facts. The same gate runs twice:
    /// once when the client asks to cast, and once when the cast bar finishes, because nano,
    /// target and recharge state can all change while the bar runs.
    /// </summary>
    public static class NanoCastRules
    {
        public static NanoCastRefusal Evaluate(in NanoCastAttempt attempt)
        {
            if (attempt.CasterIsDead)
                return NanoCastRefusal.CasterDead;
            if (attempt.CasterIsCasting)
                return NanoCastRefusal.AlreadyCasting;
            if (attempt.CasterIsRecharging)
                return NanoCastRefusal.Recharging;
            if (!attempt.IsUploaded)
                return NanoCastRefusal.NotUploaded;
            if (!attempt.TargetExists)
                return NanoCastRefusal.InvalidTarget;
            if (attempt.TargetIsDead)
                return NanoCastRefusal.TargetDead;
            if (!attempt.RequirementsMet)
                return NanoCastRefusal.RequirementsNotMet;
            if (attempt.CurrentNano < attempt.NanoCost)
                return NanoCastRefusal.NotEnoughNano;

            return NanoCastRefusal.None;
        }

        /// <summary>Human-readable refusal for the caster's chat window.</summary>
        public static string Describe(NanoCastRefusal refusal) => refusal switch
        {
            NanoCastRefusal.CasterDead => "You cannot cast nano programs while dead.",
            NanoCastRefusal.AlreadyCasting => "You are already casting a nano program.",
            NanoCastRefusal.Recharging => "Your nano programs are still recharging.",
            NanoCastRefusal.NotUploaded => "You do not have that nano program uploaded.",
            NanoCastRefusal.RequirementsNotMet => "You do not meet the requirements for that nano program.",
            NanoCastRefusal.NotEnoughNano => "You do not have enough nano energy.",
            NanoCastRefusal.InvalidTarget => "That is not a valid target for that nano program.",
            NanoCastRefusal.TargetDead => "Your target is dead.",
            _ => string.Empty,
        };
    }

    /// <summary>Resolved inputs for one <see cref="NanoCastRules.Evaluate"/> call.</summary>
    public readonly struct NanoCastAttempt
    {
        public bool CasterIsDead { get; init; }

        public bool CasterIsCasting { get; init; }

        public bool CasterIsRecharging { get; init; }

        public bool IsUploaded { get; init; }

        public bool RequirementsMet { get; init; }

        public bool TargetExists { get; init; }

        public bool TargetIsDead { get; init; }

        public int CurrentNano { get; init; }

        public int NanoCost { get; init; }
    }
}
