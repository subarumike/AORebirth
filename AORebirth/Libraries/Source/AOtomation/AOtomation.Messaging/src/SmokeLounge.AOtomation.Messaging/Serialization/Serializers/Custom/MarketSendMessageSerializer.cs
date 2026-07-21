#region License

// Copyright (c) 2005-2014, CellAO Team
// All rights reserved.

#endregion

namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Reflection;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    /// <summary>
    /// Capture 20260715-GMI: Type, Identity, Unknown(byte), Character Identity,
    /// then credit deposit (credits + 0x3F1) or item deposit (0 + N×(lowId, Inventory=0x68, placement)).
    /// Ack: credits=0 + 0x3F1.
    /// </summary>
    public class MarketSendMessageSerializer : ISerializer
    {
        private readonly Type type;

        public MarketSendMessageSerializer()
        {
            this.type = typeof(MarketSendMessage);
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
            var message = new MarketSendMessage();
            message.N3MessageType = (N3MessageType)streamReader.ReadInt32();
            message.Identity = streamReader.ReadIdentity();
            message.Unknown = streamReader.ReadByte();
            message.Character = streamReader.ReadIdentity();

            long remaining = streamReader.Length - streamReader.Position;
            if (remaining < 4)
            {
                return message;
            }

            message.Credits = streamReader.ReadInt32();
            remaining = streamReader.Length - streamReader.Position;

            if (remaining >= 12)
            {
                // One or more item triples (deposit UI = up to 8 slots).
                message.Items = new List<MarketSendItemEntry>();
                while (remaining >= 12 && message.Items.Count < MarketSendMessage.MaxDepositItems)
                {
                    var entry = new MarketSendItemEntry
                        {
                            ItemLowId = streamReader.ReadInt32(),
                            ContainerType = streamReader.ReadInt32(),
                            Placement = streamReader.ReadInt32()
                        };
                    remaining = streamReader.Length - streamReader.Position;

                    // Skip empty slots (all zero).
                    if (entry.ItemLowId == 0 && entry.ContainerType == 0 && entry.Placement == 0)
                    {
                        continue;
                    }

                    // Capture: (lowId, containerType, placement). Some Send shapes look like
                    // (lowId, placement, containerType) — detect when "container" is an inventory slot
                    // and "placement" is a known page type (Inventory=0x68 etc.).
                    NormalizeEntryRefs(entry);
                    message.Items.Add(entry);
                }

                message.SyncFirstItemFields();
            }
            else if (remaining >= 4)
            {
                message.StatusCode = streamReader.ReadInt32();
            }

            return message;
        }

        private static bool IsPageType(int value)
        {
            // Keep in sync with GMI deposit source pages (IdentityType inventory family).
            return value == 0x65 // WeaponPage
                   || value == 0x66 // ArmorPage
                   || value == 0x67 // ImplantPage
                   || value == 0x68 // Inventory
                   || value == 0x69 // Bank
                   || value == 0x6E // OverflowWindow
                   || value == 0x73; // SocialPage
        }

        private static void NormalizeEntryRefs(MarketSendItemEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            bool containerIsPage = IsPageType(entry.ContainerType);
            bool placementIsPage = IsPageType(entry.Placement);
            if (!containerIsPage && placementIsPage)
            {
                int swap = entry.ContainerType;
                entry.ContainerType = entry.Placement;
                entry.Placement = swap;
            }
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
                    <MarketSendMessageSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(
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
            var message = (MarketSendMessage)value;
            streamWriter.WriteInt32((int)message.N3MessageType);
            streamWriter.WriteIdentity(message.Identity);
            streamWriter.WriteByte(message.Unknown);
            Identity character = message.Character.Equals(Identity.None)
                                     ? message.Identity
                                     : message.Character;
            streamWriter.WriteIdentity(character);

            if (message.IsItemDeposit)
            {
                streamWriter.WriteInt32(message.Credits);
                if (message.Items != null && message.Items.Count > 0)
                {
                    int n = Math.Min(message.Items.Count, MarketSendMessage.MaxDepositItems);
                    for (int i = 0; i < n; i++)
                    {
                        MarketSendItemEntry entry = message.Items[i];
                        streamWriter.WriteInt32(entry.ItemLowId);
                        streamWriter.WriteInt32(entry.ContainerType);
                        streamWriter.WriteInt32(entry.Placement);
                    }
                }
                else
                {
                    streamWriter.WriteInt32(message.ItemLowId);
                    streamWriter.WriteInt32(message.ContainerType);
                    streamWriter.WriteInt32(message.Placement);
                }
            }
            else
            {
                streamWriter.WriteInt32(message.Credits);
                streamWriter.WriteInt32(
                    message.StatusCode != 0
                        ? message.StatusCode
                        : MarketSendMessage.CaptureCreditStatusCode);
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
                    <MarketSendMessageSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(
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
    }
}
