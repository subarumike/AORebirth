	public class MailListEntry
	{
		public ulong MailId { get; set; }

		public int TimeField { get; set; }

		public string From { get; set; }

		public string Subject { get; set; }

		public int CreditsField { get; set; }

		public int CodField { get; set; }

		public int FlagsField { get; set; }

		public bool IsSummary { get; set; }

		public int ExtendedField64 { get; set; }

		public int AcgLow { get; set; }

		public int AcgHigh { get; set; }

		public int AcgLevel { get; set; }

		public int ExtendedField74 { get; set; }

		public string Body { get; set; }

		public MailListEntry()
		{
			From = string.Empty;
			Subject = string.Empty;
			Body = string.Empty;
			IsSummary = true;
		}
	}