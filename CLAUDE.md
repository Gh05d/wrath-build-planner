# Wrath Build Planner

Applies character builds (JSON) level by level inside the level-up and character-creation windows.
Shared rules: → parent `pathfinder-mods/CLAUDE.md`. Wrath engine facts: → `../CLAUDE.md` and `../docs/engine-api.md` §Level-up & Character Creation.
Design: `pathfinder-mods/docs/superpowers/specs/2026-10-03-wrath-build-planner-design.md`.

## Build / test / deploy

    ~/.dotnet/dotnet build WrathBuildPlanner/WrathBuildPlanner.csproj -p:SolutionDir=$(pwd)/
    ~/.dotnet/dotnet build WrathBuildPlanner.Tests/WrathBuildPlanner.Tests.csproj -p:SolutionDir=$(pwd)/
    for i in 1 2 3; do ~/.dotnet/dotnet test --no-build WrathBuildPlanner.Tests/WrathBuildPlanner.Tests.csproj -p:SolutionDir=$(pwd)/ && break; done
    timeout 240 ./deploy.sh

In-game tests: skill `testing-mods-in-game`, scripts in `tests/ingame/`.

## Rules

- `Core/` and `Models/` must not reference game or Unity types — they are the unit-tested part.
- Names are matched only against the candidates of the selection at hand (`Core/NameMatcher`), never the whole blueprint database.
- The mod never commits a level.
- Re-read `controller.State` after every controller call; never hold a `FeatureSelectionState` across calls.

## Verified behaviour

- **Second language pack (2026-10-03):** `LocalizationManager.LoadPack(Locale)` + `LocalizedString.LoadString(pack, locale)` resolve names in another language while the game keeps its own (`TestHooks.NamesIn deDE` in an English game returned Arkanist/Barbar/Barde). `GameNames.English` uses this, so English build names match in a non-English game. Checked in the reverse direction only — the game's language setting was not switched.
- **Spell picks reset after a later feature pick (2026-10-03):** when a feature is selected in a later frame, the game resets that level's spell choices (GameLogFull: `Invalid action: SelectSpell`). `SpellStep` therefore works from the live state and a second Apply restores the spells.
- **Page titles** come from `FeatureSelectionExtensions.GetMenuLabel(selectionState)`, not from the selection blueprint's name (wizard school: page "School", blueprint "Specialist School").
- **Build bar placement (2026-10-03, 1280x800):** bar bottom-left below the book (window units 24/14, 610x46), result panel bottom-right above the "Class progression" button. Checked on the creation pages Character and Portrait and on the level-up pages Feat, School, Arcane Bond, Opposition School. The window's canvas is laid out for 1920x1200: sizes in `BuildBar` are window units, two thirds of that on the Deck.
- **Page jump after Apply** must wait a few frames (`PlannerController.JumpDelayFrames`): a same-frame page switch left the view blank.
## In-game tests

    bash tests/ingame/all.sh          # guards, multiclass, mythic, chargen — restarts the game per script

Scripts assert on the English result text: game language English, mod language `auto`.
`Engine/TestHooks.cs` holds the static entry points the scripts call through DevBridge.
