	public class MailMessageSerializer : ISerializer
	{
		private const int X3F1Factor = 1009;

		private readonly Type type;

		public Type Type => type;

		public MailMessageSerializer()
		{
			type = typeof(MailMessage);
		}

		public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
		{
			MailMessage mailMessage = new MailMessage();
			mailMessage.N3MessageType = (N3MessageType)streamReader.ReadInt32();
			mailMessage.Identity = streamReader.ReadIdentity();
			mailMessage.Unknown = streamReader.ReadByte();
			mailMessage.Action = (MailAction)streamReader.ReadInt16();
			switch (mailMessage.Action)
			{
			case MailAction.MailboxList:
				DeserializeMailboxList(streamReader, mailMessage);
				break;
			case MailAction.OpenOrRequest:
				if (streamReader.Position + 8 <= streamReader.Length)
				{
					mailMessage.RequestedMailId = (ulong)streamReader.ReadInt64();
					mailMessage.Unknown1 = (int)(mailMessage.RequestedMailId >> 32);
					mailMessage.Unknown2 = (int)(mailMessage.RequestedMailId & 0xFFFFFFFFu);
				}
				break;
			case MailAction.MailDetail:
				mailMessage.Detail = ReadMailListEntry(streamReader);
				break;
			case MailAction.SendMail:
				mailMessage.Recipient = ReadLengthPrefixedString(streamReader);
				mailMessage.Subject = ReadLengthPrefixedString(streamReader);
				mailMessage.Body = ReadLengthPrefixedString(streamReader);
				mailMessage.ItemField1 = streamReader.ReadInt32();
				mailMessage.ItemField2 = streamReader.ReadInt32();
				mailMessage.Credits = streamReader.ReadInt32();
				if (streamReader.Position < streamReader.Length)
				{
					mailMessage.ExpressFlag = streamReader.ReadByte();
				}
				break;
			case MailAction.TakeAll:
			case MailAction.Delete:
			case MailAction.ReturnToSender:
				if (streamReader.Position + 8 <= streamReader.Length)
				{
					mailMessage.RequestedMailId = (ulong)streamReader.ReadInt64();
				}
				break;
			case MailAction.SendAccepted:
				mailMessage.EchoAction = streamReader.ReadInt16();
				mailMessage.Unknown1 = streamReader.ReadInt32();
				mailMessage.MailId = streamReader.ReadInt32();
				mailMessage.Unknown2 = streamReader.ReadInt32();
				break;
			}
			return mailMessage;
		}

		public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<MailMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>>)((MailMessageSerializer o) => o.Deserialize));
			NewExpression instance = Expression.New(GetType());
			MethodCallExpression expression = Expression.Call(instance, methodInfo, new Expression[3]
			{
				streamReaderExpression,
				serializationContextExpression,
				Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
			});
			return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(expression, assignmentTargetExpression.Type));
		}

		public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
		{
			MailMessage mailMessage = (MailMessage)value;
			streamWriter.WriteInt32((int)mailMessage.N3MessageType);
			streamWriter.WriteIdentity(mailMessage.Identity);
			streamWriter.WriteByte(mailMessage.Unknown);
			streamWriter.WriteInt16((short)mailMessage.Action);
			switch (mailMessage.Action)
			{
			case MailAction.MailboxList:
				SerializeMailboxList(streamWriter, mailMessage);
				break;
			case MailAction.OpenOrRequest:
				streamWriter.WriteInt64((long)mailMessage.RequestedMailId);
				break;
			case MailAction.MailDetail:
				WriteMailListEntry(streamWriter, mailMessage.Detail ?? new MailListEntry
				{
					IsSummary = false
				});
				break;
			case MailAction.SendMail:
				WriteLengthPrefixedString(streamWriter, mailMessage.Recipient ?? string.Empty);
				WriteLengthPrefixedString(streamWriter, mailMessage.Subject ?? string.Empty);
				WriteLengthPrefixedString(streamWriter, mailMessage.Body ?? string.Empty);
				streamWriter.WriteInt32(mailMessage.ItemField1);
				streamWriter.WriteInt32(mailMessage.ItemField2);
				streamWriter.WriteInt32(mailMessage.Credits);
				streamWriter.WriteByte(mailMessage.ExpressFlag);
				break;
			case MailAction.SendAccepted:
				streamWriter.WriteInt16((short)((mailMessage.EchoAction == 0) ? 6 : mailMessage.EchoAction));
				streamWriter.WriteInt32(mailMessage.Unknown1);
				streamWriter.WriteInt32(mailMessage.MailId);
				streamWriter.WriteInt32(mailMessage.Unknown2);
				break;
			case MailAction.TakeAll:
			case (MailAction)4:
			case MailAction.Delete:
			case MailAction.ReturnToSender:
				break;
			}
		}

		public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
		{
			MethodInfo methodInfo = ReflectionHelper.GetMethodInfo((Expression<Func<MailMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>>)((MailMessageSerializer o) => o.Serialize));
			NewExpression instance = Expression.New(GetType());
			return Expression.Call(instance, methodInfo, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
		}

		private void SerializeMailboxList(StreamWriter streamWriter, MailMessage message)
		{
			IList<MailListEntry> list = message.Entries ?? new List<MailListEntry>();
			int count = list.Count;
			streamWriter.WriteInt32((count + 1) * 1009);
			for (int i = 0; i < count; i++)
			{
				WriteMailListEntry(streamWriter, list[i]);
			}
		}

		private void DeserializeMailboxList(StreamReader streamReader, MailMessage message)
		{
			message.Entries = new List<MailListEntry>();
			if (streamReader.Position + 4 > streamReader.Length)
			{
				return;
			}
			int num = streamReader.ReadInt32();
			int num2 = num / 1009 - 1;
			if (num2 > 0)
			{
				for (int i = 0; i < num2; i++)
				{
					message.Entries.Add(ReadMailListEntry(streamReader));
				}
			}
		}

		private static void WriteMailListEntry(StreamWriter streamWriter, MailListEntry entry)
		{
			if (entry == null)
			{
				entry = new MailListEntry();
			}
			streamWriter.WriteInt64((long)entry.MailId);
			streamWriter.WriteInt32(entry.TimeField);
			WriteLengthPrefixedString(streamWriter, entry.From ?? string.Empty);
			WriteLengthPrefixedString(streamWriter, entry.Subject ?? string.Empty);
			streamWriter.WriteInt32(entry.CreditsField);
			streamWriter.WriteInt32(entry.CodField);
			streamWriter.WriteInt32(entry.FlagsField);
			byte value = (byte)(entry.IsSummary ? 1 : 0);
			streamWriter.WriteByte(value);
			if (!entry.IsSummary)
			{
				streamWriter.WriteInt32(entry.ExtendedField64);
				streamWriter.WriteInt32(entry.AcgLow);
				streamWriter.WriteInt32(entry.AcgHigh);
				streamWriter.WriteInt32(entry.AcgLevel);
				streamWriter.WriteInt32(0);
				streamWriter.WriteInt32(entry.ExtendedField74);
				WriteLengthPrefixedString(streamWriter, entry.Body ?? string.Empty);
			}
		}

		private static MailListEntry ReadMailListEntry(StreamReader streamReader)
		{
			MailListEntry mailListEntry = new MailListEntry();
			mailListEntry.MailId = (ulong)streamReader.ReadInt64();
			mailListEntry.TimeField = streamReader.ReadInt32();
			mailListEntry.From = ReadLengthPrefixedString(streamReader);
			mailListEntry.Subject = ReadLengthPrefixedString(streamReader);
			mailListEntry.CreditsField = streamReader.ReadInt32();
			mailListEntry.CodField = streamReader.ReadInt32();
			mailListEntry.FlagsField = streamReader.ReadInt32();
			byte b = streamReader.ReadByte();
			mailListEntry.IsSummary = b == 1;
			if (mailListEntry.IsSummary)
			{
				return mailListEntry;
			}
			mailListEntry.ExtendedField64 = streamReader.ReadInt32();
			mailListEntry.AcgLow = streamReader.ReadInt32();
			mailListEntry.AcgHigh = streamReader.ReadInt32();
			mailListEntry.AcgLevel = streamReader.ReadInt32();
			streamReader.ReadInt32();
			mailListEntry.ExtendedField74 = streamReader.ReadInt32();
			mailListEntry.Body = ReadLengthPrefixedString(streamReader);
			return mailListEntry;
		}

		private static string ReadLengthPrefixedString(StreamReader streamReader)
		{
			int num = streamReader.ReadInt16();
			if (num <= 0)
			{
				return string.Empty;
			}
			return streamReader.ReadString(num);
		}

		private static void WriteLengthPrefixedString(StreamWriter streamWriter, string value)
		{
			string s = value ?? string.Empty;
			byte[] bytes = Encoding.ASCII.GetBytes(s);
			streamWriter.WriteInt16((short)bytes.Length);
			streamWriter.WriteBytes(bytes);
		}
	}