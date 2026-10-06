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
- **Page walk after Apply** (2026-10-03): a same-frame page switch left the view blank, and a page only reports itself complete once it has been shown. `PlannerController` therefore waits a few frames and then steps forward page by page (like Next) until a page needs the player; it stops on portrait, appearance, voice and name in any case. Lands on Portrait in creation and on the summary in a fully applied level-up (`extras.sh`, `chargen.sh`).
- **Mythic path (rank 3)**: `SelectClass` would accept any path; the unlock gate is only in the page's view model. `MythicPathStep.IsOffered` repeats those checks. On the fixture Lich is left to the player, Angel is applied (`mythic.sh`).
- **Spell levels above 1**: Wizard 3 learns second-level spells through the "extra" slots (`extras.sh`).
- **Creation-time assignment**: held in memory, bound to the main character on the first area load, dropped when creation is cancelled (`example.sh`).
- **Respec**: not verified; the bar is hidden in that window mode.
- **English page titles (2026-10-06):** `GameNames.EnglishPageTitle` maps a page title back to its English text, so `in` also matches in a game running in another language. In the English game the pairs are identical (`TestHooks.PageTitles` → `Bonus Combat Feat=Bonus Combat Feat`); the non-English path is not verified (game language not switched).
- **Names at the main menu (2026-10-06):** `BlueprintUnitFact.Name` throws there for names with text templates (`NameTemplate.Generate` NRE); `NameExport` reads `LocalizedString.LoadString(CurrentPack, CurrentLocale)` instead.
- **Link to the build page (2026-10-06):** in Game Mode on the Deck `Application.OpenURL` opens no browser; the address lands on the clipboard and the window says so.
- **Authoring page acceptance (2026-10-06):** two forum guides and one design request through Haiku and Sonnet: no format errors, every checker suggestion right, clean after at most one fix round; Sonnet builds applied in creation and a level-up with nothing open beyond what the guide leaves open; Haiku dropped two correct feats in its fix round instead of taking the suggested names (fix-request wording) (`docs/superpowers/specs/2026-10-04-wrath-build-planner-authoring-acceptance.md`).
- **Not verified**: "Paste from clipboard" under Proton (the import behind it is), 16:9 layout, a mercenary's assignment made during its creation, spontaneous casters swapping spells, the game running in a language other than English.

## Authoring page (`site/`)

Static page, published by `.github/workflows/pages.yml` to https://gh05d.github.io/wrath-build-planner/.
Spec: `pathfinder-mods/docs/superpowers/specs/2026-10-04-wrath-build-planner-authoring-design.md`.

    node --test tests/site/*.test.mjs        # checker tests incl. the shared vectors
    cd site && python3 -m http.server 8765    # local preview (file:// cannot load the modules or the data)

- `site/js/match.js` and `site/js/validate.js` port `Core/NameMatcher` and `Core/BuildValidator`; `tests/vectors/` run against both. Change the C# side first, then the vectors, then the port.
- `site/data/vocabulary.json` is generated from `Core/Vocabulary` (`UPDATE_VOCABULARY=1` on the unit tests).
- `site/data/names.json` comes from the running game: `bash tools/export-names.sh` (game in English, DevBridge, main menu is enough). Re-export after a game patch that changes content; `tests/site/names-data.test.mjs` checks it.
- The checker resolves names page by page: several selections share one title (eight are "Bonus Combat Feat"), and in the game only one of them is open.
- A format change touches prompt (`site/js/prompt.js`), checker and vectors in the same commit. Prompt limit: 7,500 characters (user decision 2026-10-06; 6,617 at the first export).

## In-game tests

    bash tests/ingame/all.sh          # guards, multiclass, mythic, extras, chargen, example — restarts the game per script

Scripts assert on the English result text: game language English, mod language `auto`.
`Engine/TestHooks.cs` holds the static entry points the scripts call through DevBridge.
