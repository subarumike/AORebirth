namespace AORebirth.Core.Playfields
{
    using System;

    using AORebirth.Core.Entities;

    using Utility;
    using ZoneEngine.Core;

    /// <summary>
    /// Force Havaris SCFU to players in boss wing (capture 20260823-171238 @ 125,64,174).
    /// </summary>
    internal static class NascenceDungeon2BossRoomRuntime
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

                playfield.ForceCharacterVisibilityToRecipient(active, viewer);
                LogUtil.Debug(
                    DebugInfoDetail.Zoning,
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "NascenceDungeon2 Havaris SCFU char={0} havaris={1}",
                        viewer.Identity.Instance,
                        active.Identity.Instance));
                return;
            }
        }
    }
}
