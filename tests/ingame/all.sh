#!/usr/bin/env bash
# Full in-game regression. Restarts the game before each script so every run starts from the main menu.
cd "$(dirname "$0")"
BRIDGE="../../../dev-bridge"
status=0
for script in guards.sh multiclass.sh mythic.sh extras.sh chargen.sh example.sh mercenary.sh spontaneous.sh careless.sh; do
  echo "=================== $script"
  bash "$BRIDGE/bridge.sh" quit >/dev/null 2>&1
  bash "$BRIDGE/launch.sh" || { echo "FAIL launch before $script"; status=1; continue; }
  bash "./$script" || status=1
done
bash "$BRIDGE/bridge.sh" quit >/dev/null 2>&1
echo "=================== overall: $([ $status -eq 0 ] && echo PASS || echo FAIL)"
exit $status
