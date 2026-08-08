from pathlib import Path

# --- PetCommandService ---
p = Path(r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\PetCommandService.cs")
t = p.read_text(encoding="utf-8").replace("\r\n", "\n").replace("\r", "\n")

if "using MsgPack;" not in t:
    t = t.replace(
        "using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;\n\n    using ZoneEngine.Core.Controllers;",
        "using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;\n\n    using MsgPack;\n\n    using ZoneEngine.Core.Controllers;",
        1,
    )

old = """        public const int CommandReport = 14;

        // Pet dialogue: Zone FormatFeedback only (working look before Your Pets attempts).
"""
new = """        public const int CommandReport = 14;

        /// <summary>
        /// Capture 20260806-pet-warp: shared pet-warp nano (all pet professions).
        /// OnUse is FunctionType.SummonPets with argument [0].
        /// </summary>
        public const int WarpPetsNanoId = 209488;

        // Pet dialogue: Zone FormatFeedback only (working look before Your Pets attempts).
"""
assert t.count(old) == 1, t.count(old)
t = t.replace(old, new, 1)

old2 = """            ExecuteForPet(owner, client, pet, commandId, commandTarget);
        }

        private static void ExecuteForAllOwnedPets(
"""
new2 = """            ExecuteForPet(owner, client, pet, commandId, commandTarget);
        }

        /// <summary>
        /// Capture 20260806-pet-warp nano 209488 / SummonPets [0]:
        /// Follow chat → SetPos pet to owner exact coords → DesiredTargetDistance=0 + follow.
        /// </summary>
        public static bool WarpAllOwnedPetsToOwner(ICharacter owner)
        {
            if (owner == null || owner.Playfield == null)
            {
                return false;
            }

            Playfield playfield = owner.Playfield as Playfield;
            if (playfield == null)
            {
                return false;
            }

            Coordinate ownerCoord = owner.Coordinates();
            foreach (int strain in PetRuntimeService.Default.GetActivePetStrains(owner))
            {
                ICharacter pet = PetRuntimeService.Default.GetActivePetInStrain(owner, strain);
                if (pet == null)
                {
                    continue;
                }

                WarpOwnedPetToOwner(owner, pet, playfield, ownerCoord);
            }

            return true;
        }

        /// <summary>
        /// Capture 20260806-pet-warp: SummonPets with a single int argument 0 means warp,
        /// not spawn (spawn uses pet-hash string + type id).
        /// </summary>
        public static bool IsPetWarpSummonPetsArguments(MessagePackObject[] arguments)
        {
            if (arguments == null || arguments.Length != 1)
            {
                return false;
            }

            try
            {
                return arguments[0].AsInt32() == 0;
            }
            catch
            {
                return false;
            }
        }

        private static void WarpOwnedPetToOwner(
            ICharacter owner,
            ICharacter pet,
            Playfield playfield,
            Coordinate ownerCoord)
        {
            var petController = pet.Controller as NPCController;
            if (petController == null)
            {
                return;
            }

            ActiveHealCommands.Remove(pet.Identity.Instance);
            PetsHoldingWaitStance.Remove(pet.Identity.Instance);

            // Capture order: Follow chat, then SetPos to owner, then DesiredTargetDistance=0.
            AnnouncePetSystemChat(owner, pet, PetSystemChatLines.Follow(pet));

            pet.Coordinates(ownerCoord);
            playfield.Announce(
                new SetPosMessage
                {
                    Identity = pet.Identity,
                    Coordinates =
                        new SmokeLounge.AOtomation.Messaging.GameData.Vector3
                        {
                            X = ownerCoord.x,
                            Y = ownerCoord.y,
                            Z = ownerCoord.z
                        },
                    Unknown1 = 1
                });

            ClearPetCombatState(pet, petController, playfield);
            ApplyPetFollowDesiredDistance(pet, 0);
            petController.Follow(owner.Identity, 2.0);
        }

        private static void ExecuteForAllOwnedPets(
"""
assert t.count(old2) == 1, t.count(old2)
t = t.replace(old2, new2, 1)
p.write_bytes(t.replace("\n", "\r\n").encode("utf-8"))
print("PetCommandService OK")

# --- summonpets.cs (rewrite clean like backup; current is double-spaced) ---
summon = r"""#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.

#endregion

namespace ZoneEngine.Core.Functions.GameFunctions
{
    #region Usings ...

    using AORebirth.Core.Entities;
    using AORebirth.Enums;
    using AORebirth.Interfaces;

    using MsgPack;

    using ZoneEngine.Core;

    #endregion

    internal class summonpets : FunctionPrototype
    {
        public override FunctionType FunctionId
        {
            get { return FunctionType.SummonPets; }
        }

        public override bool Execute(
            INamedEntity self,
            IEntity caller,
            IInstancedEntity target,
            MessagePackObject[] arguments)
        {
            ICharacter owner = self as ICharacter;
            if (owner == null)
            {
                return false;
            }

            // Capture 20260806-pet-warp: SummonPets [0] warps existing pets to the caster
            // (all pet professions share this nano — not a new summon).
            if (PetCommandService.IsPetWarpSummonPetsArguments(arguments))
            {
                return PetCommandService.WarpAllOwnedPetsToOwner(owner);
            }

            PetSummonParams summonParams;
            if (arguments != null && arguments.Length >= 2)
            {
                return PetRuntimeService.Default.SummonPet(
                    owner,
                    arguments[0].AsString(),
                    arguments[1].AsInt32());
            }

            foreach (var activeNano in owner.ActiveNanos.Values)
            {
                if (activeNano == null)
                {
                    continue;
                }

                if (PetSummonNanoCatalog.TryResolve(owner, activeNano.ID, out summonParams))
                {
                    return PetRuntimeService.Default.SummonPet(
                        owner,
                        summonParams.PetHash,
                        summonParams.PetTypeId);
                }
            }

            return false;
        }
    }
}
"""
Path(r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\Functions\GameFunctions\summonpets.cs").write_bytes(
    summon.replace("\n", "\r\n").encode("utf-8")
)
print("summonpets OK")

# --- NanoEventRuntimeService ---
p3 = Path(r"C:\Users\nermi\source\repos\AORebirth\AORebirth\Server\ZoneEngine\Core\NanoEventRuntimeService.cs")
t3 = p3.read_text(encoding="utf-8").replace("\r\n", "\n").replace("\r", "\n")

old3 = """        public bool HasSummonPetOnUse(int nanoId)
        {
            if (PetSummonNanoCatalog.IsCatalogSummonNano(nanoId))
            {
                return true;
            }

            NanoFormula nano;
            if (!NanoLoader.NanoList.TryGetValue(nanoId, out nano))
            {
                return false;
            }

            return this.HasSummonPetOnUse(nano);
        }

        public bool HasSummonPetOnUse(NanoFormula nano)
        {
            if (nano == null || nano.Events == null)
            {
                return false;
            }

            foreach (Event nanoEvent in nano.Events.Where(x => x.EventType == EventType.OnUse))
            {
                if (nanoEvent.Functions == null)
                {
                    continue;
                }

                foreach (Function function in nanoEvent.Functions)
                {
                    if (function.FunctionType == SummonPetFunctionId
                        || function.FunctionType == SummonPetsFunctionId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
"""
new3 = """        public bool HasSummonPetOnUse(int nanoId)
        {
            // Capture 20260806-pet-warp: nano 209488 warps pets; it is not a summon strain.
            if (nanoId == PetCommandService.WarpPetsNanoId)
            {
                return false;
            }

            if (PetSummonNanoCatalog.IsCatalogSummonNano(nanoId))
            {
                return true;
            }

            NanoFormula nano;
            if (!NanoLoader.NanoList.TryGetValue(nanoId, out nano))
            {
                return false;
            }

            return this.HasSummonPetOnUse(nano);
        }

        public bool HasSummonPetOnUse(NanoFormula nano)
        {
            if (nano == null || nano.Events == null)
            {
                return false;
            }

            if (nano.ID == PetCommandService.WarpPetsNanoId)
            {
                return false;
            }

            foreach (Event nanoEvent in nano.Events.Where(x => x.EventType == EventType.OnUse))
            {
                if (nanoEvent.Functions == null)
                {
                    continue;
                }

                foreach (Function function in nanoEvent.Functions)
                {
                    if (function == null)
                    {
                        continue;
                    }

                    if (function.FunctionType == SummonPetFunctionId)
                    {
                        return true;
                    }

                    if (function.FunctionType == SummonPetsFunctionId
                        && !IsPetWarpSummonPetsFunction(function))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Capture 20260806-pet-warp: SummonPets [0] warps living pets to the caster.
        /// </summary>
        private static bool IsPetWarpSummonPetsFunction(Function function)
        {
            if (function == null
                || function.Arguments == null
                || function.Arguments.Values == null
                || function.Arguments.Values.Count != 1)
            {
                return false;
            }

            try
            {
                return function.Arguments.Values[0].AsInt32() == 0;
            }
            catch
            {
                return false;
            }
        }
"""
assert t3.count(old3) == 1, t3.count(old3)
t3 = t3.replace(old3, new3, 1)
p3.write_bytes(t3.replace("\n", "\r\n").encode("utf-8"))
print("NanoEventRuntimeService OK")
