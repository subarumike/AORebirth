#region License

// Copyright (c) 2005-2014, CellAO Team
// All rights reserved.

#endregion

namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    /// <summary>
    /// Capture 20260722-keeper-exect-nano SpellList layouts:
    /// Named cast (Ambient Restoration): NanoEffect without GfxFade, Character,
    /// Int16BE name length + ASCII name + 6 zero pad.
    /// GfxEffect fire: full NanoEffect, Identity.None, Character, 8 zero pad.
    /// </summary>
    public class SpellListMessageSerializer : ISerializer
    {
        private const int X3F1Factor = 0x03F1;

        private readonly Type type;

        public SpellListMessageSerializer()
        {
            this.type = typeof(SpellListMessage);
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
            var message = new SpellListMessage();
            message.N3MessageType = (N3MessageType)streamReader.ReadInt32();
            message.Identity = streamReader.ReadIdentity();
            message.Unknown = streamReader.ReadByte();

            int count = (streamReader.ReadInt32() / X3F1Factor) - 1;
            if (count < 0)
            {
                count = 0;
            }

            // Detect named layout by remaining size heuristic after reading effects without GfxFade.
            long effectsStart = streamReader.Position;
            var effects = new NanoEffect[count];
            bool namedLayout = false;
            if (count > 0)
            {
                // Try named (no GfxFade): effectBytes = count * 56, then Character 8 + Int16 + name.
                int namedEffectBytes = count * 56;
                long afterNamed = effectsStart + namedEffectBytes + 8;
                if (afterNamed + 2 <= streamReader.Length)
                {
                    long saved = streamReader.Position;
                    streamReader.Position = afterNamed;
                    short probeLen = streamReader.ReadInt16();
                    streamReader.Position = saved;
                    if (probeLen > 0 && probeLen < 128 && afterNamed + 2 + probeLen <= streamReader.Length)
                    {
                        namedLayout = true;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                effects[i] = ReadNanoEffect(streamReader, includeGfxFade: !namedLayout);
            }

            message.NanoEffects = effects;

            if (!namedLayout && count > 0 && streamReader.Position + 8 <= streamReader.Length)
            {
                Identity maybeNone = streamReader.ReadIdentity();
                if (!(maybeNone.Type == 0 && maybeNone.Instance == 0))
                {
                    message.Character = maybeNone;
                    return message;
                }
            }

            if (streamReader.Position + 8 <= streamReader.Length)
            {
                message.Character = streamReader.ReadIdentity();
            }

            if (namedLayout && streamReader.Position + 2 <= streamReader.Length)
            {
                short nameLen = streamReader.ReadInt16();
                if (nameLen > 0 && streamReader.Position + nameLen <= streamReader.Length)
                {
                    message.NanoName = Encoding.ASCII.GetString(streamReader.ReadBytes(nameLen));
                }
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
                    <SpellListMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(
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

            return Expression.Assign(
                assignmentTargetExpression,
                Expression.TypeAs(callExp, assignmentTargetExpression.Type));
        }

        public void Serialize(
            StreamWriter streamWriter,
            SerializationContext serializationContext,
            object value,
            PropertyMetaData propertyMetaData = null)
        {
            var message = (SpellListMessage)value;
            streamWriter.WriteInt32((int)message.N3MessageType);
            streamWriter.WriteIdentity(message.Identity);
            streamWriter.WriteByte(message.Unknown);

            NanoEffect[] effects = message.NanoEffects ?? new NanoEffect[0];
            streamWriter.WriteInt32((effects.Length + 1) * X3F1Factor);

            bool named = !string.IsNullOrEmpty(message.NanoName);
            for (int i = 0; i < effects.Length; i++)
            {
                WriteNanoEffect(streamWriter, effects[i], includeGfxFade: !named);
            }

            if (!named && effects.Length > 0)
            {
                streamWriter.WriteIdentity(Identity.None);
            }

            streamWriter.WriteIdentity(message.Character);

            if (named)
            {
                byte[] nameBytes = Encoding.ASCII.GetBytes(message.NanoName);
                streamWriter.WriteInt16((short)nameBytes.Length);
                streamWriter.WriteBytes(nameBytes);
                streamWriter.WriteBytes(new byte[6]);
            }
            else if (effects.Length > 0)
            {
                streamWriter.WriteBytes(new byte[8]);
            }
        }

        public Expression SerializerExpression(
            ParameterExpression streamWriterExpression,
            ParameterExpression serializationContextExpression,
            Expression valueExpression,
            PropertyMetaData propertyMetaData)
        {
            MethodInfo serializeMethodInfo =
                ReflectionHelper
                    .GetMethodInfo
                    <SpellListMessageSerializer,
                        Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(o => o.Serialize);
            NewExpression serializerExp = Expression.New(this.GetType());
            return Expression.Call(
                serializerExp,
                serializeMethodInfo,
                streamWriterExpression,
                serializationContextExpression,
                Expression.Convert(valueExpression, typeof(object)),
                Expression.Constant(propertyMetaData, typeof(PropertyMetaData)));
        }

        private static NanoEffect ReadNanoEffect(StreamReader reader, bool includeGfxFade)
        {
            var effect = new NanoEffect
                             {
                                 Effect = reader.ReadIdentity(),
                                 Unknown1 = reader.ReadInt32(),
                                 CriterionCount = reader.ReadInt32(),
                                 Hits = reader.ReadInt32(),
                                 Delay = reader.ReadInt32(),
                                 Unknown2 = reader.ReadInt32(),
                                 Unknown3 = reader.ReadInt32(),
                                 GfxValue = reader.ReadInt32(),
                                 GfxLife = reader.ReadInt32(),
                                 GfxSize = reader.ReadInt32(),
                                 GfxRed = reader.ReadInt32(),
                                 GfxGreen = reader.ReadInt32(),
                                 GfxBlue = reader.ReadInt32()
                             };
            if (includeGfxFade)
            {
                effect.GfxFade = reader.ReadInt32();
            }

            return effect;
        }

        private static void WriteNanoEffect(StreamWriter writer, NanoEffect effect, bool includeGfxFade)
        {
            if (effect == null)
            {
                effect = new NanoEffect();
            }

            writer.WriteIdentity(effect.Effect);
            writer.WriteInt32(effect.Unknown1);
            writer.WriteInt32(effect.CriterionCount);
            writer.WriteInt32(effect.Hits);
            writer.WriteInt32(effect.Delay);
            writer.WriteInt32(effect.Unknown2);
            writer.WriteInt32(effect.Unknown3);
            writer.WriteInt32(effect.GfxValue);
            writer.WriteInt32(effect.GfxLife);
            writer.WriteInt32(effect.GfxSize);
            writer.WriteInt32(effect.GfxRed);
            writer.WriteInt32(effect.GfxGreen);
            writer.WriteInt32(effect.GfxBlue);
            if (includeGfxFade)
            {
                writer.WriteInt32(effect.GfxFade);
            }
        }
    }
}
