namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    public class AOTransportSignalMessageSerializer : ISerializer
    {
        public Type Type
        {
            get
            {
                return typeof(AOTransportSignalMessage);
            }
        }

        public object Deserialize(
            StreamReader streamReader,
            SerializationContext serializationContext,
            PropertyMetaData propertyMetaData = null)
        {
            var message = new AOTransportSignalMessage
            {
                N3MessageType = (N3MessageType)streamReader.ReadInt32(),
                Identity = streamReader.ReadIdentity(),
                Unknown = streamReader.ReadByte(),
                Signal = streamReader.ReadInt32()
            };

            int remaining = (int)(streamReader.Length - streamReader.Position);
            message.Payload = remaining > 0 ? streamReader.ReadBytes(remaining) : new byte[0];
            return message;
        }

        public Expression DeserializerExpression(
            ParameterExpression streamReaderExpression,
            ParameterExpression serializationContextExpression,
            Expression assignmentTargetExpression,
            PropertyMetaData propertyMetaData)
        {
            MethodInfo method =
                ReflectionHelper.GetMethodInfo
                    <AOTransportSignalMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(
                        o => o.Deserialize);
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
            var message = (AOTransportSignalMessage)value;
            byte[] payload = message.Payload ?? new byte[0];

            streamWriter.WriteInt32((int)message.N3MessageType);
            streamWriter.WriteIdentity(message.Identity);
            streamWriter.WriteByte(message.Unknown);
            streamWriter.WriteInt32(message.Signal);
            streamWriter.WriteBytes(payload);
        }

        public Expression SerializerExpression(
            ParameterExpression streamWriterExpression,
            ParameterExpression serializationContextExpression,
            Expression valueExpression,
            PropertyMetaData propertyMetaData)
        {
            MethodInfo method =
                ReflectionHelper.GetMethodInfo
                    <AOTransportSignalMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(
                        o => o.Serialize);
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
    }
}
