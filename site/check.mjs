#!/usr/bin/env node
// The build page's checker for the command line, for AI agents and scripts. Needs Node 18 or newer and the files
// next to it (js/, data/), e.g. from: git clone --depth 1 https://github.com/Gh05d/wrath-build-planner
//
//   node site/check.mjs <answer.txt|build.json|-> [--out <file.json>] [--json]
//
// The input may be the AI's whole answer; the build is found in it. Exit code 0: no errors and no warnings;
// 1: errors or warnings — the fix request for the AI is printed; 2: usage or unreadable input.
// --out writes the build once there are no errors, as the page's Copy JSON does: unknown names are only warnings
// because content from other mods is not in the name list. --json prints the result as JSON instead of text.
import { readFileSync, writeFileSync } from 'node:fs';
import { checkText } from './js/check.js';
import { indexNames } from './js/names-check.js';
import { fixRequest } from './js/fix.js';

const args = process.argv.slice(2);
const option = name => {
  const at = args.indexOf(name);
  if (at < 0) return null;
  const [, value] = args.splice(at, 2);
  return value ?? '';
};
const flag = name => {
  const at = args.indexOf(name);
  if (at >= 0) args.splice(at, 1);
  return at >= 0;
};
const out = option('--out');
const asJson = flag('--json');

if (args.length !== 1 || out === '') {
  console.error('usage: node check.mjs <answer.txt|build.json|-> [--out <file.json>] [--json]');
  process.exit(2);
}

let text;
try {
  text = readFileSync(args[0] === '-' ? 0 : args[0], 'utf8');
} catch (e) {
  console.error(`cannot read ${args[0]}: ${e.message}`);
  process.exit(2);
}

const data = file => JSON.parse(readFileSync(new URL(`./data/${file}`, import.meta.url), 'utf8'));
const vocab = data('vocabulary.json');
const names = data('names.json');
const result = checkText(text, { vocab, index: indexNames(names) });
const clean = result.ok && !result.issues.some(i => !i.error && !i.note);
const fix = result.canFix ? fixRequest(result.issues) : null;

if (out && result.ok) writeFileSync(out, result.clean + '\n');

if (asJson) {
  const { summary, ok, issues, build } = result;
  console.log(JSON.stringify({ ok, clean, summary, issues: issues.map(({ error, note, where, message }) => ({ kind: error ? 'error' : note ? 'note' : 'warning', where, message })), fixRequest: fix, build }, null, 2));
} else {
  console.log(result.summary ?? 'Nothing to check: the input is empty.');
  for (const i of result.issues) console.log(`${i.error ? 'error' : i.note ? 'note' : 'warning'}: ${i.message}${i.where ? ` (${i.where})` : ''}`);
  if (fix && !clean) console.log(`\n--- fix request for the AI ---\n${fix}`);
  if (out) console.log(result.ok ? `\nWrote ${out}.` : `\nNot written: ${out} (fix the errors first).`);
}
process.exit(clean ? 0 : 1);
