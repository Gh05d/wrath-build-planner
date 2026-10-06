// Port of WrathBuildPlanner/Core/BuildValidator.cs (rules and English messages from Core/Messages.cs)
// plus the structure the mod's parser enforces (BuildParser: unknown fields are refused).
import { normalize, editDistance } from './match.js';

const MESSAGES = {
  'validate.format': 'This build uses format {0}; this version of the mod reads format {1}.',
  'validate.no_name': 'The build has no name.',
  'validate.nothing': 'The build has nothing to apply: no start block, no levels, no mythic ranks.',
  'validate.start_without_level1': 'The start block is only used together with an entry for level 1.',
  'validate.race': "Unknown race '{0}'.",
  'validate.not_attribute': "'{0}' is not an attribute.",
  'validate.alignment': "Unknown alignment '{0}'.",
  'validate.score_range': '{0} is {1}; starting scores go from 7 to 18 (before the racial bonus).',
  'validate.skill': "Unknown skill '{0}'.",
  'validate.empty_row': "An entry in '{0}' is empty.",
  'validate.level_range': 'Level {0} is out of range; character levels go from 1 to 20.',
  'validate.level_twice': 'The build lists level {0} twice.',
  'validate.level_order': 'Levels must be in ascending order; level {0} comes after level {1}.',
  'validate.no_class': 'The level has no class.',
  'validate.class': "Unknown class '{0}'.",
  'validate.point_level': 'An attribute point is only granted every fourth level; this entry will be ignored.',
  'validate.empty_spell': 'A spell name is empty.',
  'validate.rank_range': 'Mythic rank {0} is out of range; ranks go from 1 to 10.',
  'validate.rank_twice': 'The build lists mythic rank {0} twice.',
  'validate.rank_order': 'Mythic ranks must be in ascending order; rank {0} comes after rank {1}.',
  'validate.empty_pick': 'A pick is empty or has an empty name in its chain.',
};
const msg = (key, ...args) => MESSAGES[key].replace(/\{(\d)\}/g, (_, n) => String(args[+n]));
const error = (where, message) => ({ error: true, where, message });
const warning = (where, message) => ({ error: false, where, message });
const blank = s => s == null || String(s).trim() === '';

// ---- structure (what BuildParser refuses) ----

const FIELDS = {
  top: ['format', 'name', 'author', 'source', 'for', 'start', 'skills', 'levels', 'mythic'],
  start: ['race', 'raceBonus', 'abilityScores', 'alignment'],
  level: ['level', 'class', 'archetype', 'abilityPoint', 'skills', 'picks', 'spells'],
  mythic: ['rank', 'path', 'picks'],
  pick: ['in', 'pick'],
};
const ALIASES = {
  feats: 'picks', feat: 'picks', selections: 'picks', choices: 'picks', features: 'picks',
  abilities: 'abilityScores', attributes: 'abilityScores', stats: 'abilityScores', scores: 'abilityScores',
  racialBonus: 'raceBonus', bonus: 'raceBonus', mythicPath: 'path', characterLevel: 'level', classLevel: 'level',
  archetypes: 'archetype', spell: 'spells', knownSpells: 'spells', page: 'in', selection: 'in', choice: 'pick', value: 'pick', name: 'pick',
};

function didYouMean(field, allowed) {
  if (ALIASES[field] && allowed.includes(ALIASES[field])) return ALIASES[field];
  const key = normalize(field);
  const near = allowed.map(a => [editDistance(key, normalize(a)), a]).sort((x, y) => x[0] - y[0])[0];
  return near && near[0] <= 2 ? near[1] : null;
}

// A field that is right but on the wrong level ("feats" at the top: picks belong to each level row).
const BELONGS = { picks: 'inside each entry of "levels"', spells: 'inside each entry of "levels"', class: 'inside each entry of "levels"',
  level: 'inside each entry of "levels"', race: 'inside "start"', alignment: 'inside "start"', abilityScores: 'inside "start"', path: 'inside an entry of "mythic"' };

function fields(obj, kind, where, issues) {
  for (const f of Object.keys(obj)) {
    if (FIELDS[kind].includes(f)) continue;
    const target = ALIASES[f] ?? (BELONGS[f] ? f : null);
    if (target && !FIELDS[kind].includes(target) && BELONGS[target]) {
      issues.push({ ...error(where, `Field '${f}' is not allowed here: "${target}" goes ${BELONGS[target]}. The mod refuses files with unknown fields.`), structure: true });
      continue;
    }
    const hint = didYouMean(f, FIELDS[kind]);
    issues.push({ ...error(where, `Unknown field '${f}'${hint ? ` — did you mean '${hint}'?` : '.'} The mod refuses files with unknown fields.`), structure: true });
  }
}

const isObject = v => v !== null && typeof v === 'object' && !Array.isArray(v);
const isInt = v => Number.isInteger(v) || (typeof v === 'string' && /^-?\d+$/.test(v.trim()));
const isText = v => v == null || typeof v === 'string' || typeof v === 'number';

function expect(ok, where, what, issues) {
  if (!ok) issues.push({ ...error(where, `'${where}' must be ${what}.`), structure: true });
  return ok;
}

function structure(build, issues) {
  if (!expect(isObject(build), 'file', 'an object', issues)) return;
  fields(build, 'top', 'file', issues);
  if (build.format != null) expect(isInt(build.format), 'format', 'a whole number', issues);
  for (const f of ['name', 'author', 'source', 'for']) expect(isText(build[f]), f, 'text', issues);
  if (build.start != null && expect(isObject(build.start), 'start', 'an object', issues)) {
    fields(build.start, 'start', 'start', issues);
    for (const f of ['race', 'raceBonus', 'alignment']) expect(isText(build.start[f]), `start.${f}`, 'text', issues);
    const scores = build.start.abilityScores;
    if (scores != null && expect(isObject(scores), 'start.abilityScores', 'an object', issues))
      for (const [k, v] of Object.entries(scores)) expect(isInt(v), `start.abilityScores.${k}`, 'a whole number', issues);
  }
  textList(build.skills, 'skills', issues);
  rows(build.levels, 'levels', 'level', issues);
  rows(build.mythic, 'mythic', 'mythic', issues);
}

function textList(list, where, issues) {
  if (list == null) return;
  if (expect(Array.isArray(list), where, 'a list', issues))
    list.forEach((v, i) => expect(isText(v), `${where}[${i + 1}]`, 'text', issues));
}

function rows(list, name, kind, issues) {
  if (list == null) return;
  if (!expect(Array.isArray(list), name, 'a list', issues)) return;
  list.forEach((row, i) => {
    const where = `${name}[${i + 1}]`;
    if (row == null) return;   // reported by the rule checks, like the mod
    if (!expect(isObject(row), where, 'an object', issues)) return;
    fields(row, kind, where, issues);
    const key = kind === 'level' ? 'level' : 'rank';
    if (row[key] != null) expect(isInt(row[key]), `${where}.${key}`, 'a whole number', issues);
    for (const f of ['class', 'archetype', 'abilityPoint', 'path']) if (f in row) expect(isText(row[f]), `${where}.${f}`, 'text', issues);
    textList(row.skills, `${where}.skills`, issues);
    textList(row.spells, `${where}.spells`, issues);
    if (row.picks == null) return;
    if (!expect(Array.isArray(row.picks), `${where}.picks`, 'a list', issues)) return;
    row.picks.forEach((pick, j) => {
      const at = `${where}.picks[${j + 1}]`;
      if (pick == null || typeof pick === 'string') return;
      if (!expect(isObject(pick), at, 'a name or an object with "in" and "pick"', issues)) return;
      fields(pick, 'pick', at, issues);
      expect(pick.in == null || typeof pick.in === 'string', `${at}.in`, 'text', issues);
      const p = pick.pick;
      expect(typeof p === 'string' || (Array.isArray(p) && p.every(x => typeof x === 'string')), `${at}.pick`, 'a name or a list of names', issues);
    });
  });
}

// ---- field-name case ----

// Newtonsoft matches [JsonProperty] names regardless of case, so "Levels" imports like "levels". The pick
// converter (PickEntryConverter) does not: its "in"/"pick" switch is case-sensitive, so pick keys stay as written.
export function canonicalize(input) {
  const renamed = [];
  const fix = (obj, kind) => {
    if (!isObject(obj)) return obj;
    const out = {};
    for (const [key, value] of Object.entries(obj)) {
      const canonical = FIELDS[kind].find(f => f.toLowerCase() === key.toLowerCase());
      const name = canonical && !(canonical in obj && canonical !== key) ? canonical : key;
      if (name !== key) renamed.push(key);
      out[name] = value;
    }
    return out;
  };
  const build = fix(input, 'top');
  if (isObject(build)) {
    if (isObject(build.start)) build.start = fix(build.start, 'start');
    if (Array.isArray(build.levels)) build.levels = build.levels.map(r => fix(r, 'level'));
    if (Array.isArray(build.mythic)) build.mythic = build.mythic.map(r => fix(r, 'mythic'));
  }
  return { build, renamed };
}

// ---- rules (BuildValidator) ----

function vocabularyOf(vocab) {
  const index = list => {
    const map = new Map();
    for (const [canonical, spellings] of Object.entries(list)) {
      map.set(normalize(canonical), canonical);
      for (const s of spellings) map.set(normalize(s), canonical);
    }
    return map;
  };
  const attributes = new Map();
  for (const a of vocab.attributes) {
    attributes.set(normalize(a), a);
    attributes.set(normalize(a.substring(0, 3)), a);
  }
  return {
    attribute: t => attributes.has(normalize(t)),
    skill: (map => t => map.has(normalize(t)))(index(vocab.skills)),
    alignment: (map => t => map.has(normalize(t)))(index(vocab.alignments)),
  };
}

const int = v => (typeof v === 'string' ? parseInt(v, 10) : v ?? 0);

export function validate(build, { vocab, known }) {
  const issues = [];
  structure(build, issues);
  if (issues.length > 0) return issues;

  const words = vocabularyOf(vocab);
  const levels = build.levels ?? [];
  const mythic = build.mythic ?? [];
  const format = int(build.format);
  if (format !== 1) issues.push(error('format', msg('validate.format', format, 1)));
  if (blank(build.name)) issues.push(error('name', msg('validate.no_name')));
  if (levels.length === 0 && mythic.length === 0 && build.start == null) issues.push(error('file', msg('validate.nothing')));
  if (build.start != null && !levels.some(r => r != null && int(r.level) === 1)) issues.push(warning('start', msg('validate.start_without_level1')));

  checkStart(build.start, words, known, issues);
  checkSkills(build.skills, 'skills', words, issues);
  checkLevels(levels, words, known, issues);
  checkMythic(mythic, issues);
  return issues;
}

function checkStart(start, words, known, issues) {
  if (start == null) return;
  if (!blank(start.race) && known && !known.raceExists(start.race)) issues.push(error('start.race', msg('validate.race', start.race)));
  if (!blank(start.raceBonus) && !words.attribute(start.raceBonus)) issues.push(error('start.raceBonus', msg('validate.not_attribute', start.raceBonus)));
  if (!blank(start.alignment) && !words.alignment(start.alignment)) issues.push(error('start.alignment', msg('validate.alignment', start.alignment)));
  if (start.abilityScores == null) return;
  for (const [key, raw] of Object.entries(start.abilityScores)) {
    const value = int(raw);
    if (!words.attribute(key)) issues.push(error('start.abilityScores', msg('validate.not_attribute', key)));
    else if (value < 7 || value > 18) issues.push(error('start.abilityScores', msg('validate.score_range', key, value)));
  }
}

function checkSkills(skills, where, words, issues) {
  if (skills == null) return;
  for (const skill of skills) if (!words.skill(skill)) issues.push(error(where, msg('validate.skill', skill)));
}

function checkLevels(levels, words, known, issues) {
  let previous = 0;
  const seen = new Set();
  for (const row of levels) {
    if (row == null) { issues.push(error('levels', msg('validate.empty_row', 'levels'))); continue; }
    const level = int(row.level);
    const where = `levels[level ${level}]`;
    if (level < 1 || level > 20) issues.push(error(where, msg('validate.level_range', level)));
    if (seen.has(level)) issues.push(error(where, msg('validate.level_twice', level)));
    else if (level < previous) issues.push(error(where, msg('validate.level_order', level, previous)));
    seen.add(level);
    previous = Math.max(previous, level);

    if (blank(row.class)) issues.push(error(where, msg('validate.no_class')));
    else if (known && !known.classExists(row.class)) issues.push(error(where, msg('validate.class', row.class)));

    if (!blank(row.abilityPoint)) {
      if (!words.attribute(row.abilityPoint)) issues.push(error(where, msg('validate.not_attribute', row.abilityPoint)));
      else if (level % 4 !== 0) issues.push(warning(where, msg('validate.point_level')));
    }
    checkSkills(row.skills, where, words, issues);
    checkPicks(row.picks, where, issues);
    if ((row.spells ?? []).some(blank)) issues.push(error(where, msg('validate.empty_spell')));
  }
}

function checkMythic(mythic, issues) {
  let previous = 0;
  const seen = new Set();
  for (const row of mythic) {
    if (row == null) { issues.push(error('mythic', msg('validate.empty_row', 'mythic'))); continue; }
    const rank = int(row.rank);
    const where = `mythic[rank ${rank}]`;
    if (rank < 1 || rank > 10) issues.push(error(where, msg('validate.rank_range', rank)));
    if (seen.has(rank)) issues.push(error(where, msg('validate.rank_twice', rank)));
    else if (rank < previous) issues.push(error(where, msg('validate.rank_order', rank, previous)));
    seen.add(rank);
    previous = Math.max(previous, rank);
    checkPicks(row.picks, where, issues);
  }
}

function checkPicks(picks, where, issues) {
  for (const pick of picks ?? []) {
    const chain = pick == null ? [] : typeof pick === 'string' ? [pick] : Array.isArray(pick.pick) ? pick.pick : [pick.pick];
    if (chain.length === 0 || chain.some(blank)) issues.push(error(where, msg('validate.empty_pick')));
  }
}
