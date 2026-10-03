#!/usr/bin/env bash
# Helpers for in-game tests through DevBridge. Source this file. Requires the game to be up (dev-bridge/launch.sh).
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BRIDGE_DIR="$(cd "$HERE/../../../dev-bridge" && pwd)"
MOD_DIR_DECK="/run/media/deck/3b03f019-ee3d-473e-beb1-98236afc5254/steamapps/common/Pathfinder Second Adventure/Mods/WrathBuildPlanner"
MC='Kingmaker.Game.Instance.Player.MainCharacter.Value'
FAILED=0

bridge() { bash "$BRIDGE_DIR/bridge.sh" "$@"; }

# push_builds — copy tests/builds/*.json into the mod's Builds folder on the Deck.
push_builds() { tar -C "$HERE/../builds" -cf - . | ssh -o ConnectTimeout=6 -o ServerAliveInterval=5 -o ServerAliveCountMax=3 deck-direct "mkdir -p '$MOD_DIR_DECK/Builds' && tar -xf - -C '$MOD_DIR_DECK/Builds'"; }

# apply <file> — apply the open window's level from a build file; prints the report line.
apply() { bridge "invoke WrathBuildPlanner.Engine.TestHooks.Apply $1" | sed -n 's/^returned //p'; }

describe() { bridge 'invoke WrathBuildPlanner.Engine.TestHooks.Describe' | sed -n 's/^returned //p'; }

# expect <text> <needle> <what> — assertion with a readable verdict.
expect() {
  if grep -qF -- "$2" <<<"$1"; then echo "PASS $3"; else echo "FAIL $3 — missing: $2"; echo "     got: $1"; FAILED=1; fi
}

# expect_not <text> <needle> <what> — the needle must NOT occur.
expect_not() {
  if grep -qF -- "$2" <<<"$1"; then echo "FAIL $3 — unexpected: $2"; echo "     got: $1"; FAILED=1; else echo "PASS $3"; fi
}

# expect_eq <actual> <expected> <what> — exact comparison.
expect_eq() {
  local actual
  actual=$(tr -d '\r' <<<"$1" | sed 's/[[:space:]]*$//')   # results come from a Windows process: strip CR
  if [ "$actual" = "$2" ]; then echo "PASS $3"; else echo "FAIL $3 — expected '$2', got '$actual'"; FAILED=1; fi
}

# finish <shot name> — page forward until Complete shows, screenshot the summary, press Complete.
finish() {
  local i out
  for i in $(seq 1 18); do
    out=$(bridge 'ui Bottom/BackNextButtons')
    if grep -q '| Complete |' <<<"$out"; then
      bridge "shot $1" 'clicktext Complete' 'wait 4' >/dev/null
      return 0
    fi
    bridge 'clicktext Next' 'wait 1.5' >/dev/null
  done
  echo "FAIL finish: Complete never appeared ($1)"; bridge "shot $1-stuck" >/dev/null; FAILED=1; return 1
}

# load_fixture — from the main menu, load the BridgeTest autosave (group must be listed; coordinates come from ui).
load_fixture() {
  local out header load
  out=$(bridge 'clicktext Load game' 'wait 3' 'ui')
  header=$(grep -n 'BridgeTest' <<<"$out" | head -1 | cut -d: -f1)
  [ -n "$header" ] || { echo "FAIL load_fixture: no BridgeTest group"; FAILED=1; return 1; }
  load=$(tail -n +"$header" <<<"$out" | grep -m1 '| Load |' | cut -d'|' -f1 | tr ',' ' ')
  [ -n "$load" ] || { bridge 'clicktext BridgeTest' 'wait 1' >/dev/null; out=$(bridge 'ui'); load=$(tail -n +"$(grep -n 'BridgeTest' <<<"$out" | head -1 | cut -d: -f1)" <<<"$out" | grep -m1 '| Load |' | cut -d'|' -f1 | tr ',' ' '); }
  bridge "click $load" 'waitload' | grep -E 'loaded|TIMEOUT'
}

# open_levelup <xp> — grant XP to the main character and open the level-up window.
open_levelup() {
  bridge "invoke $MC.Progression.GainExperience $1" 'wait 2' 'clickpath PartyCharacterView_01/Buttons/LevelUp' 'wait 4' >/dev/null
}
