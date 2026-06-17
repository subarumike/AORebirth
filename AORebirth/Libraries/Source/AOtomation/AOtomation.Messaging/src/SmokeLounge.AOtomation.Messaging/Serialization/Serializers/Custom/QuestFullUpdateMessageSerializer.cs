namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    public class QuestFullUpdateMessageSerializer : ISerializer
    {
        public Type Type
        {
            get { return typeof(QuestFullUpdateMessage); }
        }

        public object Deserialize(
            StreamReader streamReader,
            SerializationContext serializationContext,
            PropertyMetaData propertyMetaData = null)
        {
            var message = new QuestFullUpdateMessage
                          {
                              N3MessageType = (N3MessageType)streamReader.ReadInt32(),
                              Identity = streamReader.ReadIdentity(),
                              Unknown = streamReader.ReadByte()
                          };

            int questCount = ReadX3F1Count(streamReader);
            message.Quests = new Quest[questCount];
            for (int i = 0; i < questCount; i++)
            {
                message.Quests[i] = ReadQuest(streamReader);
            }

            return message;
        }

        public Expression DeserializerExpression(
            ParameterExpression streamReaderExpression,
            ParameterExpression serializationContextExpression,
            Expression assignmentTargetExpression,
            PropertyMetaData propertyMetaData)
        {
            MethodInfo method =
                ReflectionHelper.GetMethodInfo<QuestFullUpdateMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(
                    o => o.Deserialize);
            MethodCallExpression call = Expression.Call(
                Expression.New(this.GetType()),
                method,
                streamReaderExpression,
                serializationContextExpression,
                Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
            return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(call, assignmentTargetExpression.Type));
        }

        public void Serialize(
            StreamWriter streamWriter,
            SerializationContext serializationContext,
            object value,
            PropertyMetaData propertyMetaData = null)
        {
            var message = (QuestFullUpdateMessage)value;
            streamWriter.WriteInt32((int)message.N3MessageType);
            streamWriter.WriteIdentity(message.Identity);
            streamWriter.WriteByte(message.Unknown);

            Quest[] quests = message.Quests ?? new Quest[0];
            WriteX3F1Count(streamWriter, quests.Length);
            for (int i = 0; i < quests.Length; i++)
            {
                WriteQuest(streamWriter, quests[i]);
            }
        }

        public Expression SerializerExpression(
            ParameterExpression streamWriterExpression,
            ParameterExpression serializationContextExpression,
            Expression valueExpression,
            PropertyMetaData propertyMetaData)
        {
            MethodInfo method =
                ReflectionHelper.GetMethodInfo<QuestFullUpdateMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(
                    o => o.Serialize);
            return Expression.Call(
                Expression.New(this.GetType()),
                method,
                streamWriterExpression,
                serializationContextExpression,
                valueExpression,
                Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
        }

        private static Quest ReadQuest(StreamReader reader)
        {
            return new Quest
                   {
                       QuestId = reader.ReadIdentity(),
                       Unknown1 = reader.ReadInt32(),
                       Unknown2 = reader.ReadInt32(),
                       Unknown3 = reader.ReadInt32(),
                       Unknown4 = reader.ReadInt32(),
                       ShortInfo = ReadNullTerminatedString(reader),
                       LongInfo = ReadLengthPrefixedString(reader),
                       UnknownId1 = reader.ReadIdentity(),
                       Unknown5 = reader.ReadInt32(),
                       Unknown6 = reader.ReadInt32(),
                       Unknown7 = reader.ReadInt32(),
                       Unknown8 = reader.ReadInt32(),
                       Unknown9 = reader.ReadInt32(),
                       Unknown10 = reader.ReadInt32(),
                       MissionItemData = ReadX3F1Array(reader, ReadMissionItemReward),
                       Unknown11 = reader.ReadInt32(),
                       Unknown12 = reader.ReadInt32(),
                       Unknown13 = reader.ReadInt32(),
                       UnknownHash1 = reader.ReadString(4),
                       Unknown14 = reader.ReadInt32(),
                       Unknown15 = reader.ReadInt32(),
                       Unknown16 = reader.ReadInt32(),
                       Unknown17 = reader.ReadInt32(),
                       Unknown18 = reader.ReadInt32(),
                       UnknownId2 = reader.ReadIdentity(),
                       MissionIconId = reader.ReadInt32(),
                       Unknown20 = reader.ReadInt32(),
                       Unknown21 = reader.ReadInt32(),
                       QuestActions = ReadX3F1Array(reader, ReadQuestAction),
                       PlayerIds = ReadX3F1Array(reader, r => r.ReadIdentity()),
                       UnknownArray1 = ReadInt32Array(reader),
                       UnknownArray2 = ReadInt32Array(reader),
                       CharacterInfos = ReadInt32Array(reader, ReadCharacterInfo),
                       Unknown22 = reader.ReadInt32(),
                       PlayerIds2 = ReadX3F1Array(reader, r => r.ReadIdentity()),
                       Unknown23 = reader.ReadInt32(),
                       Unknown24 = reader.ReadInt32(),
                       UnknownId3 = reader.ReadIdentity(),
                       Unknown25 = reader.ReadInt32(),
                       Unknown26 = reader.ReadInt32(),
                       QuestIdentities = ReadInt32Array(reader, ReadQuestIdentity),
                       Unknown27 = reader.ReadInt32(),
                       FactionInfos = ReadX3F1Array(reader, r => r.ReadIdentity()),
                       Unknown28 = reader.ReadByte()
                   };
        }

        private static void WriteQuest(StreamWriter writer, Quest quest)
        {
            if (quest == null)
            {
                throw new InvalidOperationException("QuestFullUpdate cannot serialize a null quest entry.");
            }

            writer.WriteIdentity(quest.QuestId);
            writer.WriteInt32(quest.Unknown1);
            writer.WriteInt32(quest.Unknown2);
            writer.WriteInt32(quest.Unknown3);
            writer.WriteInt32(quest.Unknown4);
            WriteNullTerminatedString(writer, quest.ShortInfo);
            WriteLengthPrefixedString(writer, quest.LongInfo);
            writer.WriteIdentity(quest.UnknownId1);
            writer.WriteInt32(quest.Unknown5);
            writer.WriteInt32(quest.Unknown6);
            writer.WriteInt32(quest.Unknown7);
            writer.WriteInt32(quest.Unknown8);
            writer.WriteInt32(quest.Unknown9);
            writer.WriteInt32(quest.Unknown10);
            WriteX3F1Array(writer, quest.MissionItemData, WriteMissionItemReward);
            writer.WriteInt32(quest.Unknown11);
            writer.WriteInt32(quest.Unknown12);
            writer.WriteInt32(quest.Unknown13);
            WriteFixedString(writer, quest.UnknownHash1, 4);
            writer.WriteInt32(quest.Unknown14);
            writer.WriteInt32(quest.Unknown15);
            writer.WriteInt32(quest.Unknown16);
            writer.WriteInt32(quest.Unknown17);
            writer.WriteInt32(quest.Unknown18);
            writer.WriteIdentity(quest.UnknownId2);
            writer.WriteInt32(quest.MissionIconId);
            writer.WriteInt32(quest.Unknown20);
            writer.WriteInt32(quest.Unknown21);
            WriteX3F1Array(writer, quest.QuestActions, WriteQuestAction);
            WriteX3F1Array(writer, quest.PlayerIds, (w, identity) => w.WriteIdentity(identity));
            WriteInt32Array(writer, quest.UnknownArray1, (w, item) => w.WriteInt32(item));
            WriteInt32Array(writer, quest.UnknownArray2, (w, item) => w.WriteInt32(item));
            WriteInt32Array(writer, quest.CharacterInfos, WriteCharacterInfo);
            writer.WriteInt32(quest.Unknown22);
            WriteX3F1Array(writer, quest.PlayerIds2, (w, identity) => w.WriteIdentity(identity));
            writer.WriteInt32(quest.Unknown23);
            writer.WriteInt32(quest.Unknown24);
            writer.WriteIdentity(quest.UnknownId3);
            writer.WriteInt32(quest.Unknown25);
            writer.WriteInt32(quest.Unknown26);
            WriteInt32Array(writer, quest.QuestIdentities, WriteQuestIdentity);
            writer.WriteInt32(quest.Unknown27);
            WriteX3F1Array(writer, quest.FactionInfos, (w, identity) => w.WriteIdentity(identity));
            writer.WriteByte(quest.Unknown28);
        }

        private static MissionItemReward ReadMissionItemReward(StreamReader reader)
        {
            return new MissionItemReward
                   {
                       LowId = reader.ReadInt32(),
                       HighId = reader.ReadInt32(),
                       Ql = reader.ReadInt32(),
                       Unknown = reader.ReadInt32()
                   };
        }

        private static void WriteMissionItemReward(StreamWriter writer, MissionItemReward reward)
        {
            writer.WriteInt32(reward.LowId);
            writer.WriteInt32(reward.HighId);
            writer.WriteInt32(reward.Ql);
            writer.WriteInt32(reward.Unknown);
        }

        private static QuestActionInfo ReadQuestAction(StreamReader reader)
        {
            return new QuestActionInfo
                   {
                       Version = reader.ReadInt32(),
                       Action = reader.ReadIdentity(),
                       UnknownId1 = reader.ReadIdentity(),
                       UnknownId2 = reader.ReadIdentity(),
                       UnknownId3 = reader.ReadIdentity(),
                       UnknownId4 = reader.ReadIdentity(),
                       Unknown1 = reader.ReadSingle(),
                       Unknown2 = reader.ReadSingle(),
                       Unknown3 = reader.ReadSingle(),
                       Unknown4 = reader.ReadSingle(),
                       UnknownId5 = reader.ReadIdentity(),
                       Unknown5 = reader.ReadSingle(),
                       Unknown6 = reader.ReadSingle(),
                       Unknown7 = reader.ReadSingle(),
                       Unknown8 = reader.ReadSingle(),
                       UnknownId6 = reader.ReadIdentity(),
                       UnknownHash1 = reader.ReadString(4),
                       Unknown9 = reader.ReadInt32(),
                       UnknownId7 = reader.ReadIdentity(),
                       PlayfieldId = reader.ReadIdentity(),
                       Unknown10 = reader.ReadInt32(),
                       Unknown11 = reader.ReadInt32(),
                       Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())
                   };
        }

        private static void WriteQuestAction(StreamWriter writer, QuestActionInfo action)
        {
            writer.WriteInt32(action.Version);
            writer.WriteIdentity(action.Action);
            writer.WriteIdentity(action.UnknownId1);
            writer.WriteIdentity(action.UnknownId2);
            writer.WriteIdentity(action.UnknownId3);
            writer.WriteIdentity(action.UnknownId4);
            writer.WriteSingle(action.Unknown1);
            writer.WriteSingle(action.Unknown2);
            writer.WriteSingle(action.Unknown3);
            writer.WriteSingle(action.Unknown4);
            writer.WriteIdentity(action.UnknownId5);
            writer.WriteSingle(action.Unknown5);
            writer.WriteSingle(action.Unknown6);
            writer.WriteSingle(action.Unknown7);
            writer.WriteSingle(action.Unknown8);
            writer.WriteIdentity(action.UnknownId6);
            WriteFixedString(writer, action.UnknownHash1, 4);
            writer.WriteInt32(action.Unknown9);
            writer.WriteIdentity(action.UnknownId7);
            writer.WriteIdentity(action.PlayfieldId);
            writer.WriteInt32(action.Unknown10);
            writer.WriteInt32(action.Unknown11);
            Vector3 position = action.Position ?? new Vector3();
            writer.WriteSingle(position.X);
            writer.WriteSingle(position.Y);
            writer.WriteSingle(position.Z);
        }

        private static CharacterInfo ReadCharacterInfo(StreamReader reader)
        {
            return new CharacterInfo { MissionIdentity = reader.ReadIdentity(), Name = ReadNullTerminatedString(reader) };
        }

        private static void WriteCharacterInfo(StreamWriter writer, CharacterInfo info)
        {
            writer.WriteIdentity(info.MissionIdentity);
            WriteNullTerminatedString(writer, info.Name);
        }

        private static QuestIdentity ReadQuestIdentity(StreamReader reader)
        {
            return new QuestIdentity { Unknown1 = reader.ReadIdentity(), Unknown2 = reader.ReadInt32() };
        }

        private static void WriteQuestIdentity(StreamWriter writer, QuestIdentity identity)
        {
            writer.WriteIdentity(identity.Unknown1);
            writer.WriteInt32(identity.Unknown2);
        }

        private static string ReadLengthPrefixedString(StreamReader reader)
        {
            return reader.ReadString(reader.ReadInt32());
        }

        private static void WriteLengthPrefixedString(StreamWriter writer, string value)
        {
            string safe = value ?? string.Empty;
            writer.WriteInt32(Encoding.ASCII.GetByteCount(safe) + 1);
            writer.WriteString(safe);
            writer.WriteByte(0);
        }

        private static string ReadNullTerminatedString(StreamReader reader)
        {
            var bytes = new System.Collections.Generic.List<byte>();
            byte next;
            while ((next = reader.ReadByte()) != 0)
            {
                bytes.Add(next);
            }

            return Encoding.ASCII.GetString(bytes.ToArray());
        }

        private static void WriteNullTerminatedString(StreamWriter writer, string value)
        {
            writer.WriteString(value ?? string.Empty);
            writer.WriteByte(0);
        }

        private static void WriteFixedString(StreamWriter writer, string value, int length)
        {
            writer.WriteString(value ?? string.Empty, length);
        }

        private static int ReadX3F1Count(StreamReader reader)
        {
            return Math.Max((reader.ReadInt32() / 0x03F1) - 1, 0);
        }

        private static void WriteX3F1Count(StreamWriter writer, int count)
        {
            writer.WriteInt32((count + 1) * 0x03F1);
        }

        private static T[] ReadX3F1Array<T>(StreamReader reader, Func<StreamReader, T> readItem)
        {
            int count = ReadX3F1Count(reader);
            var result = new T[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = readItem(reader);
            }

            return result;
        }

        private static void WriteX3F1Array<T>(StreamWriter writer, T[] values, Action<StreamWriter, T> writeItem)
        {
            T[] safeValues = values ?? new T[0];
            WriteX3F1Count(writer, safeValues.Length);
            for (int i = 0; i < safeValues.Length; i++)
            {
                writeItem(writer, safeValues[i]);
            }
        }

        private static int[] ReadInt32Array(StreamReader reader)
        {
            return ReadInt32Array(reader, r => r.ReadInt32());
        }

        private static T[] ReadInt32Array<T>(StreamReader reader, Func<StreamReader, T> readItem)
        {
            int count = reader.ReadInt32();
            var result = new T[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = readItem(reader);
            }

            return result;
        }

        private static void WriteInt32Array<T>(StreamWriter writer, T[] values, Action<StreamWriter, T> writeItem)
        {
            T[] safeValues = values ?? new T[0];
            writer.WriteInt32(safeValues.Length);
            for (int i = 0; i < safeValues.Length; i++)
            {
                writeItem(writer, safeValues[i]);
            }
        }
    }
}
