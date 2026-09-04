#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

EDITOR_VERSION="${1:-6000.0.61f1}"
PROJECT=Bundles
GAME_DIR="$(cd ../.. && pwd)"
PLUGINS_DIR="$GAME_DIR/BepInEx/plugins"

unity run "$PROJECT" \
  --editor-version "$EDITOR_VERSION" \
  --allow-install --no-banner --non-interactive --timeout 1800 \
  -- -executeMethod BuildFogBundle.Build -logFile "$PROJECT/build.log"

BUNDLE="$PROJECT/AssetBundles/overlaymapfog"
if [ ! -f "$BUNDLE" ]; then
    echo "Bundle not produced - see $PROJECT/build.log" >&2
    exit 1
fi

cp "$BUNDLE" "$PLUGINS_DIR/overlaymapfog"
echo "Deployed $PLUGINS_DIR/overlaymapfog"
