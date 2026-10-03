#!/usr/bin/env bash
# Behaviour outside the happy path. Precondition: game at the main menu. Nothing is saved.
source "$(dirname "$0")/lib.sh"
push_builds

echo "--- no window open"
expect "$(apply fighter.json)" 'NO WINDOW' 'apply without a window reports it'
expect "$(bridge 'invoke WrathBuildPlanner.Engine.TestHooks.PressApply' | sed -n 's/^returned //p')" 'NO BAR' 'no bar without a window'

load_fixture || exit 1

echo "--- build has no row for this level"
open_levelup 2000
cat > /tmp/wbp-norow.json <<'JSON'
{ "format": 1, "name": "Only Level Nine", "levels": [ { "level": 9, "class": "Fighter" } ] }
JSON
ssh -o ConnectTimeout=6 deck-direct "cat > '$MOD_DIR_DECK/Builds/norow.json'" < /tmp/wbp-norow.json
expect "$(apply norow.json)" 'NO ROW level=2' 'missing row reported, nothing applied'
expect_eq "$(bridge 'get WrathBuildPlanner.Engine.WindowTracker.Controller.State.SkillPointsRemaining' | tail -1)" '3' 'skill points untouched'

echo "--- assigned file deleted"
bridge 'invoke WrathBuildPlanner.Engine.TestHooks.Assign norow.json' >/dev/null
ssh -o ConnectTimeout=6 deck-direct "rm -f '$MOD_DIR_DECK/Builds/norow.json'"
bridge 'invoke WrathBuildPlanner.UI.BuildsWindow.Toggle' 'wait 1' 'invoke WrathBuildPlanner.UI.BuildsWindow.Toggle' >/dev/null
out=$(bridge 'invoke WrathBuildPlanner.Engine.TestHooks.PressApply' | sed -n 's/^returned //p')
expect "$out" 'NO REPORT' 'missing build file: nothing applied'
expect "$(bridge 'tree WrathBuildPlannerBar' | grep -m1 "Name")$(bridge 'tree WrathBuildPlannerBar')" 'Build missing: norow.json' 'bar says the build is missing'
bridge 'shot guard-missing' >/dev/null
bridge 'invoke WrathBuildPlanner.Engine.TestHooks.Assign -' >/dev/null

echo "--- history mismatch is a notice, not a stop"
cat > /tmp/wbp-history.json <<'JSON'
{ "format": 1, "name": "Expects Wizard", "levels": [ { "level": 1, "class": "Wizard" }, { "level": 2, "class": "Fighter" } ] }
JSON
ssh -o ConnectTimeout=6 deck-direct "cat > '$MOD_DIR_DECK/Builds/history.json'" < /tmp/wbp-history.json
out=$(apply history.json)
expect "$out" 'HISTORY expected[Wizard 1] actual[Fighter 1]' 'history notice'
expect "$out" 'Class: Fighter' 'level still applied'
expect_not "$out" 'Open Class' 'class not left open'
ssh -o ConnectTimeout=6 deck-direct "rm -f '$MOD_DIR_DECK/Builds/history.json'"

exit $FAILED
