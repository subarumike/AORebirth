namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine_New.Core.Entities;

    public sealed class SetCommand : IGmCommand
    {
        public string Name => "set";

        public int RequiredGmLevel => 1;

        public string Usage => ".set <statName|statId> <value>";

        public void Execute(GmCommandContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Args.Length < 2
                || !CharacterStatParser.TryParse(context.Args[0], out CharacterStat stat)
                || !int.TryParse(context.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
                return;
            }

            if (!context.TryResolveCharacter(out Character subject))
                return;

            if (stat == CharacterStat.Level && subject.IsPlayer)
            {
                if (!subject.TrySetLevel(value))
                {
                    GmCommandFeedback.Send(
                        context.Session,
                        context.Player,
                        "Invalid level or XP table unavailable.");
                    return;
                }
            }
            else
            {
                subject.Stats.Set(stat, value, StatDetail.Base, dirty: true);
                subject.FlushDirtyStats();
            }

            GmCommandFeedback.Send(
                context.Session,
                context.Player,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Set {0} ({1}) = {2} [{3}]",
                    stat,
                    (int)stat,
                    value,
                    subject.Name ?? string.Empty));
        }
    }
}
