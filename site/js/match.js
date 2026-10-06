// Port of WrathBuildPlanner/Core/NameMatcher.cs. tests/vectors/name-matching.json runs against both;
// the C# class is the reference.
const MAX_SUGGESTIONS = 3;
const CATEGORY_SEPARATORS = [' — ', ' – ', ' - '];

// Lower-case letters and digits only; accents removed ("Cat’s Grace" == "cats grace").
// C# char.IsLetterOrDigit covers the letter categories and Nd.
const normalized = new Map();   // the name list has some 20,000 names; each is normalized once

export function normalize(text) {
  if (!text) return '';
  const cached = normalized.get(text);
  if (cached !== undefined) return cached;
  let out = '';
  for (const c of String(text).normalize('NFD')) {
    if (/\p{Mn}/u.test(c)) continue;
    if (/[\p{L}\p{Nd}]/u.test(c)) out += c.toLowerCase();
  }
  normalized.set(text, out);
  return out;
}

// The part after a category prefix, or null when the name has none.
export function stripCategory(name) {
  if (!name) return null;
  for (const sep of CATEGORY_SEPARATORS) {
    const at = name.indexOf(sep);
    if (at > 0 && at + sep.length < name.length) return name.substring(at + sep.length);
  }
  const chain = splitParenChain(name);
  return chain ? chain[1] : null;
}

// "Weapon Focus (Greatsword)" -> ["Weapon Focus", "Greatsword"].
export function splitParenChain(text) {
  if (!text) return null;
  const t = String(text).trim();
  const open = t.indexOf('(');
  if (open <= 0 || !t.endsWith(')')) return null;
  const head = t.substring(0, open).trim();
  const tail = t.substring(open + 1, t.length - 1).trim();
  return head.length > 0 && tail.length > 0 ? [head, tail] : null;
}

// suggest: false skips the similarity hints (the slow part) for callers that only need the decision.
export function match(wanted, candidates, { suggest: withSuggestions = true } = {}) {
  const outcome = { kind: 'none', match: null, tied: [], suggestions: [] };
  const key = normalize(wanted);
  if (key.length === 0 || !candidates || candidates.length === 0) return outcome;
  const exact = candidates.filter(c => c.names.some(n => normalize(n) === key));
  if (decide(exact, outcome)) return outcome;
  const stripped = candidates.filter(c => c.names.some(n => normalize(stripCategory(n)) === key));
  if (decide(stripped, outcome)) return outcome;
  if (withSuggestions) outcome.suggestions = suggest(key, candidates);
  return outcome;
}

function decide(hits, outcome) {
  if (hits.length === 0) return false;
  if (hits.length === 1) {
    outcome.kind = 'unique';
    outcome.match = hits[0];
  } else {
    outcome.kind = 'ambiguous';
    outcome.tied = hits;
  }
  return true;
}

// Hints for the human only: names that contain the wanted text (or vice versa), then near-typos.
function suggest(key, candidates) {
  const scored = [];
  const allowed = Math.max(2, Math.floor(key.length / 4));
  for (const c of candidates) {
    let best = Infinity;
    for (const name of c.names) {
      const n = normalize(name);
      if (n.length === 0) continue;
      if (n.includes(key) || key.includes(n)) { best = 0; break; }
      // The distance is at least the length difference: beyond the allowance it cannot become a hint.
      if (Math.abs(n.length - key.length) > allowed) continue;
      const d = editDistance(key, n);
      if (d < best) best = d;
    }
    if (best <= allowed) scored.push([best, c.display]);
  }
  scored.sort((a, b) => a[0] - b[0]);   // stable, like LINQ OrderBy
  return [...new Set(scored.map(s => s[1]))].slice(0, MAX_SUGGESTIONS);
}

export function editDistance(a, b) {
  let previous = Array.from({ length: b.length + 1 }, (_, j) => j);
  let current = new Array(b.length + 1);
  for (let i = 1; i <= a.length; i++) {
    current[0] = i;
    for (let j = 1; j <= b.length; j++) {
      const cost = a[i - 1] === b[j - 1] ? 0 : 1;
      current[j] = Math.min(current[j - 1] + 1, previous[j] + 1, previous[j - 1] + cost);
    }
    [previous, current] = [current, previous];
  }
  return previous[b.length];
}
