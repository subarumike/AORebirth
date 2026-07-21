namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System.Globalization;
    using System.Linq;
    using System.Threading;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Nanos;
    using AORebirth.Core.Network;
    using AORebirth.Core.Statels;
    using AORebirth.Database.Dao;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Playfields;

    #endregion

    public sealed class SurgeryClinicInteractionHandler
    {
        public static readonly SurgeryClinicInteractionHandler Default =
            new SurgeryClinicInteractionHandler();

        private const string SurgeryClinicFeedback =
            "~&!!!\":!!!)<sHYou have 5 minutes (or until you leave the playfield) to swap implants.";

        private SurgeryClinicInteractionHandler()
        {
        }

        public bool TryHandleUse(IZoneClient client, GenericCmdMessage message, Identity target)
        {
            ICharacter character = client.Controller.Character;
            StatelData statelData = GetStatelData(character, target);

            if (!SurgeryClinicInteractionRules.IsCapturedSurgeryClinicTerminal(
                target,
                statelData == null ? 0 : statelData.TemplateId))
            {
                return false;
            }

            int cashBefore = CashStatRules.Clamp(character.Stats[StatIds.cash].BaseValue);
            if (cashBefore < SurgeryClinicInteractionRules.SurgeryClinicCreditCost)
            {
                client.Server.Info(
                    client,
                    "Surgery clinic terminal use blocked by insufficient credits char={0} target={1} cash={2} cost={3}",
                    character.Identity,
                    target,
                    cashBefore,
                    SurgeryClinicInteractionRules.SurgeryClinicCreditCost);
                ChatTextMessageHandler.Default.Send(
                    character,
                    "You need 300 credits to use the Stationary Automated Surgery Clinic.");
                GenericCmdMessageHandler.Default.AcknowledgeDenied(character, message);
                return true;
            }

            int cashAfter = CashStatRules.Clamp(
                (long)cashBefore - SurgeryClinicInteractionRules.SurgeryClinicCreditCost);
            character.Stats[StatIds.cash].Set((uint)cashAfter);
            // Capture 20260721-184426: HealthDamage(0) → Stat(cash) → FormatFeedback → CastNano
            // → Buff → SetNanoDuration → SpecialUsed → GenericCmd ACK.
            SendSurgeryClinicHealthDamage(character);
            StatMessageHandler.Default.SendSingle(character, (int)StatIds.cash, (uint)cashAfter);
            SendSurgeryClinicFeedback(character);
            SendSurgeryClinicCastNano(character);
            BuffMessageHandler.Default.SendAddNanoBuff(
                character,
                SurgeryClinicInteractionRules.SurgeryClinicNanoId,
                false);
            // Nano OnUse applies temporary Treatment (Modify) + ChangeActionRestriction implant access.
            // Without ExecuteOnUseEvents, client shows the buff icon but ToWear Treatment checks fail
            // and Mason gift leg 295706 cannot install.
            ApplySurgeryClinicNanoEffects(character);
            GrantSurgeryClinicImplantAccess(character);
            CharacterActionMessageHandler.Default.SetNanoDuration(
                character,
                character.Identity,
                SurgeryClinicInteractionRules.SurgeryClinicNanoId,
                SurgeryClinicInteractionRules.SurgeryClinicNanoDuration);
            SendSurgeryClinicSpecialUsed(character);
            GenericCmdMessageHandler.Default.Acknowledge(character, message);
            SendSurgeryClinicSpecialAvailableDelayed(character);

            client.Server.Info(
                client,
                "Surgery clinic terminal use handled char={0} target={1} statelTemplate={2} cashBefore={3} cashAfter={4} nano={5} duration={6} implantAccessSeconds={7} evidence={8}",
                character.Identity,
                target,
                statelData == null ? 0 : statelData.TemplateId,
                cashBefore,
                cashAfter,
                SurgeryClinicInteractionRules.SurgeryClinicNanoId.ToString("X", CultureInfo.InvariantCulture),
                SurgeryClinicInteractionRules.SurgeryClinicNanoDuration,
                SurgeryClinicInteractionRules.SurgeryClinicImplantAccessSeconds,
                "captures/20260721-184426 Terminal:574187D1");

            return true;
        }

        private static StatelData GetStatelData(ICharacter character, Identity target)
        {
            if (character == null || character.Playfield == null)
            {
                return null;
            }

            AORebirth.Core.Playfields.PlayfieldData playfieldData;
            if (!PlayfieldLoader.PFData.TryGetValue(character.Playfield.Identity.Instance, out playfieldData))
            {
                return null;
            }

            return playfieldData.Statels.FirstOrDefault(
                x => x.Identity.Type == target.Type && x.Identity.Instance == target.Instance);
        }

        private static void ApplySurgeryClinicNanoEffects(ICharacter character)
        {
            if (character == null)
            {
                return;
            }

            NanoFormula nano;
            if (!NanoLoader.NanoList.TryGetValue(
                    SurgeryClinicInteractionRules.SurgeryClinicNanoId,
                    out nano))
            {
                return;
            }

            NanoEventRuntimeService.Default.ExecuteOnUseEvents(character, nano);
            character.CalculateSkills();
            StatMessageHandler.Default.SendChanged(character);
        }

        private static void GrantSurgeryClinicImplantAccess(ICharacter character)
        {
            Character concreteCharacter = character as Character;
            if (concreteCharacter == null)
            {
                return;
            }

            concreteCharacter.GrantImplantAccess(SurgeryClinicInteractionRules.SurgeryClinicImplantAccessSeconds);
        }

        private static void SendSurgeryClinicHealthDamage(ICharacter character)
        {
            // Capture 20260721-184426 #10 hex:
            // HealthDamage TargetHp=<current> Amount=0 Stat=Flags Unk1=0 Target=self Unk2=0
            // Wire: Unknown1=TargetHp, Unknown2=Amount. Swapping them made Amount=full HP
            // and the client drained life then aborted the socket.
            int targetHp = character.Stats[StatIds.health].Value;
            if (targetHp < 1)
            {
                targetHp = 1;
            }

            character.Controller.Client.SendCompressed(
                new HealthDamageMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Unknown1 = targetHp,
                    Unknown2 = 0,
                    Unknown3 = 0,
                    Unknown4 = 0,
                    Target = character.Identity,
                    Unknown5 = 0
                });
        }

        private static void SendSurgeryClinicFeedback(ICharacter character)
        {
            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown = 1,
                    Unknown1 = 0,
                    FormattedMessage = SurgeryClinicFeedback,
                    Unknown2 = 0
                },
                character.Identity.Instance);
        }

        private static void SendSurgeryClinicCastNano(ICharacter character)
        {
            character.Controller.Client.SendCompressed(
                new CastNanoSpellMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    NanoId = SurgeryClinicInteractionRules.SurgeryClinicNanoId,
                    Target = character.Identity,
                    Unknown1 = 1,
                    Caster = character.Identity
                });
        }

        private static void SendSurgeryClinicSpecialUsed(ICharacter character)
        {
            character.Controller.Client.SendCompressed(
                new CharacterActionMessage
                {
                    Identity = character.Identity,
                    Unknown = 0,
                    Action = CharacterActionType.SpecialUsed,
                    Unknown1 = 0,
                    Target = Identity.None,
                    Parameter1 = SurgeryClinicInteractionRules.SurgeryClinicSpecialStatId,
                    Parameter2 = SurgeryClinicInteractionRules.SurgeryClinicSpecialLockSeconds,
                    Unknown2 = 0
                });
        }

        private static void SendSurgeryClinicSpecialAvailableDelayed(ICharacter character)
        {
            ThreadPool.QueueUserWorkItem(
                _ =>
                {
                    Thread.Sleep(SurgeryClinicInteractionRules.SurgeryClinicSpecialAvailableDelayMilliseconds);
                    if (character == null || character.Controller == null || character.Controller.Client == null)
                    {
                        return;
                    }

                    CharacterActionMessageHandler.Default.SendSkillAvailable(
                        character,
                        SurgeryClinicInteractionRules.SurgeryClinicSpecialStatId);
                });
        }
    }
}
