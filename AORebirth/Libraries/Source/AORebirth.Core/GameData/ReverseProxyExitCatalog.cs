namespace AORebirth.Core.GameData
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Json;

    /// <summary>Explicit destinations for reverse exits synthesized from teleport proxies.</summary>
    public sealed class ReverseProxyExitCatalog
    {
        // Packed AO door identities store the playfield in the low 16 bits.
        private const uint DoorKind = 0xC0000000;
        private const uint DoorKindMask = 0xFF000000;
        private const uint PlayfieldMask = 0xFFFF;

        private readonly Dictionary<int, int> doors = new Dictionary<int, int>();

        private ReverseProxyExitCatalog(RoutesData data)
        {
            if (data == null || data.ReverseProxyExitDestinations == null || data.ReverseProxyExitDestinations.Length == 0)
            {
                throw new InvalidDataException("TeleportRoutes lacks ReverseProxyExitDestinations.");
            }

            foreach (ExitData row in data.ReverseProxyExitDestinations)
            {
                if (row == null || row.Playfield <= 0 || row.Playfield > PlayfieldMask
                    || !uint.TryParse(row.DoorInstance, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint instance)
                    || (instance & DoorKindMask) != DoorKind || (instance & PlayfieldMask) != row.Playfield
                    || doors.ContainsKey(row.Playfield))
                {
                    throw new InvalidDataException("TeleportRoutes contains an invalid or duplicate reverse proxy exit.");
                }

                doors.Add(row.Playfield, unchecked((int)instance));
            }
        }

        public static ReverseProxyExitCatalog Load(string path)
        {
            using (FileStream input = File.OpenRead(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(RoutesData));
                return new ReverseProxyExitCatalog((RoutesData)serializer.ReadObject(input));
            }
        }

        public IEnumerable<int> DestinationPlayfields => doors.Keys;

        public void ValidateDestinationReferences(Func<int, int, bool> doorExists)
        {
            if (doorExists == null)
            {
                throw new ArgumentNullException(nameof(doorExists));
            }

            foreach (KeyValuePair<int, int> destination in doors)
            {
                if (!doorExists(destination.Key, destination.Value))
                {
                    throw new InvalidDataException(
                        $"TeleportRoutes references missing reverse proxy door {destination.Value:X8} in playfield {destination.Key}.");
                }
            }
        }

        public void ApplyDestinationOverride(int resolvedPlayfield, ref int playfield, ref int doorInstance)
        {
            if (doors.TryGetValue(resolvedPlayfield, out int authoredDoor))
            {
                playfield = resolvedPlayfield;
                doorInstance = authoredDoor;
            }
        }

        [DataContract]
        private sealed class RoutesData
        {
            [DataMember(IsRequired = true)]
            public ExitData[] ReverseProxyExitDestinations { get; set; }
        }

        [DataContract]
        private sealed class ExitData
        {
            [DataMember(IsRequired = true)]
            public int Playfield { get; set; }

            [DataMember(IsRequired = true)]
            public string DoorInstance { get; set; }
        }
    }
}
