namespace ZoneEngine_New.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    using AORebirth.Core.GameData;

    /// <summary>
    /// Capture-backed Mail Terminal tunables from <c>GameData/MailRules.json</c>.
    /// Content-only — postage, retention, feedback text, and system sender names stay out of C#.
    /// </summary>
    public sealed class MailRulesCatalog
    {
        public const string FileName = "MailRules.json";

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        readonly HashSet<string> _systemSenders;

        MailRulesCatalog(MailRulesDocument document)
        {
            StandardPostageCredits = document.StandardPostageCredits;
            ExpressPostageCredits = document.ExpressPostageCredits;
            ComputerLiteracyLockSeconds = document.ComputerLiteracyLockSeconds;
            MailRetentionDays = document.MailRetentionDays;
            CodMailRetentionDays = document.CodMailRetentionDays;
            MailboxListLimit = document.MailboxListLimit;
            FlagsBase = document.FlagsBase;
            FailureNoDrop = document.FailureNoDrop ?? string.Empty;
            FailureNoChests = document.FailureNoChests ?? string.Empty;
            FailureReturnNotEligible = document.FailureReturnNotEligible ?? string.Empty;
            DefaultSystemSenderName = document.DefaultSystemSenderName ?? "Omni-Trade";
            CodPaymentSenderName = document.CodPaymentSenderName ?? "Mail Terminal";
            CodPaymentSubject = document.CodPaymentSubject ?? "COD payment";
            _systemSenders = new HashSet<string>(
                (document.SystemSenderNames ?? []).Where(n => !string.IsNullOrWhiteSpace(n)),
                StringComparer.OrdinalIgnoreCase);
        }

        public int StandardPostageCredits { get; }

        public int ExpressPostageCredits { get; }

        public int ComputerLiteracyLockSeconds { get; }

        public int MailRetentionDays { get; }

        public int CodMailRetentionDays { get; }

        public int MailboxListLimit { get; }

        public int FlagsBase { get; }

        public string FailureNoDrop { get; }

        public string FailureNoChests { get; }

        public string FailureReturnNotEligible { get; }

        public string DefaultSystemSenderName { get; }

        public string CodPaymentSenderName { get; }

        public string CodPaymentSubject { get; }

        public static MailRulesCatalog Empty { get; } = new(new MailRulesDocument());

        public static MailRulesCatalog Load(string gameDataRoot)
        {
            ArgumentException.ThrowIfNullOrEmpty(gameDataRoot);
            string path = Path.Combine(gameDataRoot, GameDataPaths.MailRulesFileName);
            if (!File.Exists(path))
                return Empty;

            MailRulesDocument? document =
                JsonSerializer.Deserialize<MailRulesDocument>(File.ReadAllText(path), JsonOptions);
            if (document == null)
                return Empty;

            Validate(document, path);
            return new MailRulesCatalog(document);
        }

        public int PostageForExpressFlag(byte expressFlag)
            => expressFlag != 0 ? ExpressPostageCredits : StandardPostageCredits;

        /// <summary>Live: COD expires in 2 days; normal player mail in 14 days.</summary>
        public int RetentionDaysForCredits(int credits)
            => credits < 0 ? CodMailRetentionDays : MailRetentionDays;

        public bool IsSystemSender(string? senderName)
        {
            if (string.IsNullOrWhiteSpace(senderName))
                return true;
            return _systemSenders.Contains(senderName.Trim());
        }

        static void Validate(MailRulesDocument document, string path)
        {
            if (document.StandardPostageCredits < 0
                || document.ExpressPostageCredits < 0
                || document.ComputerLiteracyLockSeconds < 0
                || document.MailRetentionDays <= 0
                || document.CodMailRetentionDays <= 0
                || document.MailboxListLimit <= 0
                || document.FlagsBase < 0)
            {
                throw new InvalidDataException("MailRules.json has invalid numeric fields: " + path);
            }

            if (string.IsNullOrWhiteSpace(document.FailureNoDrop)
                || string.IsNullOrWhiteSpace(document.FailureNoChests)
                || string.IsNullOrWhiteSpace(document.FailureReturnNotEligible))
            {
                throw new InvalidDataException(
                    "MailRules.json requires FailureNoDrop/FailureNoChests/FailureReturnNotEligible: " + path);
            }
        }

        sealed class MailRulesDocument
        {
            public int StandardPostageCredits { get; set; }

            public int ExpressPostageCredits { get; set; }

            public int ComputerLiteracyLockSeconds { get; set; }

            public int MailRetentionDays { get; set; } = 14;

            public int CodMailRetentionDays { get; set; } = 2;

            public int MailboxListLimit { get; set; } = 30;

            /// <summary>Player mail base flags from capture 20260926-061753 (0x28).</summary>
            public int FlagsBase { get; set; } = 0x28;

            public string? FailureNoDrop { get; set; }

            public string? FailureNoChests { get; set; }

            public string? FailureReturnNotEligible { get; set; }

            [JsonPropertyName("SystemSenderNames")]
            public List<string>? SystemSenderNames { get; set; }

            public string? DefaultSystemSenderName { get; set; }

            public string? CodPaymentSenderName { get; set; }

            public string? CodPaymentSubject { get; set; }
        }
    }
}
