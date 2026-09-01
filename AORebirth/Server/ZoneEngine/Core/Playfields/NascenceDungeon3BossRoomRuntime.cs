namespace AORebirth.Core.Playfields
{
    using System;

    using AORebirth.Core.Entities;

    using Utility;
    using ZoneEngine.Core;

    /// <summary>
    /// Force Havaris SCFU to players in boss wing (capture 20260830-140240).
    /// </summary>
    internal static class NascenceDungeon3BossRoomRuntime
    {
        internal static void ForceHavarisVisible(Playfield playfield, ICharacter viewer)
        {
            if (playfield == null || viewer == null)
            {
                return;
            }

            foreach (ICharacter active in playfield.EnumerateActiveCharacters())
            {
                if (active == null
                    || !string.Equals(active.Name, "Havaris", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                playfield.ForceCharacterVisibilityToRecipient(active, viewer, true);
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "NascenceDungeon3 Havaris SCFU char={0} havaris={1}",
                        viewer.Identity.Instance,
                        active.Identity.Instance));
                return;
            }
        }
    }
}
