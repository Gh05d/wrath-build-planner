#!/usr/bin/env bash
# Character creation from the main menu with tests/builds/fighter.json. Starts a throwaway new game.
# Precondition: game at the main menu (dev-bridge/launch.sh). Does not touch existing saves.
source "$(dirname "$0")/lib.sh"
push_builds

bridge 'clicktext New game' 'wait 3' 'clicktext Main Story' 'wait 1' 'clicktext Next' 'wait 3' 'clicktext Next' 'wait 8' >/dev/null
out=$(apply fighter.json)
echo "$out"
expect "$out" 'Applied Race: Human' 'race applied'
expect "$out" 'Applied Racial bonus: Strength' 'racial bonus applied'
expect "$out" 'Applied Class: Fighter' 'class applied'
expect "$out" 'Applied Ability scores: 16 14 14 12 12 11' 'point-buy applied'
expect "$out" 'Applied Alignment: Lawful Good' 'alignment applied'
expect "$out" 'Skills: 4 point(s) spent' 'skills spent'

again=$(apply fighter.json)
expect "$again" 'applied=0' 'second apply changes nothing'

bridge 'shot chargen-applied' >/dev/null
echo "screenshot: $BRIDGE_DIR/shots/chargen-applied.png"
exit $FAILED
