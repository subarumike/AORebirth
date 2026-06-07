using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmokeLounge.AOtomation.Messaging.Serialization.Serializers.Custom
{
    using System;
    using System.Linq.Expressions;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    public class FollowInfoSerializer : ISerializer
    {       
        #region Fields

        private readonly Type type;

        #endregion

        #region Constructors and Destructors

        public FollowInfoSerializer()
        {
            this.type = typeof(FollowInfo);
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

        public object Deserialize(
            StreamReader streamReader,
            SerializationContext serializationContext,
            PropertyMetaData propertyMetaData = null)
        {
            byte infoType = streamReader.ReadByte();
            if (infoType == 1)
            {
                var followCoordinateInfo = new FollowCoordinateInfo();
                followCoordinateInfo.FollowInfoType = 1;
                followCoordinateInfo.MoveMode = streamReader.ReadByte();
                followCoordinateInfo.CoordinateCount = streamReader.ReadByte();
                for (int i = 0; i < followCoordinateInfo.CoordinateCount; i++)
                {
                    followCoordinateInfo.Coordinates.Add(this.ReadVector3(streamReader));
                }
                if (followCoordinateInfo.Coordinates.Count > 0)
                {
                    followCoordinateInfo.CurrentCoordinates = followCoordinateInfo.Coordinates[0];
                    followCoordinateInfo.EndCoordinates =
                        followCoordinateInfo.Coordinates[followCoordinateInfo.Coordinates.Count - 1];
                }
                return followCoordinateInfo;
            }
            if (infoType == 2)
            {
                long positionAfterInfoType = streamReader.Position;
                byte moveType = streamReader.ReadByte();
                if ((moveType == 21) && ((streamReader.Length - streamReader.Position) >= 37))
                {
                    var followStopInfo = new FollowStopInfo();
                    followStopInfo.FollowInfoType = 2;
                    followStopInfo.MoveType = moveType;
                    followStopInfo.Unknown1 = streamReader.ReadInt32();
                    followStopInfo.Unknown2 = streamReader.ReadInt32();
                    followStopInfo.Unknown3 = streamReader.ReadInt32();
                    followStopInfo.Coordinates = this.ReadVector3(streamReader);
                    followStopInfo.Flag = streamReader.ReadByte();
                    followStopInfo.ConfirmCoordinates = this.ReadVector3(streamReader);
                    return followStopInfo;
                }
                if ((moveType == 25) && ((streamReader.Length - streamReader.Position) >= 25))
                {
                    var followPositionInfo = new FollowPositionInfo();
                    followPositionInfo.FollowInfoType = 2;
                    followPositionInfo.MoveType = moveType;
                    followPositionInfo.Unknown1 = streamReader.ReadInt32();
                    followPositionInfo.Unknown2 = streamReader.ReadInt32();
                    followPositionInfo.Unknown3 = streamReader.ReadInt32();
                    followPositionInfo.Coordinates = this.ReadVector3(streamReader);
                    followPositionInfo.Unknown4 = streamReader.ReadByte();
                    return followPositionInfo;
                }

                streamReader.Position = positionAfterInfoType;
                var followTargetInfo = new FollowTargetInfo();
                followTargetInfo.FollowInfoType = 2;
                followTargetInfo.MoveType = streamReader.ReadByte();
                IdentityType itype = (IdentityType)streamReader.ReadInt32();

                followTargetInfo.Target = new Identity() { Type = itype, Instance = streamReader.ReadInt32() };
                followTargetInfo.Dummy = streamReader.ReadByte();
                followTargetInfo.Dummy1 = streamReader.ReadInt32();
                followTargetInfo.X = streamReader.ReadSingle();
                followTargetInfo.Y = streamReader.ReadSingle();
                followTargetInfo.Z = streamReader.ReadSingle();
                return followTargetInfo;
            }

            streamReader.Position = streamReader.Position - 1;
            return null;
        }

        public void Serialize(
            StreamWriter streamWriter,
            SerializationContext serializationContext,
            object value,
            PropertyMetaData propertyMetaData = null)
        {            if (value == null)
            {
                return;
            }

            var ftinfo = value as FollowTargetInfo;
            if (ftinfo != null)
            {
                streamWriter.WriteByte(ftinfo.FollowInfoType);
                streamWriter.WriteByte(ftinfo.MoveType);
                streamWriter.WriteInt32((int)ftinfo.Target.Type);
                streamWriter.WriteInt32(ftinfo.Target.Instance);
                streamWriter.WriteByte(ftinfo.Dummy);
                streamWriter.WriteInt32(ftinfo.Dummy1);
                streamWriter.WriteSingle(ftinfo.X);
                streamWriter.WriteSingle(ftinfo.Y);
                streamWriter.WriteSingle(ftinfo.Z);
            }
            var fpinfo = value as FollowPositionInfo;
            if (fpinfo != null)
            {
                streamWriter.WriteByte(fpinfo.FollowInfoType);
                streamWriter.WriteByte(fpinfo.MoveType);
                streamWriter.WriteInt32(fpinfo.Unknown1);
                streamWriter.WriteInt32(fpinfo.Unknown2);
                streamWriter.WriteInt32(fpinfo.Unknown3);
                this.WriteVector3(streamWriter, fpinfo.Coordinates);
                streamWriter.WriteByte(fpinfo.Unknown4);
            }
            var fsinfo = value as FollowStopInfo;
            if (fsinfo != null)
            {
                Vector3 coordinates = fsinfo.Coordinates;
                Vector3 confirmCoordinates = fsinfo.ConfirmCoordinates;
                if (confirmCoordinates == null)
                {
                    confirmCoordinates = coordinates;
                }

                streamWriter.WriteByte(fsinfo.FollowInfoType);
                streamWriter.WriteByte(fsinfo.MoveType);
                streamWriter.WriteInt32(fsinfo.Unknown1);
                streamWriter.WriteInt32(fsinfo.Unknown2);
                streamWriter.WriteInt32(fsinfo.Unknown3);
                this.WriteVector3(streamWriter, coordinates);
                streamWriter.WriteByte(fsinfo.Flag);
                this.WriteVector3(streamWriter, confirmCoordinates);
            }
            var fcinfo = value as FollowCoordinateInfo;
            if (fcinfo != null)
            {
                IList<Vector3> coordinates = this.GetCoordinates(fcinfo);
                streamWriter.WriteByte(fcinfo.FollowInfoType);
                streamWriter.WriteByte(fcinfo.MoveMode);
                streamWriter.WriteByte((byte)coordinates.Count);
                foreach (Vector3 coordinate in coordinates)
                {
                    this.WriteVector3(streamWriter, coordinate);
                }
            }
        }

        private IList<Vector3> GetCoordinates(FollowCoordinateInfo fcinfo)
        {
            if ((fcinfo.Coordinates != null) && (fcinfo.Coordinates.Count > 0))
            {
                return fcinfo.Coordinates;
            }

            var result = new List<Vector3>();
            if (fcinfo.CurrentCoordinates != null)
            {
                result.Add(fcinfo.CurrentCoordinates);
            }
            if (fcinfo.EndCoordinates != null)
            {
                result.Add(fcinfo.EndCoordinates);
            }
            return result;
        }

        private Vector3 ReadVector3(StreamReader streamReader)
        {
            return new Vector3
                   {
                       X = streamReader.ReadSingle(),
                       Y = streamReader.ReadSingle(),
                       Z = streamReader.ReadSingle()
                   };
        }

        private void WriteVector3(StreamWriter streamWriter, Vector3 value)
        {
            if (value == null)
            {
                value = new Vector3();
            }

            streamWriter.WriteSingle(value.X);
            streamWriter.WriteSingle(value.Y);
            streamWriter.WriteSingle(value.Z);
        }

        public Expression DeserializerExpression(
            ParameterExpression streamReaderExpression, 
            ParameterExpression serializationContextExpression, 
            Expression assignmentTargetExpression, 
            PropertyMetaData propertyMetaData)
        {
            var deserializerMethodInfo =
                ReflectionHelper
                    .GetMethodInfo
                    <FollowInfoSerializer, Func<StreamReader, SerializationContext, PropertyMetaData, object>>(
                        o => o.Deserialize);
            var serializerExp = Expression.New(this.GetType());
            var callExp = Expression.Call(
                serializerExp, 
                deserializerMethodInfo, 
                new Expression[]
                    {
                        streamReaderExpression, serializationContextExpression, 
                        Expression.Constant(propertyMetaData, typeof(PropertyMetaData))
                    });

            var assignmentExp = Expression.Assign(
                assignmentTargetExpression, Expression.TypeAs(callExp, assignmentTargetExpression.Type));
            return assignmentExp;
        }
                public Expression SerializerExpression(
            ParameterExpression streamWriterExpression, 
            ParameterExpression serializationContextExpression, 
            Expression valueExpression, 
            PropertyMetaData propertyMetaData)
        {
            var serializerMethodInfo =
                ReflectionHelper
                    .GetMethodInfo
                    <FollowInfoSerializer, Action<StreamWriter, SerializationContext, object, PropertyMetaData>>(o => o.Serialize);
            var serializerExp = Expression.New(this.GetType());
            var callExp = Expression.Call(
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
