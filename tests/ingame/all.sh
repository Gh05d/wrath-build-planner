#!/usr/bin/env bash
# Full in-game regression. Restarts the game before each script so every run starts from the main menu.
cd "$(dirname "$0")"
BRIDGE="../../../dev-bridge"
# A dead USB link once stalled a run silently for two hours (2026-10-07: link lost at 11:42; Steam then suspended
# the idle Deck after IdleSuspendACSeconds = 3600). Check the Deck before every script and cap each script's time,
# so a lost link ends the run with a clear message. A logind sleep lock cannot be taken over SSH (polkit asks for
# admin auth), so standby is not blocked: a full run takes ~30 minutes, under Steam's one hour on AC.
deck_ok() { timeout 15 ssh -o ConnectTimeout=6 -o ControlPath=none deck-direct true 2>/dev/null; }
status=0
for script in guards.sh multiclass.sh mythic.sh extras.sh chargen.sh example.sh mercenary.sh spontaneous.sh reroute.sh careless.sh; do
  echo "=================== $script"
  deck_ok || { echo "FAIL Deck unreachable before $script — check the USB cable or wake the Deck"; status=1; break; }
  bash "$BRIDGE/bridge.sh" quit >/dev/null 2>&1
  timeout 300 bash "$BRIDGE/launch.sh" || { echo "FAIL launch before $script"; status=1; continue; }
  timeout 1200 bash "./$script" || { [ $? -eq 124 ] && echo "FAIL $script took over 20 minutes (link lost?)"; status=1; }
done
bash "$BRIDGE/bridge.sh" quit >/dev/null 2>&1
# The Deck is also where the game is played: leave no test builds in the player's library.
source ./lib.sh
names=$(cd ../builds && ls *.json | tr '\n' ' ')
timeout 20 ssh -o ConnectTimeout=6 deck-direct "cd '$MOD_DIR_DECK/Builds' && rm -f $names paste-test-*.json"
echo "=================== overall: $([ $status -eq 0 ] && echo PASS || echo FAIL)"
exit $status
