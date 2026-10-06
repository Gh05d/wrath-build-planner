import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { checkPointBuy } from '../../site/js/budget.js';

const build = scores => ({ format: 1, name: 'x', start: { abilityScores: scores }, levels: [{ level: 1, class: 'Fighter' }] });

test('25 points pass', () => {
  assert.deepEqual(checkPointBuy(build({ Strength: 16, Dexterity: 14, Constitution: 14, Intelligence: 12, Wisdom: 12, Charisma: 11 })), []);
});

test('over budget warns with the cost (acceptance 2026-10-06: 10/17/12/18/10/8)', () => {
  const [w] = checkPointBuy(build({ Strength: 10, Dexterity: 17, Constitution: 12, Intelligence: 18, Wisdom: 10, Charisma: 8 }));
  assert.equal(w.error, false);
  assert.equal(w.where, 'start.abilityScores');
  assert.match(w.message, /cost 30 points; character creation gives 25/);
});

test('missing attributes count as 10 and abbreviations are read', () => {
  assert.deepEqual(checkPointBuy(build({ Str: 18, Dex: 14, Con: 12, Int: 11 })), []);   // 17 + 5 + 2 + 1 = 25
  assert.equal(checkPointBuy(build({ Str: 18, Dex: 14, Con: 14 })).length, 1);   // 17 + 5 + 5 = 27
});

test('out-of-range or unknown entries are left to the format check', () => {
  assert.deepEqual(checkPointBuy(build({ Strength: 20 })), []);
  assert.deepEqual(checkPointBuy(build({ Luck: 18 })), []);
  assert.deepEqual(checkPointBuy({ format: 1, name: 'x' }), []);
});

test('accepted acceptance builds stay within budget', () => {
  const example = JSON.parse(readFileSync(new URL('../../Builds-examples/two-handed-fighter.json', import.meta.url)));
  assert.deepEqual(checkPointBuy(example), []);
});

test('a mercenary build over 20 points warns (mercenary creation gives 20, seen 2026-10-06)', () => {
  const b = build({ Strength: 16, Dexterity: 14, Constitution: 14, Intelligence: 12, Wisdom: 12, Charisma: 11 });
  b.for = 'any';
  const [w] = checkPointBuy(b);
  assert.match(w.message, /cost 25 points; a mercenary gets 20/);
  b.for = 'main';
  assert.deepEqual(checkPointBuy(b), []);
});

test('unspent points warn: the game will not continue until all are spent (seen 2026-10-06)', () => {
  const [w] = checkPointBuy(build({ Strength: 10, Dexterity: 16, Constitution: 14, Intelligence: 10, Wisdom: 16, Charisma: 7 }));
  assert.match(w.message, /cost 21 points; character creation gives 25 .* all points must be spent/);
  const merc = build({ Strength: 16, Dexterity: 14, Constitution: 13, Intelligence: 10, Wisdom: 10, Charisma: 10 });
  merc.for = 'any';
  assert.match(checkPointBuy(merc)[0].message, /cost 18 points; a mercenary gets 20/);
});
