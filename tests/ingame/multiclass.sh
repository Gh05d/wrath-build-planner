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
expect "$out" 'Applied Spell: Magic Missile' 'prepared-caster spell'
expect "$out" 'Applied Spell: Shield' 'spell whose blueprint name differs (MageShield)'
finish l2 || exit 1
d=$(describe)
expect "$d" 'WizardClass 1' 'Wizard level committed'
expect "$d" 'L1[Magic Missile, Grease, Mage Armor, Shield]' 'four spells in the spellbook'

echo "--- level 3: Sorcerer (Sage)"
out=$(apply multiclass.json)
echo "$out"
expect "$out" 'Applied Class: Sorcerer' 'class switch to Sorcerer'
expect "$out" 'Applied Archetype: Sage Sorcerer' 'archetype'
expect "$out" 'Applied Weapon Focus (Longsword)' 'parenthesis notation read as a chain'
expect "$out" 'Applied Sorcerer Bonus Feat: Combat Casting' 'scoped pick'
expect "$out" 'Open Not A Real Feat (NotFound' 'unknown name reported, rest applied'
expect "$out" 'Applied Spell: Mage Armor' 'spontaneous-caster spell after archetype swap'
bridge 'invoke WrathBuildPlanner.Engine.TestHooks.FillRest' >/dev/null
# A feature chosen after the spells can make the game drop the spell picks; a second apply restores them.
out=$(apply multiclass.json)
echo "$out"
expect "$out" 'AlreadySet Weapon Focus (Longsword)' 'second apply reports earlier picks as set'
expect "$out" 'Spell: Mage Armor' 'spells present after a later feature pick'
finish l3 || exit 1
d=$(describe)
expect "$d" 'SorcererClass 1 (SageSorcererArchetype)' 'Sorcerer with archetype committed'
expect "$d" 'SageSpellbook: L1[Mage Armor, Grease]' 'spells on the archetype spellbook'

echo "--- level 4: Cavalier"
out=$(apply multiclass.json)
echo "$out"
expect "$out" 'Applied Class: Cavalier' 'class switch to Cavalier'
expect "$out" 'Applied Attribute point: Charisma' 'attribute point'
expect "$out" 'Applied Order: Order of the Cockatrice' 'scoped pick'
expect "$out" 'Applied Horse' 'bare pick with category prefix (Animal Companion — Horse)'
finish l4 || exit 1
d=$(describe)
expect "$d" 'Cha 12' 'Charisma raised'
expect "$d" 'AnimalCompanionUnitHorse' 'mount in the party'

exit $FAILED
