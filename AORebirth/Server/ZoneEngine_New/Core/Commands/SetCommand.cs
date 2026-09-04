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
                || !TryParseStat(context.Args[0], out CharacterStat stat)
                || !int.TryParse(context.Args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                GmCommandFeedback.Send(context.Session, context.Player, "Usage: " + Usage);
                return;
            }

            Player player = context.Player;
            player.Stats.Set(stat, value, StatDetail.Base, dirty: true);
            player.FlushDirtyStats();

            GmCommandFeedback.Send(
                context.Session,
                context.Player,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Set {0} ({1}) = {2}",
                    stat,
                    (int)stat,
                    value));
        }

        static bool TryParseStat(string token, out CharacterStat stat)
        {
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int statId))
            {
                if (Enum.IsDefined(typeof(CharacterStat), statId))
                {
                    stat = (CharacterStat)statId;
                    return true;
                }

                stat = default;
                return false;
            }

            return Enum.TryParse(token, ignoreCase: true, out stat)
                && Enum.IsDefined(typeof(CharacterStat), stat);
        }
    }
}
