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
    using System.Linq.Expressions;
    using System.Reflection;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    #endregion

    public class GenericCmdSerializer : ISerializer
    {
        private readonly Type type;

        public GenericCmdSerializer()
        {
            this.type = typeof(GenericCmdMessage);
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
            var mess = new GenericCmdMessage();
            mess.N3MessageType = (N3MessageType)streamReader.ReadInt32();
            mess.Identity = streamReader.ReadIdentity();
            mess.Unknown = streamReader.ReadByte();
            mess.Temp1 = streamReader.ReadInt32();
            mess.Count = streamReader.ReadInt32();
            mess.Action = (GenericCmdAction)streamReader.ReadInt32();
            mess.Temp4 = streamReader.ReadInt32();
            mess.User = streamReader.ReadIdentity();
            int len = 1;
            if (mess.Action == GenericCmdAction.UseItemOnItem)
            {
                len = 2;
            }

            mess.Target = new Identity[len];
            for (int i = 0; i < mess.Target.Length; i++)
            {
                mess.Target[i] = streamReader.ReadIdentity();
            }
            return mess;
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
                    <GenericCmdSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(
                        o => o.Deserialize);
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
            var mess = (GenericCmdMessage)value;
            streamWriter.WriteInt32((int)mess.N3MessageType);
            streamWriter.WriteIdentity(mess.Identity);
            streamWriter.WriteByte(mess.Unknown);
            streamWriter.WriteInt32(mess.Temp1);
            streamWriter.WriteInt32(mess.Count);
            streamWriter.WriteInt32((int)mess.Action);
            streamWriter.WriteInt32(mess.Temp4);
            streamWriter.WriteIdentity(mess.User);
            foreach (Identity id in mess.Target)
            {
                streamWriter.WriteIdentity(id);
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
                    <GenericCmdSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(
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