// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TemplateActionType.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the TemplateActionType type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Messages.N3Messages
{
    /// <summary>
    /// Wire values for <see cref="TemplateActionMessage.Action"/>.
    /// Use/Wear/Remove match <c>ActionType</c> ToUse/ToWear/ToRemove.
    /// </summary>
    public enum TemplateActionType
    {
        /// <summary>Successful inventory/equipment use announce. Same as ActionType.ToUse.</summary>
        Use = 3,

        /// <summary>Item finished equipping onto a wear page. Same as ActionType.ToWear.</summary>
        Wear = 6,

        /// <summary>Item finished unequipping from a wear page. Same as ActionType.ToRemove.</summary>
        Remove = 7,

        /// <summary>Trade window item render. Capture 0x55.</summary>
        TradeRender = 0x55,

        /// <summary>Overflow window grant announce. Capture 87.</summary>
        Overflow = 87,

        /// <summary>
        /// Perk action performed: action template low/high and QL, Unknown1 = 1, Placement = performer,
        /// Unknown3/Unknown4 = target type/instance. Live capture 2026-10-02T03:54:58Z (Impale, Gore, Blur).
        /// </summary>
        PerkAction = 0x20,
    }
}
