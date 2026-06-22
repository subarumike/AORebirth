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

namespace ZoneEngine.Core.Functions.GameFunctions
{
    #region Usings ...

    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Statels;
    using AORebirth.Core.Vector;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using Utility;

    using ZoneEngine.Core.Playfields;

    using Quaternion = AORebirth.Core.Vector.Quaternion;
    using Vector3 = AORebirth.Core.Vector.Vector3;

    #endregion

    internal class exitproxyplayfield : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.ExitProxyPlayfield;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            uint externalDoorInstance = self.Stats[StatIds.externaldoorinstance].BaseValue;
            int externalPlayfieldId = self.Stats[StatIds.externalplayfieldinstance].Value;

            StatelData door =
                PlayfieldLoader.PFData[externalPlayfieldId].Statels.FirstOrDefault(
                    x =>
                        (uint)x.Identity.Instance == externalDoorInstance
                        && (x.Identity.Type == IdentityType.Door /*|| x.Identity.Type==IdentityType.MissionEntrance*/));
            if (door != null)
            {
                Vector3 v = new Vector3(door.X, door.Y, door.Z);

                Quaternion q = new Quaternion(door.HeadingX, door.HeadingY, door.HeadingZ, door.HeadingW);

                Quaternion.Normalize(q);
                Vector3 n = (Vector3)q.RotateVector3(Vector3.AxisZ);

                v.x += n.x * 2.5;
                v.z += n.z * 2.5;
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    string.Format(
                        "ExitProxyPlayfield caller={0} internal={1} currentPf={2} current=({3:F2},{4:F2},{5:F2}) externalDoor={6:X8} externalPf={7} dest=({8:F2},{9:F2},{10:F2})",
                        caller.Identity.ToString(true),
                        self.Identity.ToString(true),
                        self.Playfield.Identity.Instance,
                        ((Dynel)self).RawCoordinates.X,
                        ((Dynel)self).RawCoordinates.Y,
                        ((Dynel)self).RawCoordinates.Z,
                        externalDoorInstance,
                        externalPlayfieldId,
                        v.x,
                        v.y,
                        v.z));
                self.Playfield.Teleport(
                    (Dynel)self,
                    new Coordinate(v),
                    q,
                    new Identity() { Type = IdentityType.Playfield, Instance = externalPlayfieldId });
            }
            return door != null;
        }
    }

    internal class instancedplayercity : FunctionPrototype
    {
        private const int CapturedPrivateCityPlayfieldId = 1067112;

        private const int CapturedPrivateCityOrganizationInstance = 1370122;

        public override FunctionType FunctionId
        {
            get
            {
                return FunctionType.InstancedPlayerCity;
            }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            ICharacter character = self as ICharacter;
            Dynel dynel = self as Dynel;
            if (character == null || dynel == null || character.Playfield == null)
            {
                return false;
            }

            int organizationInstance = character.Stats[StatIds.clan].Value;
            int destinationPlayfieldId = ResolvePrivateCityPlayfieldId(organizationInstance);
            if (destinationPlayfieldId <= 0)
            {
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    string.Format(
                        "InstancedPlayerCity skipped character={0} org={1} reason=no_city",
                        character.Identity.ToString(true),
                        organizationInstance));
                return false;
            }

            var destination = new Coordinate(211.55756f, 3.77500f, 186.51588f);
            var heading = new Quaternion(0.0f, -0.9575281f, 0.0f, 0.28834003f);

            character.StopMovement();
            character.Playfield.Teleport(
                dynel,
                destination,
                heading,
                new Identity { Type = IdentityType.Playfield, Instance = destinationPlayfieldId });

            LogUtil.Debug(
                DebugInfoDetail.Zoning,
                string.Format(
                    "InstancedPlayerCity teleport character={0} org={1} destPf={2} dest=({3:F3},{4:F3},{5:F3}) evidence=live_capture_20260622-093540",
                    character.Identity.ToString(true),
                    organizationInstance,
                    destinationPlayfieldId,
                    destination.x,
                    destination.y,
                    destination.z));

            return true;
        }

        private static int ResolvePrivateCityPlayfieldId(int organizationInstance)
        {
            if (organizationInstance <= 0)
            {
                return 0;
            }

            try
            {
                var organization = OrganizationDao.Instance.Get(organizationInstance);
                if (organization != null && organization.CityId > 0)
                {
                    return organization.CityId;
                }
            }
            catch
            {
            }

            return organizationInstance == CapturedPrivateCityOrganizationInstance
                       ? CapturedPrivateCityPlayfieldId
                       : 0;
        }
    }
}
