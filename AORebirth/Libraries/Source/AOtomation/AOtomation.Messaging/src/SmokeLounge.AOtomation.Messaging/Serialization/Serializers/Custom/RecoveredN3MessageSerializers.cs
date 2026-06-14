namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    public class KeyOnlyN3MessageSerializer : ISerializer
    {
        private readonly Type type;

        public KeyOnlyN3MessageSerializer(Type type)
        {
            this.type = type;
        }

        public Type Type
        {
            get { return this.type; }
        }

        public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
        {
            var message = (N3Message)Activator.CreateInstance(this.type);
            message.N3MessageType = (N3MessageType)streamReader.ReadInt32();
            return message;
        }

        public Expression DeserializerExpression(
            ParameterExpression streamReaderExpression,
            ParameterExpression serializationContextExpression,
            Expression assignmentTargetExpression,
            PropertyMetaData propertyMetaData)
        {
            MethodInfo method = ReflectionHelper.GetMethodInfo<KeyOnlyN3MessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(o => o.Deserialize);
            ConstructorInfo constructor = this.GetType().GetConstructor(new[] { typeof(Type) });
            NewExpression serializer = Expression.New(constructor, Expression.Constant(this.type, typeof(Type)));
            MethodCallExpression call = Expression.Call(serializer, method, streamReaderExpression, serializationContextExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
            return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(call, assignmentTargetExpression.Type));
        }

        public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
        {
            var message = (N3Message)value;
            streamWriter.WriteInt32((int)message.N3MessageType);
        }

        public Expression SerializerExpression(
            ParameterExpression streamWriterExpression,
            ParameterExpression serializationContextExpression,
            Expression valueExpression,
            PropertyMetaData propertyMetaData)
        {
            MethodInfo method = ReflectionHelper.GetMethodInfo<KeyOnlyN3MessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(o => o.Serialize);
            ConstructorInfo constructor = this.GetType().GetConstructor(new[] { typeof(Type) });
            NewExpression serializer = Expression.New(constructor, Expression.Constant(this.type, typeof(Type)));
            return Expression.Call(serializer, method, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
        }
    }

    public class DropDynelMessageSerializer : ISerializer
    {
        public Type Type
        {
            get { return typeof(DropDynelMessage); }
        }

        public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
        {
            return new DropDynelMessage
                       {
                           N3MessageType = (N3MessageType)streamReader.ReadInt32(),
                           Identity = streamReader.ReadIdentity(),
                           Position = new Vector3
                                      {
                                          X = streamReader.ReadSingle(),
                                          Y = streamReader.ReadSingle(),
                                          Z = streamReader.ReadSingle()
                                      }
                       };
        }

        public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
        {
            MethodInfo method = ReflectionHelper.GetMethodInfo<DropDynelMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(o => o.Deserialize);
            MethodCallExpression call = Expression.Call(Expression.New(this.GetType()), method, streamReaderExpression, serializationContextExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
            return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(call, assignmentTargetExpression.Type));
        }

        public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
        {
            var message = (DropDynelMessage)value;
            streamWriter.WriteInt32((int)message.N3MessageType);
            streamWriter.WriteIdentity(message.Identity);
            streamWriter.WriteSingle(message.Position.X);
            streamWriter.WriteSingle(message.Position.Y);
            streamWriter.WriteSingle(message.Position.Z);
        }

        public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
        {
            MethodInfo method = ReflectionHelper.GetMethodInfo<DropDynelMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(o => o.Serialize);
            return Expression.Call(Expression.New(this.GetType()), method, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
        }
    }

    public class RelocateDynelsMessageSerializer : ISerializer
    {
        public Type Type
        {
            get { return typeof(RelocateDynelsMessage); }
        }

        public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
        {
            var message = new RelocateDynelsMessage();
            message.N3MessageType = (N3MessageType)streamReader.ReadInt32();
            message.Identity = streamReader.ReadIdentity();

            int encodedCount = streamReader.ReadInt32();
            int identityCount = (encodedCount / 0x03F1) - 1;
            message.RelocatedIdentities = new Identity[Math.Max(identityCount, 0)];

            for (int i = 0; i < message.RelocatedIdentities.Length; i++)
            {
                message.RelocatedIdentities[i] = streamReader.ReadIdentity();
            }

            return message;
        }

        public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
        {
            MethodInfo method = ReflectionHelper.GetMethodInfo<RelocateDynelsMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(o => o.Deserialize);
            MethodCallExpression call = Expression.Call(Expression.New(this.GetType()), method, streamReaderExpression, serializationContextExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
            return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(call, assignmentTargetExpression.Type));
        }

        public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
        {
            var message = (RelocateDynelsMessage)value;
            var identities = message.RelocatedIdentities ?? new Identity[0];

            streamWriter.WriteInt32((int)message.N3MessageType);
            streamWriter.WriteIdentity(message.Identity);
            streamWriter.WriteInt32((identities.Length + 1) * 0x03F1);

            for (int i = 0; i < identities.Length; i++)
            {
                streamWriter.WriteIdentity(identities[i]);
            }
        }

        public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
        {
            MethodInfo method = ReflectionHelper.GetMethodInfo<RelocateDynelsMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(o => o.Serialize);
            return Expression.Call(Expression.New(this.GetType()), method, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
        }
    }

    public class LocalityUpdateMessageSerializer : ISerializer
    {
        public Type Type
        {
            get { return typeof(LocalityUpdateMessage); }
        }

        public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
        {
            return new LocalityUpdateMessage
                       {
                           N3MessageType = (N3MessageType)streamReader.ReadInt32(),
                           Position = new Vector3
                                      {
                                          X = streamReader.ReadSingle(),
                                          Y = streamReader.ReadSingle(),
                                          Z = streamReader.ReadSingle()
                                      },
                           LocalityFlag = streamReader.ReadByte()
                       };
        }

        public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
        {
            MethodInfo method = ReflectionHelper.GetMethodInfo<LocalityUpdateMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(o => o.Deserialize);
            MethodCallExpression call = Expression.Call(Expression.New(this.GetType()), method, streamReaderExpression, serializationContextExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
            return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(call, assignmentTargetExpression.Type));
        }

        public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
        {
            var message = (LocalityUpdateMessage)value;
            streamWriter.WriteInt32((int)message.N3MessageType);
            streamWriter.WriteSingle(message.Position.X);
            streamWriter.WriteSingle(message.Position.Y);
            streamWriter.WriteSingle(message.Position.Z);
            streamWriter.WriteByte(message.LocalityFlag);
        }

        public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
        {
            MethodInfo method = ReflectionHelper.GetMethodInfo<LocalityUpdateMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(o => o.Serialize);
            return Expression.Call(Expression.New(this.GetType()), method, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
        }
    }

    public class ClientContainerAddItemMessageSerializer : ISerializer
    {
        public Type Type
        {
            get { return typeof(ClientContainerAddItemMessage); }
        }

        public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
        {
            return new ClientContainerAddItemMessage
                       {
                           N3MessageType = (N3MessageType)streamReader.ReadInt32(),
                           Identity1 = streamReader.ReadIdentity(),
                           Identity2 = streamReader.ReadIdentity()
                       };
        }

        public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
        {
            MethodInfo method = ReflectionHelper.GetMethodInfo<ClientContainerAddItemMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(o => o.Deserialize);
            MethodCallExpression call = Expression.Call(Expression.New(this.GetType()), method, streamReaderExpression, serializationContextExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
            return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(call, assignmentTargetExpression.Type));
        }

        public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
        {
            var message = (ClientContainerAddItemMessage)value;
            streamWriter.WriteInt32((int)message.N3MessageType);
            streamWriter.WriteIdentity(message.Identity1);
            streamWriter.WriteIdentity(message.Identity2);
        }

        public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
        {
            MethodInfo method = ReflectionHelper.GetMethodInfo<ClientContainerAddItemMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(o => o.Serialize);
            return Expression.Call(Expression.New(this.GetType()), method, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
        }
    }

    public class ClientGetItemMessageSerializer : ISerializer
    {
        public Type Type
        {
            get { return typeof(ClientGetItemMessage); }
        }

        public object Deserialize(StreamReader streamReader, SerializationContext serializationContext, PropertyMetaData propertyMetaData = null)
        {
            return new ClientGetItemMessage
                       {
                           N3MessageType = (N3MessageType)streamReader.ReadInt32(),
                           Identity1 = streamReader.ReadIdentity()
                       };
        }

        public Expression DeserializerExpression(ParameterExpression streamReaderExpression, ParameterExpression serializationContextExpression, Expression assignmentTargetExpression, PropertyMetaData propertyMetaData)
        {
            MethodInfo method = ReflectionHelper.GetMethodInfo<ClientGetItemMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(o => o.Deserialize);
            MethodCallExpression call = Expression.Call(Expression.New(this.GetType()), method, streamReaderExpression, serializationContextExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
            return Expression.Assign(assignmentTargetExpression, Expression.TypeAs(call, assignmentTargetExpression.Type));
        }

        public void Serialize(StreamWriter streamWriter, SerializationContext serializationContext, object value, PropertyMetaData propertyMetaData = null)
        {
            var message = (ClientGetItemMessage)value;
            streamWriter.WriteInt32((int)message.N3MessageType);
            streamWriter.WriteIdentity(message.Identity1);
        }

        public Expression SerializerExpression(ParameterExpression streamWriterExpression, ParameterExpression serializationContextExpression, Expression valueExpression, PropertyMetaData propertyMetaData)
        {
            MethodInfo method = ReflectionHelper.GetMethodInfo<ClientGetItemMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(o => o.Serialize);
            return Expression.Call(Expression.New(this.GetType()), method, streamWriterExpression, serializationContextExpression, valueExpression, Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
        }
    }
}
