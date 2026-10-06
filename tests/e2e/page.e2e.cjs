// Browser test of the build page as a careless player uses it: real clicks, real clipboard, odd input.
//   NODE_PATH=~/.local/share/pnpm/global/5/.pnpm/playwright@1.51.0/node_modules node tests/e2e/page.e2e.cjs
// Serves site/ itself on a free port and drives the installed Chrome (Playwright channel "chrome").
const { chromium } = require('playwright');
const http = require('http');
const fs = require('fs');
const path = require('path');

const SITE = path.join(__dirname, '..', '..', 'site');
const TYPES = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json' };
let failed = 0;
const check = (ok, what, detail = '') => {
  console.log(`${ok ? 'PASS' : 'FAIL'} ${what}${ok || !detail ? '' : ` — ${detail}`}`);
  if (!ok) failed++;
};

function serve() {
  const server = http.createServer((req, res) => {
    const file = path.join(SITE, decodeURIComponent(req.url.split('?')[0]).replace(/\/$/, '/index.html'));
    if (!file.startsWith(SITE) || !fs.existsSync(file)) { res.writeHead(404); res.end(); return; }
    res.writeHead(200, { 'content-type': TYPES[path.extname(file)] ?? 'application/octet-stream' });
    fs.createReadStream(file).pipe(res);
  });
  return new Promise(resolve => server.listen(0, '127.0.0.1', () => resolve(server)));
}

const EXAMPLE = fs.readFileSync(path.join(__dirname, '..', '..', 'Builds-examples', 'two-handed-fighter.json'), 'utf8');

(async () => {
  const server = await serve();
  const url = `http://127.0.0.1:${server.address().port}/`;
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const context = await browser.newContext({ acceptDownloads: true });
  await context.grantPermissions(['clipboard-read', 'clipboard-write'], { origin: url });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('response', r => { if (r.status() >= 400) errors.push(`${r.status()} ${r.url()}`); });

  const paste = async text => {
    await page.fill('#answer', text);
    await page.waitForTimeout(450);   // debounce 300 ms
  };
  const summary = () => page.textContent('#summary');
  const issues = () => page.$$eval('#issues li', els => els.map(e => e.textContent));
  const disabled = sel => page.$eval(sel, el => el.disabled);
  const clipboard = () => page.evaluate(() => navigator.clipboard.readText());

  await page.goto(url);
  await page.waitForFunction(() => !document.getElementById('copy-prompt').disabled, null, { timeout: 15000 });
  check(true, 'page loads and enables Copy prompt');

  // A visitor who never heard of the mod learns what it is and where to get it.
  const intro = await page.textContent('#about');
  check(/mod for Pathfinder: Wrath of the Righteous/.test(intro) && /Unity Mod Manager/.test(intro), 'intro says what the mod is and what it needs');
  check(await page.$eval('#get-mod', a => /github\.com\/Gh05d\/wrath-build-planner\/releases|nexusmods\.com/.test(a.href)), 'a link to get the mod');
  check(await page.$eval('#about img', img => img.complete && img.naturalWidth > 0), 'the in-game screenshot loads');

  // An AI agent that reads the page finds its way to the plain files.
  check(await page.$eval('#for-agents a', a => a.getAttribute('href')) === 'llms.txt', 'the page points agents to llms.txt');
  for (const file of ['llms.txt', 'prompt.txt', 'prompt-design.txt', 'check.mjs']) {
    const response = await page.request.get(url + file);
    check(response.ok() && (await response.text()).length > 500, `${file} is served`);
  }

  // Step 1: prompt and variants.
  await page.click('#copy-prompt');
  const prompt = await clipboard();
  check(prompt.includes('[paste the guide here]') && prompt.includes('Bonus Combat Feat'), 'Copy prompt puts the guide prompt on the clipboard');
  await page.click('input[value=design]');
  await page.click('#copy-prompt');
  check((await clipboard()).includes('Design a build'), 'design variant changes the copied prompt');
  await page.click('input[value=guide]');

  // Nothing, whitespace, junk.
  await paste('');
  check((await summary()).includes('Nothing pasted'), 'empty answer: nothing pasted');
  await paste('   \n\t  ');
  check((await summary()).includes('Nothing pasted'), 'whitespace: nothing pasted');
  await paste('lol idk just make me op');
  check((await issues()).some(t => /No build found/.test(t)) && await disabled('#copy-json') && await disabled('#copy-fix'),
    'junk text: no build found, Copy JSON and fix request disabled');
  await paste('🔥🔥🔥 {{{{ ]]] "');
  check((await issues()).length > 0 && await disabled('#copy-json'), 'emoji and brackets: an error, no crash');

  // The prompt pasted back instead of the answer.
  await paste(prompt);
  check((await issues()).some(t => /This is the prompt/.test(t)) && await disabled('#copy-fix'), 'pasted prompt is recognised');

  // A good answer with prose and a fenced "Left out" block.
  await paste(`Sure! Here it is:\n\`\`\`json\n${EXAMPLE}\n\`\`\`\n\`\`\`\nLeft out:\n- nothing\n\`\`\``);
  check(/0 errors, 0 warnings/.test(await summary()), 'example answer: 0 errors, 0 warnings', await summary());
  check(!(await disabled('#copy-json')), 'Copy JSON enabled for a clean build');
  check(await page.$eval('#notes-box', el => el.hidden) && (await issues()).length === 0, 'a clean build shows no problem list and no notes box');

  await page.click('#copy-json');
  const copied = await clipboard();
  let parsed = null;
  try { parsed = JSON.parse(copied); } catch { /* checked below */ }
  check(parsed?.name === 'Two-Handed Fighter' && !copied.includes('Left out'), 'Copy JSON gives the clean build only');
  const [download] = await Promise.all([page.waitForEvent('download'), page.click('#download')]);
  check(download.suggestedFilename() === 'two-handed-fighter.json', 'download name from the build name', download.suggestedFilename());

  // Only notes left (a weapon the guide does not name): no problem list, the notes apart and marked as nothing to fix.
  await paste(JSON.stringify({ format: 1, name: 'Notes only', levels: [{ level: 1, class: 'Fighter', picks: [{ in: 'Feat', pick: 'Weapon Focus' }] }] }));
  check(/ready for the game\. 1 note below, nothing to fix/.test(await summary()), 'only notes: the summary says ready', await summary());
  check((await issues()).length === 0, 'only notes: nothing in the problem list');
  check(!(await page.$eval('#notes-box', el => el.hidden)) && /nothing to fix/i.test(await page.textContent('#notes-box'))
    && (await page.$$('#notes li')).length === 1, 'only notes: one note in its own box, headed as nothing to fix');
  check(await disabled('#copy-fix') && !(await disabled('#copy-json')), 'only notes: no fix request, Copy JSON enabled');

  // A name that tries to be HTML, and a file name that tries to escape.
  await paste(JSON.stringify({ format: 1, name: '<img src=x onerror="window.__xss=1">../../evil 🐉', levels: [{ level: 1, class: '<b>Fighter</b>' }] }));
  await page.waitForTimeout(300);
  check(await page.evaluate(() => window.__xss === undefined), 'names are shown as text, never run as HTML');
  check((await page.$$eval('#issues li b, #issues li img', els => els.length)) === 0, 'no markup from the build in the issue list');
  const [evil] = await Promise.all([page.waitForEvent('download'), page.click('#download')]);
  check(/^[a-z0-9-]+\.json$/.test(evil.suggestedFilename()) && !evil.suggestedFilename().includes('..'), 'download name is sanitised', evil.suggestedFilename());

  // Broken JSON: typographic quotes, location, fix request.
  await paste('{\n  "format": 1,\n  "name": “Tank”\n}');
  const typo = (await issues()).join(' ');
  check(/line 3 of the JSON/.test(typo) && /straight quotes/.test(typo), 'typographic quotes: line and hint', typo);
  check(!(await disabled('#copy-fix')), 'fix request available for broken JSON');
  await page.click('#copy-fix');
  check(/^Fix these problems/.test(await clipboard()), 'fix request copied');

  // Wrong names, unknown fields, absurd numbers.
  await paste(JSON.stringify({ format: 1, name: 'x', feats: ['Dodge'], levels: [{ level: 1, class: 'Fighter' }] }));
  check((await issues()).some(t => /'feats' is not allowed here: "picks" goes inside each entry of "levels"/.test(t)), 'a field on the wrong level says where it belongs');
  await paste(JSON.stringify({ format: 1, name: 'x', start: { abilityScores: { Strength: 99 } }, levels: [{ level: -3, class: 'Fightr' }, { level: 1e9, class: null }] }));
  check((await issues()).length >= 3 && await disabled('#copy-json'), 'absurd numbers and nulls: errors, no crash');
  await paste('[1, 2, 3]');
  check((await issues()).some(t => /No build found/.test(t)), 'a list is not a build');

  // Size: a huge paste must not freeze the page.
  const big = JSON.stringify({ format: 1, name: 'big', levels: Array.from({ length: 20 }, (_, i) => ({ level: i + 1, class: 'Fighter', picks: Array.from({ length: 40 }, (_, j) => `Feat number ${j} that does not exist`) })) });
  const started = Date.now();
  await paste(big + ' '.repeat(2_000_000));
  check(Date.now() - started < 8000 && (await summary()).length > 0, `2 MB paste with 800 unknown picks answers (${Date.now() - started} ms)`);

  // Fast typing: only the last state counts.
  await page.fill('#answer', '');
  await page.type('#answer', '{"format":1,"name":"typed","levels":[{"level":1,"class":"Fighter"}]}', { delay: 5 });
  await page.waitForTimeout(500);
  check(/typed: Levels 1–1/.test(await summary()), 'typing character by character ends in the right result', await summary());

  // Name browser with odd queries.
  await page.click('summary:has-text("Name browser")');
  for (const q of ['(', '*', '\\', '[', 'a', '', 'zzzzqqq', 'weapon focus']) {
    await page.fill('#search', q);
    await page.waitForTimeout(250);
  }
  check((await page.$$('#results li')).length > 0, 'name search survives odd queries and finds "weapon focus"');
  const [names] = await Promise.all([page.waitForEvent('download'), page.click('#download-names')]);
  check(names.suggestedFilename() === 'wrath-names.txt', 'name list downloads');

  // Reload keeps the last paste.
  await paste(`\`\`\`json\n${EXAMPLE}\n\`\`\``);
  await page.reload();
  await page.waitForFunction(() => !document.getElementById('copy-prompt').disabled);
  await page.waitForTimeout(500);
  check(/Two-Handed Fighter/.test(await summary()), 'reload restores the last paste');

  // Phone width: nothing wider than the screen.
  await page.setViewportSize({ width: 360, height: 800 });
  await page.waitForTimeout(300);
  check(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), 'no horizontal scrolling at 360 px');

  check(errors.length === 0, 'no script errors in the console', errors.join(' | '));

  // Browsers that block clipboard and storage.
  const strict = await browser.newContext();
  const locked = await strict.newPage();
  await locked.addInitScript(() => {
    Storage.prototype.setItem = () => { throw new Error('blocked'); };
    Storage.prototype.getItem = () => { throw new Error('blocked'); };
    Object.defineProperty(navigator, 'clipboard', { value: { writeText: () => Promise.reject(new Error('denied')) } });
    document.execCommand = () => false;
  });
  const lockedErrors = [];
  locked.on('pageerror', e => lockedErrors.push(e.message));
  await locked.goto(url);
  await locked.waitForFunction(() => !document.getElementById('copy-prompt').disabled, null, { timeout: 15000 });
  await locked.click('#copy-prompt');
  check(/Copy failed/.test(await locked.textContent('#copy-prompt')), 'blocked clipboard: the button says so');
  await locked.fill('#answer', EXAMPLE);
  await locked.waitForTimeout(450);
  check(/0 errors/.test(await locked.textContent('#summary')) && lockedErrors.length === 0, 'blocked storage: the checker still works', lockedErrors.join(' | '));
  await strict.close();

  await browser.close();
  server.close();
  console.log(failed ? `${failed} FAILED` : 'ALL PASSED');
  process.exit(failed ? 1 : 0);
})().catch(e => { console.error('CRASH', e); process.exit(2); });
