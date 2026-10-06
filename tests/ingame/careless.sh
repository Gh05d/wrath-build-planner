#!/usr/bin/env bash
# A careless player: junk in the Builds folder, hotkey spam, the prompt pasted instead of the answer, the AI's whole
# answer pasted, Apply hammered, the build swapped in the middle of a level-up. The mod must answer every time and
# leave no exception in Player.log. Precondition: main menu. Nothing is saved; the junk files are removed at the end.
source "$(dirname "$0")/lib.sh"
LOGDIR="/home/deck/.local/share/Steam/steamapps/compatdata/1184370/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Wrath Of The Righteous"
LOG="$LOGDIR/Player.log"
KEY="$BRIDGE_DIR/key.sh"
b64() { printf '%s' "$1" | base64 -w0; }
# Ctrl+P the way a hand presses it: xdotool's "ctrl+p" lands within one frame, so Unity sees Ctrl already released.
hotkey() {
  ssh -o ConnectTimeout=6 deck-direct "win=\$(DISPLAY=:1 xdotool search --name 'Pathfinder Wrath Of The Righteous' | head -1)
    for i in \$(seq 1 $1); do DISPLAY=:1 xdotool keydown --window \$win ctrl; sleep 0.12; DISPLAY=:1 xdotool key --window \$win p; sleep 0.12; DISPLAY=:1 xdotool keyup --window \$win ctrl; sleep 0.25; done"
}
import() { bridge "invoke WrathBuildPlanner.Engine.TestHooks.ImportText $(b64 "$1")" | sed -n 's/^returned //p'; }
press_apply() { bridge 'invoke WrathBuildPlanner.Engine.TestHooks.PressApply' | sed -n 's/^returned //p'; }
push_builds

echo "--- junk in the Builds folder"
JUNK=$(mktemp -d)
printf '' > "$JUNK/zz-empty.json"
echo 'lol not json' > "$JUNK/zz-garbage.json"
echo '[1, 2, 3]' > "$JUNK/zz-array.json"
python3 -c "import json; print(json.dumps({'format': 1, 'name': 'huge', 'levels': [{'level': 1, 'class': 'Fighter'}] * 40000}))" > "$JUNK/zz-huge.json"
mkdir "$JUNK/zz-folder.json"
cp "$HERE/../builds/fighter.json" "$JUNK/ZZ-UPPER.JSON"
cp "$HERE/../builds/fighter.json" "$JUNK/Ünïcødé Build.json"
tar -C "$JUNK" -cf - . | ssh -o ConnectTimeout=6 deck-direct "tar -xf - -C '$MOD_DIR_DECK/Builds'"
rm -rf "$JUNK"

bridge 'log CARELESS-START' >/dev/null
load_fixture
open_levelup 2000

echo "--- the Builds window under hotkey spam"
bash "$KEY" shift >/dev/null   # under gamescope the first synthetic key only focuses the window
# Spamming may drop a press while the window reads the (deliberately huge) Builds folder; it must stay responsive.
hotkey 5
before=$(bridge 'wait 2' 'get WrathBuildPlanner.UI.BuildsWindow.IsOpen' | tail -1 | tr -d '\r')
hotkey 1
after=$(bridge 'wait 2' 'get WrathBuildPlanner.UI.BuildsWindow.IsOpen' | tail -1 | tr -d '\r')
[ "$before" != "$after" ] && echo "PASS after spamming, one Ctrl+P still toggles ($before -> $after)" || { echo "FAIL Ctrl+P no longer toggles ($before -> $after)"; FAILED=1; }
[ "$after" = "True" ] || { hotkey 1; bridge 'wait 2' >/dev/null; }
expect_eq "$(bridge 'get WrathBuildPlanner.UI.BuildsWindow.IsOpen' | tail -1)" 'True' 'window open before the Escape check'
bash "$KEY" Escape >/dev/null
expect_eq "$(bridge 'get WrathBuildPlanner.UI.BuildsWindow.IsOpen' | tail -1)" 'False' 'Escape closes it'
expect_eq "$(bridge 'wait 1' 'ui' | grep -c '| Accept |')" '0' 'Escape does not ask to close the level-up (the game must not see it)'
expect_eq "$(bridge 'get WrathBuildPlanner.Engine.WindowTracker.Window' | tail -1 | grep -c 'CharGenVM')" '1' 'the Escape for the window does not close the level-up'

echo "--- the library survives every kind of file"
bridge 'invoke WrathBuildPlanner.UI.BuildsWindow.Toggle' 'wait 1' 'clicktext Reload folder' 'wait 2' 'shot careless-library' >/dev/null
count=$(bridge 'get WrathBuildPlanner.Main.Library.Entries.Count' | tail -1 | tr -d '\r')
[ "$count" -ge 13 ] && echo "PASS library lists every .json file ($count)" || { echo "FAIL library lists $count entries"; FAILED=1; }
errs=$(bridge 'ui all' | grep -c 'error(s)')
[ "$errs" -ge 4 ] && echo "PASS broken files show their errors ($errs)" || { echo "FAIL only $errs entries show errors"; FAILED=1; }

echo "--- pasting the wrong things"
expect "$(import '   ')" 'EMPTY' 'empty clipboard (whitespace: DevBridge cannot pass an empty argument)'
expect "$(import 'lol idk just make me op')" 'FAILED' 'junk text refused'
expect "$(import "$(printf 'You convert character builds for Pathfinder: Wrath of the Righteous into a file for the mod "Wrath Build Planner".\n\n{"format":1,"name":"Example","levels":[{"level":1,"class":"Fighter"}]}')")" 'prompt' 'the prompt is refused, its example is not imported'
answer=$(printf 'Here you go!\n```text\nnotes\n```\n```json\n{"format":1,"name":"Careless Paste","levels":[{"level":2,"class":"Fighter","picks":[{"in":"Bonus Combat Feat","pick":"Cleave"}]}]}\n```\n```\nLeft out:\n- deity\n```')
expect "$(import "$answer")" 'OK careless-paste.json' "the AI's whole answer with three blocks imports"
bridge 'invoke WrathBuildPlanner.UI.BuildsWindow.Close' >/dev/null

echo "--- Apply hammered, build swapped mid-level"
bridge 'invoke WrathBuildPlanner.Engine.TestHooks.Assign ZZ-UPPER.JSON' >/dev/null
first=$(press_apply)
for i in 1 2 3 4; do last=$(press_apply); done
expect "$first" 'Class: Fighter' 'first Apply works on an upper-case file name'
expect "$last" '0 applied' 'four more Apply change nothing'
bridge 'invoke WrathBuildPlanner.Engine.TestHooks.Assign careless-paste.json' >/dev/null
swapped=$(press_apply)
expect "$swapped" 'Cleave' 'swapping the build mid-level applies the new one'
bridge 'invoke WrathBuildPlanner.Engine.TestHooks.Assign -' >/dev/null
expect "$(press_apply)" 'NO REPORT' 'no build assigned: Apply does nothing'

echo "--- the log"
errs=$(ssh -o ConnectTimeout=6 deck-direct "sed -n '/CARELESS-START/,\$p' '$LOG' | grep -c -E 'Exception|\\[WrathBuildPlanner\\].*(ERROR|Error)'")
expect_eq "$errs" '0' 'no exception or error from the mod in Player.log'
# The game's own channels (Escape handling among them) log to GameLogFull.txt, not Player.log.
escerr=$(ssh -o ConnectTimeout=6 deck-direct "grep -c 'EscHotkeyManager' '$LOGDIR/GameLogFull.txt'")
expect_eq "$escerr" '0' 'no Escape exception in GameLogFull.txt'

ssh -o ConnectTimeout=6 deck-direct "cd '$MOD_DIR_DECK/Builds' && rm -rf zz-*.json ZZ-UPPER.JSON 'Ünïcødé Build.json' careless-paste*.json"
exit $FAILED
