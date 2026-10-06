#!/usr/bin/env bash
# A spontaneous caster over four levels (Fighter 1, Sorcerer 1-4): new spells only where the class grants them
# (none at Sorcerer 2), the ability point at character level 4, a second-level spell at Sorcerer 4.
# Wrath's level-up has no spell swap (only SelectSpell/UnselectSpell, IL 2026-10-06), so known spells
# only grow. Precondition: main menu. Nothing is saved.
source "$(dirname "$0")/lib.sh"
push_builds
load_fixture || exit 1
open_levelup 15000

for level in 2 3 4 5; do
  echo "--- level $level"
  out=$(apply spontaneous.json)
  echo "$out"
  expect "$out" "level=$level" "level $level window"
  expect "$out" 'Class: Sorcerer' "level $level: Sorcerer"
  expect_not "$out" 'Open Spell' "level $level: every listed spell learned"
  bridge 'invoke WrathBuildPlanner.Engine.TestHooks.FillRest' >/dev/null
  again=$(apply spontaneous.json)
  expect_not "$again" 'Open Spell' "level $level: spells still set after filling the rest"
  finish "spont-l$level" || exit 1
done
d=$(describe)
echo "$d"
expect "$d" 'SorcererClass 4' 'four Sorcerer levels committed'
expect "$d" 'Magic Missile' 'first-level spell known'
expect "$d" 'L2[Scorching Ray]' 'second-level spell known at Sorcerer 4'
exit $FAILED
