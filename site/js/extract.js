// Finds the build JSON in an LLM reply. The mod's own reader (BuildParser) accepts the text inside a
// Markdown fence or a bare object; the page is more forgiving but hands on clean JSON only.
import { PROMPT_START } from './prompt.js';

export function extractJson(input) {
  const result = { value: null, text: null, notes: [], error: null };
  const s = String(input ?? '').replace(/^﻿/, '').trim();
  if (s.length === 0) {
    result.error = { message: 'Paste the answer from your LLM first.' };
    return result;
  }

  if (s.includes(PROMPT_START)) {
    result.error = { message: 'This is the prompt, not your AI’s answer. Paste the prompt into your AI, then paste what it answers here.' };
    return result;
  }

  let body = fromFence(s) ?? fromBraces(s);
  if (body == null) {
    result.error = { message: 'No build found: the text contains no JSON object. Paste the part of the answer in the code block.' };
    return result;
  }

  const noComments = stripComments(body);
  if (noComments !== body) result.notes.push('Removed comments from the JSON, so the file is clean.');
  const noCommas = stripTrailingCommas(noComments);
  if (noCommas !== noComments) result.notes.push('Removed trailing commas from the JSON, so the file is clean.');
  body = noCommas;

  try {
    result.value = JSON.parse(body);
  } catch {
    result.error = locate(body);
    return result;
  }
  if (result.value === null || typeof result.value !== 'object' || Array.isArray(result.value)) {
    result.value = null;
    result.error = { message: 'No build found: the JSON is not an object with the build’s fields.' };
    return result;
  }
  result.text = JSON.stringify(result.value, null, 2);
  return result;
}

// First ```json block; otherwise the first fenced block whose content starts with "{".
function fromFence(s) {
  const blocks = [...s.matchAll(/```([^\n`]*)\n([\s\S]*?)```/g)].map(m => ({ lang: m[1].trim().toLowerCase(), body: m[2].trim() }));
  const json = blocks.find(b => b.lang === 'json' || b.lang === 'jsonc');
  if (json) return json.body;
  const object = blocks.find(b => b.body.startsWith('{'));
  return object ? object.body : null;
}

// From the first "{" to its matching "}", skipping braces inside strings. Unclosed: to the end.
function fromBraces(s) {
  const start = s.indexOf('{');
  if (start < 0) return null;
  let depth = 0, inString = false, quote = '';
  for (let i = start; i < s.length; i++) {
    const c = s[i];
    if (inString) {
      if (c === '\\') { i++; continue; }
      if (c === quote) inString = false;
      continue;
    }
    if (c === '"') { inString = true; quote = c; continue; }
    if (c === '{') depth++;
    else if (c === '}' && --depth === 0) return s.substring(start, i + 1);
  }
  return s.substring(start);
}

// Walks the text outside of strings and drops // and /* */ comments.
function stripComments(s) {
  let out = '';
  for (let i = 0; i < s.length; i++) {
    const c = s[i];
    if (c === '"') {
      const end = stringEnd(s, i);
      out += s.substring(i, end);
      i = end - 1;
      continue;
    }
    if (c === '/' && s[i + 1] === '/') {
      while (i < s.length && s[i] !== '\n') i++;
      out += '\n';
      continue;
    }
    if (c === '/' && s[i + 1] === '*') {
      const close = s.indexOf('*/', i + 2);
      i = close < 0 ? s.length : close + 1;
      continue;
    }
    out += c;
  }
  return out;
}

// Drops a comma that is followed (after whitespace) by } or ], outside of strings.
function stripTrailingCommas(s) {
  let out = '';
  for (let i = 0; i < s.length; i++) {
    const c = s[i];
    if (c === '"') {
      const end = stringEnd(s, i);
      out += s.substring(i, end);
      i = end - 1;
      continue;
    }
    if (c === ',') {
      let j = i + 1;
      while (j < s.length && /\s/.test(s[j])) j++;
      if (s[j] === '}' || s[j] === ']') continue;
    }
    out += c;
  }
  return out;
}

// Index just past the closing quote of the string starting at i (or the end of the text).
function stringEnd(s, i) {
  for (let j = i + 1; j < s.length; j++) {
    if (s[j] === '\\') { j++; continue; }
    if (s[j] === '"') return j + 1;
  }
  return s.length;
}

// Where the JSON breaks, from our own scanner: browsers word JSON.parse errors differently and Safari gives no
// position at all. Lines and columns count within the extracted JSON block, not the whole pasted text.
function locate(body) {
  const at = firstErrorAt(body);
  let message = 'The JSON is not valid';
  let line, column;
  if (at >= 0) {
    const before = body.substring(0, at);
    line = before.split('\n').length;
    column = before.length - before.lastIndexOf('\n');
    message += ` (line ${line} of the JSON, column ${column})`;
  }
  message += '.';
  if (/[“”„]/.test(body)) message += ' It uses typographic quotes (“ ”) where JSON needs straight quotes (").';
  return { message, line, column };
}

// Index of the first character a strict JSON reader rejects, or -1 if the text is valid JSON.
function firstErrorAt(s) {
  let i = 0;
  const ws = () => { while (i < s.length && ' \t\n\r'.includes(s[i])) i++; };
  const fail = () => { throw new RangeError(String(i)); };
  const string = () => {
    i++;
    while (i < s.length && s[i] !== '"') {
      if (s[i] < ' ') fail();
      i += s[i] === '\\' ? 2 : 1;
    }
    if (i >= s.length) fail();
    i++;
  };
  const value = () => {
    ws();
    if (s[i] === '{') {
      i++; ws();
      if (s[i] === '}') { i++; return; }
      for (;;) {
        ws();
        if (s[i] !== '"') fail();
        string(); ws();
        if (s[i] !== ':') fail();
        i++; value(); ws();
        if (s[i] === ',') { i++; continue; }
        if (s[i] === '}') { i++; return; }
        fail();
      }
    }
    if (s[i] === '[') {
      i++; ws();
      if (s[i] === ']') { i++; return; }
      for (;;) {
        value(); ws();
        if (s[i] === ',') { i++; continue; }
        if (s[i] === ']') { i++; return; }
        fail();
      }
    }
    if (s[i] === '"') { string(); return; }
    const literal = /^(-?(0|[1-9]\d*)(\.\d+)?([eE][+-]?\d+)?|true|false|null)/.exec(s.substring(i, i + 40));
    if (!literal) fail();
    i += literal[0].length;
  };
  try {
    value(); ws();
    if (i < s.length) fail();
    return -1;
  } catch (e) {
    if (e instanceof RangeError) return Math.min(Number(e.message), s.length);
    throw e;
  }
}
