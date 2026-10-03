#!/usr/bin/env bash
# Spec open points checked on the BridgeTest fixture: second-level spells, the page the window lands on
# when nothing is open. Precondition: game at the main menu. Nothing is saved.
source "$(dirname "$0")/lib.sh"
push_builds
load_fixture || exit 1
open_levelup 9000
WIN='WrathBuildPlanner.Engine.WindowTracker.Window'

for level in 2 3 4; do
  echo "--- level $level (Wizard $((level - 1)))"
  out=$(apply wizard3.json)
  echo "$out"
  expect_not "$out" 'Open Spell' "level $level: every listed spell learned"
  bridge 'invoke WrathBuildPlanner.Engine.TestHooks.FillRest' >/dev/null
  if [ "$level" = 4 ]; then
    # Everything is set now: pressing the bar's Apply must land on the summary page.
    bridge 'invoke WrathBuildPlanner.Engine.TestHooks.Assign wizard3.json' 'invoke WrathBuildPlanner.Engine.TestHooks.PressApply' 'wait 5' >/dev/null
    page=$(bridge "get $WIN.CurrentPhaseVM.Value" | tail -1)
    expect "$page" 'CharGenTotalPhaseVM' 'window lands on the summary when nothing is open'
    bridge 'invoke WrathBuildPlanner.Engine.TestHooks.Assign -' >/dev/null
  fi
  out=$(apply wizard3.json)
  expect_not "$out" 'Open Spell' "level $level: spells still set after filling the rest"
  finish "x$level" || exit 1
done

d=$(describe)
echo "$d"
expect "$d" 'WizardClass 3' 'three wizard levels committed'
expect "$d" 'Mirror Image' 'second-level spell learned'
expect "$d" 'Int 13' 'attribute point on Intelligence'
exit $FAILED
