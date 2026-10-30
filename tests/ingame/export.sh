#!/usr/bin/env bash
# Character export round trip: build a mercenary from mercenary.json, export it, check the file, and apply the
# export to a second mercenary. Precondition: main menu. Nothing is saved; both mercenaries live only in the session.
source "$(dirname "$0")/lib.sh"
push_builds
SCRATCH=$(mktemp -d)
trap 'rm -rf "$SCRATCH"' EXIT
REPO="$HERE/../.."
load_fixture

# create_mercenary <build file> — open a mercenary's creation, apply the build, print the result line.
create_mercenary() {
  bridge 'invoke Kingmaker.Game.Instance.Player.CreateCustomCompanion' 'wait 6' >/dev/null
  bridge "invoke WrathBuildPlanner.Engine.TestHooks.Assign $1" >/dev/null
  press_apply
}

# complete_creation <name> — fill what the build leaves open, name the unit and press Complete.
complete_creation() {
  bridge 'wait 2' 'invoke WrathBuildPlanner.Engine.TestHooks.FillRest' 'wait 2' >/dev/null
  for i in $(seq 1 16); do
    bridge "settext NameField|$1" >/dev/null
    if bridge 'ui Bottom/BackNextButtons' | grep -q '| Complete |'; then break; fi
    bridge 'clicktext Next' 'wait 1.5' >/dev/null
  done
  bridge "shot export-$1" 'clicktext Complete' 'wait 5' >/dev/null
}

export_unit() { bridge "invoke WrathBuildPlanner.Engine.TestHooks.Export $1" | sed -n 's/^returned //p' | tr -d '\r'; }
fetch() { ssh -o ConnectTimeout=6 deck-direct "cat '$MOD_DIR_DECK/Builds/$1'" > "$SCRATCH/$1"; }

echo "--- a mercenary built from mercenary.json"
create_mercenary mercenary.json >/dev/null
uid=$(bridge 'get WrathBuildPlanner.Engine.WindowTracker.Controller.Unit.UniqueId' | tail -1 | tr -d '\r')
complete_creation WbpExport

echo "--- export"
out=$(export_unit "$uid")
echo "$out"
expect "$out" 'OK wbpexport-export.json' 'export stored under the character name'
expect "$out" 'gaps=export.gap.scores' 'point-buy reported as left open'
fetch wbpexport-export.json
( cd "$REPO" && node site/check.mjs "$SCRATCH/wbpexport-export.json" >"$SCRATCH/check.txt" 2>&1 )
expect_eq "$?" '0' 'site checker: no errors or warnings'
cat "$SCRATCH/check.txt" | tail -1
missing=$(python3 -I - "$REPO/tests/builds/mercenary.json" "$SCRATCH/wbpexport-export.json" <<'EOF'
import json, sys
src, exp = (json.load(open(p)) for p in sys.argv[1:3])
row = next(r for r in exp['levels'] if r['level'] == 1)
names = {n for p in row.get('picks', []) for n in ([p['pick']] if isinstance(p['pick'], str) else p['pick'])}
wanted = {p['pick'] for p in src['levels'][0]['picks']}
print(','.join(sorted(wanted - names)) or 'none', row['class'], exp['start']['race'])
EOF
)
expect_eq "$missing" 'none Fighter Human' 'class, race and every pick of mercenary.json in the export'

echo "--- a second export never overwrites the first"
expect "$(export_unit "$uid")" 'OK wbpexport-export-2.json' 'second export gets a suffix'

echo "--- the export applies to a fresh mercenary"
out=$(create_mercenary wbpexport-export.json)
echo "$out"
expect "$out" 'Race: Human — applied' 'race from the export'
# The export leaves the point-buy open, so feats with an ability requirement wait for it (Power Attack: Str 13).
expect "$out" 'Feat: Power Attack — not selectable (Strength' 'feat found, waits for the ability scores'
expect "$out" 'Bonus Combat Feat: Cleave — not selectable' 'bonus feat found, waits for Power Attack'
expect "$out" ', 2 open' 'nothing else open'
expect_not "$out" 'not found' 'every exported name matches'
bridge 'clickpath RoadmapMenuPlace/CloseButton' 'wait 2' >/dev/null

echo "--- the main character exports too"
out=$(export_unit main)
echo "$out"
expect "$out" 'OK ' 'main character exported'
file=$(sed -n 's/^OK \([^ ]*\).*/\1/p' <<<"$out")
fetch "$file"
( cd "$REPO" && node site/check.mjs "$SCRATCH/$file" >/dev/null 2>&1 )
expect_eq "$?" '0' 'main character export passes the checker'

# Only the files this run wrote: the player's own exports in the same folder stay.
ssh -o ConnectTimeout=6 deck-direct "cd '$MOD_DIR_DECK/Builds' && rm -f wbpexport-export.json wbpexport-export-2.json '$file'"
exit $FAILED
