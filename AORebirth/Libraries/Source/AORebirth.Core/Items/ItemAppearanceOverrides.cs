namespace AORebirth.Core.Items
{
    using AORebirth.Core.Functions;
    using AORebirth.Core.GameData;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    internal static class ItemAppearanceOverrides
    {
        internal static void TryInjectBackMeshOverride(Function function, IItem item)
        {
            if (item == null || function == null
                || function.FunctionType != (int)FunctionType.BackMesh
                || function.Arguments == null
                || function.Arguments.Values == null
                || function.Arguments.Values.Count != 1)
            {
                return;
            }

            int meshId;
            try
            {
                meshId = function.Arguments.Values[0].AsInt32();
            }
            catch
            {
                return;
            }

            // A mesh-only BackMesh has no override texture.
            if (ItemBehaviorBindings.Current.TryResolveWornMeshOverride(item.LowID, item.HighID, meshId, 0, out int textureId))
            {
                function.Arguments.Values.Insert(0, new MessagePackObject(textureId));
            }
        }
    }
}
