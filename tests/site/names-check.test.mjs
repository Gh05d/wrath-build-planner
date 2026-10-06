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
  assert.match(w.message, /Unknown option 'Power Atack' on page 'Feat'.*Did you mean: Power Attack\?/);
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

test('an empty chain adds no name warning next to the format error', () => {
  assert.deepEqual(checkNames(build([{ in: 'Feat', pick: [] }]), index), []);
});

test('the several-pages warning names only titles players see', () => {
  const [w] = checkNames(build(['Power Attack']), index);
  assert.doesNotMatch(w.message, /InternalOnlySelection/);
});

test('an unknown class or race says the mod refuses the file', () => {
  const messages = checkNames({ format: 1, name: 'x', start: { race: 'Hooman' }, levels: [{ level: 1, class: 'Wizzard' }] }, index).map(i => i.message);
  assert.ok(messages.every(m => /refuse/.test(m) && !/Fine if/.test(m)), messages.join('\n'));
});

test('an archetype that is a class says so', () => {
  const [w] = checkNames({ format: 1, name: 'x', levels: [{ level: 11, class: 'Fighter', archetype: 'Eldritch Knight' }] }, index);
  assert.match(w.message, /'Eldritch Knight' is a class of its own, not an archetype of Fighter/);
});

test('a race that is an option on a page says where it belongs', () => {
  const [w] = checkNames({ format: 1, name: 'x', start: { race: 'Grimspawn' }, levels: [{ level: 1, class: 'Fighter' }] }, index);
  assert.match(w.message, /'Grimspawn' is not a race: 'Grimspawn \(Daemon-Spawn\)' is an option on page 'Heritage'/);
});

test('the fix request explains scores above 18', () => {
  const text = fixRequest([{ error: true, where: 'start.abilityScores', message: 'Dexterity is 19; starting scores go from 7 to 18 (before the racial bonus).' }]);
  assert.match(text, /subtract the racial bonus/);
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
  // A plain "leave out what you are unsure of" made Haiku drop correct picks instead of taking the suggestion (2026-10-06).
  assert.match(text, /Where a suggestion is the name the guide means, use it; leave out only what you cannot match\./);
  assert.doesNotMatch(text, /Leave out what you are unsure of/);
});

// End to end with the real export (Task 7).
const vocab = read('../../site/data/vocabulary.json');
const real = indexNames(read('../../site/data/names.json'));

test('shipped example build: no errors, no warnings', () => {
  const example = read('../../Builds-examples/two-handed-fighter.json');
  const issues = [...validate(example, { vocab, known: null }), ...checkNames(example, real)];
  assert.deepEqual(issues, []);
});

test('sixty picks without "in" are checked fast (Important 1)', () => {
  const names = ['Power Attack', 'Dodge', 'Cleave', 'Toughness', 'Power Atack', 'Dodgee', 'Weapon Focus (Greatsword)', 'Improved Initiative', 'Clevae', 'Iron Will'];
  const levels = Array.from({ length: 6 }, (_, i) => ({ level: i + 1, class: 'Fighter', picks: names }));
  const started = performance.now();
  checkNames({ format: 1, name: 'x', levels }, real);
  const ms = performance.now() - started;
  console.log(`60 bare picks: ${Math.round(ms)} ms`);
  assert.ok(ms < 1000, `${Math.round(ms)} ms`);
});

test('test builds: no format errors', () => {
  for (const file of readdirSync(new URL('../builds/', import.meta.url))) {
    const b = read(`../builds/${file}`);
    assert.deepEqual(validate(b, { vocab, known: null }).filter(i => i.error), [], file);
  }
});
