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

// A name that is unknown in one category often exists in another: a prestige class written as an archetype, an
// archetype written as a class, a racial heritage written as the race. Saying so lets the LLM fix the build instead
// of dropping the entry. Matches are exact (or the part before "(…)"), never a prefix: "Elven" must not find
// "ElvenArcaneFocus".
function asClass(name, index) {
  const o = match(name, index.classes.map(c => c.cand), { suggest: false });
  return o.kind === 'unique' ? o.match.display : null;
}

function asArchetype(name, index) {
  const owners = [];
  let display = null;
  for (const c of index.classes) {
    const o = match(name, c.archetypes, { suggest: false });
    if (o.kind === 'unique') { owners.push(c.cand.display); display = o.match.display; }
  }
  return owners.length ? { display, of: owners } : null;
}

// Only pages that exist for one race (heritages) can tell which race a name belongs to.
function asHeritage(name, index) {
  const key = normalize(name);
  const hits = [];
  for (const page of index.pages) {
    if (!page.race || page.n.length < 2) continue;
    const hit = page.cands.find(c => normalize(c.display) === key || normalize(splitParenChain(c.display)?.[0]) === key);
    if (hit) hits.push({ display: hit.display, page: page.cand.display, race: page.race });
  }
  const races = [...new Set(hits.map(h => h.race))];
  return races.length === 1 ? hits[0] : null;
}

const REFUSES = 'The mod refuses a build with an unknown race, unless a mod you use adds it.';

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
  suggestionRun = { left: SUGGESTION_BUDGET, cache: new Map() };
  const issues = [];
  if (build.start && !blank(build.start.race)) {
    const o = match(build.start.race, index.races);
    if (o.kind === 'none') {
      const heritage = asHeritage(build.start.race, index);
      if (heritage)
        issues.push(warn('start.race', `'${build.start.race}' is not a race: '${heritage.display}' is a ${heritage.race} option on page '${heritage.page}'. Set "race": "${heritage.race}" and add { "in": "${heritage.page}", "pick": "${heritage.display}" } on level 1. ${REFUSES}`));
      else issues.push(refused('start.race', 'race', build.start.race, o));
    }
  }

  // The build's race as the game names it, so "TieflingRace" or "tiefling" compare equal to a page's race.
  const raceMatch = build.start && !blank(build.start.race) ? match(build.start.race, index.races, { suggest: false }) : null;
  const raceName = raceMatch?.kind === 'unique' ? raceMatch.match.display : null;
  const classes = [];
  const unknownClasses = [];   // one message per name, with all its levels
  for (const row of build.levels ?? []) {
    if (!row) continue;
    const where = `levels[level ${row.level}]`;
    let cls = null;
    if (!blank(row.class)) {
      const o = match(row.class, index.classes.map(c => c.cand));
      if (o.kind === 'unique') classes.push(cls = index.classes.find(c => c.cand === o.match));
      else if (o.kind === 'none') {
        const other = asArchetype(row.class, index);
        const owner = other?.of.length === 1 ? other.of[0] : null;
        unknownClasses.push({ level: row.level, name: row.class, issue: owner
          ? warn('', `'${row.class}' is an archetype of ${owner}: write "class": "${owner}" and "archetype": "${other.display}" on the first level taken in it, "class": "${owner}" on the others.`)
          : other
            ? warn('', `'${row.class}' is an archetype of ${other.of.join(' or ')}, not a class: write the class it belongs to, with "archetype": "${other.display}" on the first level taken in it.`)
            : refused('', 'class', row.class, o) });
      }
    }
    if (!blank(row.archetype) && cls) {
      const o = match(row.archetype, cls.archetypes);
      const isClass = o.kind === 'none' ? asClass(row.archetype, index) : null;
      const elsewhereArchetype = o.kind === 'none' && !isClass ? asArchetype(row.archetype, index) : null;
      if (isClass)
        issues.push(warn(where, `'${row.archetype}' is a class of its own, not an archetype of ${cls.cand.display}: write "class": "${isClass}" on the levels taken in it.`));
      else if (elsewhereArchetype)
        issues.push(warn(where, `'${row.archetype}' is an archetype of ${elsewhereArchetype.of.join(' or ')}, not of ${cls.cand.display}.`));
      else if (o.kind === 'none') issues.push(unknown(where, `archetype of ${cls.cand.display}`, row.archetype, o));
    }
    checkPicks(row.picks, where, index, issues, raceName);
  }

  const byName = new Map();
  for (const u of unknownClasses) {
    if (!byName.has(u.name)) byName.set(u.name, { issue: u.issue, levels: [] });
    byName.get(u.name).levels.push(u.level);
  }
  for (const { issue, levels } of byName.values()) issues.push({ ...issue, where: `levels[level ${levels.join(', ')}]` });

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

  const bare = issues.filter(i => i.bare).map(i => i.bare);
  const result = issues.filter(i => !i.bare);
  if (bare.length > 0)
    result.push({ error: false, note: true, where: '',
      message: `${bare.length} pick${bare.length === 1 ? '' : 's'} without "in" (${[...new Set(bare)].join(', ')}): the game offers ${bare.length === 1 ? 'it' : 'each'} on several pages. The mod takes the page when only one open page on that level offers the name, and otherwise reports it as ambiguous; then add "in".` });
  return result;
}

function checkPicks(picks, where, index, issues, race) {
  // Pages the row names with "in": a further choice can come as its own pick ({ "in": "Weapon Focus", … }).
  const rowPages = new Set((picks ?? []).filter(p => p && typeof p === 'object' && !blank(p.in)).map(p => normalize(p.in)));
  (picks ?? []).forEach((entry, i) => {
    if (entry == null) return;
    const at = `${where} picks[${i + 1}]`;
    const pick = typeof entry === 'string'
      ? { in: null, chain: [entry] }
      : { in: entry.in ?? null, chain: Array.isArray(entry.pick) ? entry.pick : [entry.pick] };
    pick.race = race;
    pick.rowPages = rowPages;
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
  // Suggestions are the slow part (~40 ms each on the full list): computed only when a message needs them,
  // once per name, and for at most SUGGESTION_BUDGET names per check — a nonsense answer must not freeze the page.
  return { kind: 'none', get outcome() { return suggestOnce(name, pages, index); } };
}

const SUGGESTION_BUDGET = 20;
let suggestionRun = { left: SUGGESTION_BUDGET, cache: new Map() };

function suggestOnce(name, pages, index) {
  const key = `${pages === index.pages ? '*' : pages.map(p => p.cand.display).join('|')}|${normalize(name)}`;
  if (suggestionRun.cache.has(key)) return suggestionRun.cache.get(key);
  // resolve() already found no match on any page; without a budget left there is nothing more to compute.
  const outcome = suggestionRun.left-- > 0
    ? match(name, candidatesOf(pages, index))
    : { kind: 'none', match: null, tied: [], suggestions: [] };
  suggestionRun.cache.set(key, outcome);
  return outcome;
}

// An option the page named by "in" does not offer is often a choice inside a group on that page ("Martial Disciple"
// under "Oblate") or an option of another page ("Last Stand" is a Mythic Ability, not a Mythic Feat). Say so, and
// for a misspelling name the page of each suggestion (ChatGPT build from a Neoseeker guide, 2026-10-06).
function elsewhereOnPages(at, pageName, name, pages, index) {
  const key = normalize(name);
  let nested = null;
  for (const page of pages)
    for (const c of page.cands) {
      const group = c.id ? index.features.get(c.id) : null;
      const inner = group?.sub?.map(id => index.features.get(id)?.cand).filter(Boolean) ?? [];
      const hit = match(name, inner, { suggest: false });
      if (hit.kind === 'unique' && !nested) nested = { group: c.display, display: hit.match.display };
    }
  const other = resolve(name, index.pages.filter(p => !p.nested), index);
  const titles = other.kind === 'unique'
    ? [...new Set(other.hits.filter(h => h.page.n.length > 1 && !pages.includes(h.page)).map(h => h.page.cand.display))] : [];
  // A close name on the page itself ("Lizard Familiar" for "Lizard") beats the same word on another page, which is
  // not even mentioned: the guide meant this page, and naming the other one would lure the LLM there.
  const inPage = suggestOnce(name, pages, index);
  if (inPage.suggestions.length)
    return warn(at, `Unknown option '${name}' on page '${pageName}'. Did you mean: ${inPage.suggestions.join(', ')}? ${NOTE}`);
  if (titles.length)
    return warn(at, `'${name}' is not on page '${pageName}' but on ${titles.map(t => `'${t}'`).join(' or ')}: write "in": "${titles[0]}"` +
      (nested ? ` (or ["${nested.group}", "${nested.display}"] on '${pageName}').` : '.'));
  if (nested)
    return warn(at, `'${name}' is a choice under '${nested.group}': write ["${nested.group}", "${nested.display}"] with "in": "${pageName}".`);
  if (!key) return null;
  const outcome = suggestOnce(name, index.pages, index);
  if (!outcome.suggestions.length) return null;
  const withPages = outcome.suggestions.map(display => {
    const c = index.allCands.find(x => x.display === display);
    const on = c?.id ? [...(index.pagesOf.get(c.id) ?? [])] : [];
    return on.length && !on.includes(pageName) ? `${display} (page '${on[0]}')` : display;
  });
  return warn(at, `Unknown option '${name}' on page '${pageName}'. Did you mean: ${withPages.join(', ')}? ${NOTE}`);
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
  if (r.kind === 'none') {
    issues.push(blank(pick.in) ? unknown(at, 'option', chain[0], r.outcome) : elsewhereOnPages(at, pick.in, chain[0], pages, index) ?? unknown(at, 'option', chain[0], r.outcome, ` on page '${pick.in}'`));
    return;
  }
  if (r.kind === 'ambiguous') { issues.push(tied(at, chain[0], r.outcome)); return; }
  // A racial page (heritage) exists only for its race.
  const race = pick.race;
  const racial = r.hits.map(h => h.page.race).filter(Boolean);
  if (race && racial.length === r.hits.length && !racial.some(x => normalize(x) === normalize(race)))
    issues.push(warn(at, `'${chain[0]}' on page '${r.hits[0].page.cand.display}' belongs to the race ${racial[0]}, but the build's race is ${race}.`));
  if (blank(pick.in)) {
    const titles = [...new Set(r.hits.filter(h => h.page.n.length > 1).map(h => h.page.cand.display))];
    // Bare names are allowed (the prompt suggests them when the page is unknown); collected into one note.
    if (titles.length > 1) issues.push({ bare: chain.join(' > ') });
  }
  const first = r.hits[0].match;
  let current = first.id ? index.features.get(first.id) : null;
  // A parametrized feat (weapon, school …) or a nested selection without its choice stays open in the game.
  // A note, not a warning: when the guide names no weapon or school, the honest answer is to leave it open.
  if (chain.length === 1 && !pick.rowPages.has(normalize(first.display))) {
    const sub = subCandidates(current, index);
    if (sub.length > 0) {
      const examples = sub.slice(0, 3).map(c => c.display).join(', ');
      issues.push({ error: false, note: true, where: at,
        message: `'${chain[0]}' needs a further choice (such as ${examples}${sub.length > 3 ? ', …' : ''}). Write it as ["${chain[0]}", <choice>] if the guide names it; if the guide names none, the player chooses it in the game.` });
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
