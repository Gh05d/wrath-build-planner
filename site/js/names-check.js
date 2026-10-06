// Checks every name in a build against site/data/names.json. Only warnings: content from other mods is
// not in the list and still works in the game. What depends on the live character (prerequisites, whether a
// page is offered on that level, free spell slots) is left to the mod.
import { match, normalize, splitParenChain, stripCategory } from './match.js';

const NOTE = 'Fine if a mod you use adds it.';
const blank = s => s == null || String(s).trim() === '';
const cand = (names, id) => ({ id, names: id ? [...names, id] : names, display: names[0] });

export function indexNames(data) {
  const features = new Map(Object.entries(data.features).map(([id, f]) => [id, { ...f, cand: cand(f.n, id) }]));
  const spells = new Map(Object.entries(data.spells).map(([id, n]) => [id, cand(n, id)]));
  const pages = data.pages.map(p => ({ ...p, cand: cand(p.n, null) }));
  // Candidates per page, built once: the checker matches a name against every page for a pick without "in".
  for (const p of pages) {
    p.cands = [...(p.items ?? []).map(id => features.get(id)?.cand).filter(Boolean), ...(p.params ?? []).map(n => cand(n, null))];
    p.exact = keyIndex(p.cands, n => normalize(n));
    p.stripped = keyIndex(p.cands, n => normalize(stripCategory(n)));
  }
  const pagesOf = new Map();
  for (const p of pages)
    for (const id of p.items ?? []) {
      if (!pagesOf.has(id)) pagesOf.set(id, new Set());
      pagesOf.get(id).add(p.cand.display);
    }
  const all = new Map();
  for (const p of pages) for (const c of p.cands) all.set(c.id ?? `param:${c.display}`, c);
  return {
    features, spells, pages, pagesOf, allCands: [...all.values()],
    classes: data.classes.map(c => ({ cand: cand(c.n, null), archetypes: c.archetypes.map(a => cand(a, null)), spells: c.spells })),
    races: data.races.map(r => cand(r, null)),
    paths: data.mythicPaths.map(p => cand(p, null)),
  };
}

// normalized name -> candidates that have it; one entry per candidate even if several of its names agree.
function keyIndex(cands, key) {
  const map = new Map();
  for (const c of cands) {
    for (const k of new Set(c.names.map(key))) {
      if (!k) continue;
      if (!map.has(k)) map.set(k, []);
      map.get(k).push(c);
    }
  }
  return map;
}

// match() on one page without suggestions, through the page's prebuilt indexes: same two stages, same decision.
function decideOnPage(page, key) {
  const hits = page.exact.get(key) ?? page.stripped.get(key) ?? [];
  if (hits.length === 1) return { kind: 'unique', match: hits[0] };
  if (hits.length > 1) return { kind: 'ambiguous', match: null, tied: hits, suggestions: [] };
  return { kind: 'none' };
}

const warn = (where, message) => ({ error: false, where, message });

function unknown(where, what, name, outcome, context = '') {
  const hint = outcome.suggestions.length ? ` Did you mean: ${outcome.suggestions.join(', ')}?` : '';
  return warn(where, `Unknown ${what} '${name}'${context}.${hint} ${NOTE}`);
}

// A name that is unknown in one category often exists in another: a prestige class written as an archetype,
// a racial heritage written as the race. Saying so lets the LLM fix the build instead of dropping the entry.
function elsewhere(name, index) {
  const asClass = match(name, index.classes.map(c => c.cand), { suggest: false });
  if (asClass.kind === 'unique') return { kind: 'class', display: asClass.match.display };
  const key = normalize(name);
  for (const page of index.pages) {
    if (page.n.length < 2) continue;
    const hit = page.cands.find(c => c.names.some(n => normalize(n) === key || normalize(n).startsWith(key)));
    if (hit) return { kind: 'option', display: hit.display, page: page.cand.display, race: page.race };
  }
  return null;
}

// The mod checks class and race on import (BuildValidator) and refuses the file when one is unknown.
function refused(where, what, name, outcome) {
  const hint = outcome.suggestions.length ? ` Did you mean: ${outcome.suggestions.join(', ')}?` : '';
  return warn(where, `Unknown ${what} '${name}'.${hint} The mod refuses a build with an unknown ${what}, unless a mod you use adds it.`);
}

function tied(where, name, outcome) {
  return warn(where, `'${name}' fits several options: ${outcome.tied.map(t => t.display).join(', ')}. Use the exact name.`);
}

export function checkNames(build, index) {
  if (!index) return [{ error: false, note: true, where: '', message: 'Names were not checked: the name list could not be loaded.' }];
  const issues = [];
  if (build.start && !blank(build.start.race)) {
    const o = match(build.start.race, index.races);
    if (o.kind === 'none') {
      const other = elsewhere(build.start.race, index);
      if (other?.kind === 'option' && other.race)
        issues.push(warn('start.race', `'${build.start.race}' is not a race: '${other.display}' is a ${other.race} option on page '${other.page}'. Set "race": "${other.race}" and add { "in": "${other.page}", "pick": "${other.display}" } on level 1.`));
      else if (other?.kind === 'option')
        issues.push(warn('start.race', `'${build.start.race}' is not a race: '${other.display}' is an option on page '${other.page}'. Set the race it belongs to and add the option as a pick on level 1.`));
      else issues.push(refused('start.race', 'race', build.start.race, o));
    }
  }

  const raceName = build.start && !blank(build.start.race) ? build.start.race : null;
  const classes = [];
  for (const row of build.levels ?? []) {
    if (!row) continue;
    const where = `levels[level ${row.level}]`;
    let cls = null;
    if (!blank(row.class)) {
      const o = match(row.class, index.classes.map(c => c.cand));
      if (o.kind === 'unique') classes.push(cls = index.classes.find(c => c.cand === o.match));
      else if (o.kind === 'none') issues.push(refused(where, 'class', row.class, o));
    }
    if (!blank(row.archetype) && cls) {
      const o = match(row.archetype, cls.archetypes);
      const other = o.kind === 'none' ? elsewhere(row.archetype, index) : null;
      if (other?.kind === 'class')
        issues.push(warn(where, `'${row.archetype}' is a class of its own, not an archetype of ${cls.cand.display}: write "class": "${other.display}" on the levels taken in it.`));
      else if (o.kind === 'none') issues.push(unknown(where, `archetype of ${cls.cand.display}`, row.archetype, o));
    }
    checkPicks(row.picks, where, index, issues, raceName);
  }

  // Spells: against the spell lists of every class the build takes.
  const spellCands = [...new Set(classes.flatMap(c => c.spells))].map(id => index.spells.get(id)).filter(Boolean);
  if (spellCands.length > 0)
    for (const row of build.levels ?? []) {
      if (!row) continue;
      for (const spell of row.spells ?? []) {
        if (blank(spell)) continue;
        const o = match(spell, spellCands);
        if (o.kind === 'none') issues.push(unknown(`levels[level ${row.level}]`, 'spell for the classes in this build', spell, o));
      }
    }

  for (const row of build.mythic ?? []) {
    if (!row) continue;
    const where = `mythic[rank ${row.rank}]`;
    if (!blank(row.path)) {
      const o = match(row.path, index.paths);
      if (o.kind === 'none') issues.push(unknown(where, 'mythic path', row.path, o));
    }
    checkPicks(row.picks, where, index, issues);
  }
  return issues;
}

function checkPicks(picks, where, index, issues, race) {
  (picks ?? []).forEach((entry, i) => {
    if (entry == null) return;
    const at = `${where} picks[${i + 1}]`;
    const pick = typeof entry === 'string'
      ? { in: null, chain: [entry] }
      : { in: entry.in ?? null, chain: Array.isArray(entry.pick) ? entry.pick : [entry.pick] };
    pick.race = race;
    if (pick.chain.length === 0 || pick.chain.some(blank)) return;   // the format check reports it
    let pages = index.pages;
    if (!blank(pick.in)) {
      const o = match(pick.in, index.pages.map(p => p.cand));
      if (o.kind === 'none') { issues.push(unknown(at, 'page', pick.in, o)); return; }
      const hit = o.kind === 'unique' ? [o.match] : o.tied;   // several selections can share one title
      pages = index.pages.filter(p => hit.includes(p.cand));
    }
    checkChain(pick, pages, at, index, issues);
  });
}

function candidatesOf(pages, index) {
  if (pages === index.pages) return index.allCands;
  const seen = new Map();
  for (const p of pages) for (const c of p.cands) seen.set(c.id ?? `param:${c.display}`, c);
  return [...seen.values()];
}

function subCandidates(feature, index) {
  if (feature?.sub) return feature.sub.map(id => index.features.get(id)?.cand).filter(Boolean);
  if (feature?.params) return feature.params.map(n => cand(n, null));
  return [];
}

// The game shows one page at a time, and several selections share a title ("Bonus Combat Feat" is the title of
// eight). So a name is resolved page by page, like the mod does against the open page: unique on any page is a hit;
// suggestions come from all pages together.
function resolve(name, pages, index) {
  const hits = [];
  let tiedOutcome = null;
  const key = normalize(name);
  for (const page of pages) {
    const o = key ? decideOnPage(page, key) : { kind: 'none' };
    if (o.kind === 'unique') hits.push({ page, match: o.match });
    else if (o.kind === 'ambiguous' && !tiedOutcome) tiedOutcome = o;
  }
  if (hits.length > 0) {
    hits.sort((x, y) => (y.match.id ? 1 : 0) - (x.match.id ? 1 : 0));   // a feature beats a bare parameter value
    return { kind: 'unique', hits };
  }
  if (tiedOutcome) return { kind: 'ambiguous', outcome: tiedOutcome };
  // Suggestions are the slow part: computed only when a message needs them.
  return { kind: 'none', get outcome() { return match(name, candidatesOf(pages, index)); } };
}

function checkChain(pick, pages, at, index, issues) {
  let chain = pick.chain;
  let r = resolve(chain[0], pages, index);
  // Guide notation "Weapon Focus (Greatsword)" is the chain Weapon Focus > Greatsword.
  if (r.kind === 'none' && chain.length === 1) {
    const split = splitParenChain(chain[0]);
    if (split) {
      const head = resolve(split[0], pages, index);
      if (head.kind !== 'none') { chain = split; r = head; }
    }
  }
  if (r.kind === 'none') { issues.push(unknown(at, 'option', chain[0], r.outcome, blank(pick.in) ? '' : ` on page '${pick.in}'`)); return; }
  if (r.kind === 'ambiguous') { issues.push(tied(at, chain[0], r.outcome)); return; }
  // A racial page (heritage) exists only for its race.
  const race = pick.race;
  const racial = r.hits.map(h => h.page.race).filter(Boolean);
  if (race && racial.length === r.hits.length && !racial.some(x => normalize(x) === normalize(race)))
    issues.push(warn(at, `'${chain[0]}' on page '${r.hits[0].page.cand.display}' belongs to the race ${racial[0]}, but the build's race is ${race}.`));
  if (blank(pick.in)) {
    const titles = [...new Set(r.hits.filter(h => h.page.n.length > 1).map(h => h.page.cand.display))];
    if (titles.length > 1)
      issues.push(warn(at, `'${chain[0]}' is offered on several pages (${titles.join(', ')}). Add "in" with the page title so the mod knows which one.`));
  }
  const first = r.hits[0].match;
  let current = first.id ? index.features.get(first.id) : null;
  // A parametrized feat (weapon, school …) or a nested selection without its choice stays open in the game.
  if (chain.length === 1) {
    const sub = subCandidates(current, index);
    if (sub.length > 0) {
      const examples = sub.slice(0, 3).map(c => c.display).join(', ');
      issues.push(warn(at, `'${chain[0]}' needs a further choice, e.g. ["${chain[0]}", "${sub[0].display}"] (choices include ${examples}${sub.length > 3 ? ', …' : ''}); without it the mod leaves the pick open.`));
    }
  }
  for (let k = 1; k < chain.length; k++) {
    const sub = subCandidates(current, index);
    if (sub.length === 0) { issues.push(warn(at, `'${chain[k - 1]}' offers no further choice.`)); return; }
    const next = match(chain[k], sub);
    if (next.kind === 'none') { issues.push(unknown(at, 'choice', chain[k], next, ` under '${chain[k - 1]}'`)); return; }
    if (next.kind === 'ambiguous') { issues.push(tied(at, chain[k], next)); return; }
    current = next.match.id ? index.features.get(next.match.id) : null;
  }
}
