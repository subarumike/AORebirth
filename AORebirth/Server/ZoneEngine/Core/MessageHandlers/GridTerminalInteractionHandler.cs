namespace ZoneEngine.Core.MessageHandlers
{
    #region Usings ...

    using System;
    using System.Globalization;
    using System.Linq;

    using AORebirth.Core.Components;
    using AORebirth.Core.Entities;
    using AORebirth.Core.Events;
    using AORebirth.Core.Functions;
    using AORebirth.Core.Network;
    using AORebirth.Core.Requirements;
    using AORebirth.Core.Statels;
    using AORebirth.Core.Vector;
    using AORebirth.Enums;

    using SmokeLounge.AOtomation.Messaging.GameData;
    using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

    using ZoneEngine.Core.Playfields;

    #endregion

    public sealed class GridTerminalInteractionHandler
    {
        public static readonly GridTerminalInteractionHandler Default =
            new GridTerminalInteractionHandler();

        private GridTerminalInteractionHandler()
        {
        }

        private const int CapturedGridPlayfieldId = GridTerminalInteractionRules.CapturedGridPlayfieldId;

        private const int GridEnterTerminalTemplateId = GridTerminalInteractionRules.GridEnterTerminalTemplateId;

        private const int GridExitTerminalTemplateId = GridTerminalInteractionRules.GridExitTerminalTemplateId;

        private const float GridDestinationTerminalClearance = GridTerminalInteractionRules.GridDestinationTerminalClearance;

        private static readonly CapturedGridTerminalRoute[] CapturedGridTerminalRoutes =
        {
            new CapturedGridTerminalRoute(
                567,
                unchecked((int)0xC0010237),
                unchecked((int)0xC00D0098),
                177.7f,
                3.8f,
                181.7f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Newland grid-side exit anchor 2026-06-22;source Terminal:C0010237;PF152 nearest exit Terminal:C00D0098"),
            new CapturedGridTerminalRoute(
                640,
                unchecked((int)0xC0030280),
                unchecked((int)0xC00B0098),
                156.0f,
                3.8f,
                185.1f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Tir grid-side exit anchor 2026-06-22;source Terminal:C0030280;PF152 nearest exit Terminal:C00B0098"),
            new CapturedGridTerminalRoute(
                710,
                unchecked((int)0xC00502C6),
                unchecked((int)0xC0000098),
                165.2f,
                3.8f,
                235.0f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Omni Trade grid-side exit anchor 2026-06-22;source Terminal:C00502C6;PF152 nearest exit Terminal:C0000098"),
            new CapturedGridTerminalRoute(
                540,
                unchecked((int)0xC00A021C),
                unchecked((int)0xC04A0098),
                210.2f,
                3.8f,
                172.8f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Old Athen grid-side exit anchor 2026-06-22;source Terminal:C00A021C;PF152 nearest exit Terminal:C04A0098"),
            new CapturedGridTerminalRoute(
                556,
                unchecked((int)0xC002022C),
                unchecked((int)0xC0510098),
                202.1f,
                3.8f,
                249.8f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Coast of Peace grid-side landing 2026-06-22;source Terminal:C002022C;PF152 nearest exit Terminal:C0510098"),
            new CapturedGridTerminalRoute(
                565,
                unchecked((int)0xC0050235),
                unchecked((int)0xC00C0098),
                169.5f,
                37.4f,
                165.2f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Newland Desert grid-side landing 2026-06-22;source Terminal:C0050235;PF152 nearest exit Terminal:C00C0098"),
            new CapturedGridTerminalRoute(
                635,
                unchecked((int)0xC003027B),
                unchecked((int)0xC0050098),
                188.7f,
                37.4f,
                211.1f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Stret East Bank grid-side landing 2026-06-22;source Terminal:C003027B;PF152 nearest exit Terminal:C0050098"),
            new CapturedGridTerminalRoute(
                646,
                unchecked((int)0xC0040286),
                unchecked((int)0xC00B0098),
                155.4f,
                3.8f,
                185.5f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Tir County grid-side landing 2026-06-22;source Terminal:C0040286;PF152 nearest exit Terminal:C00B0098"),
            new CapturedGridTerminalRoute(
                656,
                unchecked((int)0xC0020290),
                unchecked((int)0xC0520098),
                219.2f,
                3.8f,
                246.4f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Coast of Tranquility grid-side landing 2026-06-22;source Terminal:C0020290;PF152 nearest exit Terminal:C0520098"),
            new CapturedGridTerminalRoute(
                6007,
                unchecked((int)0xC0001777),
                unchecked((int)0xC0580098),
                209.4f,
                3.8f,
                210.5f,
                0.0f,
                0.707108f,
                0.0f,
                0.707105f,
                "user supplied Unicorn Defence Hub grid-side exit anchor 2026-06-22;source Terminal:C0001777;PF152 nearest exit Terminal:C0580098"),
            new CapturedGridTerminalRoute(
                655,
                unchecked((int)0xC002028F),
                unchecked((int)0xC04E0098),
                234.3062f,
                3.7750f,
                212.8138f,
                0.0f,
                1.0f,
                0.0f,
                -4.371139E-08f,
                "captures/20260621-091447/events.log:255-256,321-322,645-646;PF152 nearest exit Terminal:C04E0098"),
            new CapturedGridTerminalRoute(
                705,
                unchecked((int)0xC00602C1),
                unchecked((int)0xC0010098),
                174.8353f,
                37.3750f,
                240.2071f,
                0.0f,
                1.0f,
                0.0f,
                -4.371139E-08f,
                "captures/20260622-003221/events.log:2420-2423,2624-2625;PF152 nearest exit Terminal:C0010098"),
            new CapturedGridTerminalRoute(
                730,
                unchecked((int)0xC00002DA),
                unchecked((int)0xC0010098),
                174.8353f,
                37.3750f,
                240.2071f,
                0.0f,
                1.0f,
                0.0f,
                -4.371139E-08f,
                "captures/20260622-003221/events.log:3109-3112,3313-3314;PF152 nearest exit Terminal:C0010098"),
            new CapturedGridTerminalRoute(
                665,
                unchecked((int)0xC0000299),
                unchecked((int)0xC00A0098),
                239.6f,
                37.4f,
                221.6f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Broken Shores grid-side landing 2026-06-22;source Terminal:C0000299;PF152 nearest exit Terminal:C00A0098"),
            new CapturedGridTerminalRoute(
                685,
                unchecked((int)0xC00502AD),
                unchecked((int)0xC0080098),
                215.9f,
                37.4f,
                225.7f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Galway County grid-side landing 2026-06-22;source Terminal:C00502AD;PF152 nearest exit Terminal:C0080098"),
            new CapturedGridTerminalRoute(
                695,
                unchecked((int)0xC00702B7),
                unchecked((int)0xC0040098),
                185.1f,
                37.4f,
                227.4f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Lush Fields Harry's grid-side landing 2026-06-22;source Terminal:C00702B7;PF152 nearest exit Terminal:C0040098"),
            new CapturedGridTerminalRoute(
                695,
                unchecked((int)0xC00802B7),
                unchecked((int)0xC0030098),
                188.4104f,
                37.37542f,
                234.9863f,
                0.0f,
                0.9952047f,
                0.0f,
                0.09781352f,
                "captures/20260622-003221/events.log:3806-3809,4010-4011;PF152 nearest exit Terminal:C0030098"),
            new CapturedGridTerminalRoute(
                670,
                unchecked((int)0xC003029E),
                unchecked((int)0xC0070098),
                203.2488f,
                37.38467f,
                222.7339f,
                0.0f,
                -0.9641039f,
                0.0f,
                0.2655251f,
                "captures/20260622-003221/events.log:5421-5424,5639-5640;PF152 nearest exit Terminal:C0070098"),
            new CapturedGridTerminalRoute(
                560,
                unchecked((int)0xC0060230),
                unchecked((int)0xC00E0098),
                183.9474f,
                44.0150f,
                150.8788f,
                0.0f,
                0.7062106f,
                0.0f,
                0.7080019f,
                "captures/20260622-003221/events.log:7519-7522,7733-7734;PF152 nearest exit Terminal:C00E0098"),
            new CapturedGridTerminalRoute(
                705,
                unchecked((int)0xC00302C1),
                unchecked((int)0xC0020098),
                180.2f,
                37.4f,
                248.0f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Omni-1 Entertainment South grid-side landing 2026-06-22;source Terminal:C00302C1;PF152 nearest exit Terminal:C0020098"),
            new CapturedGridTerminalRoute(
                700,
                unchecked((int)0xC00202BC),
                unchecked((int)0xC0090098),
                224.5596f,
                44.0050f,
                231.8543f,
                0.0f,
                -0.7107669f,
                0.0f,
                0.7034276f,
                "captures/20260622-003221/events.log:8380-8383,8594-8595;PF152 nearest exit Terminal:C0090098"),
            new CapturedGridTerminalRoute(
                505,
                unchecked((int)0xC02301F9),
                unchecked((int)0xC0100098),
                215.8618f,
                43.9950f,
                151.7285f,
                0.0f,
                0.6904334f,
                0.0f,
                0.7233959f,
                "captures/20260622-003221/events.log:9339-9342,9563-9564;PF152 nearest exit Terminal:C0100098"),
            new CapturedGridTerminalRoute(
                760,
                unchecked((int)0xC00502F8),
                unchecked((int)0xC0060098),
                196.6f,
                37.4f,
                208.1f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied 4 Holes grid-side landing 2026-06-22;source Terminal:C00502F8;PF152 nearest exit Terminal:C0060098"),
            new CapturedGridTerminalRoute(
                800,
                unchecked((int)0xC0040320),
                unchecked((int)0xC04C0098),
                234.4f,
                3.8f,
                198.9f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Borealis grid-side landing 2026-06-22;source Terminal:C0040320;PF152 nearest exit Terminal:C04C0098"),
            new CapturedGridTerminalRoute(
                6101,
                unchecked((int)0xC00017D5),
                unchecked((int)0xC0480098),
                218.3f,
                3.8f,
                190.7f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Three Craters West grid-side landing 2026-06-22;source Terminal:C00017D5;PF152 nearest exit Terminal:C0480098"),
            new CapturedGridTerminalRoute(
                6102,
                unchecked((int)0xC00017D6),
                unchecked((int)0xC0480098),
                218.3f,
                3.8f,
                190.7f,
                0.0f,
                0.0f,
                0.0f,
                1.0f,
                "user supplied Three Craters East grid-side landing 2026-06-22;source Terminal:C00017D6;PF152 nearest exit Terminal:C0480098")
        };

        private sealed class CapturedGridTerminalRoute
        {
            public CapturedGridTerminalRoute(
                int sourcePlayfieldId,
                int sourceTerminalInstance,
                int destinationExitTerminalInstance,
                float destinationX,
                float destinationY,
                float destinationZ,
                float headingX,
                float headingY,
                float headingZ,
                float headingW,
                string evidence)
            {
                this.SourcePlayfieldId = sourcePlayfieldId;
                this.SourceTerminalInstance = sourceTerminalInstance;
                this.DestinationExitTerminalInstance = destinationExitTerminalInstance;
                this.DestinationX = destinationX;
                this.DestinationY = destinationY;
                this.DestinationZ = destinationZ;
                this.HeadingX = headingX;
                this.HeadingY = headingY;
                this.HeadingZ = headingZ;
                this.HeadingW = headingW;
                this.Evidence = evidence;
            }

            public int SourcePlayfieldId { get; private set; }

            public int SourceTerminalInstance { get; private set; }

            public int DestinationExitTerminalInstance { get; private set; }

            public float DestinationX { get; private set; }

            public float DestinationY { get; private set; }

            public float DestinationZ { get; private set; }

            public float HeadingX { get; private set; }

            public float HeadingY { get; private set; }

            public float HeadingZ { get; private set; }

            public float HeadingW { get; private set; }

            public string Evidence { get; private set; }
        }

        public bool TryHandleCapturedUse(
            IZoneClient client,
            Identity target)
        {
            ICharacter character = client.Controller.Character;
            StatelData statelData = this.GetStatelData(character, target);
            CapturedGridTerminalRoute route;

            if (!this.TryGetCapturedGridTerminalRoute(character, target, statelData, out route))
            {
                return false;
            }

            Dynel dynel = character as Dynel;
            if (dynel == null)
            {
                return false;
            }

            character.StopMovement();
            character.Stats[StatIds.externaldoorinstance].BaseValue = 0;
            character.Stats[StatIds.externalplayfieldinstance].BaseValue = 0;
            var destination = new Coordinate(
                route.DestinationX,
                route.DestinationY,
                route.DestinationZ);
            var heading = new AORebirth.Core.Vector.Quaternion(
                route.HeadingX,
                route.HeadingY,
                route.HeadingZ,
                route.HeadingW);

            Function rawTeleportFunction;
            int rawDestinationPlayfieldId;
            int rawDestinationInstance;
            if (!this.TryGetGridTeleportProxy2Destination(
                statelData,
                out rawTeleportFunction,
                out rawDestinationPlayfieldId,
                out rawDestinationInstance))
            {
                rawDestinationInstance = 0;
            }

            StatelData destinationTerminal;
            this.TryGetGridDestinationTerminal(
                CapturedGridPlayfieldId,
                route.DestinationExitTerminalInstance,
                out destinationTerminal);
            ZoneEngine.Core.GridZoneInDiagnostics.RecordGridEntry(
                character,
                statelData,
                destinationTerminal,
                destination,
                "CapturedGridTerminalRoute",
                route.Evidence,
                rawDestinationInstance);

            character.Playfield.Teleport(
                dynel,
                destination,
                heading,
                new Identity { Type = IdentityType.Playfield, Instance = CapturedGridPlayfieldId });

            client.Server.Info(
                client,
                "Captured grid terminal use handled char={0} target={1} sourcePf={2} destPf={3} destExit={4:X8} dest=({5:F3},{6:F3},{7:F3}) evidence={8}",
                character.Identity,
                target,
                route.SourcePlayfieldId,
                CapturedGridPlayfieldId,
                unchecked((uint)route.DestinationExitTerminalInstance),
                destination.x,
                destination.y,
                destination.z,
                route.Evidence);

            return true;
        }

        public bool TryHandleGridEnterUse(
            IZoneClient client,
            Identity target)
        {
            ICharacter character = client.Controller.Character;
            StatelData statelData = this.GetStatelData(character, target);

            if (!this.IsGridEnterTerminal(character, target, statelData))
            {
                return false;
            }

            Function teleportFunction;
            int destinationPlayfieldId;
            int destinationInstance;
            if (!this.TryGetGridTeleportProxy2Destination(
                statelData,
                out teleportFunction,
                out destinationPlayfieldId,
                out destinationInstance))
            {
                client.Server.Info(
                    client,
                    "Grid enter terminal route skipped; no supported TeleportProxy2 char={0} target={1} sourcePf={2} template={3}",
                    character.Identity,
                    target,
                    statelData.PlayfieldId,
                    statelData.TemplateId);
                return false;
            }

            if (!this.GridTeleportRequirementsPass(character, teleportFunction))
            {
                this.SendGridTerminalRequirementFeedback(character, statelData, teleportFunction);
                client.Server.Info(
                    client,
                    "Grid enter terminal use blocked by requirements char={0} target={1} sourcePf={2} computerLiteracy={3} isfightingme={4}",
                    character.Identity,
                    target,
                    statelData.PlayfieldId,
                    character.Stats[StatIds.computerliteracy].Value,
                    character.Stats[StatIds.isfightingme].Value);
                return true;
            }

            Dynel dynel = character as Dynel;
            if (dynel == null)
            {
                return false;
            }

            StatelData destinationTerminal;
            if (!this.TryGetGridDestinationTerminal(destinationPlayfieldId, destinationInstance, out destinationTerminal))
            {
                this.SendGridTerminalFeedback(character, "Grid terminal route is unavailable.");
                client.Server.Info(
                    client,
                    "Grid enter terminal destination missing char={0} target={1} sourcePf={2} destPf={3} destInstance={4:X8}",
                    character.Identity,
                    target,
                    statelData.PlayfieldId,
                    destinationPlayfieldId,
                    unchecked((uint)destinationInstance));
                return true;
            }

            var heading = new AORebirth.Core.Vector.Quaternion(
                destinationTerminal.HeadingX,
                destinationTerminal.HeadingY,
                destinationTerminal.HeadingZ,
                destinationTerminal.HeadingW);
            AORebirth.Core.Vector.Quaternion.Normalize(heading);

            var destination = new AORebirth.Core.Vector.Vector3(
                destinationTerminal.X,
                destinationTerminal.Y,
                destinationTerminal.Z);
            AORebirth.Core.Vector.Vector3 forward =
                (AORebirth.Core.Vector.Vector3)heading.RotateVector3(AORebirth.Core.Vector.Vector3.AxisZ);
            destination.x += forward.x * GridDestinationTerminalClearance;
            destination.z += forward.z * GridDestinationTerminalClearance;

            character.StopMovement();
            character.Stats[StatIds.externaldoorinstance].BaseValue = 0;
            character.Stats[StatIds.externalplayfieldinstance].BaseValue = 0;
            ZoneEngine.Core.GridZoneInDiagnostics.RecordGridEntry(
                character,
                statelData,
                destinationTerminal,
                new Coordinate(destination),
                "GridTeleportProxy2TerminalRoute",
                "playfields.dat Enter The Grid template 95350 TeleportProxy2 -> PF152; destination template 95351",
                destinationInstance);
            character.Playfield.Teleport(
                dynel,
                new Coordinate(destination),
                heading,
                new Identity { Type = IdentityType.Playfield, Instance = CapturedGridPlayfieldId });

            client.Server.Info(
                client,
                "Grid enter terminal use handled char={0} target={1} sourcePf={2} destPf={3} destTerminal={4} dest=({5:F3},{6:F3},{7:F3}) evidence={8}",
                character.Identity,
                target,
                statelData.PlayfieldId,
                destinationPlayfieldId,
                destinationTerminal.Identity,
                destination.x,
                destination.y,
                destination.z,
                "playfields.dat Enter The Grid template 95350 TeleportProxy2 -> PF152; destination template 95351");

            return true;
        }

        private StatelData GetStatelData(ICharacter character, Identity target)
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

        private bool TryGetCapturedGridTerminalRoute(
            ICharacter character,
            Identity target,
            StatelData statelData,
            out CapturedGridTerminalRoute route)
        {
            route = null;

            if (character == null || character.Playfield == null)
            {
                return false;
            }

            if (target.Type != IdentityType.Terminal
                || statelData == null
                || statelData.TemplateId != GridEnterTerminalTemplateId)
            {
                return false;
            }

            if (statelData.PlayfieldId != character.Playfield.Identity.Instance
                || statelData.Identity.Type != target.Type
                || statelData.Identity.Instance != target.Instance)
            {
                return false;
            }

            route = CapturedGridTerminalRoutes.FirstOrDefault(
                x => x.SourcePlayfieldId == character.Playfield.Identity.Instance
                     && x.SourceTerminalInstance == target.Instance);

            return route != null;
        }

        private bool IsGridEnterTerminal(
            ICharacter character,
            Identity target,
            StatelData statelData)
        {
            if (character == null || character.Playfield == null)
            {
                return false;
            }

            if (target.Type != IdentityType.Terminal
                || statelData == null
                || statelData.TemplateId != GridEnterTerminalTemplateId)
            {
                return false;
            }

            CapturedGridTerminalRoute route;
            if (this.TryGetCapturedGridTerminalRoute(character, target, statelData, out route))
            {
                return false;
            }

            return statelData.PlayfieldId == character.Playfield.Identity.Instance
                   && statelData.Identity.Type == target.Type
                   && statelData.Identity.Instance == target.Instance;
        }

        private bool TryGetGridTeleportProxy2Destination(
            StatelData statelData,
            out Function teleportFunction,
            out int destinationPlayfieldId,
            out int destinationInstance)
        {
            teleportFunction = null;
            destinationPlayfieldId = 0;
            destinationInstance = 0;

            if (statelData == null)
            {
                return false;
            }

            foreach (Event eventData in statelData.Events.Where(x => x.EventType == EventType.OnUse))
            {
                foreach (Function function in eventData.Functions.Where(
                    x => x.FunctionType == (int)FunctionType.TeleportProxy2))
                {
                    if (function.Arguments.Values.Count < 3)
                    {
                        continue;
                    }

                    int playfieldId = function.Arguments.Values[1].AsInt32();
                    if (playfieldId != CapturedGridPlayfieldId)
                    {
                        continue;
                    }

                    int destinationIndex = function.Arguments.Values[2].AsInt32();
                    teleportFunction = function;
                    destinationPlayfieldId = playfieldId;
                    destinationInstance = unchecked(
                        (int)(0xC0000000u | (uint)playfieldId | ((uint)destinationIndex << 16)));
                    return true;
                }
            }

            return false;
        }

        private bool GridTeleportRequirementsPass(ICharacter character, Function teleportFunction)
        {
            bool result = true;
            for (int i = 0; i < teleportFunction.Requirements.Count; i++)
            {
                Requirement requirement = teleportFunction.Requirements[i];
                if ((i == 0) && (requirement.ChildOperator == Operator.Or))
                {
                    result = false;
                }

                if (requirement.ChildOperator == Operator.Or)
                {
                    result |= requirement.CheckRequirement(character);
                }
                else
                {
                    result &= requirement.CheckRequirement(character);
                }

                if (!result && requirement.ChildOperator != Operator.Or)
                {
                    return false;
                }
            }

            return result;
        }

        private void SendGridTerminalRequirementFeedback(
            ICharacter character,
            StatelData statelData,
            Function teleportFunction)
        {
            int computerLiteracyRequirement;
            if (this.TryGetGreaterThanRequirement(
                teleportFunction,
                StatIds.computerliteracy,
                out computerLiteracyRequirement)
                && character.Stats[StatIds.computerliteracy].Value <= computerLiteracyRequirement)
            {
                this.SendGridTerminalFeedback(
                    character,
                    this.GetGridTerminalSystemText(statelData, "Computer")
                    ?? ("Your skill in Computer Literacy needs to be "
                        + (computerLiteracyRequirement + 1).ToString(CultureInfo.InvariantCulture)
                        + " or better to activate this terminal."));
                return;
            }

            if (this.HasEqualToZeroRequirement(teleportFunction, StatIds.isfightingme)
                && character.Stats[StatIds.isfightingme].Value != 0)
            {
                this.SendGridTerminalFeedback(
                    character,
                    this.GetGridTerminalSystemText(statelData, "combat")
                    ?? "This terminal can not be activated while you are in combat.");
                return;
            }

            this.SendGridTerminalFeedback(character, "Grid terminal requirements are not met.");
        }

        private bool TryGetGreaterThanRequirement(Function function, StatIds statId, out int value)
        {
            foreach (Requirement requirement in function.Requirements)
            {
                if (requirement.Statnumber == (int)statId
                    && requirement.Operator == Operator.GreaterThan)
                {
                    value = requirement.Value;
                    return true;
                }
            }

            value = 0;
            return false;
        }

        private bool HasEqualToZeroRequirement(Function function, StatIds statId)
        {
            return function.Requirements.Any(
                x => x.Statnumber == (int)statId
                     && x.Operator == Operator.EqualTo
                     && x.Value == 0);
        }

        private string GetGridTerminalSystemText(StatelData statelData, string contains)
        {
            if (statelData == null)
            {
                return null;
            }

            foreach (Event eventData in statelData.Events.Where(x => x.EventType == EventType.OnUse))
            {
                foreach (Function function in eventData.Functions.Where(
                    x => x.FunctionType == (int)FunctionType.SystemText))
                {
                    if (function.Arguments.Values.Count == 0)
                    {
                        continue;
                    }

                    string text = function.Arguments.Values[0].AsString();
                    if (text.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return text;
                    }
                }
            }

            return null;
        }

        private bool TryGetGridDestinationTerminal(
            int destinationPlayfieldId,
            int destinationInstance,
            out StatelData destinationTerminal)
        {
            destinationTerminal = null;

            AORebirth.Core.Playfields.PlayfieldData destinationPlayfield;
            if (!PlayfieldLoader.PFData.TryGetValue(destinationPlayfieldId, out destinationPlayfield))
            {
                return false;
            }

            destinationTerminal = destinationPlayfield.Statels.FirstOrDefault(
                x => (x.Identity.Type == IdentityType.Terminal || x.Identity.Type == IdentityType.Door)
                     && x.Identity.Instance == destinationInstance
                     && x.TemplateId == GridExitTerminalTemplateId);

            return destinationTerminal != null;
        }

        private void SendGridTerminalFeedback(ICharacter character, string text)
        {
            character.Controller.Client.SendCompressed(
                new FormatFeedbackMessage
                {
                    Identity = character.Identity,
                    Unknown1 = 0,
                    FormattedMessage = text,
                    Unknown2 = 0
                },
                character.Identity.Instance);
        }

    }
}
