using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AORebirth.MissionEvidence
{
    internal sealed class SecondarySliderSetting
    {
        internal string RequestedToken { get; private set; }
        internal string SemanticState { get; private set; }
        internal int? SemanticValue { get; private set; }
        internal byte RawValue { get; private set; }
        internal string Resolution { get; private set; }

        private SecondarySliderSetting(
            string requestedToken,
            string semanticState,
            int? semanticValue,
            byte rawValue,
            string resolution)
        {
            RequestedToken = requestedToken;
            SemanticState = semanticState;
            SemanticValue = semanticValue;
            RawValue = rawValue;
            Resolution = resolution;
        }

        internal static SecondarySliderSetting FromSemantic(string requestedToken, int value)
        {
            byte rawValue = value == 0 ? (byte)255 : unchecked((byte)value);
            string semanticState = value == -100
                ? "FULL_LEFT"
                : value == 0
                    ? "CENTER"
                    : value == 100
                        ? "FULL_RIGHT"
                        : "SIGNED_VALUE";
            return new SecondarySliderSetting(
                requestedToken,
                semanticState,
                value,
                rawValue,
                "CANONICAL_SIGNED_VALUE_TO_NATIVE_BYTE");
        }

        internal static SecondarySliderSetting FromRaw(string requestedToken, byte rawValue)
        {
            return new SecondarySliderSetting(
                requestedToken,
                "EXACT_RAW",
                null,
                rawValue,
                "EXACT_NATIVE_BYTE");
        }

        internal static bool TryParse(string token, out SecondarySliderSetting setting, out string error)
        {
            setting = null;
            error = null;
            if (string.IsNullOrWhiteSpace(token))
            {
                error = "SECONDARY_SLIDER_VALUE_MISSING";
                return false;
            }

            string normalized = token.Trim().ToUpperInvariant();
            if (normalized == "FULL_LEFT")
            {
                setting = FromSemantic(token, -100);
                return true;
            }
            if (normalized == "CENTER" || normalized == "CENTRE")
            {
                setting = FromSemantic(token, 0);
                return true;
            }
            if (normalized == "FULL_RIGHT")
            {
                setting = FromSemantic(token, 100);
                return true;
            }
            if (normalized.StartsWith("RAW:", StringComparison.Ordinal))
            {
                byte raw;
                if (!byte.TryParse(normalized.Substring(4), NumberStyles.Integer, CultureInfo.InvariantCulture, out raw))
                {
                    error = "SECONDARY_SLIDER_RAW_VALUE_OUT_OF_BYTE_RANGE";
                    return false;
                }
                setting = FromRaw(token, raw);
                return true;
            }

            int signed;
            if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out signed)
                || signed < -100
                || signed > 100)
            {
                error = "SECONDARY_SLIDER_SEMANTIC_VALUE_OUT_OF_RANGE_-100_TO_100";
                return false;
            }
            setting = FromSemantic(token, signed);
            return true;
        }

        internal IDictionary<string, object> ToPayload()
        {
            return new Dictionary<string, object>
            {
                ["requested_token"] = RequestedToken,
                ["semantic_state"] = SemanticState,
                ["semantic_value"] = SemanticValue,
                ["native_raw_value"] = RawValue,
                ["resolution"] = Resolution
            };
        }

    }

    internal sealed class NativeMissionSliderValues
    {
        internal byte Difficulty { get; private set; }
        internal byte GoodBad { get; private set; }
        internal byte OrderChaos { get; private set; }
        internal byte OpenHidden { get; private set; }
        internal byte PhysicalMystical { get; private set; }
        internal byte HeadonStealth { get; private set; }
        internal byte CreditsXp { get; private set; }

        internal NativeMissionSliderValues(
            byte difficulty,
            byte goodBad,
            byte orderChaos,
            byte openHidden,
            byte physicalMystical,
            byte headonStealth,
            byte creditsXp)
        {
            Difficulty = difficulty;
            GoodBad = goodBad;
            OrderChaos = orderChaos;
            OpenHidden = openHidden;
            PhysicalMystical = physicalMystical;
            HeadonStealth = headonStealth;
            CreditsXp = creditsXp;
        }

        internal bool Matches(NativeMissionSliderValues other)
        {
            return other != null
                && Difficulty == other.Difficulty
                && GoodBad == other.GoodBad
                && OrderChaos == other.OrderChaos
                && OpenHidden == other.OpenHidden
                && PhysicalMystical == other.PhysicalMystical
                && HeadonStealth == other.HeadonStealth
                && CreditsXp == other.CreditsXp;
        }

        internal string CanonicalString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "difficulty={0};good_bad={1};order_chaos={2};open_hidden={3};physical_mystical={4};headon_stealth={5};credits_xp={6}",
                Difficulty,
                GoodBad,
                OrderChaos,
                OpenHidden,
                PhysicalMystical,
                HeadonStealth,
                CreditsXp);
        }

        internal IDictionary<string, object> ToPayload()
        {
            return new Dictionary<string, object>
            {
                ["difficulty"] = Difficulty,
                ["good_bad"] = GoodBad,
                ["order_chaos"] = OrderChaos,
                ["open_hidden"] = OpenHidden,
                ["physical_mystical"] = PhysicalMystical,
                ["headon_stealth"] = HeadonStealth,
                ["credits_xp"] = CreditsXp
            };
        }

        internal byte[] ToRawByteArray()
        {
            return new[]
            {
                Difficulty,
                GoodBad,
                OrderChaos,
                OpenHidden,
                PhysicalMystical,
                HeadonStealth,
                CreditsXp
            };
        }
    }

    internal sealed class MissionSliderState
    {
        internal int DifficultyDetent { get; private set; }
        internal string PresetName { get; private set; }
        internal SecondarySliderSetting GoodBad { get; private set; }
        internal SecondarySliderSetting OrderChaos { get; private set; }
        internal SecondarySliderSetting OpenHidden { get; private set; }
        internal SecondarySliderSetting PhysicalMystical { get; private set; }
        internal SecondarySliderSetting HeadonStealth { get; private set; }
        internal SecondarySliderSetting CreditsXp { get; private set; }
        internal string SliderStateId { get; private set; }

        private MissionSliderState(
            int difficultyDetent,
            string presetName,
            SecondarySliderSetting goodBad,
            SecondarySliderSetting orderChaos,
            SecondarySliderSetting openHidden,
            SecondarySliderSetting physicalMystical,
            SecondarySliderSetting headonStealth,
            SecondarySliderSetting creditsXp)
        {
            DifficultyDetent = difficultyDetent;
            PresetName = presetName;
            GoodBad = goodBad;
            OrderChaos = orderChaos;
            OpenHidden = openHidden;
            PhysicalMystical = physicalMystical;
            HeadonStealth = headonStealth;
            CreditsXp = creditsXp;
            SliderStateId = Sha256(ToNativeValues().CanonicalString());
        }

        internal static bool TryCreatePreset(
            int difficultyDetent,
            string presetName,
            out MissionSliderState state,
            out string error)
        {
            state = null;
            error = null;
            if (difficultyDetent < 1 || difficultyDetent > MissionQlResolver.DifficultyCount)
            {
                error = "DIFFICULTY_DETENT_OUT_OF_RANGE_1_TO_11";
                return false;
            }
            if (string.IsNullOrWhiteSpace(presetName))
            {
                error = "SLIDER_PRESET_MISSING";
                return false;
            }

            string normalized = presetName.Trim().ToUpperInvariant();
            SecondarySliderSetting goodBad = SecondarySliderSetting.FromSemantic("CENTER", 0);
            SecondarySliderSetting orderChaos = SecondarySliderSetting.FromSemantic("CENTER", 0);
            SecondarySliderSetting openHidden = SecondarySliderSetting.FromSemantic("CENTER", 0);
            SecondarySliderSetting physicalMystical = SecondarySliderSetting.FromSemantic("CENTER", 0);
            SecondarySliderSetting headonStealth = SecondarySliderSetting.FromSemantic("CENTER", 0);
            SecondarySliderSetting creditsXp = SecondarySliderSetting.FromSemantic("CENTER", 0);

            switch (normalized)
            {
                case "CENTERED_BASELINE":
                    break;
                case "FIND_ITEM_HEAVY":
                    goodBad = SecondarySliderSetting.FromSemantic("FULL_RIGHT", 100);
                    creditsXp = SecondarySliderSetting.FromSemantic("FULL_LEFT", -100);
                    break;
                case "GOOD_BAD_FULL_LEFT":
                    goodBad = SecondarySliderSetting.FromSemantic("FULL_LEFT", -100);
                    break;
                case "GOOD_BAD_FULL_RIGHT":
                    goodBad = SecondarySliderSetting.FromSemantic("FULL_RIGHT", 100);
                    break;
                case "ORDER_CHAOS_FULL_LEFT":
                    orderChaos = SecondarySliderSetting.FromSemantic("FULL_LEFT", -100);
                    break;
                case "ORDER_CHAOS_FULL_RIGHT":
                    orderChaos = SecondarySliderSetting.FromSemantic("FULL_RIGHT", 100);
                    break;
                case "OPEN_HIDDEN_FULL_LEFT":
                    openHidden = SecondarySliderSetting.FromSemantic("FULL_LEFT", -100);
                    break;
                case "OPEN_HIDDEN_FULL_RIGHT":
                    openHidden = SecondarySliderSetting.FromSemantic("FULL_RIGHT", 100);
                    break;
                case "PHYSICAL_MYSTICAL_FULL_LEFT":
                    physicalMystical = SecondarySliderSetting.FromSemantic("FULL_LEFT", -100);
                    break;
                case "PHYSICAL_MYSTICAL_FULL_RIGHT":
                    physicalMystical = SecondarySliderSetting.FromSemantic("FULL_RIGHT", 100);
                    break;
                case "HEADON_STEALTH_FULL_LEFT":
                    headonStealth = SecondarySliderSetting.FromSemantic("FULL_LEFT", -100);
                    break;
                case "HEADON_STEALTH_FULL_RIGHT":
                    headonStealth = SecondarySliderSetting.FromSemantic("FULL_RIGHT", 100);
                    break;
                case "MONEY_XP_FULL_LEFT":
                    creditsXp = SecondarySliderSetting.FromSemantic("FULL_LEFT", -100);
                    break;
                case "MONEY_XP_FULL_RIGHT":
                    creditsXp = SecondarySliderSetting.FromSemantic("FULL_RIGHT", 100);
                    break;
                default:
                    error = "UNKNOWN_SLIDER_PRESET";
                    return false;
            }

            state = new MissionSliderState(
                difficultyDetent,
                normalized,
                goodBad,
                orderChaos,
                openHidden,
                physicalMystical,
                headonStealth,
                creditsXp);
            return true;
        }

        internal static bool TryCreateCustom(
            int difficultyDetent,
            string goodBadToken,
            string orderChaosToken,
            string openHiddenToken,
            string physicalMysticalToken,
            string headonStealthToken,
            string creditsXpToken,
            out MissionSliderState state,
            out string error)
        {
            state = null;
            error = null;
            if (difficultyDetent < 1 || difficultyDetent > MissionQlResolver.DifficultyCount)
            {
                error = "DIFFICULTY_DETENT_OUT_OF_RANGE_1_TO_11";
                return false;
            }

            SecondarySliderSetting goodBad;
            SecondarySliderSetting orderChaos;
            SecondarySliderSetting openHidden;
            SecondarySliderSetting physicalMystical;
            SecondarySliderSetting headonStealth;
            SecondarySliderSetting creditsXp;
            if (!SecondarySliderSetting.TryParse(goodBadToken, out goodBad, out error)
                || !SecondarySliderSetting.TryParse(orderChaosToken, out orderChaos, out error)
                || !SecondarySliderSetting.TryParse(openHiddenToken, out openHidden, out error)
                || !SecondarySliderSetting.TryParse(physicalMysticalToken, out physicalMystical, out error)
                || !SecondarySliderSetting.TryParse(headonStealthToken, out headonStealth, out error)
                || !SecondarySliderSetting.TryParse(creditsXpToken, out creditsXp, out error))
            {
                return false;
            }

            state = new MissionSliderState(
                difficultyDetent,
                "CUSTOM_EXPLICIT",
                goodBad,
                orderChaos,
                openHidden,
                physicalMystical,
                headonStealth,
                creditsXp);
            return true;
        }

        internal NativeMissionSliderValues ToNativeValues()
        {
            return new NativeMissionSliderValues(
                (byte)DifficultyDetent,
                GoodBad.RawValue,
                OrderChaos.RawValue,
                OpenHidden.RawValue,
                PhysicalMystical.RawValue,
                HeadonStealth.RawValue,
                CreditsXp.RawValue);
        }

        internal IDictionary<string, object> RequestedSemanticPayload()
        {
            return new Dictionary<string, object>
            {
                ["difficulty"] = new Dictionary<string, object>
                {
                    ["semantic_state"] = "EXPLICIT_DETENT",
                    ["semantic_value"] = DifficultyDetent,
                    ["native_raw_value"] = DifficultyDetent
                },
                ["good_bad"] = GoodBad.ToPayload(),
                ["order_chaos"] = OrderChaos.ToPayload(),
                ["open_hidden"] = OpenHidden.ToPayload(),
                ["physical_mystical"] = PhysicalMystical.ToPayload(),
                ["headon_stealth"] = HeadonStealth.ToPayload(),
                ["money_xp"] = CreditsXp.ToPayload()
            };
        }

        internal string Describe()
        {
            NativeMissionSliderValues native = ToNativeValues();
            return string.Format(
                CultureInfo.InvariantCulture,
                "preset={0}; stateId={1}; difficulty={2}; goodBad={3}/{4}; orderChaos={5}/{6}; openHidden={7}/{8}; physicalMystical={9}/{10}; headonStealth={11}/{12}; moneyXp={13}/{14}",
                PresetName,
                SliderStateId,
                DifficultyDetent,
                GoodBad.SemanticState,
                native.GoodBad,
                OrderChaos.SemanticState,
                native.OrderChaos,
                OpenHidden.SemanticState,
                native.OpenHidden,
                PhysicalMystical.SemanticState,
                native.PhysicalMystical,
                HeadonStealth.SemanticState,
                native.HeadonStealth,
                CreditsXp.SemanticState,
                native.CreditsXp);
        }

        private static string Sha256(string text)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }

    internal sealed class SliderRequestGate
    {
        internal string RequestId { get; private set; }
        internal MissionSliderState RequestedState { get; private set; }
        internal string Phase { get; private set; }
        internal string FailureCode { get; private set; }

        internal SliderRequestGate(string requestId, MissionSliderState requestedState)
        {
            RequestId = requestId;
            RequestedState = requestedState;
            Phase = "RESOLVED";
        }

        internal bool TryApplyNative(NativeMissionSliderValues actual, bool apiAvailable, out string error)
        {
            if (!RequirePhase("RESOLVED", out error))
                return false;
            if (!apiAvailable)
                return Fail("SLIDER_API_UNAVAILABLE", out error);
            if (!RequestedState.ToNativeValues().Matches(actual))
                return Fail("NATIVE_READBACK_MISMATCH", out error);
            Phase = "NATIVE_APPLIED_AND_READ_BACK";
            return true;
        }

        internal bool TryVerifySerialized(NativeMissionSliderValues actual, out string error)
        {
            if (!RequirePhase("NATIVE_APPLIED_AND_READ_BACK", out error))
                return false;
            if (!RequestedState.ToNativeValues().Matches(actual))
                return Fail("SERIALIZED_REQUEST_SLIDER_MISMATCH", out error);
            Phase = "SERIALIZED_PRE_SEND_VERIFIED";
            return true;
        }

        internal bool TryMarkTransmitted(
            string requestId,
            NativeMissionSliderValues actual,
            string expectedPacketSha256,
            string observedPacketSha256,
            out string error)
        {
            if (!RequirePhase("SERIALIZED_PRE_SEND_VERIFIED", out error))
                return false;
            if (!string.Equals(RequestId, requestId, StringComparison.Ordinal))
                return Fail("RAW_REQUEST_ASSOCIATION_MISMATCH", out error);
            if (!RequestedState.ToNativeValues().Matches(actual))
                return Fail("TRANSMITTED_REQUEST_SLIDER_MISMATCH", out error);
            if (!string.Equals(expectedPacketSha256, observedPacketSha256, StringComparison.OrdinalIgnoreCase))
                return Fail("TRANSMITTED_REQUEST_PACKET_HASH_MISMATCH", out error);
            Phase = "TRANSMISSION_OBSERVED_AND_VERIFIED";
            return true;
        }

        internal bool TryVerifyResponse(NativeMissionSliderValues actual, out string error)
        {
            if (!RequirePhase("TRANSMISSION_OBSERVED_AND_VERIFIED", out error))
                return false;
            if (!RequestedState.ToNativeValues().Matches(actual))
                return Fail("RETURNED_RESPONSE_SLIDER_MISMATCH", out error);
            Phase = "RESPONSE_SLIDERS_VERIFIED";
            return true;
        }

        internal bool TryAssociateCohort(string requestId, string sliderStateId, out string error)
        {
            if (!RequirePhase("RESPONSE_SLIDERS_VERIFIED", out error))
                return false;
            if (!string.Equals(RequestId, requestId, StringComparison.Ordinal)
                || !string.Equals(RequestedState.SliderStateId, sliderStateId, StringComparison.Ordinal))
            {
                return Fail("COHORT_ASSOCIATION_MISMATCH", out error);
            }
            Phase = "COHORT_ASSOCIATED";
            return true;
        }

        private bool RequirePhase(string expected, out string error)
        {
            error = null;
            if (string.Equals(Phase, expected, StringComparison.Ordinal))
                return true;
            return Fail("SLIDER_APPLICATION_ORDER_VIOLATION", out error);
        }

        private bool Fail(string code, out string error)
        {
            FailureCode = code;
            Phase = "FAILED_CLOSED";
            error = code;
            return false;
        }
    }

    internal sealed class MissionSliderPlanEntry
    {
        internal int MatrixIndex { get; private set; }
        internal string Label { get; private set; }
        internal MissionSliderState SliderState { get; private set; }

        internal MissionSliderPlanEntry(int matrixIndex, string label, MissionSliderState sliderState)
        {
            MatrixIndex = matrixIndex;
            Label = label;
            SliderState = sliderState;
        }

        internal IDictionary<string, object> ToPayload()
        {
            return new Dictionary<string, object>
            {
                ["matrix_index"] = MatrixIndex,
                ["label"] = Label,
                ["difficulty_detent"] = SliderState.DifficultyDetent,
                ["slider_state_id"] = SliderState.SliderStateId,
                ["slider_preset"] = SliderState.PresetName,
                ["requested_semantic_state"] = SliderState.RequestedSemanticPayload(),
                ["native_values"] = SliderState.ToNativeValues().ToPayload()
            };
        }
    }

    internal static class LowLevelSliderMatrix
    {
        internal const int StateCount = 27;

        internal static bool TryBuild(
            int startIndex,
            int endIndex,
            out IList<MissionSliderPlanEntry> selected,
            out string error)
        {
            selected = null;
            error = null;
            if (startIndex < 1 || endIndex > StateCount || startIndex > endIndex)
            {
                error = "MATRIX_RANGE_MUST_BE_WITHIN_1_TO_27_AND_START_NOT_AFTER_END";
                return false;
            }

            var all = new List<MissionSliderPlanEntry>();
            if (!AddPreset(all, 1, "CENTERED_BASELINE_D1", 1, "CENTERED_BASELINE", out error)
                || !AddPreset(all, 2, "GOOD_BAD_FULL_LEFT", 1, "GOOD_BAD_FULL_LEFT", out error)
                || !AddPreset(all, 3, "GOOD_BAD_FULL_RIGHT", 1, "GOOD_BAD_FULL_RIGHT", out error)
                || !AddCustom(all, 4, "GOOD_BAD_MINUS_50", 1, "-50", "CENTER", "CENTER", "CENTER", "CENTER", "CENTER", out error)
                || !AddCustom(all, 5, "GOOD_BAD_PLUS_50", 1, "50", "CENTER", "CENTER", "CENTER", "CENTER", "CENTER", out error)
                || !AddPreset(all, 6, "ORDER_CHAOS_FULL_LEFT", 1, "ORDER_CHAOS_FULL_LEFT", out error)
                || !AddPreset(all, 7, "ORDER_CHAOS_FULL_RIGHT", 1, "ORDER_CHAOS_FULL_RIGHT", out error)
                || !AddCustom(all, 8, "ORDER_CHAOS_MINUS_50", 1, "CENTER", "-50", "CENTER", "CENTER", "CENTER", "CENTER", out error)
                || !AddCustom(all, 9, "ORDER_CHAOS_PLUS_50", 1, "CENTER", "50", "CENTER", "CENTER", "CENTER", "CENTER", out error)
                || !AddPreset(all, 10, "OPEN_HIDDEN_FULL_LEFT", 1, "OPEN_HIDDEN_FULL_LEFT", out error)
                || !AddPreset(all, 11, "OPEN_HIDDEN_FULL_RIGHT", 1, "OPEN_HIDDEN_FULL_RIGHT", out error)
                || !AddCustom(all, 12, "OPEN_HIDDEN_MINUS_50", 1, "CENTER", "CENTER", "-50", "CENTER", "CENTER", "CENTER", out error)
                || !AddCustom(all, 13, "OPEN_HIDDEN_PLUS_50", 1, "CENTER", "CENTER", "50", "CENTER", "CENTER", "CENTER", out error)
                || !AddPreset(all, 14, "PHYSICAL_MYSTICAL_FULL_LEFT", 1, "PHYSICAL_MYSTICAL_FULL_LEFT", out error)
                || !AddPreset(all, 15, "PHYSICAL_MYSTICAL_FULL_RIGHT", 1, "PHYSICAL_MYSTICAL_FULL_RIGHT", out error)
                || !AddCustom(all, 16, "PHYSICAL_MYSTICAL_MINUS_50", 1, "CENTER", "CENTER", "CENTER", "-50", "CENTER", "CENTER", out error)
                || !AddCustom(all, 17, "PHYSICAL_MYSTICAL_PLUS_50", 1, "CENTER", "CENTER", "CENTER", "50", "CENTER", "CENTER", out error)
                || !AddPreset(all, 18, "HEADON_STEALTH_FULL_LEFT", 1, "HEADON_STEALTH_FULL_LEFT", out error)
                || !AddPreset(all, 19, "HEADON_STEALTH_FULL_RIGHT", 1, "HEADON_STEALTH_FULL_RIGHT", out error)
                || !AddCustom(all, 20, "HEADON_STEALTH_MINUS_50", 1, "CENTER", "CENTER", "CENTER", "CENTER", "-50", "CENTER", out error)
                || !AddCustom(all, 21, "HEADON_STEALTH_PLUS_50", 1, "CENTER", "CENTER", "CENTER", "CENTER", "50", "CENTER", out error)
                || !AddPreset(all, 22, "MONEY_XP_FULL_LEFT", 1, "MONEY_XP_FULL_LEFT", out error)
                || !AddPreset(all, 23, "MONEY_XP_FULL_RIGHT", 1, "MONEY_XP_FULL_RIGHT", out error)
                || !AddCustom(all, 24, "MONEY_XP_MINUS_50", 1, "CENTER", "CENTER", "CENTER", "CENTER", "CENTER", "-50", out error)
                || !AddCustom(all, 25, "MONEY_XP_PLUS_50", 1, "CENTER", "CENTER", "CENTER", "CENTER", "CENTER", "50", out error)
                || !AddPreset(all, 26, "CENTERED_BASELINE_D6", 6, "CENTERED_BASELINE", out error)
                || !AddPreset(all, 27, "CENTERED_BASELINE_D10", 10, "CENTERED_BASELINE", out error))
                return false;

            selected = all.GetRange(startIndex - 1, endIndex - startIndex + 1);
            return true;
        }

        private static bool AddPreset(
            IList<MissionSliderPlanEntry> entries,
            int index,
            string label,
            int detent,
            string preset,
            out string error)
        {
            MissionSliderState state;
            if (!MissionSliderState.TryCreatePreset(detent, preset, out state, out error))
                return false;
            entries.Add(new MissionSliderPlanEntry(index, label, state));
            return true;
        }

        private static bool AddCustom(
            IList<MissionSliderPlanEntry> entries,
            int index,
            string label,
            int detent,
            string goodBad,
            string orderChaos,
            string openHidden,
            string physicalMystical,
            string headonStealth,
            string moneyXp,
            out string error)
        {
            MissionSliderState state;
            if (!MissionSliderState.TryCreateCustom(
                    detent,
                    goodBad,
                    orderChaos,
                    openHidden,
                    physicalMystical,
                    headonStealth,
                    moneyXp,
                    out state,
                    out error))
                return false;
            entries.Add(new MissionSliderPlanEntry(index, label, state));
            return true;
        }
    }
}
