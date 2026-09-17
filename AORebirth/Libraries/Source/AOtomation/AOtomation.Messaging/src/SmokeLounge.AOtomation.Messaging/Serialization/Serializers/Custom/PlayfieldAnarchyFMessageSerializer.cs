namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    /// <summary>
    /// Serializes <see cref="PlayfieldAnarchyFMessage"/> per client
    /// <c>n3PlayfieldFullUpdateIIR_t::ReadSubClass</c> /
    /// <c>PlayfieldAnarchyFIIR_t</c> extension (PF world X/Z).
    /// </summary>
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
                                  Version = streamReader.ReadInt32(),
                                  CharacterCoordinates =
                                      new Vector3
                                          {
                                              X = streamReader.ReadSingle(),
                                              Y = streamReader.ReadSingle(),
                                              Z = streamReader.ReadSingle()
                                          }
                              };

            if (message.Version > 1)
            {
                message.PlayfieldProxyVersion = streamReader.ReadByte();
                message.PlayfieldId1 = streamReader.ReadIdentity();
                message.Unknown3 = streamReader.ReadInt32();
                message.Unknown4 = streamReader.ReadInt32();
                message.PlayfieldId2 = streamReader.ReadIdentity();
            }

            // version > 3: generator DbObject or Identity 0:0, then always PF world X/Z.
            if (message.Version > 3)
            {
                int remaining = (int)(streamReader.Length - streamReader.Position);
                if (remaining >= 16)
                {
                    long generatorStart = streamReader.Position;
                    int generatorType = streamReader.ReadInt32();
                    int generatorInstance = streamReader.ReadInt32();
                    if (generatorType != 0 || generatorInstance != 0)
                    {
                        int payloadLength = remaining - 8;
                        streamReader.Position = generatorStart;
                        message.GeneratorPayload = streamReader.ReadBytes(payloadLength);
                        AcgBuildingGeneratorData acg;
                        if (AcgBuildingGeneratorData.TryParse(message.GeneratorPayload, out acg))
                            message.AcgBuildingGenerator = acg;
                    }
                }
                else if (remaining >= 8)
                {
                    // Truncated live shapes still carry world X/Z without a generator slot.
                }
            }

            int worldRemaining = (int)(streamReader.Length - streamReader.Position);
            if (worldRemaining >= 8)
            {
                message.PlayfieldX = streamReader.ReadInt32();
                message.PlayfieldZ = streamReader.ReadInt32();
            }

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
            if (RequiresGeneratorPayload(message.PlayfieldId1.Type)
                && (message.GeneratorPayload == null || message.GeneratorPayload.Length == 0)
                && message.AcgBuildingGenerator == null)
            {
                throw new InvalidOperationException(
                    "Generated playfield identity requires an exact generator payload.");
            }

            streamWriter.WriteInt32((int)message.N3MessageType);
            streamWriter.WriteIdentity(message.Identity);
            streamWriter.WriteByte(message.Unknown);
            streamWriter.WriteInt32(message.Version);
            streamWriter.WriteSingle(message.CharacterCoordinates.X);
            streamWriter.WriteSingle(message.CharacterCoordinates.Y);
            streamWriter.WriteSingle(message.CharacterCoordinates.Z);

            if (message.Version > 1)
            {
                streamWriter.WriteByte(message.PlayfieldProxyVersion);
                streamWriter.WriteIdentity(message.PlayfieldId1);
                streamWriter.WriteInt32(message.Unknown3);
                streamWriter.WriteInt32(message.Unknown4);
                streamWriter.WriteIdentity(message.PlayfieldId2);
            }

            if (message.Version > 3)
            {
                if (message.GeneratorPayload != null && message.GeneratorPayload.Length > 0)
                    streamWriter.WriteBytes(message.GeneratorPayload);
                else if (message.AcgBuildingGenerator != null)
                    streamWriter.WriteBytes(message.AcgBuildingGenerator.ToByteArray());
                else
                {
                    streamWriter.WriteInt32(0);
                    streamWriter.WriteInt32(0);
                }
            }

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

        private static bool RequiresGeneratorPayload(IdentityType type)
        {
            // PlayfieldDoor (0xC79E), ACG building (0xC79F), ACGEntrance (0xC7A1).
            return type == IdentityType.PlayfieldDoor
                   || type == IdentityType.AcgBuildingGenerator
                   || type == IdentityType.AcgEntrance;
        }
    }
}
