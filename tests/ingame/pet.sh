#!/usr/bin/env bash
# An animal companion levelled from its own build: the master becomes a Cavalier with a horse, then every master
# level-up is followed by the horse's (pet.json: archetype, skills, a Feat page pick, a bare pick, an attribute point).
# The horse's first level opens in creation mode and asks for a name. Precondition: main menu. Nothing is saved.
source "$(dirname "$0")/lib.sh"
H=WrathBuildPlanner.Engine.TestHooks
PET=Kingmaker.Game.Instance.Player.PartyAndPets[1]
push_builds
load_fixture || exit 1
bridge "invoke $MC.Progression.GainExperience 23000" 'wait 2' >/dev/null

value() { bridge "get $1" | tail -1 | tr -d '\r'; }

# level_pet — open the horse's level-up and apply its assigned build (assigned on its first level).
level_pet() {
  local out level
  level=$(value "$PET.Progression.CharacterLevel")
  bridge "invoke $H.OpenLevelUp $PET_NAME" 'wait 5' >/dev/null
  expect "$(value WrathBuildPlanner.Engine.WindowTracker.Controller.Unit.IsPet)" 'True' "horse window open (level $((level + 1)))"
  [ "$level" = 0 ] && bridge "invoke $H.Assign pet.json" >/dev/null
  out=$(press_apply)
  echo "$out"
  expect "$out" ', 0 open' "horse level $((level + 1)): nothing left open"
  if [ "$level" = 0 ]; then
    # Creation mode ends on the Name page, whose Next stays off until a name is typed.
    for i in $(seq 1 12); do
      bridge 'settext NameField|Sturmwind' >/dev/null
      bridge 'ui Bottom/BackNextButtons' | grep -q '| Complete |' && break
      bridge 'clicktext Next' 'wait 1.5' >/dev/null
    done
    PET_NAME=Sturmwind
  fi
  finish "pet-l$((level + 1))"
}

# Each Complete makes the game open the unit's next pending level on its own; OpenLevelUp brings up the
# right unit either way. The horse joins at Cavalier 1 without a level and levels once the master is done.
for master in 2 3 4 5 6; do
  echo "--- master level $master"
  bridge "invoke $H.OpenLevelUp main" 'wait 4' >/dev/null
  apply pet-master.json | cut -c1-200
  bridge "invoke $H.FillRest" >/dev/null
  finish "master-l$master" || exit 1
done
expect_eq "$(value "$PET.Progression.CharacterLevel")" '0' 'horse joined without a level'
PET_NAME=Horse
for pet in 1 2 3 4; do level_pet || exit 1; done

d=$(bridge "invoke $H.DescribeUnit Sturmwind" | sed -n 's/^returned //p')
echo "$d"
expect "$d" 'Sturmwind L4' 'horse renamed and at level 4'
expect "$d" 'AnimalCompanionClass 4 (BulwarkArchetype)' 'pet class and archetype'
expect "$d" 'BasicFeatSelection@1=Toughness' 'Feat page pick at level 1'
expect "$d" '@3=Dodge' 'bare pick at level 3'
expect "$d" 'Str 17' 'attribute point on Strength at level 4'

# The Builds window lists the horse under its own name only (it comes first: its level 5 is open).
rows=$(bridge "invoke $H.PartyRows" | sed -n 's/^returned //p' | tr -d '\r')
expect_eq "$rows" 'Sturmwind | BridgeTest' 'Builds window rows'

log=$(ssh -o ConnectTimeout=6 deck-direct "cat \"\$(ls -t '$MOD_DIR_DECK/Logs/'*.log | head -1)\"")
expect "$log" "apply 'Test Horse' mode=CharGen level=1" 'mod log: horse creation applied'
expect "$log" "apply 'Test Horse' mode=LevelUp level=4" 'mod log: horse level-up applied'

exit $FAILED
