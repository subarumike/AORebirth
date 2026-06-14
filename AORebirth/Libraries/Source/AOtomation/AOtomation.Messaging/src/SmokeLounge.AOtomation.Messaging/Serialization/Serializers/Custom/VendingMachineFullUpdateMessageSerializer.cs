#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
    #region Usings ...

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq.Expressions;
    using System.Reflection;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using StreamReader = SmokeLounge.AOtomation.Messaging.Serialization.StreamReader;
    using StreamWriter = SmokeLounge.AOtomation.Messaging.Serialization.StreamWriter;

    #endregion

    internal class VendingMachineFullUpdateMessageSerializer : ISerializer
    {
        #region Fields

        private readonly Type type;

        #endregion

        #region Constructors and Destructors

        public VendingMachineFullUpdateMessageSerializer()
        {
            this.type = typeof(VendingMachineFullUpdateMessage);
        }

        #endregion

        #region Public Properties

        public Type Type
        {
            get
            {
                return this.type;
            }
        }

        #endregion

        #region Public Methods and Operators

        public object Deserialize(
            StreamReader streamReader,
            SerializationContext serializationContext,
            PropertyMetaData propertyMetaData = null)
        {
            VendingMachineFullUpdateMessage message = new VendingMachineFullUpdateMessage();
            message.N3MessageType = (N3MessageType)streamReader.ReadInt32();
            message.Identity = streamReader.ReadIdentity();
            message.Unknown = streamReader.ReadByte();

            message.TypeIdentifier = streamReader.ReadInt32();

            var identityType = (IdentityType)streamReader.ReadInt32();
            int identityInstance = streamReader.ReadInt32();

            message.NpcIdentity = new Identity() { Type = identityType, Instance = identityInstance };

            if (message.NpcIdentity.Instance == 0)
            {
                message.Coordinates = new Vector3();
                message.Coordinates.X = streamReader.ReadSingle();
                message.Coordinates.Y = streamReader.ReadSingle();
                message.Coordinates.Z = streamReader.ReadSingle();
                message.Heading = new Quaternion();
                message.Heading.X = streamReader.ReadSingle();
                message.Heading.Y = streamReader.ReadSingle();
                message.Heading.Z = streamReader.ReadSingle();
                message.Heading.W = streamReader.ReadSingle();
            }
            message.PlayfieldId = streamReader.ReadInt32();
            message.Unknown4 = streamReader.ReadInt32();
            message.Unknown5 = streamReader.ReadInt32();
            message.Unknown6 = streamReader.ReadInt16();

            int x3f1 = streamReader.ReadInt32();
            x3f1 = x3f1 / 0x03f1;
            List<GameTuple<CharacterStat, uint>> temp = new List<GameTuple<CharacterStat, uint>>();
            while (x3f1 > 1)
            {
                var temptuple = new GameTuple<CharacterStat, uint>();
                temptuple.Value1 = (CharacterStat)streamReader.ReadInt32();
                temptuple.Value2 = streamReader.ReadUInt32();
                temp.Add(temptuple);
                x3f1--;
            }
            message.Stats = temp.ToArray();

            message.Unknown7 = streamReader.ReadString(streamReader.ReadInt32()).Replace("\0", "");
            /*int templen = streamReader.ReadInt32(); // String length!!
            message.Unknown7 = "";
            while (templen > 0)
            {
                message.Unknown7 += (char)streamReader.ReadByte();
                templen--;
            }
            message.Unknown7 = message.Unknown7.TrimEnd('\0');*/
            message.Unknown8 = streamReader.ReadInt32();

            if (message.Unknown8 == 2)
            {
                message.Unknown9 = streamReader.ReadInt32();
                x3f1 = streamReader.ReadInt32();
                x3f1 = x3f1 / 0x03f1;
                List<Identity> tempids = new List<Identity>();
                while (x3f1 > 1)
                {
                    identityType = (IdentityType)streamReader.ReadInt32();
                    identityInstance = streamReader.ReadInt32();
                    tempids.Add(new Identity() { Type = identityType, Instance = identityInstance });
                    x3f1--;
                }
                message.Unknown10 = tempids.ToArray();
            }
            message.Unknown11 = streamReader.ReadInt32();
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
                    <VendingMachineFullUpdateMessageSerializer,
                        Func<StreamReader, SerializationContext, PropertyMetaData, object>>(o => o.Deserialize);
            NewExpression serializerExp = Expression.New(this.GetType());
            MethodCallExpression callExp = Expression.Call(
                serializerExp,
                deserializerMethodInfo,
                new Expression[]
                {
                    streamReaderExpression, serializationContextExpression,
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
            if (value == null)
            {
                return;
            }

            var mes = (VendingMachineFullUpdateMessage)value;
            streamWriter.WriteInt32((int)mes.N3MessageType);
            streamWriter.WriteIdentity(mes.Identity);
            streamWriter.WriteByte(mes.Unknown);

            streamWriter.WriteInt32(mes.TypeIdentifier);
            streamWriter.WriteIdentity(mes.NpcIdentity);
            if (mes.NpcIdentity.Instance == 0)
            {
                streamWriter.WriteSingle(mes.Coordinates.X);
                streamWriter.WriteSingle(mes.Coordinates.Y);
                streamWriter.WriteSingle(mes.Coordinates.Z);
                streamWriter.WriteSingle(mes.Heading.X);
                streamWriter.WriteSingle(mes.Heading.Y);
                streamWriter.WriteSingle(mes.Heading.Z);
                streamWriter.WriteSingle(mes.Heading.W);
            }
            streamWriter.WriteInt32(mes.PlayfieldId);
            streamWriter.WriteInt32(mes.Unknown4);
            streamWriter.WriteInt32(mes.Unknown5);
            streamWriter.WriteInt16(mes.Unknown6);

            if (mes.Stats == null)
            {
                streamWriter.WriteInt32(0x3f1);
            }
            else
            {
                int len = mes.Stats.Length;
                len = (len + 1) * 0x3f1;
                streamWriter.WriteInt32(len);

                foreach (GameTuple<CharacterStat, uint> v in mes.Stats)
                {
                    streamWriter.WriteInt32((int)v.Value1);
                    streamWriter.WriteUInt32(v.Value2);
                }
            }

            if (mes.Unknown7 == null)
            {
                streamWriter.WriteInt32(0);
            }
            else
            {
                streamWriter.WriteInt32(mes.Unknown7.Length);
                streamWriter.WriteString(mes.Unknown7);
            }

            streamWriter.WriteInt32(mes.Unknown8);
            streamWriter.WriteInt32(mes.Unknown9);
            streamWriter.WriteInt32((mes.Unknown10.Length + 1) * 0x3f1);
            foreach (Identity id in mes.Unknown10)
            {
                streamWriter.WriteIdentity(id);
            }

            streamWriter.WriteInt32(mes.Unknown11);
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
                    <VendingMachineFullUpdateMessageSerializer,
                        Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(o => o.Serialize);
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

        #endregion
    }
}