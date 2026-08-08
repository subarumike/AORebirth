namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using SmokeLounge.AOtomation.Messaging.GameData;

    #endregion

    /// <summary>
    /// Capture 20260806-202421 — ICC HQ proximity → Sunrise Station lobby (PF 6002),
    /// then Orbital Apartment Door UseItemOnItem → luxury apartment instance 0x19E000.
    /// </summary>
    public static class LuxuryApartmentSunriseRules
    {
        public const int IccHqPlayfieldId = 655;

        public const int SunriseStationPlayfieldId = 6002;

        public const int LuxuryApartmentPlayfieldId = 1695744;

        public const int LuxuryApartmentBuildingInstance = 0x005E3820;

        /// <summary>
        /// Capture 20260806-213039: unique apartment access card (same name as claim key, different id).
        /// Must remain in inventory for later door entry.
        /// </summary>
        public const int CapturedApartmentAccessCardTemplateId = 281129;

        /// <summary>
        /// Capture 20260806-213039 TemplateAction: Phasefront Classic - Charon (promotional vehicle key).
        /// </summary>
        public const int CapturedPhasefrontClassicCharonTemplateId = 281570;

        public const int CapturedPhasefrontClassicCharonQuality = 100;

        public const int CapturedAccessCardQuality = 1;

        /// <summary>
        /// Capture FormatFeedback wire after claim-key UseItemOnItem on Orbital Apartment Door.
        /// </summary>
        public const string CapturedPromotionalVehicleKeyFeedback =
            "~&!!!\":!!!)<s0You have received your promotional vehicle key.";

        // Capture SIFU Flags for access card 281129.
        public const uint CapturedAccessCardFlags = 201326593u;

        // Door:C0001772 / C001 / C002 / C003 on PF 6002.
        public const int OrbitalApartmentDoorC000 = unchecked((int)0xC0001772);

        public const int OrbitalApartmentDoorC001 = unchecked((int)0xC0011772);

        public const int OrbitalApartmentDoorC002 = unchecked((int)0xC0021772);

        public const int OrbitalApartmentDoorC003 = unchecked((int)0xC0031772);

        // Door:C0041772 "To Rubi-Ka".
        public const int ToRubiKaDoorInstance = unchecked((int)0xC0041772);

        // Door:109DE493 interior exit.
        public const int ApartmentExitDoorInstance = unchecked((int)0x109DE493);

        // Capture 20260806-202421 MailTerminal:79A08A; 20260806-213039 uses 79A84D.
        public const int ApartmentMailTerminalInstance = unchecked((int)0x0079A08A);

        public const int ApartmentMailTerminalInstanceAlt = unchecked((int)0x0079A84D);

        public const float ApartmentMailTerminalX = 512f;

        public const float ApartmentMailTerminalY = 51.7f;

        public const float ApartmentMailTerminalZ = 482f;

        public const float LobbyEntrySourceX = 3110.824f;

        public const float LobbyEntrySourceY = 51.49423f;

        public const float LobbyEntrySourceZ = 867.0231f;

        public const float LobbyEntryRadius = 2.5f;

        public const float LobbyEntryVerticalTolerance = 4.0f;

        public const float LobbyLandingX = 76.68774f;

        public const float LobbyLandingY = 160.215f;

        public const float LobbyLandingZ = 360.0767f;

        public const float LobbyLandingHeadingY = 0.4226208f;

        public const float LobbyLandingHeadingW = 0.9063066f;

        // N3Teleport envelope at door (capture success path).
        public const float ApartmentEntryEnvelopeX = 100.1977f;

        public const float ApartmentEntryEnvelopeY = 161.215f;

        public const float ApartmentEntryEnvelopeZ = 347.853f;

        public const float ApartmentEntryEnvelopeHeadingY = -0.9988338f;

        public const float ApartmentEntryEnvelopeHeadingW = 0.0482818f;

        public const float ApartmentLandingX = 500.0127f;

        public const float ApartmentLandingY = 51.71431f;

        public const float ApartmentLandingZ = 499.8001f;

        public const float ApartmentLandingHeadingY = 0.9999905f;

        public const float ApartmentLandingHeadingW = 0.004345933f;

        // Capture 20260806-210903: walk-out trigger near apartment north side
        // (N3Teleport fires at ~500/51.71/501.34 — no GenericCmd Use).
        public const float ApartmentExitTriggerX = 499.975f;

        public const float ApartmentExitTriggerY = 51.71487f;

        public const float ApartmentExitTriggerZ = 501.3361f;

        public const float ApartmentExitTriggerRadius = 2.5f;

        public const float ApartmentExitTriggerVerticalTolerance = 4.0f;

        // Capture 20260806-210903 SCFU landing outside Orbital Apartment Door C000.
        public const float ApartmentExitLobbyX = 99.8548f;

        public const float ApartmentExitLobbyY = 161.215f;

        public const float ApartmentExitLobbyZ = 347.1255f;

        public const float ApartmentExitLobbyHeadingY = -0.008719266f;

        public const float ApartmentExitLobbyHeadingW = 0.999962f;

        public static bool IsOrbitalApartmentDoor(Identity target)
        {
            if (target.Type != IdentityType.Door)
            {
                return false;
            }

            int instance = target.Instance;
            return instance == OrbitalApartmentDoorC000
                   || instance == OrbitalApartmentDoorC001
                   || instance == OrbitalApartmentDoorC002
                   || instance == OrbitalApartmentDoorC003;
        }

        public static bool IsLuxuryApartmentPlayfield(int playfieldInstance)
        {
            return LuxuryApartmentInstanceRuntime.IsLuxuryApartmentPlayfield(playfieldInstance);
        }

        /// <summary>
        /// Lobby Orbital Apartment Door positions (capture DYNEL-SPAWNED on PF 6002).
        /// Used for inventory-card proximity entry (20260806-220142 — no GenericCmd).
        /// </summary>
        public static readonly float[][] OrbitalApartmentDoorProximitySpots =
        {
            // Door:C0001772
            new[] { 99.93325f, 161.178f, 342.6272f },
            // Door:C0011772
            new[] { 140.307f, 161.209f, 383.1037f },
            // Door:C0021772
            new[] { 59.45443f, 161.2106f, 383.0081f },
            // Door:C0031772
            new[] { 99.92036f, 161.2588f, 423.5085f }
        };

        public const float OrbitalApartmentDoorProximityRadius = 3.0f;

        public const float OrbitalApartmentDoorProximityVerticalTolerance = 4.0f;

        // Capture 20260806-213039 / 20260806-221532 apartment terminals.
        public const int ApartmentGridEnterTerminalInstance = unchecked((int)0x57C12A71);

        public const int ApartmentBankTerminalInstance = unchecked((int)0x57C12A72);

        // First capture / older PAF layout aliases.
        public const int ApartmentGridEnterTerminalInstanceLegacy = unchecked((int)0x57C114EA);

        public const int ApartmentBankTerminalInstanceLegacy = unchecked((int)0x57C114EB);

        public const int CapturedGridPlayfieldId = 152;

        // Capture 20260806-221532 apartment → Grid N3Teleport secondary identities.
        public const int CapturedApartmentGridChangePlayfieldInstance = 0x00166801;

        public const int CapturedApartmentGridPlayfield3Instance = 0x00260098;

        public static bool IsApartmentBankTerminal(Identity target)
        {
            if (target.Type != IdentityType.Terminal)
            {
                return false;
            }

            return target.Instance == ApartmentBankTerminalInstance
                   || target.Instance == ApartmentBankTerminalInstanceLegacy;
        }

        public static bool IsApartmentGridEnterTerminal(Identity target)
        {
            if (target.Type != IdentityType.Terminal)
            {
                return false;
            }

            return target.Instance == ApartmentGridEnterTerminalInstance
                   || target.Instance == ApartmentGridEnterTerminalInstanceLegacy;
        }

        public static bool IsApartmentMailTerminal(Identity target)
        {
            if (target.Type != IdentityType.MailTerminal)
            {
                return false;
            }

            return target.Instance == ApartmentMailTerminalInstance
                   || target.Instance == ApartmentMailTerminalInstanceAlt;
        }
    }
}
