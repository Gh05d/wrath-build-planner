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

test('bare picks offered on several pages give one note, not warnings (the prompt allows bare names)', () => {
  const issues = checkNames(build(['Power Attack', 'Weapon Focus (Greatsword)']), index);
  assert.equal(issues.length, 1, JSON.stringify(issues));
  assert.equal(issues[0].note, true);
  assert.match(issues[0].message, /2 picks without "in".*Power Attack.*Weapon Focus/);
  assert.match(issues[0].message, /reports it as ambiguous/);
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

test('a bare pick only on internal-name pages adds no note', () => {
  assert.deepEqual(checkNames(build([{ in: 'Feat', pick: 'Power Attack' }]), index), []);
});

test('an unknown class or race says the mod refuses the file', () => {
  const messages = checkNames({ format: 1, name: 'x', start: { race: 'Hooman' }, levels: [{ level: 1, class: 'Wizzard' }] }, index).map(i => i.message);
  assert.ok(messages.every(m => /refuse/.test(m) && !/Fine if/.test(m)), messages.join('\n'));
});

test('an archetype that is a class says so', () => {
  const [w] = checkNames({ format: 1, name: 'x', levels: [{ level: 11, class: 'Fighter', archetype: 'Eldritch Knight' }] }, index);
  assert.match(w.message, /'Eldritch Knight' is a class of its own, not an archetype of Fighter/);
});

test('a class that is an archetype says whose, once for all its levels', () => {
  const levels = [1, 2, 3].map(level => ({ level, class: 'Two-Handed Fighter' }));
  const issues = checkNames({ format: 1, name: 'x', levels }, index);
  assert.equal(issues.length, 1, JSON.stringify(issues));
  assert.equal(issues[0].where, 'levels[level 1, 2, 3]');
  assert.match(issues[0].message, /'Two-Handed Fighter' is an archetype of Fighter: write "class": "Fighter" and "archetype": "Two-Handed Fighter" on the first level/);
});

test('a race that is an option on a page says where it belongs', () => {
  const [w] = checkNames({ format: 1, name: 'x', start: { race: 'Grimspawn' }, levels: [{ level: 1, class: 'Fighter' }] }, index);
  assert.match(w.message, /'Grimspawn' is not a race: 'Grimspawn \(Daemon-Spawn\)' is a Tiefling option on page 'Heritage'\. Set "race": "Tiefling"/);
  assert.match(w.message, /The mod refuses a build with an unknown race/);
});

test('a racial page pick under another race is flagged', () => {
  const [w] = checkNames({ format: 1, name: 'x', start: { race: 'Human' }, levels: [{ level: 1, class: 'Fighter', picks: [{ in: 'Heritage', pick: 'Grimspawn (Daemon-Spawn)' }] }] }, index);
  assert.match(w.message, /page 'Heritage' belongs to the race Tiefling, but the build's race is Human/);
});

test('a racial page pick under its own race passes', () => {
  assert.deepEqual(checkNames({ format: 1, name: 'x', start: { race: 'Tiefling' }, levels: [{ level: 1, class: 'Fighter', picks: [{ in: 'Heritage', pick: 'Grimspawn (Daemon-Spawn)' }] }] }, index), []);
});

test('a feat that needs a further choice gives a note, not a warning, and names no example to copy', () => {
  const [n] = checkNames(build([{ in: 'Feat', pick: 'Weapon Focus' }]), index);
  assert.equal(n.note, true);
  assert.match(n.message, /'Weapon Focus' needs a further choice \(such as Greatsword, Longsword\)/);
  // Written for the player reading the page: nothing to fix, chosen in the game.
  assert.match(n.message, /The player chooses it in the game; if the guide names one, add it as \["Weapon Focus", <choice>\]\./);
});

test('the further choice given as its own pick is not flagged', () => {
  assert.deepEqual(checkNames(build([{ in: 'Feat', pick: 'Weapon Focus' }, { in: 'Weapon Focus', pick: 'Longsword' }]), index), []);
});

test('a race hint comes only from racial pages and never from a prefix of an internal name', () => {
  const [w] = checkNames({ format: 1, name: 'x', start: { race: 'Elven' }, levels: [{ level: 1, class: 'Fighter' }] }, index);
  assert.doesNotMatch(w.message, /Set "race"/);
  assert.match(w.message, /The mod refuses a build with an unknown race/);
});

test('a heritage offered to several races names no single race', () => {
  const [w] = checkNames({ format: 1, name: 'x', start: { race: 'Adopted Elf' }, levels: [{ level: 1, class: 'Fighter' }] }, index);
  assert.doesNotMatch(w.message, /Set "race"/);
  assert.match(w.message, /refuses/);
});

test('the build race is compared after matching, so internal names work', () => {
  assert.deepEqual(checkNames({ format: 1, name: 'x', start: { race: 'TieflingRace' }, levels: [{ level: 1, class: 'Fighter', picks: [{ in: 'Heritage', pick: 'Grimspawn (Daemon-Spawn)' }] }] }, index), []);
});

const mythic = picks => ({ format: 1, name: 'x', mythic: [{ rank: 1, picks }] });

test('a name on another page says which page (ChatGPT build, 2026-10-06)', () => {
  const [w] = checkNames(mythic([{ in: 'Mythic Feat', pick: 'Last Stand' }]), index);
  assert.match(w.message, /'Last Stand' is not on page 'Mythic Feat' but on 'Mythic Ability': write "in": "Mythic Ability"/);
});

test('a close name on the page itself beats the same word on another page', () => {
  const [w] = checkNames(build([{ in: "Witch's Familiar", pick: 'Lizard' }]), index);
  assert.match(w.message, /Did you mean: Lizard Familiar\?/);
  assert.doesNotMatch(w.message, /Shifter Aspect/);
});

test('a misspelt name suggests the right page too', () => {
  const [w] = checkNames(mythic([{ in: 'Mythic Ability', pick: 'Dance Macabre' }]), index);
  assert.match(w.message, /Danse Macabre \(page 'First Ascension'\)/);
});

test('a choice inside a group says the chain', () => {
  const [w] = checkNames(build([{ in: 'Background Selection', pick: 'Gladiator' }]), index);
  assert.match(w.message, /'Gladiator' is a choice under 'Warrior': write \["Warrior", "Gladiator"\]/);
});

test('the fix request explains scores above 18', () => {
  const text = fixRequest([{ error: true, where: 'start.abilityScores', message: 'Dexterity is 19; starting scores go from 7 to 18 (before the racial bonus).' }]);
  assert.match(text, /remove the racial modifier/);
  assert.doesNotMatch(fixRequest([{ error: true, where: 'start.abilityScores', message: 'Charisma is 6; starting scores go from 7 to 18 (before the racial bonus).' }]), /racial modifier/);
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

test('hundreds of distinct unknown names stay fast (a nonsense answer must not freeze the page)', () => {
  const levels = Array.from({ length: 20 }, (_, i) => ({ level: i + 1, class: 'Fighter', picks: Array.from({ length: 40 }, (_, j) => `Made up feat ${i} ${j}`) }));
  const started = performance.now();
  const issues = checkNames({ format: 1, name: 'x', levels }, real);
  const ms = performance.now() - started;
  console.log(`800 unknown picks: ${Math.round(ms)} ms`);
  assert.equal(issues.length, 800);
  assert.ok(ms < 1500, `${Math.round(ms)} ms`);
});

test('test builds: no format errors', () => {
  for (const file of readdirSync(new URL('../builds/', import.meta.url))) {
    const b = read(`../builds/${file}`);
    assert.deepEqual(validate(b, { vocab, known: null }).filter(i => i.error), [], file);
  }
});
