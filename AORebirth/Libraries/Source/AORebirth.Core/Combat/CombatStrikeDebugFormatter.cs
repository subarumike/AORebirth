namespace AORebirth.Core.Combat
{
    using System.Globalization;
    using System.Text;

    using AORebirth.Core.Entities;

    internal static class CombatStrikeDebugFormatter
    {
        public static string Format(
            ICharacter attacker,
            ICharacter target,
            CombatStrikeContext context,
            CombatStrikeResult result)
        {
            var output = new StringBuilder();
            output.Append("Combat strike attacker=");
            output.Append(FormatIdentity(attacker));
            output.Append(" target=");
            output.AppendLine(FormatIdentity(target));
            AppendContext(output, context);
            AppendResult(output, result);
            return output.ToString().TrimEnd();
        }

        private static void AppendContext(StringBuilder output, CombatStrikeContext context)
        {
            if (context == null)
            {
                output.AppendLine("  context=null");
                return;
            }

            output.AppendLine("  context:");
            output.AppendFormat(CultureInfo.InvariantCulture, "    damage={0}-{1} bonus={2}\r\n", context.MinDamage, context.MaxDamage, context.DamageBonus);
            output.AppendFormat(CultureInfo.InvariantCulture, "    range={0:F2} slot={1} source={2}\r\n", context.Range, context.WeaponSlot, context.DamageSource);
            output.AppendFormat(
                CultureInfo.InvariantCulture,
                "    weapon={0}:{1} ql={2} usesEquipped={3}\r\n",
                context.WeaponLowId,
                context.WeaponHighId,
                context.WeaponQualityLevel,
                context.UsesEquippedWeapon);
            output.AppendFormat(
                CultureInfo.InvariantCulture,
                "    damageType={0} attackRating={1} addAllOff={2} scale={3}\r\n",
                context.RawDamageType,
                FormatNullable(context.EffectiveAttackRating),
                FormatNullable(context.AddAllOff),
                context.OutgoingDamageScale);
            output.AppendFormat(
                CultureInfo.InvariantCulture,
                "    attackInfo slot={0} ammo={1} hitType={2} weaponInstance={3} send={4}\r\n",
                context.AttackInfoWeaponSlot,
                context.AttackInfoAmmoCount,
                context.AttackInfoHitType,
                context.AttackInfoWeaponInstance,
                context.SendAttackInfo);
            output.AppendFormat(
                CultureInfo.InvariantCulture,
                "    skills defs={0} values={1} special={2}\r\n",
                context.AttackSkillDefinitions ?? string.Empty,
                context.AttackSkillValues ?? string.Empty,
                context.SpecialAttackStat.HasValue ? context.SpecialAttackStat.Value.ToString() : "none");
        }

        private static void AppendResult(StringBuilder output, CombatStrikeResult result)
        {
            if (result == null)
            {
                output.AppendLine("  result=null");
                return;
            }

            output.AppendLine("  result:");
            output.AppendFormat(
                CultureInfo.InvariantCulture,
                "    outcome={0} hit={1} hitType={2} damage={3}\r\n",
                result.Outcome,
                result.IsHit,
                result.HitType,
                result.Damage);
            output.AppendFormat(
                CultureInfo.InvariantCulture,
                "    health {0}->{1} killingHit={2}\r\n",
                result.PreviousHealth,
                result.NewHealth,
                result.KillingHit);
        }

        private static string FormatIdentity(ICharacter character)
        {
            if (character == null)
            {
                return "null";
            }

            return character.Identity.ToString(true);
        }

        private static string FormatNullable(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "null";
        }
    }
}
