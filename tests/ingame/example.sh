#!/usr/bin/env bash
# The shipped example build in a real character creation, the cancel path, and the binding of a
# creation-time assignment to the new game. Starts a throwaway new game. Precondition: main menu.
source "$(dirname "$0")/lib.sh"
push_builds
tar -C "$HERE/../../Builds-examples" -cf - . | ssh -o ConnectTimeout=6 -o ServerAliveInterval=5 -o ServerAliveCountMax=3 deck-direct "tar -xf - -C '$MOD_DIR_DECK/Builds'"
PENDING='WrathBuildPlanner.Persistence.AssignmentStore.pendingForNewGame'
enter_creation() { bridge 'clicktext New game' 'wait 3' 'clicktext Main Story' 'wait 1' 'clicktext Next' 'wait 3' 'clicktext Next' 'wait 8' >/dev/null; }

echo "--- cancelling creation drops the pending assignment"
enter_creation
bridge 'invoke WrathBuildPlanner.Engine.TestHooks.Assign fighter.json' >/dev/null
expect_eq "$(bridge "get $PENDING" | tail -1)" 'fighter.json' 'assignment is pending during creation'
bridge 'clickpath RoadmapMenuPlace/CloseButton' 'wait 2' 'ui' > /tmp/wbp-close.txt
# The game asks for confirmation; take the confirming button whatever it is called in this language.
yes=$(grep -iE '\| (Yes|OK|Confirm|Accept) \|' /tmp/wbp-close.txt | head -1 | cut -d'|' -f1 | tr ',' ' ')
[ -n "$yes" ] && bridge "click $yes" 'wait 3' >/dev/null
expect_eq "$(bridge "get $PENDING" | tail -1)" 'null' 'pending assignment cleared after cancelling'

echo "--- example build, level 1"
bridge 'wait 2' >/dev/null
enter_creation
bridge 'invoke WrathBuildPlanner.Engine.TestHooks.Assign two-handed-fighter.json' >/dev/null
# This second creation defers Apply behind the premade page; a quick second press must not run it twice.
twice=$(bridge 'invoke WrathBuildPlanner.Engine.TestHooks.PressApply' 'invoke WrathBuildPlanner.Engine.TestHooks.PressApply' | sed -n 's/^returned //p')
expect_eq "$(grep -c '^DEFERRED' <<<"$twice")" '2' 'a second press while Apply waits does not run it'
out=$(bridge 'wait 2' 'invoke WrathBuildPlanner.Engine.TestHooks.BarText' | sed -n 's/^returned //p' | sed 's/^.*details([^)]*)=//')
echo "$out"
expect "$out" ', 0 open' 'example build applies completely at level 1'
# A second creation in the session opens on the portrait page with the premade still set (2026-10-06: blank page).
bridge 'wait 2' >/dev/null
page_not_blank example-after-apply 'the page shown after Apply is not blank'
expect "$out" 'Archetype: Two-Handed Fighter' 'archetype in the example'
expect "$out" 'Weapon Focus > Greatsword' 'chain in the example'

echo "--- finishing creation binds the build to the new main character"
bridge 'wait 2' 'click 193 375' 'wait 1' >/dev/null
for i in $(seq 1 16); do
  bridge 'settext NameField|WbpExample' >/dev/null
  if bridge 'ui Bottom/BackNextButtons' | grep -q '| Complete |'; then break; fi
  bridge 'clicktext Next' 'wait 1.5' >/dev/null
done
bridge 'shot example-total' 'clicktext Complete' 'wait 5' 'waitload' >/dev/null
# Earlier runs leave their own files behind: look at the file of the game that was just started.
game=$(bridge 'get Kingmaker.Game.Instance.Player.GameId' | tail -1 | tr -d '\r')
bound=$(ssh -o ConnectTimeout=6 deck-direct "grep -c 'two-handed-fighter.json' '$MOD_DIR_DECK/UserSettings/assignments-$game.json'")
expect_eq "$bound" '1' 'assignment written for the new game'
expect "$(describe)" 'FighterClass 1 (TwoHandedFighterArchetype)' 'created character has the archetype'
exit $FAILED
