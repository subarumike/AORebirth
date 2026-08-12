#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$script_dir"

dotnet run --project Tools/SourceInventoryGuard/SourceInventoryGuard.csproj -- \
  --repository-root .. \
  --manifest source-inventory/inventory.json \
  --check

dotnet build AORebirth.Linux.slnx --configuration Release --nologo
dotnet run --project Tools/CompatibilitySmokeTests/CompatibilitySmokeTests.csproj \
  --configuration Release \
  --no-build
