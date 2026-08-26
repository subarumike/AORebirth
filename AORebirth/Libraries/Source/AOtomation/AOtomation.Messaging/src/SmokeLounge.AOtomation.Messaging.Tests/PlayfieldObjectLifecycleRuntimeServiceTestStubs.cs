namespace AORebirth.Core.Entities
{
    internal interface ICharacter
    {
    }
}

namespace AORebirth.Interfaces
{
    internal interface IInstancedEntity
    {
    }
}

namespace AORebirth.ObjectManager
{
    using AORebirth.Interfaces;

    internal sealed class Pool
    {
        internal static readonly Pool Instance = new Pool();

        internal void RemoveObject(IInstancedEntity entity)
        {
        }
    }
}

namespace AORebirth.Enums
{
    internal enum DebugInfoDetail
    {
        Engine,
        Network
    }
}

namespace Utility
{
    using AORebirth.Enums;

    internal static class LogUtil
    {
        internal static void Debug(DebugInfoDetail detail, string message)
        {
        }
    }
}
