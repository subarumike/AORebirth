	public class MailMessage : N3Message
	{
		public MailAction Action { get; set; }

		public string Recipient { get; set; }

		public string Subject { get; set; }

		public string Body { get; set; }

		public int ItemField1 { get; set; }

		public int ItemField2 { get; set; }

		public int Credits { get; set; }

		public byte ExpressFlag { get; set; }

		public short EchoAction { get; set; }

		public int MailId { get; set; }

		public ulong RequestedMailId { get; set; }

		public int Unknown1 { get; set; }

		public int Unknown2 { get; set; }

		public IList<MailListEntry> Entries { get; set; }

		public MailListEntry Detail { get; set; }

		public MailMessage()
		{
			base.N3MessageType = N3MessageType.Mail;
			Recipient = string.Empty;
			Subject = string.Empty;
			Body = string.Empty;
			Entries = new List<MailListEntry>();
		}
	}