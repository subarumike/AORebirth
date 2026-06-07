namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    public class PlayfieldAnarchyFMessageSerializer : ISerializer
    {
        public Type Type
        {
            get { return typeof(PlayfieldAnarchyFMessage); }
        }

        public object Deserialize(
            StreamReader streamReader,
            SerializationContext serializationContext,
            PropertyMetaData propertyMetaData = null)
        {
            var message = new PlayfieldAnarchyFMessage
                              {
                                  N3MessageType = (N3MessageType)streamReader.ReadInt32(),
                                  Identity = streamReader.ReadIdentity(),
                                  Unknown = streamReader.ReadByte(),
                                  Unknown1 = streamReader.ReadInt32(),
                                  CharacterCoordinates =
                                      new Vector3
                                          {
                                              X = streamReader.ReadSingle(),
                                              Y = streamReader.ReadSingle(),
                                              Z = streamReader.ReadSingle()
                                          },
                                  Unknown2 = streamReader.ReadByte(),
                                  PlayfieldId1 = streamReader.ReadIdentity(),
                                  Unknown3 = streamReader.ReadInt32(),
                                  Unknown4 = streamReader.ReadInt32(),
                                  PlayfieldId2 = streamReader.ReadIdentity()
                              };

            int remaining = (int)(streamReader.Length - streamReader.Position);
            if (remaining <= 0)
            {
                return message;
            }

            if (LooksLikeGeneratorPayload(streamReader, remaining))
            {
                message.GeneratorPayload = streamReader.ReadBytes(remaining);
                return message;
            }

            message.Unknown5 = streamReader.ReadInt32();
            message.Unknown6 = streamReader.ReadInt32();
            message.PlayfieldVendorInfo = (PlayfieldVendorInfo)new PlayfieldVendorInfoSerializer().Deserialize(
                streamReader,
                serializationContext,
                propertyMetaData);
            message.PlayfieldX = streamReader.ReadInt32();
            message.PlayfieldZ = streamReader.ReadInt32();
            return message;
        }

        public Expression DeserializerExpression(
            ParameterExpression streamReaderExpression,
            ParameterExpression serializationContextExpression,
            Expression assignmentTargetExpression,
            PropertyMetaData propertyMetaData)
        {
            MethodInfo method = ReflectionHelper.GetMethodInfo<PlayfieldAnarchyFMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(o => o.Deserialize);
            NewExpression serializer = Expression.New(this.GetType());
            MethodCallExpression call = Expression.Call(
                serializer,
                method,
                new Expression[]
                    {
                        streamReaderExpression,
                        serializationContextExpression,
                        Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
                    });
            return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(call, assignmentTargetExpression.Type));
        }

        public void Serialize(
            StreamWriter streamWriter,
            SerializationContext serializationContext,
            object value,
            PropertyMetaData propertyMetaData = null)
        {
            var message = (PlayfieldAnarchyFMessage)value;
            streamWriter.WriteInt32((int)message.N3MessageType);
            streamWriter.WriteIdentity(message.Identity);
            streamWriter.WriteByte(message.Unknown);
            streamWriter.WriteInt32(message.Unknown1);
            streamWriter.WriteSingle(message.CharacterCoordinates.X);
            streamWriter.WriteSingle(message.CharacterCoordinates.Y);
            streamWriter.WriteSingle(message.CharacterCoordinates.Z);
            streamWriter.WriteByte(message.Unknown2);
            streamWriter.WriteIdentity(message.PlayfieldId1);
            streamWriter.WriteInt32(message.Unknown3);
            streamWriter.WriteInt32(message.Unknown4);
            streamWriter.WriteIdentity(message.PlayfieldId2);

            if (message.GeneratorPayload != null)
            {
                streamWriter.WriteBytes(message.GeneratorPayload);
                return;
            }

            streamWriter.WriteInt32(message.Unknown5);
            streamWriter.WriteInt32(message.Unknown6);
            new PlayfieldVendorInfoSerializer().Serialize(
                streamWriter,
                serializationContext,
                message.PlayfieldVendorInfo,
                propertyMetaData);
            streamWriter.WriteInt32(message.PlayfieldX);
            streamWriter.WriteInt32(message.PlayfieldZ);
        }

        public Expression SerializerExpression(
            ParameterExpression streamWriterExpression,
            ParameterExpression serializationContextExpression,
            Expression valueExpression,
            PropertyMetaData propertyMetaData)
        {
            MethodInfo method = ReflectionHelper.GetMethodInfo<PlayfieldAnarchyFMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(o => o.Serialize);
            NewExpression serializer = Expression.New(this.GetType());
            return Expression.Call(
                serializer,
                method,
                new[]
                    {
                        streamWriterExpression,
                        serializationContextExpression,
                        valueExpression,
                        Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
                    });
        }

        private static bool LooksLikeGeneratorPayload(StreamReader streamReader, int remaining)
        {
            if (remaining <= 16)
            {
                return false;
            }

            long position = streamReader.Position;
            int firstWord = streamReader.ReadInt32();
            streamReader.Position = position;
            return firstWord == (int)IdentityType.Door ||
                   firstWord == (int)IdentityType.Terminal ||
                   firstWord == (int)IdentityType.VendingMachine ||
                   firstWord == unchecked((int)0x0000C77D);
        }
    }
}
