import { test } from 'node:test';
import assert from 'node:assert/strict';
import { extractJson } from '../../site/js/extract.js';

test('plain object', () => {
  const r = extractJson('{"format": 1, "name": "x"}');
  assert.equal(r.error, null);
  assert.equal(r.value.name, 'x');
  assert.equal(r.text, '{\n  "format": 1,\n  "name": "x"\n}');
});

test('json block wins over a text block before it', () => {
  const reply = 'Here you go:\n```text\nnotes\n```\n```json\n{"name": "a"}\n```\nLeft out:\n- traits';
  assert.equal(extractJson(reply).value.name, 'a');
});

test('unlabelled fence that holds an object', () => {
  assert.equal(extractJson('```\n{"name": "b"}\n```').value.name, 'b');
});

test('prose around a bare object, braces inside strings', () => {
  const r = extractJson('Sure! {"name": "c {x}", "levels": []} Hope this helps.');
  assert.equal(r.value.name, 'c {x}');
});

test('comments and trailing commas are removed with a note', () => {
  const r = extractJson('{\n  // the name\n  "name": "d", /* x */\n  "levels": [1, 2,],\n}');
  assert.equal(r.error, null);
  assert.deepEqual(r.value, { name: 'd', levels: [1, 2] });
  assert.equal(r.notes.length, 2);
});

test('no JSON at all (Review Focus 2)', () => {
  const r = extractJson('Level 1: Fighter, Power Attack. Level 2: Cleave.');
  assert.equal(r.value, null);
  assert.match(r.error.message, /No build found/);
});

test('empty input', () => {
  assert.match(extractJson('   ').error.message, /Paste/);
});

test('typographic quotes (Review Focus 4)', () => {
  const r = extractJson('{“name”: “x”}');
  assert.equal(r.value, null);
  assert.match(r.error.message, /straight quotes/);
  assert.equal(r.error.line, 1);
});

test('a list is not a build', () => {
  assert.match(extractJson('[1, 2]').error.message, /No build found/);
});

test('syntax error has a location', () => {
  const r = extractJson('{\n  "name": "x"\n  "format": 1\n}');
  assert.equal(r.value, null);
  assert.equal(r.error.line, 3);
});
