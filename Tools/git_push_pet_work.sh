#!/usr/bin/env bash
# Pull/merge from GitHub, commit MP pet work (45 files), push to origin/master.
# Run from Git Bash:  bash tools/git_push_pet_work.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

echo "Repo: $REPO_ROOT"
echo "Remote: https://github.com/subarumike/AORebirth"

if [ -d .git/rebase-merge ] || [ -d .git/rebase-apply ]; then
  echo "Stuck rebase detected — aborting so merge can proceed."
  git rebase --abort || true
fi

git fetch origin

echo "--- Pull (merge, not rebase) ---"
git pull origin master --no-rebase || {
  echo ""
  echo "MERGE CONFLICTS — fix files, then:"
  echo "  git add <fixed files>"
  echo "  git commit"
  echo "  bash tools/git_push_pet_work.sh   # or: git push origin master"
  exit 1
}

PREFIX="AORebirth"
FILES=(
  "$PREFIX/Server/ZoneEngine/ZoneEngine.csproj"
  "$PREFIX/Server/ZoneEngine/ChatCommands/ChatCommandGivePetShell.cs"
  "$PREFIX/Server/ZoneEngine/Core/ActiveNanoRuntimeService.cs"
  "$PREFIX/Server/ZoneEngine/Core/NanoEventRuntimeService.cs"
  "$PREFIX/Server/ZoneEngine/Core/ZoneClient.cs"
  "$PREFIX/Server/ZoneEngine/Core/InventoryContainerRuntimeService.cs"
  "$PREFIX/Server/ZoneEngine/Core/PetCommandService.cs"
  "$PREFIX/Server/ZoneEngine/Core/PetHealNanoCatalog.cs"
  "$PREFIX/Server/ZoneEngine/Core/PetCombatRules.cs"
  "$PREFIX/Server/ZoneEngine/Core/PetRuntimeService.cs"
  "$PREFIX/Server/ZoneEngine/Core/PetSlotClassifier.cs"
  "$PREFIX/Server/ZoneEngine/Core/PetSummonSpellListService.cs"
  "$PREFIX/Server/ZoneEngine/Core/PetSummonSpellListBuilder.cs"
  "$PREFIX/Server/ZoneEngine/Core/PetSummonScfuExtensions.cs"
  "$PREFIX/Server/ZoneEngine/Core/PetSummonNanoCatalog.cs"
  "$PREFIX/Server/ZoneEngine/Core/PetSummonCaptureWireReplayer.cs"
  "$PREFIX/Server/ZoneEngine/Core/PetMobTemplateResolver.cs"
  "$PREFIX/Server/ZoneEngine/Core/PetShellItemService.cs"
  "$PREFIX/Server/ZoneEngine/Core/Functions/GameFunctions/summonpet.cs"
  "$PREFIX/Server/ZoneEngine/Core/Functions/GameFunctions/summonpets.cs"
  "$PREFIX/Server/ZoneEngine/Core/MessageHandlers/AddPetMessageHandler.cs"
  "$PREFIX/Server/ZoneEngine/Core/MessageHandlers/RemovePetMessageHandler.cs"
  "$PREFIX/Server/ZoneEngine/Core/MessageHandlers/PetCommandMessageHandler.cs"
  "$PREFIX/Server/ZoneEngine/Core/MessageHandlers/CastNanoMessageHandler.cs"
  "$PREFIX/Server/ZoneEngine/Core/MessageHandlers/CharacterActionMessageHandler.cs"
  "$PREFIX/Server/ZoneEngine/Core/MessageHandlers/LookAtMessageHandler.cs"
  "$PREFIX/Server/ZoneEngine/Core/MessageHandlers/ChatCmdMessageHandler.cs"
  "$PREFIX/Server/ZoneEngine/Core/Controllers/PlayerController.cs"
  "$PREFIX/Server/ZoneEngine/Core/Controllers/NPCController.cs"
  "$PREFIX/Server/ZoneEngine/Core/Playfields/Playfield.cs"
  "$PREFIX/Server/ZoneEngine/Core/Playfields/NPCRuntimeService.cs"
  "$PREFIX/Server/ZoneEngine/Core/Playfields/NpcCombatTickCoordinator.cs"
  "$PREFIX/Server/ZoneEngine/Core/Playfields/NpcCombatAttackRules.cs"
  "$PREFIX/Server/ZoneEngine/Core/Playfields/PlayfieldNpcCombatMovementRuntimeService.cs"
  "$PREFIX/Server/ZoneEngine/Core/Playfields/PlayfieldAnnouncementRuntimeService.cs"
  "$PREFIX/Server/ZoneEngine/Core/Playfields/CapturedSubwayOrdinarySpawnOrchestrator.cs"
  "$PREFIX/Server/ZoneEngine/Core/Playfields/CapturedSubwaySpawnOrchestrator.cs"
  "$PREFIX/Server/ZoneEngine/Core/Packets/SimpleCharFullUpdate.cs"
  "$PREFIX/Libraries/Source/AORebirth.ObjectManager/Pool.cs"
  "$PREFIX/Libraries/Source/AORebirth.Core/NPCHandler/NonPlayerCharacterHandler.cs"
  "$PREFIX/Libraries/Source/AORebirth.Stats/SpecialStats/StatMaxNanoEnergy.cs"
  "$PREFIX/Libraries/Source/AORebirth.Database/Dao/PetSummonMobTemplateCatalog.cs"
  "$PREFIX/Libraries/Source/AORebirth.Database/Dao/MobTemplateDao.cs"
  "$PREFIX/Libraries/Source/AORebirth.Database/SqlTables/mobtemplate.sql"
  "$PREFIX/Libraries/Source/AORebirth.Database/AORebirth.Database.csproj"
)

echo "--- Stage pet work files ---"
for f in "${FILES[@]}"; do
  if [ -f "$f" ]; then
    git add "$f"
    echo "  staged: $f"
  else
    echo "  MISSING (skipped): $f"
  fi
done

if git diff --cached --quiet; then
  echo "Nothing new to commit (already committed or no changes)."
else
  git commit -m "$(cat <<'EOF'
Add MP pet summon, dual-slot, combat, and heal groundwork.

Belamorte (BSLX/heal) and Demon (PT56/attack) with SpellList slots,
pet commands, attack combat packets, heal cast sequence, pool/zone
fixes, and mobtemplate rows for sharing with collaborators.
EOF
)"
fi

echo "--- Push ---"
git push origin master

echo "Done. https://github.com/subarumike/AORebirth"
