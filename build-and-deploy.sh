#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

GAME_DIR="$(cd ../.. && pwd)"
PLUGINS_DIR="$GAME_DIR/BepInEx/plugins"

dotnet build ValheimSkillCapMod/ValheimSkillCapMod.Local.csproj -c Debug
cp ValheimSkillCapMod/bin/Debug/net48/ValheimSkillCapMod.dll "$PLUGINS_DIR/"
echo "Deployed to $PLUGINS_DIR/ValheimSkillCapMod.dll"
