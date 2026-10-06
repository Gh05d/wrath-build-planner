import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { buildPrompt, EXAMPLE } from '../../site/js/prompt.js';
import { extractJson } from '../../site/js/extract.js';
import { validate } from '../../site/js/validate.js';
import { indexNames, checkNames } from '../../site/js/names-check.js';

const read = rel => JSON.parse(readFileSync(new URL(rel, import.meta.url)));
const vocab = read('../../site/data/vocabulary.json');
const names = read('../../site/data/names.json');

test('guide variant', () => {
  const p = buildPrompt(names, vocab, 'guide');
  assert.match(p, /\[paste the guide here\]/);
  assert.match(p, /Bonus Combat Feat/);
  assert.match(p, /Knowledge \(Arcana\)/);
  assert.match(p, /Lawful Good/);
  console.log(`prompt length: ${p.length}`);
  // Limit raised from 6,000 to 7,500 by the user on 2026-10-06: all 186 page titles stay in the prompt.
  assert.ok(p.length < 7500, `prompt is ${p.length} characters`);
});

test('page titles that differ only in case appear once', () => {
  const line = buildPrompt(names, vocab, 'guide').split('\n').find(l => l.includes('Page titles:'));
  const titles = line.substring(line.indexOf('Page titles:') + 12).replace(/\.$/, '').split(', ').map(t => t.trim().toLowerCase());
  assert.equal(new Set(titles).size, titles.length);
  assert.ok(line.includes('Channel Energy'));
});

test('pages known only by their internal name are not listed as titles', () => {
  const p = buildPrompt(names, vocab, 'guide');
  assert.doesNotMatch(p, /OracleRevelationWeaponMastery/);
});

test('design variant', () => {
  const p = buildPrompt(names, vocab, 'design');
  assert.match(p, /Design a build/);
  assert.doesNotMatch(p, /\[paste the guide here\]/);
});

test('without names a fixed title list is used', () => {
  assert.match(buildPrompt(null, vocab, 'guide'), /Mythic Ability/);
});

test('the example in the prompt passes the checker', () => {
  const r = extractJson(EXAMPLE);
  assert.equal(r.error, null);
  const issues = [...validate(r.value, { vocab, known: null }), ...checkNames(r.value, indexNames(names))];
  assert.deepEqual(issues, []);
});
