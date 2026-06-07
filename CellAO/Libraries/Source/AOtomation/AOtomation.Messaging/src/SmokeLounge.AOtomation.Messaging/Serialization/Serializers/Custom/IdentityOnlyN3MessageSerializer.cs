namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    public class IdentityOnlyN3MessageSerializer : ISerializer
    {
        private readonly Type type;

        public IdentityOnlyN3MessageSerializer(Type type)
        {
            this.type = type;
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
            var message = (N3Message)Activator.CreateInstance(this.type);
            message.N3MessageType = (N3MessageType)streamReader.ReadInt32();
            message.Identity = streamReader.ReadIdentity();
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
                    <IdentityOnlyN3MessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(
                        o => o.Deserialize);
            ConstructorInfo constructorInfo = this.GetType().GetConstructor(new[] { typeof(Type) });
            NewExpression serializerExp = Expression.New(constructorInfo, Expression.Constant(this.type, typeof(Type)));
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
            var message = (N3Message)value;
            streamWriter.WriteInt32((int)message.N3MessageType);
            streamWriter.WriteIdentity(message.Identity);
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
                    <IdentityOnlyN3MessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(
                        o => o.Serialize);
            ConstructorInfo constructorInfo = this.GetType().GetConstructor(new[] { typeof(Type) });
            NewExpression serializerExp = Expression.New(constructorInfo, Expression.Constant(this.type, typeof(Type)));
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
