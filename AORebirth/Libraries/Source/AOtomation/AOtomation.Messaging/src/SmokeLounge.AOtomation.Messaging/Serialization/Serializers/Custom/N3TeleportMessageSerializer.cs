namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    public class N3TeleportMessageSerializer : ISerializer
    {
        private readonly Type type;

        public N3TeleportMessageSerializer()
        {
            this.type = typeof(N3TeleportMessage);
        }

        public Type Type
        {
            get { return this.type; }
        }

        public object Deserialize(
            StreamReader streamReader,
            SerializationContext serializationContext,
            PropertyMetaData propertyMetaData = null)
        {
            var message = new N3TeleportMessage();
            message.N3MessageType = (N3MessageType)streamReader.ReadInt32();
            message.Identity = streamReader.ReadIdentity();
            message.Unknown = streamReader.ReadByte();
            message.Destination = new Vector3
                                      {
                                          X = streamReader.ReadSingle(),
                                          Y = streamReader.ReadSingle(),
                                          Z = streamReader.ReadSingle()
                                      };
            message.Heading = new Quaternion
                                  {
                                      X = streamReader.ReadSingle(),
                                      Y = streamReader.ReadSingle(),
                                      Z = streamReader.ReadSingle(),
                                      W = streamReader.ReadSingle()
                                  };
            message.Unknown1 = streamReader.ReadByte();
            message.Playfield = streamReader.ReadIdentity();
            message.GameServerId = streamReader.ReadInt32();
            message.SgId = streamReader.ReadInt32();
            message.ChangePlayfield = streamReader.ReadIdentity();
            message.Unknown4 = streamReader.ReadInt32();
            message.Unknown5 = streamReader.ReadInt32();
            message.Playfield2 = streamReader.ReadIdentity();
            message.Unknown6 = streamReader.ReadInt32();
            message.Payload = message.Unknown6 > 0 ? streamReader.ReadBytes(message.Unknown6) : new byte[0];
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
                    <N3TeleportMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(
                        o => o.Deserialize);
            NewExpression serializerExp = Expression.New(this.GetType());
            MethodCallExpression callExp = Expression.Call(
                serializerExp,
                deserializerMethodInfo,
                new Expression[]
                    {
                        streamReaderExpression,
                        serializationContextExpression,
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
            var message = (N3TeleportMessage)value;
            var payload = message.Payload ?? new byte[0];

            streamWriter.WriteInt32((int)message.N3MessageType);
            streamWriter.WriteIdentity(message.Identity);
            streamWriter.WriteByte(message.Unknown);
            streamWriter.WriteSingle(message.Destination.X);
            streamWriter.WriteSingle(message.Destination.Y);
            streamWriter.WriteSingle(message.Destination.Z);
            streamWriter.WriteSingle(message.Heading.X);
            streamWriter.WriteSingle(message.Heading.Y);
            streamWriter.WriteSingle(message.Heading.Z);
            streamWriter.WriteSingle(message.Heading.W);
            streamWriter.WriteByte(message.Unknown1);
            streamWriter.WriteIdentity(message.Playfield);
            streamWriter.WriteInt32(message.GameServerId);
            streamWriter.WriteInt32(message.SgId);
            streamWriter.WriteIdentity(message.ChangePlayfield);
            streamWriter.WriteInt32(message.Unknown4);
            streamWriter.WriteInt32(message.Unknown5);
            streamWriter.WriteIdentity(message.Playfield2);
            streamWriter.WriteInt32(payload.Length);
            streamWriter.WriteBytes(payload);
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
                    <N3TeleportMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(
                        o => o.Serialize);
            NewExpression serializerExp = Expression.New(this.GetType());
            MethodCallExpression callExp = Expression.Call(
                serializerExp,
                serializerMethodInfo,
                new[]
                    {
                        streamWriterExpression,
                        serializationContextExpression,
                        valueExpression,
                        Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
                    });
            return callExp;
        }
    }
}
