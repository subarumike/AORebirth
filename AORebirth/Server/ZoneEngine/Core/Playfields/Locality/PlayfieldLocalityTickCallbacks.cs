namespace ZoneEngine.Core.Playfields.Locality
{
    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;

    using SmokeLounge.AOtomation.Messaging.GameData;

    internal sealed class PlayfieldLocalityTickCallbacks
    {
        internal Func<Identity, bool> HasPendingDeadNpcDespawn { get; set; }

        internal Func<ICharacter, bool> ProcessDeadNpcDespawn { get; set; }

        internal Action<ICharacter, double> ProcessCharacterTick { get; set; }

        internal Action<ICharacter> ProcessNpcPatrolTick { get; set; }

        internal Action<ICharacter> ProcessFollow { get; set; }

        internal Action<ICharacter> ProcessPlayerCollision { get; set; }

        internal Func<IEnumerable<ICharacter>> GetAllCharacters { get; set; }

        internal Func<IEnumerable<ICharacter>> GetConnectedPlayers { get; set; }
    }
}
