namespace ZoneEngine.Core.Playfields.Hydration
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    internal static class PlayfieldDefinitionCanonicalizer
    {
        internal static string Serialize(HydratedPlayfieldDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            var builder = new StringBuilder();
            builder.Append("{\"formatVersion\":").Append(definition.FormatVersion.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"playfieldInstance\":").Append(definition.PlayfieldInstance.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"resourceIdentity\":").Append(definition.ResourceIdentity.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"name\":");
            AppendString(builder, definition.Name);
            builder.Append(",\"records\":[");
            AppendRecords(builder, definition.Records);
            builder.Append("],\"provenance\":[");
            AppendProvenance(builder, definition.Provenance);
            builder.Append("],\"warnings\":[");
            AppendStrings(builder, definition.Warnings.OrderBy(value => value, StringComparer.Ordinal));
            builder.Append("],\"conflicts\":[");
            AppendStrings(builder, definition.Conflicts.OrderBy(value => value, StringComparer.Ordinal));
            builder.Append("]}");
            return builder.ToString();
        }

        internal static string ComputeDigest(HydratedPlayfieldDefinition definition)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Serialize(definition));
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest)
                {
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static void AppendRecords(StringBuilder builder, IEnumerable<HydratedPlayfieldRecord> records)
        {
            bool first = true;
            foreach (HydratedPlayfieldRecord record in records.OrderBy(value => value.Category, StringComparer.Ordinal)
                .ThenBy(value => value.Identity, StringComparer.Ordinal))
            {
                AppendSeparator(builder, ref first);
                builder.Append("{\"category\":");
                AppendString(builder, record.Category);
                builder.Append(",\"identity\":");
                AppendString(builder, record.Identity);
                builder.Append(",\"values\":[");
                AppendValues(builder, record.Values);
                builder.Append("],\"provenance\":[");
                AppendProvenance(builder, record.Provenance);
                builder.Append("]}");
            }
        }

        private static void AppendValues(StringBuilder builder, IEnumerable<HydratedPlayfieldValue> values)
        {
            bool first = true;
            foreach (HydratedPlayfieldValue value in values.OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                AppendSeparator(builder, ref first);
                builder.Append("{\"name\":");
                AppendString(builder, value.Name);
                builder.Append(",\"collection\":").Append(value.IsCollection ? "true" : "false");
                builder.Append(",\"values\":[");
                IEnumerable<string> ordered = value.Values;
                if (value.IsCollection)
                {
                    ordered = value.Values.OrderBy(item => item, StringComparer.Ordinal);
                }

                AppendStrings(builder, ordered);
                builder.Append("]}");
            }
        }

        private static void AppendProvenance(
            StringBuilder builder,
            IEnumerable<PlayfieldSourceProvenance> provenance)
        {
            bool first = true;
            foreach (PlayfieldSourceProvenance source in provenance
                .OrderBy(value => value.ContributionOrder)
                .ThenBy(value => value.SourceKind)
                .ThenBy(value => value.SourceIdentity, StringComparer.Ordinal)
                .ThenBy(value => value.Adapter, StringComparer.Ordinal))
            {
                AppendSeparator(builder, ref first);
                builder.Append("{\"kind\":");
                AppendString(builder, source.SourceKind.ToString());
                builder.Append(",\"identity\":");
                AppendString(builder, source.SourceIdentity);
                builder.Append(",\"digest\":");
                AppendString(builder, source.SourceDigest);
                builder.Append(",\"adapter\":");
                AppendString(builder, source.Adapter);
                builder.Append(",\"order\":").Append(source.ContributionOrder.ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"resolution\":");
                AppendString(builder, source.Resolution.ToString());
                builder.Append("}");
            }
        }

        private static void AppendStrings(StringBuilder builder, IEnumerable<string> values)
        {
            bool first = true;
            foreach (string value in values)
            {
                AppendSeparator(builder, ref first);
                AppendString(builder, value ?? string.Empty);
            }
        }

        private static void AppendSeparator(StringBuilder builder, ref bool first)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < 32)
                        {
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
