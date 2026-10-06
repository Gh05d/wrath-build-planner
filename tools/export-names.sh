#!/usr/bin/env bash
# Exports site/data/names.json from the running game. Precondition: game running in English with DevBridge
# (../dev-bridge/launch.sh); the main menu is enough. Re-run after a game patch that changes content.
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"
BRIDGE="$REPO/../dev-bridge/bridge.sh"
MOD_DIR_DECK="/run/media/deck/3b03f019-ee3d-473e-beb1-98236afc5254/steamapps/common/Pathfinder Second Adventure/Mods/WrathBuildPlanner"

out=$(bash "$BRIDGE" 'invoke WrathBuildPlanner.Engine.TestHooks.ExportNames' | sed -n 's/^returned //p' | tr -d '\r')
echo "$out"
case "$out" in pages=*) ;; *) echo "export failed" >&2; exit 1 ;; esac
# Through a temp file: a failed transfer must not truncate the committed file.
tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT
ssh -o ConnectTimeout=6 deck-direct "cat '$MOD_DIR_DECK/names.json'" > "$tmp"
python3 -c 'import json, sys; d = json.load(open(sys.argv[1])); assert d["pages"] and d["features"]' "$tmp"
chmod 644 "$tmp"
mv "$tmp" "$REPO/site/data/names.json"
ls -l "$REPO/site/data/names.json"
# The plain prompt files for AI agents list the page titles: keep them in step.
node "$REPO/tools/agent-files.mjs"
