#!/usr/bin/env node
// Writes the prompts the build page copies as plain files (site/prompt.txt, site/prompt-design.txt), so an AI agent
// that fetches pages without running scripts can read them. Run after names.json, vocabulary.json or prompt.js change.
//   node tools/agent-files.mjs           write the files
//   node tools/agent-files.mjs --check   exit 1 if they are stale (the site tests do this)
import { readFileSync, writeFileSync } from 'node:fs';
import { buildPrompt } from '../site/js/prompt.js';

const site = rel => new URL(`../site/${rel}`, import.meta.url);
const data = file => JSON.parse(readFileSync(site(`data/${file}`), 'utf8'));
const names = data('names.json');
const vocab = data('vocabulary.json');
const files = {
  'prompt.txt': buildPrompt(names, vocab, 'guide') + '\n',
  'prompt-design.txt': buildPrompt(names, vocab, 'design') + '\n',
};

const check = process.argv.includes('--check');
let stale = 0;
for (const [file, text] of Object.entries(files)) {
  let current = null;
  try { current = readFileSync(site(file), 'utf8'); } catch { /* missing */ }
  if (current === text) continue;
  if (check) { console.error(`stale: site/${file} — run node tools/agent-files.mjs`); stale++; }
  else { writeFileSync(site(file), text); console.log(`wrote site/${file}`); }
}
process.exit(stale ? 1 : 0);
