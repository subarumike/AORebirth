namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    public class ResurrectMessageSerializer : ISerializer
    {
        private readonly Type type;

        public ResurrectMessageSerializer()
        {
            this.type = typeof(ResurrectMessage);
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
            var message = new ResurrectMessage();
            message.N3MessageType = (N3MessageType)streamReader.ReadInt32();
            message.Unknown1 = streamReader.ReadInt32();
            message.Unknown2 = streamReader.ReadInt32();
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
                    <ResurrectMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(
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
            var message = (ResurrectMessage)value;
            streamWriter.WriteInt32((int)message.N3MessageType);
            streamWriter.WriteInt32(message.Unknown1);
            streamWriter.WriteInt32(message.Unknown2);
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
                    <ResurrectMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(
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
