import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const names = JSON.parse(readFileSync(new URL('../../site/data/names.json', import.meta.url)));
const titles = new Set(names.pages.map(p => p.n[0]));
const feature = name => Object.values(names.features).find(f => f.n[0] === name);

test('nested selections are marked', () => {
  const warrior = names.pages.find(p => p.n[0] === 'Warrior');
  assert.ok(!warrior || warrior.nested === true, 'Warrior is reached through Background Selection');
  assert.ok(!names.pages.find(p => p.n[0] === 'Background Selection').nested);
});

test('meta', () => {
  assert.ok(names.meta.gameVersion);
  assert.match(names.meta.exported, /^\d{4}-\d{2}-\d{2}$/);
});

test('page titles seen in the game are exported', () => {
  for (const t of ['Feat', 'Bonus Combat Feat', 'Background Selection', 'Deity', 'School', 'Arcane Bond', 'Order', 'Mythic Ability', 'Mythic Feat', 'Opposition School'])
    assert.ok(titles.has(t), `missing page title ${t}; have: ${[...titles].sort().join(', ')}`);
});

test('classes, archetypes, spells', () => {
  const magus = names.classes.find(c => c.n[0] === 'Magus');
  assert.ok(magus, 'Magus');
  assert.ok(magus.archetypes.some(a => a[0] === 'Sword Saint'), 'Sword Saint archetype');
  const wizard = names.classes.find(c => c.n[0] === 'Wizard');
  assert.ok(wizard.spells.some(id => names.spells[id][0] === 'Magic Missile'), 'Magic Missile for the wizard');
});

test('chains: parameters and nested selections', () => {
  assert.ok(feature('Weapon Focus').params.some(p => p[0] === 'Greatsword'), 'Weapon Focus > Greatsword');
  const warrior = feature('Warrior');
  assert.ok(warrior.sub.some(id => names.features[id].n[0] === 'Gladiator'), 'Warrior > Gladiator');
});

test('races and mythic paths', () => {
  assert.ok(names.races.some(r => r[0] === 'Human'));
  assert.ok(names.mythicPaths.some(p => p[0] === 'Angel'));
});
