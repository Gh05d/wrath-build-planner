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
