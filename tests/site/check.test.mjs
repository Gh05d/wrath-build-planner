import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, mkdtempSync } from 'node:fs';
import { execFileSync, spawnSync } from 'node:child_process';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { checkText } from '../../site/js/check.js';
import { indexNames } from '../../site/js/names-check.js';
import { buildPrompt } from '../../site/js/prompt.js';

const read = rel => JSON.parse(readFileSync(new URL(rel, import.meta.url)));
const vocab = read('../../site/data/vocabulary.json');
const names = read('../../site/data/names.json');
const index = indexNames(names);
const path = rel => fileURLToPath(new URL(rel, import.meta.url));
const EXAMPLE = readFileSync(path('../../Builds-examples/two-handed-fighter.json'), 'utf8');
const CLI = path('../../site/check.mjs');

test('a clean build: ok, clean JSON, summary', () => {
  const r = checkText('Sure!\n```json\n' + EXAMPLE + '\n```', { vocab, index });
  assert.equal(r.ok, true);
  assert.equal(JSON.parse(r.clean).name, 'Two-Handed Fighter');
  assert.match(r.summary, /^Two-Handed Fighter: .* 0 errors, 0 warnings — ready for the game\.$/);
});

test('only notes left: ready for the game, the notes counted as nothing to fix', () => {
  const r = checkText(JSON.stringify({ format: 1, name: 'x', levels: [{ level: 1, class: 'Fighter', picks: [{ in: 'Feat', pick: 'Weapon Focus' }] }] }), { vocab, index });
  assert.equal(r.ok, true);
  assert.match(r.summary, /0 errors, 0 warnings — ready for the game\. 1 note below, nothing to fix\.$/);
});

test('with a warning the summary does not say ready', () => {
  const r = checkText(JSON.stringify({ format: 1, name: 'x', levels: [{ level: 1, class: 'Fightr' }] }), { vocab, index });
  assert.doesNotMatch(r.summary, /ready/);
});

test('names or vocabulary not loaded: never "ready" (nothing was checked)', () => {
  const r = checkText(JSON.stringify({ format: 1, name: 'x', levels: [{ level: 1, class: 'Fighter' }] }), { vocab: null, index: null });
  assert.doesNotMatch(r.summary, /ready/);
  assert.match(r.summary, /not checked/);
});

test('nothing pasted: no summary, nothing to fix', () => {
  const r = checkText('  ', { vocab, index });
  assert.equal(r.ok, false);
  assert.equal(r.summary, null);
  assert.equal(r.canFix, false);
});

test('no build in the text: an error, but no fix request', () => {
  const r = checkText('lol idk', { vocab, index });
  assert.equal(r.ok, false);
  assert.equal(r.clean, null);
  assert.equal(r.canFix, false);
});

test('a wrong name is a warning: the build can be used (content from other mods), the fix request is offered', () => {
  const r = checkText(JSON.stringify({ format: 1, name: 'x', levels: [{ level: 1, class: 'Fightr' }] }), { vocab, index });
  assert.equal(r.ok, true);
  assert.ok(r.issues.some(i => !i.error && !i.note));
  assert.equal(r.canFix, true);
});

test('a structural error: not ok', () => {
  const r = checkText('{"format": 1}', { vocab, index });
  assert.equal(r.ok, false);
  assert.equal(r.canFix, true);
});

const tmp = mkdtempSync(join(tmpdir(), 'wbp-check-'));
const cli = (args, input) => spawnSync(process.execPath, [CLI, ...args], { input, encoding: 'utf8' });

test('CLI: a clean build exits 0 and writes the clean file', () => {
  const answer = join(tmp, 'answer.txt');
  const out = join(tmp, 'out.json');
  writeFileSync(answer, 'Here:\n```json\n' + EXAMPLE + '\n```\nLeft out:\n- nothing');
  const r = cli([answer, '--out', out]);
  assert.equal(r.status, 0, r.stdout + r.stderr);
  assert.match(r.stdout, /0 errors, 0 warnings/);
  assert.equal(JSON.parse(readFileSync(out, 'utf8')).name, 'Two-Handed Fighter');
});

test('CLI: reads stdin, a build with errors exits 1 with the fix request', () => {
  const r = cli(['-'], '{"format": 1}');
  assert.equal(r.status, 1);
  assert.match(r.stdout, /^error: /m);
  assert.match(r.stdout, /Fix these problems/);
});

test('CLI: warnings exit 1 too (an agent should fix them), but --out is written', () => {
  const out = join(tmp, 'warned.json');
  const r = cli(['-', '--out', out], JSON.stringify({ format: 1, name: 'x', levels: [{ level: 1, class: 'Fightr' }] }));
  assert.equal(r.status, 1);
  assert.match(r.stdout, /^warning: .*Fightr/m);
  assert.match(r.stdout, /Fix these problems/);
  assert.equal(JSON.parse(readFileSync(out, 'utf8')).name, 'x');
});

test('CLI: --json gives a machine-readable result', () => {
  const r = cli(['-', '--json'], EXAMPLE);
  assert.equal(r.status, 0);
  const result = JSON.parse(r.stdout);
  assert.equal(result.ok, true);
  assert.equal(result.clean, true);
  assert.equal(result.build.name, 'Two-Handed Fighter');
});

test('CLI: --out is not written when the build has errors', () => {
  const out = join(tmp, 'never.json');
  cli(['-', '--out', out], '{"format":1}');
  assert.throws(() => readFileSync(out));
});

test('CLI: --out into a folder that does not exist: a message and exit 2, no stack trace', () => {
  const r = cli(['-', '--out', join(tmp, 'missing', 'x.json')], EXAMPLE);
  assert.equal(r.status, 2);
  assert.match(r.stderr, /cannot write/);
  assert.doesNotMatch(r.stderr, /at .*\(/);
});

test('CLI: the modules load as ES modules on any Node (site/package.json says so; Node 18 and 20 < 20.19 do not detect it)', () => {
  assert.equal(JSON.parse(readFileSync(path('../../site/package.json'), 'utf8')).type, 'module');
});

test('llms.txt explains every exit code of check.mjs', () => {
  const llms = readFileSync(path('../../site/llms.txt'), 'utf8');
  assert.match(llms, /Exit code 2/);
  assert.match(llms, /without a fix request/);
});

test('CLI: no arguments prints the usage and exits 2', () => {
  const r = cli([]);
  assert.equal(r.status, 2);
  assert.match(r.stderr, /usage/i);
});

test('the published prompt files are the prompts the page copies', () => {
  // Stale after a names.json or prompt change: run node tools/agent-files.mjs.
  assert.equal(readFileSync(path('../../site/prompt.txt'), 'utf8'), buildPrompt(names, vocab, 'guide') + '\n');
  assert.equal(readFileSync(path('../../site/prompt-design.txt'), 'utf8'), buildPrompt(names, vocab, 'design') + '\n');
});

test('llms.txt names every file an agent needs', () => {
  const llms = readFileSync(path('../../site/llms.txt'), 'utf8');
  for (const needed of ['prompt.txt', 'prompt-design.txt', 'check.mjs', 'Mods/WrathBuildPlanner/Builds', 'Reload folder']) {
    assert.ok(llms.includes(needed), needed);
  }
});

test('the generator reproduces the prompt files', () => {
  execFileSync(process.execPath, [path('../../tools/agent-files.mjs'), '--check']);
});
