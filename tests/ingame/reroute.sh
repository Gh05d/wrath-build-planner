#!/usr/bin/env bash
# A page the build names that is not part of the level ("Bonus Feat" for the human bonus feat, which the game shows
# as a second "Feat"): the pick goes to the open page that offers it, but only after the picks with a matching
# page have had their slots, and the result says where it went. Fixture: human Fighter 1. Nothing is saved.
source "$(dirname "$0")/lib.sh"
push_builds
load_fixture || exit 1
open_levelup 5000   # enough for levels 2 and 3; the window reopens for the next level after Complete

echo "--- level 2: one Bonus Combat Feat slot, the exact pick listed second still gets it"
out=$(apply reroute.json)
echo "$out"
expect "$out" 'Applied Bonus Combat Feat: Improved Initiative' 'the pick with a matching page takes the slot'
expect "$out" 'Open Bonus Feat: Dodge (SelectionMissing' 'the rerouted pick does not take its place'
bridge 'invoke WrathBuildPlanner.Engine.TestHooks.FillRest' >/dev/null
finish reroute-l2 || exit 1

echo "--- level 3: only a Feat page; Bonus Feat: Dodge lands there and says so"
out=$(apply reroute.json)
echo "$out"
expect "$out" 'Applied Bonus Feat: Dodge (on the page Feat' 'rerouted to the Feat page, with the page named'
expect "$out" 'Open Bonus Feat: No Such Feat Anywhere (SelectionMissing' 'a name no page offers keeps "no such selection"'
again=$(apply reroute.json)
expect "$again" 'AlreadySet Bonus Feat: Dodge' 'a second Apply sees the rerouted pick as set'
bar=$(bridge 'invoke WrathBuildPlanner.Engine.TestHooks.PressApply' | sed -n 's/^returned //p')
expect "$bar" 'Dodge' 'the bar result lists the pick'
exit $FAILED
