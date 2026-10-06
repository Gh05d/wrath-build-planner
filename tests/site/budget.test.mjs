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
  assert.equal(checkPointBuy(build({ Str: 18, Dex: 14, Con: 14 })).length, 1);   // 17 + 5 + 5 = 27, over budget
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

test('"for" other than main accepts both budgets (main 25, mercenary 20)', () => {
  const at25 = build({ Strength: 16, Dexterity: 14, Constitution: 14, Intelligence: 12, Wisdom: 12, Charisma: 11 });
  const at20 = build({ Strength: 16, Dexterity: 14, Constitution: 14, Intelligence: 10, Wisdom: 10, Charisma: 10 });
  for (const f of ['any', 'Seelah', 'Main character']) {
    at25.for = f; at20.for = f;
    assert.deepEqual(checkPointBuy(at25), [], f);
    assert.deepEqual(checkPointBuy(at20), [], f);
  }
  const at22 = build({ Strength: 16, Dexterity: 14, Constitution: 14, Intelligence: 12, Wisdom: 10, Charisma: 10 });
  at22.for = 'any';
  assert.match(checkPointBuy(at22)[0].message, /cost 22 points; the main character gets 25, a mercenary 20/);
});

test('unspent points warn only when all six scores are given', () => {
  const [w] = checkPointBuy(build({ Strength: 10, Dexterity: 16, Constitution: 14, Intelligence: 10, Wisdom: 16, Charisma: 7 }));
  assert.match(w.message, /cost 21 points; character creation gives 25 .* all points must be spent/);
  assert.deepEqual(checkPointBuy(build({ Strength: 16, Dexterity: 14 })), []);
});

test('two keys for one attribute skip the check', () => {
  assert.deepEqual(checkPointBuy(build({ Strength: 16, Str: 7, Dexterity: 14, Constitution: 14, Intelligence: 12, Wisdom: 12, Charisma: 11 })), []);
});
