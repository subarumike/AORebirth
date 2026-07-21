namespace AORebirth.Core.Items
{
    #region Usings ...

    using AORebirth.Core.Functions;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    #endregion

    /// <summary>
    /// Capture-backed appearance for Awakened Burden of Competence (AOID 302730).
    /// Capture 20260718-125957 (live): social VisualFlags=59 shows BackMesh 245106 with
    /// OverrideTexture 302715 (blue). Unequip removes that mesh; re-equip restores it.
    /// items.dat only stores BackMesh mesh ids (no override), so worn look stays orange
    /// unless 302715 is injected.
    /// </summary>
    public static class AwakenedBurdenOfCompetenceAppearance
    {
        public const int ItemId = 302730;

        public const int CapturedBackMeshId = 245106;

        public const int CapturedOverrideTextureId = 302715;

        public const int IconId = 302952;

        public static bool IsAwakenedBurden(IItem item)
        {
            return item != null && (item.HighID == ItemId || item.LowID == ItemId);
        }

        public static bool TryResolveIcon(IItem item, out int iconId)
        {
            if (!IsAwakenedBurden(item))
            {
                iconId = 0;
                return false;
            }

            iconId = IconId;
            return true;
        }

        /// <summary>
        /// When template BackMesh is mesh-only (245106), prepend capture override 302715
        /// so backmesh.Execute receives (overrideTexture, meshId, slot).
        /// </summary>
        public static void TryInjectBackMeshOverride(Function function, IItem item)
        {
            if (function == null
                || function.FunctionType != (int)FunctionType.BackMesh
                || function.Arguments == null
                || function.Arguments.Values == null
                || !IsAwakenedBurden(item))
            {
                return;
            }

            if (function.Arguments.Values.Count != 1)
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

            if (meshId != CapturedBackMeshId)
            {
                return;
            }

            function.Arguments.Values.Insert(0, new MessagePackObject(CapturedOverrideTextureId));
        }
    }
}
