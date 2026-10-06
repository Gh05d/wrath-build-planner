// The whole check of a pasted answer, without the page: the page and check.mjs (agents, command line) share it.
import { extractJson } from './extract.js';
import { validate, canonicalize } from './validate.js';
import { checkNames } from './names-check.js';
import { checkPointBuy } from './budget.js';

// vocab and index may be null (data not loaded): those checks are skipped.
// Returns { build, clean, issues, summary, ok, canFix }; summary is null when nothing was pasted.
export function checkText(text, { vocab, index }) {
  const result = { build: null, clean: null, issues: [], summary: null, ok: false, canFix: false };
  if (!text.trim()) return result;

  const extracted = extractJson(text);
  const issues = extracted.notes.map(message => ({ error: false, note: true, where: '', message }));
  if (extracted.error) {
    // No build at all (prompt pasted, no JSON): nothing an AI could fix, so no fix request.
    issues.push({ error: true, where: '', message: extracted.error.message, noBuild: !/not valid/.test(extracted.error.message) });
    return finish(result, issues, 'The build could not be read.');
  }
  const { build, renamed } = canonicalize(extracted.value);
  if (renamed.length) issues.push({ error: false, note: true, where: '', message: `Wrote the field names ${renamed.join(', ')} in the spelling the format uses.` });
  if (vocab) issues.push(...validate(build, { vocab, known: null }));
  if (!issues.some(i => i.structure)) issues.push(...checkPointBuy(build), ...checkNames(build, index));
  result.build = build;
  result.clean = JSON.stringify(build, null, 2);
  return finish(result, issues, summary(build, issues));
}

function finish(result, issues, summaryText) {
  result.issues = issues;
  result.summary = summaryText;
  result.ok = !!result.clean && !issues.some(i => i.error);
  result.canFix = issues.some(i => !i.note) && !issues.some(i => i.noBuild);
  return result;
}

function summary(build, issues) {
  const levels = (build.levels ?? []).filter(Boolean).map(r => Number(r.level)).filter(Number.isFinite);
  const picks = [...(build.levels ?? []), ...(build.mythic ?? [])].filter(Boolean).reduce((n, r) => n + (r.picks?.length ?? 0), 0);
  const errors = issues.filter(i => i.error).length;
  const warnings = issues.filter(i => !i.error && !i.note).length;
  const range = levels.length ? `Levels ${Math.min(...levels)}–${Math.max(...levels)}` : 'No levels';
  const mythic = (build.mythic ?? []).filter(Boolean).length;
  const n = (count, word) => `${count} ${word}${count === 1 ? '' : 's'}`;
  return `${build.name ?? 'Unnamed build'}: ${range}${mythic ? `, ${n(mythic, 'mythic rank')}` : ''}, ${n(picks, 'pick')} — ${n(errors, 'error')}, ${n(warnings, 'warning')}.`;
}
