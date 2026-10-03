#!/usr/bin/env bash
# Character creation from the main menu with tests/builds/fighter.json. Starts a throwaway new game.
# Precondition: game at the main menu (dev-bridge/launch.sh). Does not touch existing saves.
source "$(dirname "$0")/lib.sh"
push_builds

bridge 'clicktext New game' 'wait 3' 'clicktext Main Story' 'wait 1' 'clicktext Next' 'wait 3' 'clicktext Next' 'wait 8' >/dev/null
bridge 'invoke WrathBuildPlanner.Engine.TestHooks.Assign fighter.json' >/dev/null
out=$(bridge 'invoke WrathBuildPlanner.Engine.TestHooks.PressApply' | sed -n 's/^returned //p')
echo "$out"
expect "$out" 'Race: Human' 'race applied'
expect "$out" 'Class: Fighter' 'class applied'
expect "$out" 'Ability scores: 16 14 14 12 12 11' 'point-buy applied'
expect "$out" 'Alignment: Lawful Good' 'alignment applied'

# The page jump happens a few frames after Apply.
page=$(bridge 'wait 2' 'get WrathBuildPlanner.Engine.WindowTracker.Window.CurrentPhaseVM.Value' | tail -1)
expect "$page" 'Portrait' 'window moved to the first page that needs the player'

again=$(bridge 'invoke WrathBuildPlanner.Engine.TestHooks.PressApply' | sed -n 's/^returned //p')
expect "$again" '0 applied' 'second apply changes nothing'

bridge 'shot chargen-applied' >/dev/null
echo "screenshot: $BRIDGE_DIR/shots/chargen-applied.png"
exit $FAILED
