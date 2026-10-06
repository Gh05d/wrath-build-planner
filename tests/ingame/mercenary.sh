#!/usr/bin/env bash
# A mercenary's creation: build applied (the race replaces the preselected one), assignment bound to the new unit.
# Opens the creation through Player.CreateCustomCompanion on the fixture (no vendor, no gold). Precondition: main menu.
# Nothing is saved; the new mercenary only lives in the running session.
source "$(dirname "$0")/lib.sh"
push_builds
load_fixture
before=$(bridge 'get Kingmaker.Game.Instance.Player.AllCharacters.Count' | tail -1 | tr -d '\r')

bridge 'invoke Kingmaker.Game.Instance.Player.CreateCustomCompanion' 'wait 6' >/dev/null
expect "$(bridge 'get WrathBuildPlanner.Engine.WindowTracker.Controller.Unit.Blueprint' | tail -1)" 'CustomCompanion' 'mercenary creation is open'
bridge 'invoke WrathBuildPlanner.Engine.TestHooks.Assign mercenary.json' >/dev/null
out=$(bridge 'invoke WrathBuildPlanner.Engine.TestHooks.PressApply' | sed -n 's/^returned //p')
echo "$out"
expect "$out" 'Race: Human — applied' 'race replaces the preselected one'
expect "$out" 'Ability scores: 16 14 14 10 10 10 — applied' '20-point scores applied'
expect "$out" ', 0 open' 'nothing left open'

uid=$(bridge 'get WrathBuildPlanner.Engine.WindowTracker.Controller.Unit.UniqueId' | tail -1 | tr -d '\r')
# The test build leaves background, deity and the human bonus feat open: fill them so the game lets it complete.
bridge 'wait 2' 'invoke WrathBuildPlanner.Engine.TestHooks.FillRest' 'wait 2' >/dev/null
for i in $(seq 1 16); do
  bridge 'settext NameField|WbpMerc' >/dev/null
  if bridge 'ui Bottom/BackNextButtons' | grep -q '| Complete |'; then break; fi
  bridge 'clicktext Next' 'wait 1.5' >/dev/null
done
bridge 'shot mercenary-total' 'clicktext Complete' 'wait 5' >/dev/null
game=$(bridge 'get Kingmaker.Game.Instance.Player.GameId' | tail -1 | tr -d '\r')
bound=$(ssh -o ConnectTimeout=6 deck-direct "grep -c '\"$uid\": \"mercenary.json\"' '$MOD_DIR_DECK/UserSettings/assignments-$game.json'")
expect_eq "$bound" '1' 'assignment bound to the new mercenary'
after=$(bridge 'get Kingmaker.Game.Instance.Player.AllCharacters.Count' | tail -1 | tr -d '\r')
expect_eq "$after" "$((before + 1))" 'the mercenary exists after Complete'
exit $FAILED
