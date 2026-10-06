// node tools/fetch-guide.cjs <url> <out.txt> [headed] — renders a guide page in the installed Chrome (Playwright) and saves
// its visible text, table cells joined with " | ". For acceptance inputs: Fextralife and Steam refuse plain curl.
// Needs NODE_PATH to a Playwright install, e.g. NODE_PATH=~/.local/share/pnpm/global/5/.pnpm/playwright@1.51.0/node_modules.
const { chromium } = require('playwright');
(async () => {
  const [url, out, mode] = process.argv.slice(2);
  const browser = await chromium.launch({ channel: 'chrome', headless: mode !== 'headed' });
  const page = await browser.newPage({ userAgent: 'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0 Safari/537.36' });
  await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 60000 });
  // Cloudflare or similar: wait until the real page is there (a person may solve a check in headed mode).
  for (let i = 0; i < (mode === 'headed' ? 120 : 15); i++) {
    const title = await page.title();
    if (!/just a moment|attention required|checking/i.test(title)) break;
    await page.waitForTimeout(1000);
  }
  await page.waitForTimeout(2000);
  const text = await page.evaluate(() => {
    document.querySelectorAll('td, th').forEach(c => c.append(' | '));
    return document.body.innerText;
  });
  require('fs').writeFileSync(out, text);
  console.log(await page.title(), text.length);
  await browser.close();
})().catch(e => { console.error('FAILED', e.message.split('\n')[0]); process.exit(1); });
