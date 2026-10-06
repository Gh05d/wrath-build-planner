// Finds the build JSON in an LLM reply. The mod's own reader (BuildParser) accepts the text inside a
// Markdown fence or a bare object; the page is more forgiving but hands on clean JSON only.

export function extractJson(input) {
  const result = { value: null, text: null, notes: [], error: null };
  const s = String(input ?? '').replace(/^﻿/, '').trim();
  if (s.length === 0) {
    result.error = { message: 'Paste the answer from your LLM first.' };
    return result;
  }

  let body = fromFence(s) ?? fromBraces(s);
  if (body == null) {
    result.error = { message: 'No build found: the text contains no JSON object. Paste the part of the answer in the code block.' };
    return result;
  }

  const noComments = stripComments(body);
  if (noComments !== body) result.notes.push('Removed comments from the JSON (the mod does not accept them).');
  const noCommas = stripTrailingCommas(noComments);
  if (noCommas !== noComments) result.notes.push('Removed trailing commas from the JSON.');
  body = noCommas;

  try {
    result.value = JSON.parse(body);
  } catch (e) {
    result.error = locate(e, body);
    if (/[“”„]/.test(body)) {
      result.error.message += ' The JSON contains typographic quotes (“ ”); it needs straight quotes (").';
    }
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

// Engines word JSON errors differently: V8 "at position N (line L column C)", Firefox "at line L column C".
function locate(error, body) {
  const message = 'The JSON is not valid';
  const lineCol = /line (\d+) column (\d+)/.exec(error.message);
  if (lineCol) return { message: `${message} (line ${lineCol[1]}, column ${lineCol[2]}).`, line: +lineCol[1], column: +lineCol[2] };
  const pos = /position (\d+)/.exec(error.message);
  if (pos) {
    const before = body.substring(0, +pos[1]);
    const line = before.split('\n').length;
    const column = before.length - before.lastIndexOf('\n');
    return { message: `${message} (line ${line}, column ${column}).`, line, column };
  }
  return { message: `${message}: ${error.message}` };
}
