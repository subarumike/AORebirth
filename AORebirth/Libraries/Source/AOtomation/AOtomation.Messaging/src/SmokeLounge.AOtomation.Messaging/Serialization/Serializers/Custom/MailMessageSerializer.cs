#region License

// Copyright (c) 2005-2014, CellAO Team
// All rights reserved.

#endregion

namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    /// <summary>
    /// Capture + Gamecode.dll MailIIR-backed Mail serializer.
    /// List: X3F1 + summary rows. Detail (action 2): one full MailMessage entry.
    /// ACGItem wire: GameData &lt;&lt; low, high, level, 0.
    /// </summary>
    public class MailMessageSerializer : ISerializer
    {
        private const int X3F1Factor = 0x03F1;

        private readonly Type type;

        public MailMessageSerializer()
        {
            this.type = typeof(MailMessage);
        }

        public Type Type
        {
            get
            {
                return this.type;
            }
        }

        public object Deserialize(
            StreamReader streamReader,
            SerializationContext serializationContext,
            PropertyMetaData propertyMetaData = null)
        {
            var message = new MailMessage();
            message.N3MessageType = (N3MessageType)streamReader.ReadInt32();
            message.Identity = streamReader.ReadIdentity();
            message.Unknown = streamReader.ReadByte();
            message.Action = (MailAction)streamReader.ReadInt16();

            switch (message.Action)
            {
                case MailAction.MailboxList:
                    this.DeserializeMailboxList(streamReader, message);
                    break;

                case MailAction.OpenOrRequest:
                    if (streamReader.Position + 8 <= streamReader.Length)
                    {
                        message.RequestedMailId = unchecked((ulong)streamReader.ReadInt64());
                        message.Unknown1 = (int)(message.RequestedMailId >> 32);
                        message.Unknown2 = (int)(message.RequestedMailId & 0xFFFFFFFFUL);
                    }
                    break;

                case MailAction.MailDetail:
                    message.Detail = ReadMailListEntry(streamReader);
                    break;

                case MailAction.SendMail:
                    message.Recipient = ReadLengthPrefixedString(streamReader);
                    message.Subject = ReadLengthPrefixedString(streamReader);
                    message.Body = ReadLengthPrefixedString(streamReader);
                    message.ItemField1 = streamReader.ReadInt32();
                    message.ItemField2 = streamReader.ReadInt32();
                    message.Credits = streamReader.ReadInt32();
                    if (streamReader.Position < streamReader.Length)
                    {
                        message.ExpressFlag = streamReader.ReadByte();
                    }
                    break;

                case MailAction.TakeAll:
                case MailAction.Delete:
                case MailAction.ReturnToSender:
                    if (streamReader.Position + 8 <= streamReader.Length)
                    {
                        message.RequestedMailId = unchecked((ulong)streamReader.ReadInt64());
                    }
                    break;

                case MailAction.SendAccepted:
                    message.EchoAction = streamReader.ReadInt16();
                    message.Unknown1 = streamReader.ReadInt32();
                    message.MailId = streamReader.ReadInt32();
                    message.Unknown2 = streamReader.ReadInt32();
                    break;
            }

            return message;
        }

        public Expression DeserializerExpression(
            ParameterExpression streamReaderExpression,
            ParameterExpression serializationContextExpression,
            Expression assignmentTargetExpression,
            PropertyMetaData propertyMetaData)
        {
            MethodInfo deserializerMethodInfo =
                ReflectionHelper
                    .GetMethodInfo
                    <MailMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(
                        o => o.Deserialize);
            NewExpression serializerExp = Expression.New(this.GetType());
            MethodCallExpression callExp = Expression.Call(
                serializerExp,
                deserializerMethodInfo,
                new Expression[]
                {
                    streamReaderExpression, serializationContextExpression,
                    Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
                });

            BinaryExpression assignmentExp = Expression.Assign(
                assignmentTargetExpression,
                Expression.TypeAs(callExp, assignmentTargetExpression.Type));
            return assignmentExp;
        }

        public void Serialize(
            StreamWriter streamWriter,
            SerializationContext serializationContext,
            object value,
            PropertyMetaData propertyMetaData = null)
        {
            var message = (MailMessage)value;
            streamWriter.WriteInt32((int)message.N3MessageType);
            streamWriter.WriteIdentity(message.Identity);
            streamWriter.WriteByte(message.Unknown);
            streamWriter.WriteInt16((short)message.Action);

            switch (message.Action)
            {
                case MailAction.MailboxList:
                    this.SerializeMailboxList(streamWriter, message);
                    break;

                case MailAction.OpenOrRequest:
                    streamWriter.WriteInt64(unchecked((long)message.RequestedMailId));
                    break;

                case MailAction.MailDetail:
                    WriteMailListEntry(streamWriter, message.Detail ?? new MailListEntry { IsSummary = false });
                    break;

                case MailAction.SendMail:
                    WriteLengthPrefixedString(streamWriter, message.Recipient ?? string.Empty);
                    WriteLengthPrefixedString(streamWriter, message.Subject ?? string.Empty);
                    WriteLengthPrefixedString(streamWriter, message.Body ?? string.Empty);
                    streamWriter.WriteInt32(message.ItemField1);
                    streamWriter.WriteInt32(message.ItemField2);
                    streamWriter.WriteInt32(message.Credits);
                    streamWriter.WriteByte(message.ExpressFlag);
                    break;

                case MailAction.SendAccepted:
                    streamWriter.WriteInt16(message.EchoAction == 0 ? (short)MailAction.SendMail : message.EchoAction);
                    streamWriter.WriteInt32(message.Unknown1);
                    streamWriter.WriteInt32(message.MailId);
                    streamWriter.WriteInt32(message.Unknown2);
                    break;
            }
        }

        public Expression SerializerExpression(
            ParameterExpression streamWriterExpression,
            ParameterExpression serializationContextExpression,
            Expression valueExpression,
            PropertyMetaData propertyMetaData)
        {
            MethodInfo serializerMethodInfo =
                ReflectionHelper
                    .GetMethodInfo
                    <MailMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(
                        o => o.Serialize);
            NewExpression serializerExp = Expression.New(this.GetType());
            MethodCallExpression callExp = Expression.Call(
                serializerExp,
                serializerMethodInfo,
                new[]
                {
                    streamWriterExpression, serializationContextExpression, valueExpression,
                    Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
                });
            return callExp;
        }

        private void SerializeMailboxList(StreamWriter streamWriter, MailMessage message)
        {
            IList<MailListEntry> entries = message.Entries ?? new List<MailListEntry>();
            int count = entries.Count;
            streamWriter.WriteInt32((count + 1) * X3F1Factor);

            for (int i = 0; i < count; i++)
            {
                WriteMailListEntry(streamWriter, entries[i]);
            }
        }

        private void DeserializeMailboxList(StreamReader streamReader, MailMessage message)
        {
            message.Entries = new List<MailListEntry>();
            if (streamReader.Position + 4 > streamReader.Length)
            {
                return;
            }

            int encoded = streamReader.ReadInt32();
            int count = (encoded / X3F1Factor) - 1;
            if (count <= 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                message.Entries.Add(ReadMailListEntry(streamReader));
            }
        }

        private static void WriteMailListEntry(StreamWriter streamWriter, MailListEntry entry)
        {
            if (entry == null)
            {
                entry = new MailListEntry();
            }

            streamWriter.WriteInt64(unchecked((long)entry.MailId));
            streamWriter.WriteInt32(entry.TimeField);
            WriteLengthPrefixedString(streamWriter, entry.From ?? string.Empty);
            WriteLengthPrefixedString(streamWriter, entry.Subject ?? string.Empty);
            streamWriter.WriteInt32(entry.CreditsField);
            streamWriter.WriteInt32(entry.CodField);
            streamWriter.WriteInt32(entry.FlagsField);

            byte summaryByte = entry.IsSummary ? (byte)1 : (byte)0;
            streamWriter.WriteByte(summaryByte);

            if (entry.IsSummary)
            {
                return;
            }

            streamWriter.WriteInt32(entry.ExtendedField64);
            // GameData ACGItem << : low, high, level, 0
            streamWriter.WriteInt32(entry.AcgLow);
            streamWriter.WriteInt32(entry.AcgHigh);
            streamWriter.WriteInt32(entry.AcgLevel);
            streamWriter.WriteInt32(0);
            streamWriter.WriteInt32(entry.ExtendedField74);
            WriteLengthPrefixedString(streamWriter, entry.Body ?? string.Empty);
        }

        private static MailListEntry ReadMailListEntry(StreamReader streamReader)
        {
            var entry = new MailListEntry();
            entry.MailId = unchecked((ulong)streamReader.ReadInt64());
            entry.TimeField = streamReader.ReadInt32();
            entry.From = ReadLengthPrefixedString(streamReader);
            entry.Subject = ReadLengthPrefixedString(streamReader);
            entry.CreditsField = streamReader.ReadInt32();
            entry.CodField = streamReader.ReadInt32();
            entry.FlagsField = streamReader.ReadInt32();
            byte summaryByte = streamReader.ReadByte();
            entry.IsSummary = summaryByte == 1;
            if (entry.IsSummary)
            {
                return entry;
            }

            entry.ExtendedField64 = streamReader.ReadInt32();
            entry.AcgLow = streamReader.ReadInt32();
            entry.AcgHigh = streamReader.ReadInt32();
            entry.AcgLevel = streamReader.ReadInt32();
            streamReader.ReadInt32(); // ACGItem trailing 0
            entry.ExtendedField74 = streamReader.ReadInt32();
            entry.Body = ReadLengthPrefixedString(streamReader);
            return entry;
        }

        private static string ReadLengthPrefixedString(StreamReader streamReader)
        {
            int length = streamReader.ReadInt16();
            if (length <= 0)
            {
                return string.Empty;
            }

            return streamReader.ReadString(length);
        }

        private static void WriteLengthPrefixedString(StreamWriter streamWriter, string value)
        {
            string safe = value ?? string.Empty;
            byte[] bytes = Encoding.ASCII.GetBytes(safe);
            streamWriter.WriteInt16((short)bytes.Length);
            streamWriter.WriteBytes(bytes);
        }
    }
}
