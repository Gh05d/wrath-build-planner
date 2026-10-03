#!/usr/bin/env bash
# Multiclass run on the BridgeTest fixture: Wizard, Sorcerer with archetype, Cavalier with mount.
# Precondition: game at the main menu. Nothing is saved.
source "$(dirname "$0")/lib.sh"
push_builds
load_fixture || exit 1
open_levelup 9000

echo "--- level 2: Wizard"
out=$(apply multiclass.json)
echo "$out"
expect "$out" 'Applied Class: Wizard' 'class switch to Wizard'
expect "$out" 'Applied Wizard Bonus Feat: Spell Focus > Evocation' 'scoped chain'
expect "$out" 'Applied School: Evocation' 'category prefix stripped (Specialist School — Evocation)'
expect "$out" 'Applied Opposition School: Necromancy' 'first of two equal selections'
expect "$out" 'Applied Opposition School: Abjuration' 'second of two equal selections'
expect "$out" 'Applied Hare Familiar' 'bare pick'
finish l2 || exit 1
d=$(describe)
expect "$d" 'WizardClass 1' 'Wizard level committed'
expect "$d" 'ArcaneBondSelection@1=HareFamiliarBondFeature' 'familiar committed'

exit $FAILED
