import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { normalize, match, splitParenChain } from '../../site/js/match.js';

const vectors = JSON.parse(readFileSync(new URL('../vectors/name-matching.json', import.meta.url)));
const cands = lists => lists.map(names => ({ names, display: names[0] }));

for (const c of vectors) {
  test(`vector: ${c.name}`, () => {
    const o = match(c.wanted, cands(c.candidates));
    assert.equal(o.kind, c.kind);
    assert.equal(o.match ? o.match.display : null, c.match);
    assert.deepEqual(o.tied.map(t => t.display), c.tied);
    assert.deepEqual(o.suggestions, c.suggestions);
  });
}

test('normalize', () => {
  assert.equal(normalize('Cat’s  Grace'), 'catsgrace');
  assert.equal(normalize('Résumé'), 'resume');
  assert.equal(normalize(null), '');
});

test('splitParenChain', () => {
  assert.deepEqual(splitParenChain('Weapon Focus (Greatsword)'), ['Weapon Focus', 'Greatsword']);
  assert.equal(splitParenChain('(Greatsword)'), null);
  assert.equal(splitParenChain('Weapon Focus'), null);
});
