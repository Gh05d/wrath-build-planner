import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { indexNames, checkNames } from '../../site/js/names-check.js';
import { fixRequest } from '../../site/js/fix.js';
import { validate } from '../../site/js/validate.js';

const read = rel => JSON.parse(readFileSync(new URL(rel, import.meta.url)));
const fixture = read('./fixtures/names-small.json');
const index = indexNames(fixture);

const build = (picks, extra = {}) => ({ format: 1, name: 'x', levels: [{ level: 1, class: 'Fighter', picks, ...extra }] });

test('known names pass', () => {
  assert.deepEqual(checkNames(build([{ in: 'Feat', pick: 'Power Attack' }, { in: 'Bonus Combat Feat', pick: ['Weapon Focus', 'Greatsword'] }]), index), []);
});

test('a name on two pages with the same title is not ambiguous (only one page is open in the game)', () => {
  assert.deepEqual(checkNames(build([{ in: 'Bonus Combat Feat', pick: ['Weapon Focus', 'Greatsword'] }]), index), []);
});

test('typo gets a suggestion', () => {
  const [w] = checkNames(build([{ in: 'Feat', pick: 'Power Atack' }]), index);
  assert.equal(w.error, false);
  assert.equal(w.where, 'levels[level 1] picks[1]');
  assert.match(w.message, /Unknown option on page 'Feat' 'Power Atack'.*Did you mean: Power Attack\?/);
});

test('unknown page', () => {
  const [w] = checkNames(build([{ in: 'Feats', pick: 'Power Attack' }]), index);
  assert.match(w.message, /Unknown page 'Feats'.*Did you mean: Feat/);
});

test('bare pick offered on two pages asks for "in" (Review Focus 5)', () => {
  const [w] = checkNames(build(['Power Attack']), index);
  assert.match(w.message, /offered on several pages \(.*Feat.*Bonus Combat Feat.*\)/);
});

test('guide notation without "in" resolves as a chain (Review Focus 5)', () => {
  const issues = checkNames(build(['Weapon Focus (Greatsword)']), index);
  assert.ok(issues.every(i => !/Unknown/.test(i.message)), JSON.stringify(issues));
});

test('chain below a leaf', () => {
  const [w] = checkNames(build([{ in: 'Feat', pick: ['Power Attack', 'Greatsword'] }]), index);
  assert.match(w.message, /'Power Attack' offers no further choice/);
});

test('class, archetype, race, path, spells', () => {
  const b = { format: 1, name: 'x', start: { race: 'Hooman' }, levels: [
    { level: 1, class: 'Wizzard' },
    { level: 2, class: 'Fighter', archetype: 'Two-Handed Fightr' },
    { level: 3, class: 'Wizard', spells: ['Magic Missile', 'Magic Misile'] }],
    mythic: [{ rank: 3, path: 'Angle' }] };
  const messages = checkNames(b, index).map(i => i.message);
  assert.equal(messages.length, 5, messages.join('\n'));
  assert.ok(messages.some(m => /Unknown race 'Hooman'.*Human/.test(m)));
  assert.ok(messages.some(m => /Unknown class 'Wizzard'.*Wizard/.test(m)));
  assert.ok(messages.some(m => /archetype of Fighter 'Two-Handed Fightr'/.test(m)));
  assert.ok(messages.some(m => /'Magic Misile'.*Magic Missile/.test(m)));
  assert.ok(messages.some(m => /Unknown mythic path 'Angle'.*Angel/.test(m)));
});

test('no name list (Review Focus 3)', () => {
  const issues = checkNames(build(['Power Attack']), null);
  assert.equal(issues.length, 1);
  assert.equal(issues[0].note, true);
});

test('fix request lists every problem', () => {
  const text = fixRequest([{ error: true, where: 'levels[level 1]', message: 'A.' }, { error: false, where: '', message: 'B.' }]);
  assert.match(text, /^Fix these problems/);
  assert.match(text, /1\. levels\[level 1\]: A\.\n2\. B\./);
  assert.match(text, /Leave out what you are unsure of/);
});

// End to end with the real export (Task 7).
const vocab = read('../../site/data/vocabulary.json');
const real = indexNames(read('../../site/data/names.json'));

test('shipped example build: no errors, no warnings', () => {
  const example = read('../../Builds-examples/two-handed-fighter.json');
  const issues = [...validate(example, { vocab, known: null }), ...checkNames(example, real)];
  assert.deepEqual(issues, []);
});

test('test builds: no format errors', () => {
  for (const file of readdirSync(new URL('../builds/', import.meta.url))) {
    const b = read(`../builds/${file}`);
    assert.deepEqual(validate(b, { vocab, known: null }).filter(i => i.error), [], file);
  }
});
