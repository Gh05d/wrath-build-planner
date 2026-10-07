# Wrath Build Planner

Download: **[Nexus Mods](https://www.nexusmods.com/pathfinderwrathoftherighteous/mods/1210)** (or the [GitHub releases](https://github.com/Gh05d/wrath-build-planner/releases)). Requires Unity Mod Manager.

Follow a character build from a guide without comparing every level by hand. Import the build as a
small text file, assign it to a character, and press **Apply build** in the level-up or character
creation window. The mod makes the picks for that level; you look them over and press Complete.

**Share your builds:** have a build that works well, or know a popular one from a guide? Send it in and it
can ship with the mod for everyone — post it on the [Nexus page](https://www.nexusmods.com/pathfinderwrathoftherighteous/mods/1210), send a private message there, or
[open an issue](https://github.com/Gh05d/wrath-build-planner/issues). Say how you want to be credited.

## How it works

1. Put a build file (`.json`) into `Mods/WrathBuildPlanner/Builds/`, or open the Builds window
   (button in the HUD, or Ctrl+P) and choose **Paste from clipboard**.
2. In the Builds window, pick a build for each character. During character creation, click
   the build name or **Set build…** in the bar at the bottom of the window.
3. On every level-up press **Apply build**. Whatever could not be set is listed with the reason.

The mod never finishes a level for you, and it only fills what is still open — picks you made
yourself stay. Portrait, appearance, voice and name are always yours.

## Creating builds

Use the build page: **https://gh05d.github.io/wrath-build-planner/** — copy a prompt into ChatGPT, Claude or
any other chat AI together with a guide, check the answer on the page, and paste it into the game. The page
also has the full format reference and a search over every class, archetype, feat and spell name.

Using an AI agent that runs commands (Claude Code, Codex, …)? Point it to
**https://gh05d.github.io/wrath-build-planner/llms.txt**: the prompt as a plain file, the checker for the
command line (`node site/check.mjs answer.txt --out build.json`) and where to save the build.

The Builds window links to the page ("Create a build with ChatGPT or Claude…"). An example build:
`Builds-examples/two-handed-fighter.json`.

## When a pick is not applied

| Message | Meaning |
|---|---|
| not found | No option with that name on the page. Similar names are listed. |
| ambiguous | The name fits more than one selection. Add `"in"`. |
| not selectable | The option exists but the game does not allow it now (prerequisites are shown). |
| this level has no such selection | The page named by `"in"` does not exist on this level, and no other open page offers the name either. |

When the page named by `"in"` is not part of the level but exactly one open page offers the name (an AI calling the
human bonus feat page "Bonus Feat"; in the game it is a second "Feat"), the pick goes there after all picks with a
matching page are set, and the result says so: `applied (on the page Feat: …)`.
| no free spell slot | More spells listed than the level grants. |
| left to you | The mod does not decide this for you, e.g. a mythic path the game does not offer yet, or skill points without a skill list. |
| internal error | Something went wrong inside the mod for this entry; the rest was still applied. The mod's log has the details. |

## Good to know

- Class, race, starting ability scores and alignment are always set from the build, even if you had
  chosen something else. Everything else is only filled where nothing is chosen yet.
- Write names in English or in your game's language; English names also work when the game runs in
  another language.
- Using AutoLevelUp as well? Give a companion a build in one of the two mods, not in both.

- If you pick or change a feat by hand after applying, the game resets that level's spell choices.
  Press **Apply build** again and they are set again.
- Pages unlock in order, as always: after applying, the window jumps to the first page that still
  needs you, and you walk on with Next.

## Limits

- Mouse and keyboard interface only.
- No editing of builds inside the game — change the file and press **Reload folder**.
- Companions keep the levels they join with; a build applies from their next level.
