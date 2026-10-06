// The prompt the player copies into their LLM. Page titles and word lists come from the data files,
// so they follow the game and the mod.

// The prompt's first sentence; extract.js uses it to notice a pasted prompt.
export const PROMPT_START = 'You convert character builds for Pathfinder: Wrath of the Righteous into a file for the mod "Wrath Build Planner".';

export const EXAMPLE = `{
  "format": 1,
  "name": "Two-Handed Fighter",
  "author": "guide author, if known",
  "source": "guide URL, if known",
  "for": "main",
  "start": {
    "race": "Human",
    "raceBonus": "Strength",
    "abilityScores": { "Strength": 16, "Dexterity": 14, "Constitution": 14, "Intelligence": 12, "Wisdom": 12, "Charisma": 11 },
    "alignment": "Lawful Good"
  },
  "skills": ["Athletics", "Perception", "Persuasion", "Mobility"],
  "levels": [
    { "level": 1, "class": "Fighter", "archetype": "Two-Handed Fighter",
      "picks": [
        { "in": "Feat", "pick": "Power Attack" },
        { "in": "Bonus Combat Feat", "pick": ["Weapon Focus", "Greatsword"] },
        { "in": "Background Selection", "pick": ["Warrior", "Gladiator"] },
        { "in": "Deity", "pick": "Iomedae" }
      ] },
    { "level": 4, "class": "Fighter", "abilityPoint": "Strength",
      "picks": [ { "in": "Bonus Combat Feat", "pick": ["Weapon Specialization", "Greatsword"] } ] },
    { "level": 5, "class": "Wizard", "spells": ["Magic Missile", "Grease"] }
  ],
  "mythic": [
    { "rank": 3, "path": "Angel" }
  ]
}`;

// Used when names.json could not be loaded: titles observed in the game.
const FALLBACK_TITLES = ['Feat', 'Bonus Combat Feat', 'Background Selection', 'Deity', 'School', 'Opposition School',
  'Arcane Bond', 'Order', 'Mythic Ability', 'Mythic Feat'];

const VARIANTS = {
  guide: 'THE GUIDE\nConvert the guide below. Keep its order and its choices; do not improve them.\n\n[paste the guide here]',
  design: 'THE REQUEST\nDesign a build for the request below. Use only options from the base game and its DLCs, no content from other mods. Prefer a well-known, proven build.\n\n[describe the character: class, role, mythic path]',
};

// Titles of pages a progression grants directly, sorted. Spellings that differ only in case ("Channel energy",
// "Channel Energy") are one page for the mod's matcher; the one with more capitals is kept.
function pageTitles(names) {
  const byKey = new Map();
  for (const page of names.pages) {
    if (page.nested || page.n.length < 2) continue;   // a lone identity is the internal name: no title in the game
    const title = page.n[0];
    const key = title.toLowerCase();
    const capitals = t => (t.match(/[A-Z]/g) ?? []).length;
    if (!byKey.has(key) || capitals(title) > capitals(byKey.get(key))) byKey.set(key, title);
  }
  return [...byKey.values()].sort((a, b) => a.localeCompare(b));
}

export function buildPrompt(names, vocab, variant) {
  const titles = names ? pageTitles(names) : FALLBACK_TITLES;
  const skills = Object.values(vocab.skills).map(s => s[0]);
  const alignments = Object.values(vocab.alignments).map(s => s[0]);
  return `${PROMPT_START}

Output only the build as JSON in one code block. After the block you may add a short list headed "Left out:" with what you could not convert. Write nothing else.

EXAMPLE
${EXAMPLE}

FIELDS
- format: always 1. name: a short name for the build. author, source: optional.
- for: "main" for the main character, "any", or a companion's name.
- start: character creation only. race; raceBonus = the attribute that gets a free +2 (Human, Half-Elf, Half-Orc); abilityScores = point-buy values BEFORE racial bonuses, each 7-18; alignment.
- skills: the order in which skill points are spent on every level.
- levels: one entry per character level 1-20, ascending. "class" on every entry. "archetype" only on the first level of that class. "abilityPoint" on levels 4, 8, 12, 16, 20.
- picks: what the player selects on that level: { "in": page title, "pick": name }. A choice inside a choice is a list, parent first: ["Weapon Focus", "Greatsword"].
- spells: spells learned on that level, in order.
- mythic: one entry per mythic rank 1-10, with "picks" like levels. "path" on the rank where the game asks for the mythic path.

NAMES
- Use the names exactly as the game shows them in English. No abbreviations: "PA" is "Power Attack".
- "in" is the page title in the level-up window. Page titles: ${titles.join(', ')}.
- If you know the feat but not its page, write just the name: "picks": ["Deadly Aim"]. The mod finds the page when only one open page offers it.
- Do not list class features the game grants automatically, only what the player chooses.
- If the guide gives final ability scores, subtract the racial bonus.
- Attributes: ${vocab.attributes.join(', ')}.
- Alignments: ${alignments.join(', ')}.
- Skills: ${skills.join(', ')}.

HONESTY
Leave out anything the guide leaves open or that you are not sure exists in Wrath of the Righteous. Do not guess: a missing pick stays open for the player, a wrong pick is worse.

${VARIANTS[variant] ?? VARIANTS.guide}`;
}
