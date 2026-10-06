import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { validate, canonicalize } from '../../site/js/validate.js';
import { match } from '../../site/js/match.js';

const vocab = JSON.parse(readFileSync(new URL('../../site/data/vocabulary.json', import.meta.url)));
const vectors = JSON.parse(readFileSync(new URL('../vectors/validation.json', import.meta.url)));
const cands = names => names.map(n => ({ names: [n], display: n }));
const known = {
  classExists: n => match(n, cands(vectors.knownClasses)).kind === 'unique',
  raceExists: n => match(n, cands(vectors.knownRaces)).kind === 'unique',
};

for (const c of vectors.cases) {
  test(`vector: ${c.name}`, () => {
    const issues = validate(canonicalize(c.build).build, { vocab, known });   // as the page does
    if (c.parse === 'error') {
      assert.ok(issues.some(i => i.error && i.structure), JSON.stringify(issues));
      return;
    }
    assert.deepEqual(issues.map(i => ({ error: i.error, where: i.where, message: i.message })), c.issues);
  });
}

test('unknown field gets a did-you-mean', () => {
  const issues = validate({ format: 1, name: 'x', levels: [{ level: 1, class: 'Fighter', feats: ['Dodge'] }] }, { vocab, known: null });
  assert.equal(issues.length, 1);
  assert.match(issues[0].message, /Unknown field 'feats'.*did you mean 'picks'/);
  assert.equal(issues[0].where, 'levels[1]');
});

test('numeric strings are accepted like the mod does', () => {
  assert.deepEqual(validate({ format: '1', name: 'x', levels: [{ level: '1', class: 'Fighter' }] }, { vocab, known: null }), []);
});

test('pick shapes', () => {
  const ok = { format: 1, name: 'x', levels: [{ level: 1, class: 'Fighter', picks: ['Dodge', { in: 'Feat', pick: ['Weapon Focus', 'Greatsword'] }] }] };
  assert.deepEqual(validate(ok, { vocab, known: null }), []);
  const bad = { format: 1, name: 'x', levels: [{ level: 1, class: 'Fighter', picks: [{ in: 'Feat', pick: { a: 1 } }] }] };
  assert.ok(validate(bad, { vocab, known: null })[0].structure);
});

test('field names in another case are accepted like Newtonsoft does', () => {
  const { build, renamed } = canonicalize({ Format: 1, Name: 'x', Levels: [{ Level: 1, Class: 'Fighter', Picks: ['Dodge'] }] });
  assert.deepEqual(validate(build, { vocab, known: null }), []);
  assert.deepEqual(build.levels[0].picks, ['Dodge']);
  assert.ok(renamed.includes('Levels'));
});

test("pick object keys stay case-sensitive, as the mod's PickEntryConverter is", () => {
  const { build } = canonicalize({ format: 1, name: 'x', levels: [{ level: 1, class: 'Fighter', picks: [{ In: 'Feat', pick: 'Dodge' }] }] });
  assert.ok(validate(build, { vocab, known: null }).some(i => i.structure));
});

test('a field on the wrong level says where it belongs', () => {
  const [e] = validate({ format: 1, name: 'x', feats: ['Dodge'], levels: [{ level: 1, class: 'Fighter' }] }, { vocab, known: null });
  assert.match(e.message, /"picks" goes inside each entry of "levels"/);
  const [r] = validate({ format: 1, name: 'x', race: 'Human', levels: [{ level: 1, class: 'Fighter' }] }, { vocab, known: null });
  assert.match(r.message, /"race" goes inside "start"/);
});

test('background and deity in the start block get the level-1 pick to write instead', () => {
  const issues = validate({ format: 1, name: 'x', start: { race: 'Human', background: 'Martial Disciple', deity: 'Lamashtu' }, levels: [{ level: 1, class: 'Fighter' }] }, { vocab, known: null });
  assert.match(issues[0].message, /'background' is not a field of "start": add \{ "in": "Background Selection", "pick": "Martial Disciple" \} to the picks of level 1/);
  assert.match(issues[1].message, /\{ "in": "Deity", "pick": "Lamashtu" \}/);
});
