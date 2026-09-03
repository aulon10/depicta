const { chromium } = require('/home/claude/.npm-global/lib/node_modules/playwright');
const OUT = '/home/claude/shots';
(async () => {
  const b = await chromium.launch();
  const errs = [];
  const p = await b.newPage({ viewport: { width: 1380, height: 900 }, deviceScaleFactor: 2 });
  p.on('pageerror', e => errs.push('PAGEERR: ' + e.message));
  await p.goto('http://localhost:4173/', { waitUntil: 'networkidle' });
  await p.evaluate(async () => { try { await document.fonts.ready; } catch {} });
  await p.waitForTimeout(400);
  await p.screenshot({ path: OUT + '/auth-1-login.png' });

  // Wrong credentials -> error state
  await p.getByLabel('Username').fill('admin');
  await p.getByLabel('Password').fill('wrong-pass');
  await p.getByRole('button', { name: /Sign in/ }).click();
  await p.waitForTimeout(700);
  await p.screenshot({ path: OUT + '/auth-2-badcreds.png' });

  // Correct credentials -> dashboard
  await p.getByLabel('Password').fill('depicta-dev-2026');
  await p.getByRole('button', { name: /Sign in/ }).click();
  await p.waitForTimeout(1200);
  await p.screenshot({ path: OUT + '/auth-3-dashboard.png' });

  await p.close();

  // Mobile login
  const m = await b.newPage({ viewport: { width: 402, height: 860 }, deviceScaleFactor: 2 });
  await m.goto('http://localhost:4173/', { waitUntil: 'networkidle' });
  await m.waitForTimeout(400);
  await m.screenshot({ path: OUT + '/auth-4-mobile-login.png' });
  await b.close();
  console.log('ERRORS:', errs.length ? JSON.stringify(errs) : 'none');
})();
