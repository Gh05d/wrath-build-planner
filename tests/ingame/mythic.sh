#!/usr/bin/env bash
# Mythic ranks 1 and 2 on the BridgeTest fixture. Precondition: game at the main menu. Nothing is saved.
source "$(dirname "$0")/lib.sh"
push_builds
load_fixture || exit 1

bridge "invoke $MC.Progression.GainMythicExperience 2" 'wait 2' 'clickpath PartyCharacterView_01/Buttons/Mythic' 'wait 4' >/dev/null
out=$(apply multiclass.json)
echo "$out"
expect "$out" 'Applied Close to the Heavens' 'bare mythic pick'
expect "$out" 'Applied Mythic Ability: Archmage Armor' 'scoped mythic pick'
finish m1 || exit 1
expect "$(bridge "get $MC.Progression.MythicLevel" | tail -1)" '1' 'mythic rank 1 committed'

# The rank-2 window may open by itself; if not, the portrait button is back and this opens it.
bridge 'clickpath PartyCharacterView_01/Buttons/Mythic' 'wait 4' >/dev/null
out=$(apply multiclass.json)
echo "$out"
expect "$out" 'level=2' 'rank 2 row found'
bridge 'invoke WrathBuildPlanner.Engine.TestHooks.FillRest' >/dev/null
finish m2 || exit 1
expect "$(bridge "get $MC.Progression.MythicLevel" | tail -1)" '2' 'mythic rank 2 committed'

echo "--- rank 3: a path the game does not offer is left to the player"
bridge "invoke $MC.Progression.GainMythicExperience 1" 'wait 2' 'clickpath PartyCharacterView_01/Buttons/Mythic' 'wait 4' >/dev/null
out=$(apply path-lich.json)
echo "$out"
expect "$out" 'Open Mythic path: Lich (LeftToPlayer' 'locked path is not selected'
expect_eq "$(bridge 'get WrathBuildPlanner.Engine.WindowTracker.Controller.State.SelectedClass' | tail -1)" 'null' 'no mythic class selected after a locked path'
out=$(apply path-angel.json)
echo "$out"
expect "$out" 'Applied Mythic path: Angel' 'offered path is selected'

exit $FAILED
