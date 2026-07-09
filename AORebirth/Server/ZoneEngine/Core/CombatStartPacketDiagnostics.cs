namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    using AORebirth.Core.Entities;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using Utility;

    #endregion

    internal static class CombatStartPacketDiagnostics
    {
        private const string EnvironmentVariableName = "AOREBIRTH_COMBAT_PACKET_DIAGNOSTICS";

        private const string FlagFileName = "combat-packet-diagnostics.enabled";

        private static readonly TimeSpan CombatStartWindow = TimeSpan.FromSeconds(6);

        private static DateTime diagnosticWindowUntilUtc = DateTime.MinValue;

        internal static bool Enabled
        {
            get
            {
                string environmentValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);
                if (IsEnabledValue(environmentValue))
                {
                    return true;
                }

                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                if (string.IsNullOrEmpty(baseDirectory))
                {
                    return false;
                }

                return File.Exists(Path.Combine(baseDirectory, FlagFileName));
            }
        }

        internal static void LogAttackCommand(ICharacter character, Identity target, byte action, ICharacter resolvedTarget)
        {
            if (!Enabled)
            {
                return;
            }

            diagnosticWindowUntilUtc = DateTime.UtcNow.Add(CombatStartWindow);

            Log(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "direction=IN route=AttackMessageHandler.Read message=AttackCommand source={0} target={1} action={2} resolvedTarget={3} targetHealth={4} sourceState={5} sourceCurrentState={6} sourceActionCategory={7} sourceAggDef={8}",
                    IdentityText(character == null ? Identity.None : character.Identity),
                    IdentityText(target),
                    action,
                    resolvedTarget != null,
                    StatValue(resolvedTarget, StatIds.health),
                    StatValue(character, StatIds.state),
                    StatValue(character, StatIds.currentstate),
                    StatValue(character, StatIds.actioncategory),
                    StatValue(character, StatIds.aggdef)));
        }

        internal static void LogOutbound(string route, MessageBody body, Identity recipient)
        {
            if (!Enabled)
            {
                return;
            }

            N3Message n3Message = body as N3Message;
            if (n3Message == null)
            {
                return;
            }

            if (!ShouldLogN3Message(n3Message))
            {
                return;
            }

            Log(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "direction=OUT route={0} recipient={1} {2}",
                    route,
                    IdentityText(recipient),
                    DescribeN3Message(n3Message)));
        }

        internal static void LogSerializedOutbound(
            string route,
            MessageBody body,
            int sender,
            Identity receiver,
            byte[] buffer)
        {
            if (!Enabled)
            {
                return;
            }

            N3Message n3Message = body as N3Message;
            if (n3Message == null || !ShouldLogN3Message(n3Message))
            {
                return;
            }

            Log(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "direction=OUT_RAW route={0} sender={1} receiver={2} {3} rawLen={4} rawHex={5}",
                    route,
                    sender,
                    IdentityText(receiver),
                    DescribeN3Message(n3Message),
                    buffer == null ? 0 : buffer.Length,
                    HexExcerpt(buffer)));
        }

        internal static void LogStatBulk(
            string route,
            ICharacter character,
            Dictionary<int, uint> stats,
            bool announceToPlayfield)
        {
            if (!Enabled || stats == null || stats.Count == 0 || !IsInCombatStartWindow())
            {
                return;
            }

            Log(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "direction=OUT route={0} message=Stat source={1} announceToPlayfield={2} len=unavailable stats={3}",
                    route,
                    IdentityText(character == null ? Identity.None : character.Identity),
                    announceToPlayfield ? 1 : 0,
                    DescribeStats(stats)));
        }

        private static string DescribeN3Message(N3Message message)
        {
            AttackMessage attack = message as AttackMessage;
            if (attack != null)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "message=Attack n3Type={0} len=unavailable source={1} target={2} headerUnknown={3} action={4}",
                    attack.N3MessageType,
                    IdentityText(attack.Identity),
                    IdentityText(attack.Target),
                    attack.Unknown,
                    attack.Action);
            }

            AttackInfoMessage attackInfo = message as AttackInfoMessage;
            if (attackInfo != null)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "message=AttackInfo n3Type={0} len=unavailable source={1} target={2} headerUnknown={3} damage={4} ammoCount={5} weaponSlot={6} unk4={7} hitType={8} weaponInstance={9}",
                    attackInfo.N3MessageType,
                    IdentityText(attackInfo.Identity),
                    IdentityText(attackInfo.Target),
                    attackInfo.Unknown,
                    attackInfo.Unknown1,
                    attackInfo.Unknown2,
                    attackInfo.Unknown3,
                    attackInfo.Unknown4,
                    attackInfo.Unknown5,
                    attackInfo.Unknown6);
            }

            SpecialAttackWeaponMessage specialAttackWeapon = message as SpecialAttackWeaponMessage;
            if (specialAttackWeapon != null)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "message=SpecialAttackWeapon n3Type={0} len=unavailable source={1} target={2} headerUnknown={3} specials={4} u1={5} u2={6} u3={7} u4={8} u5={9} specialList={10}",
                    specialAttackWeapon.N3MessageType,
                    IdentityText(specialAttackWeapon.Identity),
                    IdentityText(Identity.None),
                    specialAttackWeapon.Unknown,
                    specialAttackWeapon.Specials == null ? 0 : specialAttackWeapon.Specials.Length,
                    specialAttackWeapon.Unknown1,
                    specialAttackWeapon.Unknown2,
                    specialAttackWeapon.Unknown3,
                    specialAttackWeapon.Unknown4,
                    specialAttackWeapon.Unknown5,
                    DescribeSpecials(specialAttackWeapon.Specials));
            }

            StatMessage stat = message as StatMessage;
            if (stat != null)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "message=Stat n3Type={0} len=unavailable source={1} target={2} headerUnknown={3} stats={4}",
                    stat.N3MessageType,
                    IdentityText(stat.Identity),
                    IdentityText(Identity.None),
                    stat.Unknown,
                    DescribeStats(stat.Stats));
            }

            CharacterActionMessage characterAction = message as CharacterActionMessage;
            if (characterAction != null)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "message=CharacterAction n3Type={0} len=unavailable source={1} target={2} headerUnknown={3} action={4} p1={5} p2={6} u1={7} u2={8}",
                    characterAction.N3MessageType,
                    IdentityText(characterAction.Identity),
                    IdentityText(characterAction.Target),
                    characterAction.Unknown,
                    characterAction.Action,
                    characterAction.Parameter1,
                    characterAction.Parameter2,
                    characterAction.Unknown1,
                    characterAction.Unknown2);
            }

            FormatFeedbackMessage formatFeedback = message as FormatFeedbackMessage;
            if (formatFeedback != null)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "message=FormatFeedback n3Type={0} len=unavailable source={1} target={2} headerUnknown={3} u1={4} u2={5} text={6}",
                    formatFeedback.N3MessageType,
                    IdentityText(formatFeedback.Identity),
                    IdentityText(Identity.None),
                    formatFeedback.Unknown,
                    formatFeedback.Unknown1,
                    formatFeedback.Unknown2,
                    formatFeedback.FormattedMessage);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "message={0} n3Type={1} len=unavailable source={2} target={3} headerUnknown={4}",
                message.GetType().Name,
                message.N3MessageType,
                IdentityText(message.Identity),
                IdentityText(Identity.None),
                message.Unknown);
        }

        private static bool ShouldLogN3Message(N3Message message)
        {
            return IsInCombatStartWindow() || IsCombatPacket(message);
        }

        private static bool IsInCombatStartWindow()
        {
            return DateTime.UtcNow <= diagnosticWindowUntilUtc;
        }

        private static bool IsCombatPacket(N3Message message)
        {
            return message is AttackMessage
                   || message is AttackInfoMessage
                   || message is SpecialAttackWeaponMessage
                   || message is StopFightMessage
                   || message is CharacterActionMessage
                   || message is FormatFeedbackMessage
                   || message is FightModeUpdateMessage;
        }

        private static string DescribeStats(GameTuple<CharacterStat, uint>[] stats)
        {
            if (stats == null || stats.Length == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder();
            builder.Append("[");
            for (int i = 0; i < stats.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(";");
                }

                builder.Append(stats[i].Value1);
                builder.Append("=");
                builder.Append(stats[i].Value2);
            }

            builder.Append("]");
            return builder.ToString();
        }

        private static string DescribeStats(Dictionary<int, uint> stats)
        {
            if (stats == null || stats.Count == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder();
            builder.Append("[");
            bool first = true;
            foreach (KeyValuePair<int, uint> stat in stats)
            {
                if (!first)
                {
                    builder.Append(";");
                }

                first = false;
                builder.Append((CharacterStat)stat.Key);
                builder.Append("=");
                builder.Append(stat.Value);
            }

            builder.Append("]");
            return builder.ToString();
        }

        private static string DescribeSpecials(SpecialAttack[] specials)
        {
            if (specials == null || specials.Length == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder();
            builder.Append("[");
            for (int i = 0; i < specials.Length; i++)
            {
                SpecialAttack special = specials[i];
                if (i > 0)
                {
                    builder.Append(";");
                }

                builder.Append(special.Unknown4);
                builder.Append(":");
                builder.Append(special.Unknown1);
                builder.Append("/");
                builder.Append(special.Unknown2);
                builder.Append("/");
                builder.Append(special.Unknown3);
            }

            builder.Append("]");
            return builder.ToString();
        }

        private static int StatValue(ICharacter character, StatIds statId)
        {
            if (character == null)
            {
                return 0;
            }

            return character.Stats[statId].Value;
        }

        private static string IdentityText(Identity identity)
        {
            return identity.ToString();
        }

        private static string HexExcerpt(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return string.Empty;
            }

            int byteCount = Math.Min(buffer.Length, 160);
            var builder = new StringBuilder(byteCount * 2 + 3);
            for (int i = 0; i < byteCount; i++)
            {
                builder.Append(buffer[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            if (buffer.Length > byteCount)
            {
                builder.Append("...");
            }

            return builder.ToString();
        }

        private static bool IsEnabledValue(string value)
        {
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static void Log(string details)
        {
            LogUtil.Debug(
                DebugInfoDetail.Engine,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "COMBAT_START_DIAG utc={0:o} {1}",
                    DateTime.UtcNow,
                    details));
        }
    }
}
