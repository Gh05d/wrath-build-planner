// Checks every name in a build against site/data/names.json. Only warnings: content from other mods is
// not in the list and still works in the game. What depends on the live character (prerequisites, whether a
// page is offered on that level, free spell slots) is left to the mod.
import { match, splitParenChain } from './match.js';

const NOTE = 'Fine if a mod you use adds it.';
const blank = s => s == null || String(s).trim() === '';
const cand = (names, id) => ({ id, names: id ? [...names, id] : names, display: names[0] });

export function indexNames(data) {
  const features = new Map(Object.entries(data.features).map(([id, f]) => [id, { ...f, cand: cand(f.n, id) }]));
  const spells = new Map(Object.entries(data.spells).map(([id, n]) => [id, cand(n, id)]));
  const pages = data.pages.map(p => ({ ...p, cand: cand(p.n, null) }));
  const pagesOf = new Map();
  for (const p of pages)
    for (const id of p.items ?? []) {
      if (!pagesOf.has(id)) pagesOf.set(id, new Set());
      pagesOf.get(id).add(p.cand.display);
    }
  return {
    features, spells, pages, pagesOf,
    classes: data.classes.map(c => ({ cand: cand(c.n, null), archetypes: c.archetypes.map(a => cand(a, null)), spells: c.spells })),
    races: data.races.map(r => cand(r, null)),
    paths: data.mythicPaths.map(p => cand(p, null)),
  };
}

const warn = (where, message) => ({ error: false, where, message });

function unknown(where, what, name, outcome) {
  const hint = outcome.suggestions.length ? ` Did you mean: ${outcome.suggestions.join(', ')}?` : '';
  return warn(where, `Unknown ${what} '${name}'.${hint} ${NOTE}`);
}

function tied(where, name, outcome) {
  return warn(where, `'${name}' fits several options: ${outcome.tied.map(t => t.display).join(', ')}. Use the exact name.`);
}

export function checkNames(build, index) {
  if (!index) return [{ error: false, note: true, where: '', message: 'Names were not checked: the name list could not be loaded.' }];
  const issues = [];
  if (build.start && !blank(build.start.race)) {
    const o = match(build.start.race, index.races);
    if (o.kind === 'none') issues.push(unknown('start.race', 'race', build.start.race, o));
  }

  const classes = [];
  for (const row of build.levels ?? []) {
    if (!row) continue;
    const where = `levels[level ${row.level}]`;
    let cls = null;
    if (!blank(row.class)) {
      const o = match(row.class, index.classes.map(c => c.cand));
      if (o.kind === 'unique') classes.push(cls = index.classes.find(c => c.cand === o.match));
      else if (o.kind === 'none') issues.push(unknown(where, 'class', row.class, o));
    }
    if (!blank(row.archetype) && cls) {
      const o = match(row.archetype, cls.archetypes);
      if (o.kind === 'none') issues.push(unknown(where, `archetype of ${cls.cand.display}`, row.archetype, o));
    }
    checkPicks(row.picks, where, index, issues);
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

function checkPicks(picks, where, index, issues) {
  (picks ?? []).forEach((entry, i) => {
    if (entry == null) return;
    const at = `${where} picks[${i + 1}]`;
    const pick = typeof entry === 'string'
      ? { in: null, chain: [entry] }
      : { in: entry.in ?? null, chain: Array.isArray(entry.pick) ? entry.pick : [entry.pick] };
    if (pick.chain.some(blank)) return;   // the format check reports it
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
  const ids = new Set();
  const params = [];
  for (const p of pages) {
    for (const id of p.items ?? []) ids.add(id);
    for (const n of p.params ?? []) params.push(cand(n, null));
  }
  return [...[...ids].map(id => index.features.get(id)?.cand).filter(Boolean), ...params];
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
  for (const page of pages) {
    const o = match(name, candidatesOf([page], index));
    if (o.kind === 'unique') hits.push({ page, match: o.match });
    else if (o.kind === 'ambiguous' && !tiedOutcome) tiedOutcome = o;
  }
  if (hits.length > 0) {
    hits.sort((x, y) => (y.match.id ? 1 : 0) - (x.match.id ? 1 : 0));   // a feature beats a bare parameter value
    return { kind: 'unique', hits };
  }
  if (tiedOutcome) return { kind: 'ambiguous', outcome: tiedOutcome };
  return { kind: 'none', outcome: match(name, candidatesOf(pages, index)) };
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
  const what = blank(pick.in) ? 'option' : `option on page '${pick.in}'`;
  if (r.kind === 'none') { issues.push(unknown(at, what, chain[0], r.outcome)); return; }
  if (r.kind === 'ambiguous') { issues.push(tied(at, chain[0], r.outcome)); return; }
  if (blank(pick.in)) {
    const titles = [...new Set(r.hits.map(h => h.page.cand.display))];
    if (titles.length > 1)
      issues.push(warn(at, `'${chain[0]}' is offered on several pages (${titles.join(', ')}). Add "in" with the page title so the mod knows which one.`));
  }
  const first = r.hits[0].match;
  let current = first.id ? index.features.get(first.id) : null;
  for (let k = 1; k < chain.length; k++) {
    const sub = subCandidates(current, index);
    if (sub.length === 0) { issues.push(warn(at, `'${chain[k - 1]}' offers no further choice.`)); return; }
    const next = match(chain[k], sub);
    if (next.kind === 'none') { issues.push(unknown(at, `choice under '${chain[k - 1]}'`, chain[k], next)); return; }
    if (next.kind === 'ambiguous') { issues.push(tied(at, chain[k], next)); return; }
    current = next.match.id ? index.features.get(next.match.id) : null;
  }
}
