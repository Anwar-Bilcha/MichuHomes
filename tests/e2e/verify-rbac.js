// Browser journey for Foundation 1: permission-based access, company approval, password recovery.
// Runs against a running app on a TEST database (never production). See tests/README.md.
// usage: npm install && node verify-rbac.js
const puppeteer = require('puppeteer-core');
const fs = require('fs');
const path = require('path');
const base = process.env.E2E_BASE_URL || 'http://localhost:5310';
const PW = process.env.E2E_PASSWORD; // password of the seeded test accounts (never committed)
if (!PW) { console.log("Set E2E_PASSWORD to the test accounts password."); process.exit(2); }
const MAIL = process.env.E2E_MAIL_DIR || path.resolve(__dirname, '../../RadiantHomes/App_Data/mail');
const ts = Date.now().toString().slice(-7);
const PLATFORM_SUPER = '6f1c2b8e-3d4a-4f55-9a61-0c7e2d9b1a01';
let AMAL;
let AYAT;
let DDD;
let failures = 0;
const ok = (label, cond, extra = '') => { console.log(`${cond ? 'PASS' : 'FAIL'}  ${label}${extra ? '  — ' + extra : ''}`); if (!cond) failures++; };

async function session(browser) {
  const ctx = browser.createBrowserContext ? await browser.createBrowserContext() : await browser.createIncognitoBrowserContext();
  const page = await ctx.newPage();
  page.setDefaultNavigationTimeout(90000);
  await page.setViewport({ width: 1366, height: 900 });
  page.errors = [];
  page.on('pageerror', e => page.errors.push(e.message));
  return { ctx, page };
}
async function login(page, email, password = PW) {
  await page.goto(base + '/Identity/Account/Login', { waitUntil: 'networkidle2' });
  await page.type('#account input[name="Input.Email"]', email);
  await page.type('#account input[name="Input.Password"]', password);
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle2' }), page.click('#login-submit')]);
  return new URL(page.url()).pathname;
}
async function open(page, url) {
  const r = await page.goto(base + url, { waitUntil: 'networkidle2' });
  return { status: r.status(), path: new URL(page.url()).pathname, html: await page.content() };
}
const denied = r => r.path === '/AccessDenied' || r.status === 403;
const toast = async page => (await page.content());
async function submit(page, selector) {
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle2' }), page.$eval(selector, el => el.click())]);
  return page.content();
}
// Forms with data-confirm open a dialog; post them directly instead.
async function postForm(page, formSelector, extra = {}) {
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle2' }), page.evaluate((sel, extra) => {
    const f = document.querySelector(sel);
    for (const [k, v] of Object.entries(extra)) { const i = document.createElement('input'); i.type = 'hidden'; i.name = k; i.value = v; f.appendChild(i); }
    HTMLFormElement.prototype.submit.call(f);
  }, formSelector, extra)]);
  return page.content();
}

(async () => {
  // wait for the app (it may still be building / starting)
  for (let i = 0; i < 90; i++) {
    try { const r = await fetch(base + '/'); if (r.ok) break; } catch { }
    await new Promise(res => setTimeout(res, 2000));
  }
  const browser = await puppeteer.launch({ executablePath: process.env.BROWSER_PATH || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: true });
  try {
    // ---------- Platform super admin ----------
    const A = await session(browser);
    ok('platform admin: sign-in', (await login(A.page, 'admin@michuhomes.local')) !== '/Identity/Account/Login');
    let r = await open(A.page, '/Admin/Dashboard/AdminDashboard');
    ok('platform admin: dashboard opens', r.status === 200 && r.path === '/Admin/Dashboard/AdminDashboard');
    for (const link of ['/Admin/Companies', '/Staff/Users', '/Staff/Roles', '/Staff/Audit']) ok(`platform admin: sidebar link ${link}`, r.html.includes(`href="${link}`));

    r = await open(A.page, '/Admin/Companies');
    const pendingIds = [...new Set([...r.html.matchAll(/Companies\/Details\?id=([0-9a-f-]{36})/g)].map(m => m[1]))];
    ok('company approvals: pending companies listed', r.status === 200 && pendingIds.length >= 3, pendingIds.length + ' pending');
    [AMAL, AYAT, DDD] = pendingIds; // decision flow, pending-admin flow, stays pending (public check)

    r = await open(A.page, `/Admin/Companies/Details?id=${AMAL}`);
    ok('company review page opens', r.status === 200 && r.html.includes('Ask for more information'));
    let html = await submit(A.page, 'button[formaction*="handler=RequestInfo"]');
    ok('ask for information without a reason is refused', html.includes('Please give a reason'));
    await A.page.type('#reason', 'Please upload your trade licence.');
    html = await submit(A.page, 'button[formaction*="handler=RequestInfo"]');
    ok('ask for information with a reason', html.includes('was asked for more information') && html.includes('More information requested'));
    html = await submit(A.page, 'button[formaction*="handler=Approve"]');
    ok('approve after information request', html.includes('was approved') && html.includes('Please upload your trade licence.'));

    // Narrow platform role + user
    await open(A.page, '/Staff/Roles/Create');
    await A.page.type('#Input_Name', `Support officer ${ts}`);
    await A.page.click('#perm-companies-view'); await A.page.click('#perm-notifications-view');
    html = await submit(A.page, 'button[type="submit"].btn-primary');
    const supportRoleId = new URL(A.page.url()).searchParams.get('id');
    ok('platform role created', html.includes('was created') && !!supportRoleId);

    await open(A.page, '/Staff/Users/Create');
    await A.page.type('#Input_FullName', `Support ${ts}`);
    await A.page.type('#Input_Email', `support.${ts}@michuhomes.local`);
    await A.page.click(`#role-${supportRoleId}`);
    await A.page.click('#setup-password');
    await A.page.type('#Input_Password', 'Sup@1234x');
    html = await submit(A.page, 'form[method="post"] button[type="submit"].btn-primary');
    const supportUserId = new URL(A.page.url()).searchParams.get('id');
    ok('platform user created', html.includes('account was created') && !!supportUserId);
    ok('effective permissions shown (exactly the role\'s two)', html.includes('View companies') && html.includes('View notifications') && !html.includes('View platform dashboard'));

    // ---------- Support user: only what the role grants ----------
    const S = await session(browser);
    await login(S.page, `support.${ts}@michuhomes.local`, 'Sup@1234x');
    r = await open(S.page, '/Admin/Companies');
    ok('support: company approvals open (companies.view)', r.status === 200 && r.path === '/Admin/Companies');
    ok('support: no decision buttons without companies.approve', !(await open(S.page, `/Admin/Companies/Details?id=${AYAT}`)).html.includes('handler=Approve'));
    ok('support: dashboard refused', denied(await open(S.page, '/Admin/Dashboard/AdminDashboard')));
    ok('support: users refused', denied(await open(S.page, '/Staff/Users')));
    ok('support: roles refused', denied(await open(S.page, '/Staff/Roles')));
    ok('support: company area pages refused', denied(await open(S.page, '/Projects/Create')));
    const forged = await S.page.evaluate(async (id) => {
      const res = await fetch(`/Admin/Companies/Details?id=${id}&handler=Approve`, { method: 'POST', body: new URLSearchParams({ Reason: 'x', __RequestVerificationToken: document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' }) });
      return res.url;
    }, AYAT);
    r = await open(S.page, `/Admin/Companies/Details?id=${AYAT}`);
    ok('support: forged approve POST refused', r.html.includes('Waiting for approval'));

    // Revoke a permission → effective at once
    await open(A.page, `/Staff/Roles/Edit?id=${supportRoleId}`);
    await A.page.click('#perm-companies-view');
    html = await submit(A.page, 'form[method="post"] button[type="submit"].btn-primary');
    ok('role edited (permission removed)', html.includes('was saved'));
    ok('support: revoked permission takes effect on the next request', denied(await open(S.page, '/Admin/Companies')));

    // Deactivate → no access, can't sign in
    await open(A.page, `/Staff/Users/Edit?id=${supportUserId}`);
    html = await postForm(A.page, 'form[action*="handler=Deactivate"]');
    ok('user deactivated', html.includes('was deactivated'));
    ok('support: deactivated account loses access at once', denied(await open(S.page, '/Admin/Notifications/NotificationsAll')));
    const S2 = await session(browser);
    ok('support: deactivated account cannot sign in', (await login(S2.page, `support.${ts}@michuhomes.local`, 'Sup@1234x')) === '/Identity/Account/Login' || S2.page.url().includes('Lockout'));
    await S2.ctx.close();

    // Platform admin can't deactivate or re-role themselves
    const me = await A.page.evaluate(async () => (await fetch('/Staff/Users?q=admin%40michuhomes.local')).text());
    const myId = (me.match(/Edit\?id=([0-9a-f-]{36})/) || [])[1];
    r = await open(A.page, `/Staff/Users/Edit?id=${myId}`);
    ok('own account: roles are locked', r.html.includes("You can't change your own roles"));

    // ---------- Company super admin (Ephison, approved) ----------
    const C = await session(browser);
    await login(C.page, 'r3.realestate@michuhomes.local');
    r = await open(C.page, '/HousingCompanyAdmin');
    ok('company admin: company dashboard opens', r.status === 200 && r.path.startsWith('/HousingCompanyAdmin'));
    ok('company admin: users & access in the sidebar', r.html.includes('href="/Staff/Users"') && r.html.includes('href="/Staff/Roles"'));
    ok('company admin: no platform menu', !r.html.includes('href="/Admin/Companies"'));
    ok('company admin: platform dashboard refused', denied(await open(C.page, '/Admin/Dashboard/AdminDashboard')));
    ok('company admin: company approvals refused', denied(await open(C.page, '/Admin/Companies')));

    await open(C.page, '/Staff/Roles/Create');
    const companyPicker = await C.page.content();
    ok('company role picker has no platform permissions', !companyPicker.includes('perm-companies-approve') && companyPicker.includes('perm-reservations-manage'));
    await C.page.type('#Input_Name', `Sales agent ${ts}`);
    await C.page.click('#perm-reservations-view'); await C.page.click('#perm-reservations-manage');
    html = await submit(C.page, 'button[type="submit"].btn-primary');
    const agentRoleId = new URL(C.page.url()).searchParams.get('id');
    ok('company role created', html.includes('was created') && !!agentRoleId);

    await open(C.page, '/Staff/Roles/Create');
    const forgedRole = await C.page.evaluate(async (ts) => {
      const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
      const body = new URLSearchParams({ 'Input.Name': 'Escalation ' + ts, 'Input.IsActive': 'true', 'Input.Permissions': 'companies.approve', __RequestVerificationToken: token });
      return (await fetch('/Staff/Roles/Create', { method: 'POST', body })).text();
    }, ts);
    ok('company admin: forged platform permission refused', forgedRole.includes("can&#x27;t be part of a company role") || forgedRole.includes("can't be part of a company role"));

    await open(C.page, '/Staff/Users/Create');
    await C.page.type('#Input_FullName', `Agent ${ts}`);
    await C.page.type('#Input_Email', `agent.${ts}@michuhomes.local`);
    await C.page.click(`#role-${agentRoleId}`);
    await C.page.click('#setup-password');
    await C.page.type('#Input_Password', 'Agent@1234x');
    html = await submit(C.page, 'form[method="post"] button[type="submit"].btn-primary');
    const agentUserId = new URL(C.page.url()).searchParams.get('id');
    ok('company user created', html.includes('account was created') && !!agentUserId);

    ok('company admin: platform user not visible (404)', (await open(C.page, `/Staff/Users/Edit?id=${supportUserId}`)).status === 404);
    ok('company admin: platform role not visible (404)', (await open(C.page, `/Staff/Roles/Edit?id=${PLATFORM_SUPER}`)).status === 404);
    r = await open(C.page, '/Staff/Users');
    ok('company admin: user list only has own company staff', r.html.includes(`agent.${ts}@michuhomes.local`) && !r.html.includes('admin@michuhomes.local'));

    // ---------- Company agent: only reservations ----------
    const G = await session(browser);
    await login(G.page, `agent.${ts}@michuhomes.local`, 'Agent@1234x');
    r = await open(G.page, '/HousingCompanyAdmin/Reservations');
    ok('agent: reservations open', r.status === 200 && r.path.startsWith('/HousingCompanyAdmin/Reservations'));
    ok('agent: project creation refused', denied(await open(G.page, '/Projects/Create')));
    ok('agent: users refused', denied(await open(G.page, '/Staff/Users')));
    r = await open(G.page, '/HousingCompanyAdmin');
    ok('agent: sidebar shows reservations, not projects', r.html.includes('/HousingCompanyAdmin/Reservations') && !r.html.includes('href="/Projects/Create"'));

    // ---------- New company: starts Pending, locked until approved ----------
    const coName = 'RBAC Test Homes ' + ts;
    await open(A.page, '/RealEstates/Create');
    await A.page.type('#HousingCompany_Name', coName);
    await A.page.type('#HousingCompany_LicenseNumber', 'LIC-' + ts);
    await A.page.$eval('#HousingCompany_EstablishedDate', e => { e.value = '2020-01-15'; });
    await A.page.$eval('#HousingCompany_AddressId', e => { const o = [...e.options].find(x => x.value); if (o) e.value = o.value; });
    await A.page.type('#HousingCompany_Email', 'info.' + ts + '@rbac-test.example');
    await A.page.type('#HousingCompany_PhoneNumber', '+251911000000');
    await A.page.type('#HousingCompany_Website', 'https://rbac-test.example');
    await Promise.all([A.page.waitForNavigation({ waitUntil: 'networkidle2' }), A.page.$eval('#HousingCompany_Name', e => e.form.querySelector('button[type=submit]').click())]);
    r = await open(A.page, '/Admin/Companies?status=Pending&q=' + encodeURIComponent(coName));
    const NEWCO = (r.html.match(/Companies\/Details\?id=([0-9a-f-]{36})/) || [])[1];
    ok('new company created and waiting for approval', !!NEWCO);
    r = await open(A.page, '/Admin/Companies/Details?id=' + NEWCO);
    ok('new company history: registered, waiting for approval', r.html.includes('Company registered; waiting for approval.'));

    const V0 = await session(browser);
    ok('public: pending company profile hidden', (await open(V0.page, '/RealEstates/Details?id=' + NEWCO)).status === 404);
    ok('public: pending company not in the list', !(await open(V0.page, '/RealEstates')).html.includes(coName));
    await V0.ctx.close();

    await open(A.page, '/Staff/Users/Create?company=' + NEWCO);
    const coHtml = await A.page.content();
    const superRole = (coHtml.match(/value="([0-9a-f-]{36})" id="role-[0-9a-f-]{36}"/) || [])[1];
    ok('new company already has its Company Super Admin role', coHtml.includes('Company Super Admin') && !!superRole);
    await A.page.type('#Input_FullName', 'First admin ' + ts);
    await A.page.type('#Input_Email', 'first.' + ts + '@michuhomes.local');
    if (superRole) await A.page.click('#role-' + superRole);
    await A.page.click('#setup-password');
    await A.page.type('#Input_Password', 'First@1234x');
    html = await submit(A.page, 'form[method="post"] button[type="submit"].btn-primary');
    ok('platform admin: first administrator of the pending company created', html.includes('account was created'));
    const P = await session(browser);
    await login(P.page, 'first.' + ts + '@michuhomes.local', 'First@1234x');
    r = await open(P.page, '/HousingCompanyAdmin/Status');
    ok('pending company: status page explains the review', r.status === 200 && r.html.includes('Michu Homes is reviewing your company'));
    ok('pending company: approval banner on company pages', r.html.includes('Company administration opens once Michu Homes approves your company'));
    ok('pending company: project creation refused', denied(await open(P.page, '/Projects/Create')));
    ok('pending company: users refused until approved', denied(await open(P.page, '/Staff/Users')));
    ok('pending company: profile still editable', (await open(P.page, '/RealEstates/Edit?id=' + NEWCO)).status === 200);
    await open(A.page, '/Admin/Companies/Details?id=' + NEWCO);
    html = await submit(A.page, 'button[formaction*="handler=Approve"]');
    ok('platform admin approves the company', html.includes('was approved'));
    r = await open(P.page, '/Projects/Create');
    ok('approved company: project creation opens', r.status === 200 && r.path === '/Projects/Create');
    ok('approved company: users & roles open', (await open(P.page, '/Staff/Users')).status === 200);
    r = await open(P.page, '/Admin/Notifications/NotificationsAll');
    ok('approved company: notified of the approval', r.html.includes('Company approved'));

    // ---------- Customer and public ----------
    const U = await session(browser);
    await login(U.page, 'r1.owner@michuhomes.local');
    ok('customer: users refused', denied(await open(U.page, '/Staff/Users')));
    ok('customer: company approvals refused', denied(await open(U.page, '/Admin/Companies')));
    ok('customer: portal works', (await open(U.page, '/Portal')).path === '/Portal');
    const V = await session(browser);
    r = await open(V.page, '/RealEstates');
    ok('public: approved companies listed', r.html.includes('Ephison') && r.html.includes(coName));

    // ---------- Password recovery (pickup folder) ----------
    const before = fs.existsSync(MAIL) ? new Set(fs.readdirSync(MAIL)) : new Set();
    await open(V.page, '/Identity/Account/ForgotPassword');
    await V.page.type('#Input_Email', `agent.${ts}@michuhomes.local`);
    html = await submit(V.page, 'button[type="submit"]');
    ok('forgot password: confirmation page', html.includes('Check your email'));
    const fresh = (fs.existsSync(MAIL) ? fs.readdirSync(MAIL) : []).filter(f => !before.has(f));
    ok('forgot password: one email written', fresh.length === 1, fresh.join(','));
    if (fresh.length) {
      let raw = fs.readFileSync(path.join(MAIL, fresh[0]), 'utf8');
      const isB64 = /Content-Transfer-Encoding: base64/i.test(raw);
      let body = raw.split(/\r?\n\r?\n/).slice(1).join('\n');
      body = isB64 ? Buffer.from(body.replace(/[^A-Za-z0-9+/=]/g, ''), 'base64').toString('utf8')
                   : body.replace(/=\r?\n/g, '').replace(/=([0-9A-F]{2})/g, (_, h) => String.fromCharCode(parseInt(h, 16)));
      const link = ((body.match(/href="([^"]*ResetPassword[^"]*)"/) || [])[1] || '').replace(/&amp;/g, '&');
      ok('email has a reset link and no password', !!link && !body.includes('Agent@1234x'));
      await V.page.goto(link, { waitUntil: 'networkidle2' });
      ok('reset page prefilled with the email', (await V.page.$eval('#Input_Email', e => e.value)) === `agent.${ts}@michuhomes.local`);
      await V.page.type('#Input_Password', 'Agent@5678y');
      await V.page.type('#Input_ConfirmPassword', 'Agent@5678y');
      html = await submit(V.page, 'button[type="submit"]');
      ok('password changed page', html.includes('Password changed'));
      const G2 = await session(browser);
      ok('new password signs in', (await login(G2.page, `agent.${ts}@michuhomes.local`, 'Agent@5678y')) !== '/Identity/Account/Login');
      await G2.ctx.close();
    }
    r = await open(V.page, '/Identity/Account/ResendEmailConfirmation');
    ok('resend confirmation page exists', r.status === 200);

    // ---------- Audit log ----------
    r = await open(A.page, '/Staff/Audit');
    ok('audit: platform sees role, user and company entries', r.html.includes(`Support officer ${ts}`) && r.html.includes(`support.${ts}@michuhomes.local`) && r.html.includes('Amal Micro Finance'));
    r = await open(C.page, '/Staff/Audit');
    ok('audit: company sees its own entries only', r.html.includes(`Sales agent ${ts}`) && !r.html.includes(`Support officer ${ts}`));

    const scriptErrors = [A, S, C, G, P, U, V].flatMap(s => s.page.errors);
    ok('no script errors', scriptErrors.length === 0, scriptErrors.slice(0, 3).join(' | '));
  } catch (e) {
    console.log('ERROR', e.stack);
    failures++;
  } finally {
    await browser.close();
    console.log(failures === 0 ? 'ALL PASSED' : `${failures} FAILED`);
    process.exit(failures === 0 ? 0 : 1);
  }
})();
