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
- **Mercenary creation (2026-10-06):** opened on the fixture through `Player.CreateCustomCompanion()` (`mercenary.sh`). A mercenary starts with a race preselected, and `SelectRace.Apply` sets `LevelUpState.CanSelectRace` to false once any race is chosen — `RaceStep` therefore no longer checks that flag; `LevelUpController.SelectRace` removes the old choice first, like the race page. Mercenaries get 20 point-buy points (main character 25), and the game keeps Next disabled until all points are spent; the page checks both. The assignment made during creation is bound to the new unit.
- **Paste under Proton (2026-10-06):** an LLM-style answer (prose + ```json block) put into the Deck's X clipboard by another process (`python3` tkinter on `DISPLAY=:1`, the game's display) was imported by "Paste from clipboard".
- **Escape in the Builds window (2026-10-06):** routed through `Game.Instance.UI.EscManager` (newest subscriber only). The own key check let the level-up underneath ask to discard the player's choices; a static-method subscription broke Escape game-wide (`IsBad` calls `Target.Equals`). `careless.sh` covers it.
- **DevBridge/xdotool for key tests:** the first synthetic key after a hook call only focuses the window; `xdotool key ctrl+p` lands in one frame (Unity sees Ctrl released) — hold Ctrl with keydown/keyup and pauses. Bridge values end in `\r`. Mouse wheel: `xdotool click --window <id> 5` sends synthetic events the game ignores; plain `xdotool mousemove X Y; xdotool click 5` (XTEST, game window at 0,0) scrolls (`careless.sh`).
- **Builds window scrolling (2026-10-07):** labels and the gaps between rows are no raycast targets, so the wheel only scrolled over a button; each list now has a transparent catcher image and a scrollbar that shows on overflow. Verified with real wheel events (pos 1.00 → 0.65).
- **Page not part of the level (2026-10-07):** ChatGPT wrote the human bonus feat as `"in": "Bonus Feat"` (the game shows a second "Feat"; "Bonus Feat" pages exist only for Cavalier, Skald, Pack Rager, so the site's checker passes it). `PickStep` reroutes such a pick as a bare name in a second phase, after every pick with a matching page had its slot, never when that page exists but is filled, and reports `on the page X`; a second Apply finds it as set (`reroute.sh`).
- **16:9 (2026-10-06):** at 1280x720 (`Screen.SetResolution`, same ratio as 1920x1080) bar, result panel and Builds window fit in creation and level-up; the bar sits on the book's bottom edge without covering content.
- **German game (2026-10-06):** with `settings.game.main.locale` = deDE in `general_settings.json` (setting `LocalizationManager.CurrentLocale` directly is not enough: its getter reads the setting), English builds apply completely: example build in creation 12/0, wizard level-up 9/0. Page titles map back (Schule=School, Arkane Verbindung=Arcane Bond, Gegensätzliche Schule=Opposition School). Parameter values (Weapon Focus > Greatsword) needed `GameNames.ParamNames` (enum and blueprint names).
- **Spontaneous casters (2026-10-06):** Sorcerer 1–4 after Fighter 1 learns the listed spells on every level (none at Sorcerer 2, a second-level spell at Sorcerer 4), also after FillRest (`spontaneous.sh`). Swapping a known spell does not exist in Wrath's level-up (only `SelectSpell`/`UnselectSpell`, IL), so there is nothing to apply.

## Authoring page (`site/`)

Static page, published by `.github/workflows/pages.yml` to https://gh05d.github.io/wrath-build-planner/.
Spec: `pathfinder-mods/docs/superpowers/specs/2026-10-04-wrath-build-planner-authoring-design.md`.

    node --test tests/site/*.test.mjs        # checker tests incl. the shared vectors
    cd site && python3 -m http.server 8765    # local preview (file:// cannot load the modules or the data)
    NODE_PATH=~/.local/share/pnpm/global/5/.pnpm/playwright@1.51.0/node_modules node tests/e2e/page.e2e.cjs   # careless-player browser test

- `site/js/match.js` and `site/js/validate.js` port `Core/NameMatcher` and `Core/BuildValidator`; `tests/vectors/` run against both. Change the C# side first, then the vectors, then the port.
- `site/data/vocabulary.json` is generated from `Core/Vocabulary` (`UPDATE_VOCABULARY=1` on the unit tests).
- `site/data/names.json` comes from the running game: `bash tools/export-names.sh` (game in English, DevBridge, main menu is enough). Re-export after a game patch that changes content; `tests/site/names-data.test.mjs` checks it.
- Pages reached only from a race's features carry `"race"` in `names.json` (races are walked last: walked first, the Human bonus feat tagged the whole Feat page as Human). The checker uses it to name a heritage's race and to flag a heritage picked under another race; its cross-category hints match exact names only, never prefixes.
- Checker notes vs. warnings: anything the honest answer to is "the guide leaves it open" (bare picks, a weapon/school choice) is a note and stays out of the fix request; warnings go to the LLM. The page lists notes in their own box ("nothing to fix") and the summary says "ready for the game" — a note in the problem list read as an open warning to the user. `check.mjs` exits 0 only without errors and warnings (agents would keep typos otherwise); `--out` writes once there are no errors.
- Acceptance inputs (guide pages) come from `tools/fetch-guide.cjs` (Playwright, installed Chrome): Fextralife and Steam refuse plain curl.
- The checker resolves names page by page: several selections share one title (eight are "Bonus Combat Feat"), and in the game only one of them is open.
- AI agents: `site/llms.txt` (hand-written) points to `site/prompt.txt` / `prompt-design.txt` (generated by `node tools/agent-files.mjs`, also run by `export-names.sh`; `tests/site/check.test.mjs` fails when they are stale) and `site/check.mjs`, the checker for Node (`site/js/check.js` is the pipeline the page and the CLI share). `site/package.json` (`"type": "module"`) is required: Node 18 and 20 < 20.19 load the `.js` modules as CommonJS and `check.mjs` crashes (review 2026-10-06, reproduced on 20.9). The page itself builds its prompt in JS: a plain fetch of index.html never sees it. Acceptance 2026-10-06: two Sonnet agents given only the page URL, a guide file and a game folder found llms.txt, cloned, checked and saved into `Builds/` unaided (Neoseeker answer: three check rounds; raw Fextralife page: clean at the first check).
- Agent acceptance recipe: scratchpad game folder with an empty `Wrath.exe` and `Mods/WrathBuildPlanner/Info.json`, a subagent (Sonnet) told only the page URL, the guide file and the game folder; afterwards re-run `check.mjs` on the file it saved. Give it the raw guide text (`tools/fetch-guide.cjs`), not an LLM's answer — that only tests checking, not converting.
- A format change touches prompt (`site/js/prompt.js`), checker and vectors in the same commit. Prompt limit: 7,500 characters (user decision 2026-10-06; 6,617 at the first export).

## In-game tests

    bash tests/ingame/all.sh          # guards, multiclass, mythic, extras, chargen, example, mercenary, spontaneous, reroute, careless — restarts the game per script

Scripts assert on the English result text: game language English, mod language `auto`.
Test builds must match the game's own counts: a spell the class does not grant that level ends in `Open Spell … (NoFreeSlot)`, a missing `abilityPoint` on levels 4/8/… leaves Next blocked — `finish` then reports "Complete never appeared". Look at the `<name>-stuck.png` shot before suspecting the mod.
`Engine/TestHooks.cs` holds the static entry points the scripts call through DevBridge.
