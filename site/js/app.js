import { checkText } from './check.js';
import { indexNames } from './names-check.js';
import { fixRequest } from './fix.js';
import { buildPrompt, EXAMPLE } from './prompt.js';
import { normalize } from './match.js';

const $ = id => document.getElementById(id);
const state = { vocab: null, names: null, index: null, clean: null, value: null, issues: [], canFix: false };
const STORE_KEY = 'wbp.lastPaste';

async function loadJson(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${url}: ${response.status}`);
  return response.json();
}

async function init() {
  // A paste while the data loads must not be replaced by the stored text, and is checked once the data is in.
  $('answer').addEventListener('input', debounce(run, 300));
  const [vocab, names] = await Promise.allSettled([loadJson('data/vocabulary.json'), loadJson('data/names.json')]);
  if (vocab.status === 'fulfilled') state.vocab = vocab.value;
  else showBanner('The page data could not be loaded. Reload the page; if you opened the file directly, use the published page instead.');
  if (names.status === 'fulfilled') {
    state.names = names.value;
    state.index = indexNames(state.names);
    $('names-version').textContent = `Names from game version ${state.names.meta.gameVersion}, exported ${state.names.meta.exported}.`;
  } else {
    $('names-version').textContent = 'The name list could not be loaded: names are not checked.';
    $('prompt-note').textContent = 'The name list could not be loaded, so the prompt lists only the most common page titles.';
    $('prompt-note').hidden = false;
    $('download-names').disabled = true;
  }

  $('format-example').textContent = EXAMPLE;
  updatePrompt();
  document.querySelectorAll('input[name=variant]').forEach(r => r.addEventListener('change', updatePrompt));
  $('copy-prompt').addEventListener('click', e => copy(currentPrompt(), e.currentTarget));
  $('copy-fix').addEventListener('click', e => copy(fixRequest(state.issues), e.currentTarget));
  $('copy-json').addEventListener('click', e => copy(state.clean, e.currentTarget));
  $('download').addEventListener('click', download);
  $('download-names').addEventListener('click', downloadNames);
  $('search').addEventListener('input', debounce(() => renderSearch($('search').value), 150));
  if (!$('answer').value) {
    try { $('answer').value = localStorage.getItem(STORE_KEY) ?? ''; } catch { /* storage blocked */ }
  }
  run();
}

function variant() { return document.querySelector('input[name=variant]:checked').value; }
function currentPrompt() { return state.vocab ? buildPrompt(state.names, state.vocab, variant()) : ''; }
function updatePrompt() {
  $('prompt-preview').textContent = currentPrompt();
  $('copy-prompt').disabled = !state.vocab;
}

function run() {
  try {
    check();
  } catch (e) {
    state.clean = null;
    state.canFix = false;   // a checker bug is nothing the AI can fix
    state.issues = [{ error: true, where: '', message: `The checker failed on this text (${e.message}). Please report it with the text you pasted.` }];
    render('The build could not be checked.');
  }
}

function check() {
  const text = $('answer').value;
  try { localStorage.setItem(STORE_KEY, text); } catch { /* storage blocked */ }
  const result = checkText(text, { vocab: state.vocab, index: state.index });
  state.issues = result.issues;
  state.value = result.build;
  state.clean = result.clean;
  state.canFix = result.canFix;
  render(result.summary ?? 'Nothing pasted yet.');
}

function render(text) {
  $('summary').textContent = text;
  const list = $('issues');
  list.replaceChildren(...state.issues.map(issue => {
    const li = document.createElement('li');
    const kind = issue.error ? 'error' : issue.note ? 'note' : 'warning';
    li.className = kind;
    const tag = document.createElement('span');
    tag.className = 'tag';
    tag.textContent = kind;
    li.append(tag, issue.message);
    if (issue.where) {
      const where = document.createElement('span');
      where.className = 'where';
      where.textContent = issue.where;
      li.append(where);
    }
    return li;
  }));
  const hasErrors = state.issues.some(i => i.error);
  $('copy-fix').disabled = !state.canFix;
  $('copy-json').disabled = !state.clean || hasErrors;
  $('download').disabled = !state.clean || hasErrors;
}

async function copy(text, button) {
  if (!text) return;
  let ok = false;
  try {
    await navigator.clipboard.writeText(text);
    ok = true;
  } catch {
    const area = document.createElement('textarea');
    area.value = text;
    area.setAttribute('readonly', '');
    area.style.position = 'fixed';
    area.style.opacity = '0';
    document.body.append(area);
    area.select();
    try { ok = document.execCommand('copy'); } catch { ok = false; }
    area.remove();
  }
  flash(button, ok ? 'Copied' : button.id === 'copy-prompt' ? 'Copy failed — open "Show the prompt" and copy it by hand' : 'Copy failed — your browser blocked the clipboard');
}

function flash(button, text) {
  const original = button.dataset.label ?? button.textContent;
  button.dataset.label = original;
  button.textContent = text;
  setTimeout(() => { button.textContent = original; }, 1800);
}

function save(name, text, type) {
  const url = URL.createObjectURL(new Blob([text], { type }));
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function download() {
  const slug = (state.value?.name ?? 'build').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'build';
  save(`${slug}.json`, state.clean + '\n', 'application/json');
}

// A plain-text name list to attach to an LLM chat: one block per page, then classes and spells.
function downloadNames() {
  const { names, index } = state;
  if (!names) return;
  const lines = [`Wrath of the Righteous names (game version ${names.meta.gameVersion})`, ''];
  for (const page of [...index.pages].sort((a, b) => a.cand.display.localeCompare(b.cand.display))) {
    const items = (page.items ?? []).map(id => index.features.get(id)?.cand.display).filter(Boolean);
    const params = (page.params ?? []).map(p => p[0]);
    lines.push(`Page "${page.cand.display}": ${[...items, ...params].join(', ')}`);
  }
  lines.push('');
  for (const c of index.classes) lines.push(`Class ${c.cand.display}: archetypes ${c.archetypes.map(a => a.display).join(', ') || '—'}`);
  save('wrath-names.txt', lines.join('\n') + '\n', 'text/plain');
}

function renderSearch(query) {
  const results = $('results');
  const key = normalize(query);
  if (!state.index || key.length < 2) { results.replaceChildren(); return; }
  const hits = [];
  const hit = (title, detail) => hits.length < 60 && hits.push([title, detail]);
  const has = c => c.names.some(n => normalize(n).includes(key));
  for (const c of state.index.classes) {
    if (has(c.cand)) hit(`Class: ${c.cand.display}`, `Archetypes: ${c.archetypes.map(a => a.display).join(', ') || '—'}`);
    for (const a of c.archetypes) if (has(a)) hit(`Archetype: ${a.display}`, `Class: ${c.cand.display}`);
  }
  for (const p of state.index.pages) if (p.n.length > 1 && has(p.cand)) hit(`Page: ${p.cand.display}`, `${(p.items ?? []).length + (p.params ?? []).length} options`);
  for (const [id, f] of state.index.features) {
    if (!has(f.cand)) continue;
    const pages = [...(state.index.pagesOf.get(id) ?? [])];
    const sub = f.params ? `choices: ${f.params.slice(0, 8).map(p => p[0]).join(', ')}${f.params.length > 8 ? ', …' : ''}` : '';
    hit(f.cand.display, [pages.length ? `on pages: ${pages.join(', ')}` : 'not on a page of its own', sub].filter(Boolean).join(' — '));
  }
  for (const [, s] of state.index.spells) if (has(s)) hit(`Spell: ${s.display}`, '');
  results.replaceChildren(...hits.map(([title, detail]) => {
    const li = document.createElement('li');
    li.textContent = title;
    if (detail) {
      const small = document.createElement('small');
      small.textContent = detail;
      li.append(small);
    }
    return li;
  }));
}

function showBanner(text) {
  $('banner').textContent = text;
  $('banner').hidden = false;
}

function debounce(fn, ms) {
  let timer;
  return (...args) => { clearTimeout(timer); timer = setTimeout(() => fn(...args), ms); };
}

init();
