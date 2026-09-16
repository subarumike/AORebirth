namespace ZoneEngine_New.Core.Commands
{
    using System;
    using System.Globalization;

    using SmokeLounge.AOtomation.Messaging.GameData;

    public static class CharacterStatParser
    {
        public static bool TryParse(string token, out CharacterStat stat)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                stat = default;
                return false;
            }

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
