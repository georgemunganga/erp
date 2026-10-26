import { expect, test } from '@playwright/test';
import { createHash } from 'node:crypto';
import { lstatSync, readFileSync, realpathSync, writeFileSync } from 'node:fs';
import path from 'node:path';

// Executes only against the accepted private HTTPS proxy and current compiled UI.
// The Python owner verifier is a separate required gate.
type Fixture = {
  schemaVersion: 1; status: string; productSourceCommit: string; databaseName: string;
  proxyOrigin: string; companyAId?: string; branchAId?: string;
  core: { companyAId: string; branchAId: string };
  datasets: { filterQuery: { items: string } };
  actors: Record<string, { principalId: string; credentialKey: string }>;
  credentialsFile: { path: string; sha256: string };
};
const guid = /^[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/;
const sha = (bytes: Buffer) => createHash('sha256').update(bytes).digest('hex');
function csvRowCount(bytes: Buffer): number {
  if (!bytes.subarray(0, 3).equals(Buffer.from([0xef, 0xbb, 0xbf])))
    throw Error('CSV BOM missing');
  const text = bytes.subarray(3).toString('utf8');
  let quoted = false;
  let lines = 0;
  for (let index = 0; index < text.length; index++) {
    if (text[index] === '"') {
      if (quoted && text[index + 1] === '"') index++;
      else quoted = !quoted;
    } else if (!quoted && text[index] === '\r' && text[index + 1] === '\n') {
      lines++;
      index++;
    }
  }
  if (quoted || !text.endsWith('\r\n') || lines < 1) throw Error('CSV record boundary invalid');
  return lines - 1;
}
function protectedFile(name: string): Buffer {
  const location = process.env[name];
  if (!location || !path.isAbsolute(location) || realpathSync(location) !== location ||
    !lstatSync(location).isFile() || lstatSync(location).isSymbolicLink() ||
    (lstatSync(location).mode & 0o777) !== 0o600) throw Error('Protected BE-01 input required');
  return readFileSync(location);
}
const fixtureBytes = protectedFile('BE01_PRIVATE_FIXTURE');
const fixture = JSON.parse(fixtureBytes.toString()) as Fixture;
const credentialsBytes = protectedFile('BE01_PRIVATE_CREDENTIALS');
if (fixture.schemaVersion !== 1 || fixture.status !== 'READY_PRIVATE_PROCUREMENT_CSV_EXPORT_FIXTURE' ||
  fixture.productSourceCommit !== '86ed727774853140fee93a35aa6ce05362e71edf' ||
  !/^proc_csv_export_20261025_[a-z0-9_]{1,24}$/.test(fixture.databaseName) ||
  fixture.credentialsFile.path !== process.env.BE01_PRIVATE_CREDENTIALS ||
  fixture.credentialsFile.sha256 !== sha(credentialsBytes) ||
  !fixture.proxyOrigin.startsWith('https://127.0.0.1:') ||
  fixture.datasets?.filterQuery?.items !== '?search=BE01FIELD' ||
  !guid.test(fixture.core.companyAId) || !guid.test(fixture.core.branchAId))
  throw Error('Reviewed private BE-01 fixture required');
const credentials = JSON.parse(credentialsBytes.toString()) as Record<string, { email: string; password: string }>;
const resultPath = process.env.BE01_PRIVATE_BROWSER_RESULT;
if (!resultPath || !path.isAbsolute(resultPath) || path.dirname(resultPath) !==
  path.dirname(fixture.credentialsFile.path)) throw Error('Protected browser result path required');
const scope = { 'X-Shell-Entity': fixture.core.companyAId, 'X-Shell-Location': fixture.core.branchAId };

for (const entry of [
  { actor: 'approver', section: 'vendors', route: '/procurement/workspace/vendors', choice: 'Export Vendors' },
  { actor: 'manager', section: 'items', route: '/procurement/workspace/items', choice: 'Export Items' },
] as const) {
  test(`actual ${entry.section} menu exports through normal cookie`, async ({ page, context }) => {
    const exportRequests: string[] = [];
    page.on('request', request => {
      const url = new URL(request.url());
      if (url.pathname === `/api/procurement/v1/${entry.section}/export`)
        exportRequests.push(url.search);
    });
    const user = fixture.actors[entry.actor];
    const credential = credentials[user.credentialKey];
    expect(user.principalId).toMatch(guid);
    expect(credential.email).toBeTruthy();
    expect(credential.password).toBeTruthy();
    const login = await context.request.post(`${fixture.proxyOrigin}/api/hrm/auth/login`, {
      data: { email: credential.email, password: credential.password },
      failOnStatusCode: false,
    });
    expect(login.status()).toBe(200);
    const me = await context.request.get(`${fixture.proxyOrigin}/api/hrm/auth/me`);
    expect((await me.json()).user.id).toBe(user.principalId);
    await page.addInitScript(({ entityId, branch }) =>
      localStorage.setItem('erp.shell.state.v1', JSON.stringify({ entityId, branch })),
      { entityId: fixture.core.companyAId, branch: fixture.core.branchAId });
    await page.goto(`${fixture.proxyOrigin}${entry.route}`);
    await page.getByRole('button', { name: 'More Actions' }).click();
    await page.getByRole('menuitem', { name: 'Export', exact: true }).click();
    await expect(page.getByRole('menuitem', { name: /Selected/ })).toBeDisabled();
    let query = '';
    if (entry.section === 'items') {
      // All 10,001 seeded items must be rejected without starting a download.
      const unexpectedDownloads: string[] = [];
      page.on('download', download => unexpectedDownloads.push(download.suggestedFilename()));
      const limitResponsePromise = page.waitForResponse(r =>
        r.url().includes('/api/procurement/v1/items/export') &&
        new URL(r.url()).search === '');
      await page.getByRole('menuitem', { name: 'Export Items' }).click();
      const limitResponse = await limitResponsePromise;
      expect(limitResponse.status()).toBe(422);
      const limitHeaders = await limitResponse.request().allHeaders();
      expect(limitHeaders['x-shell-entity']).toBe(fixture.core.companyAId);
      expect(limitHeaders['x-shell-location']).toBe(fixture.core.branchAId);
      expect(limitHeaders['x-shell-branch-scope']).toBeUndefined();
      await expect(page.getByRole('alert')).toHaveText('Too many records match. Narrow your search and try again.');
      expect(unexpectedDownloads).toEqual([]);
      await page.getByRole('textbox', { name: 'Search items' }).fill('BE01FIELD');
      await page.getByRole('button', { name: 'Search', exact: true }).click();
      await page.getByRole('button', { name: 'More Actions' }).click();
      await page.getByRole('menuitem', { name: 'Export', exact: true }).click();
      query = fixture.datasets.filterQuery.items;
    }
    const responsePromise = page.waitForResponse(r =>
      r.url().includes(`/api/procurement/v1/${entry.section}/export`) &&
      new URL(r.url()).search === query);
    const downloadPromise = page.waitForEvent('download');
    await page.getByRole('menuitem', { name: entry.section === 'items'
      ? 'Search results (default order)' : entry.choice }).click();
    const response = await responsePromise;
    const requestHeaders = await response.request().allHeaders();
    expect(requestHeaders['x-shell-entity']).toBe(fixture.core.companyAId);
    expect(requestHeaders['x-shell-location']).toBe(fixture.core.branchAId);
    expect(requestHeaders['x-shell-branch-scope']).toBeUndefined();
    const download = await downloadPromise;
    expect(response.status()).toBe(200);
    expect(response.headers()['cache-control']).toBe('no-store');
    expect(response.headers()['content-type']).toMatch(/^text\/csv/);
    expect(response.headers()['x-export-id']).toMatch(guid);
    expect(download.suggestedFilename()).toMatch(new RegExp(`^${entry.section}-[0-9]{8}\\.csv$`));
    // Response.body() can make Playwright issue a second credentialed GET for
    // downloads. Hash only the browser's actual downloaded artifact.
    const stream = await download.createReadStream();
    const chunks: Buffer[] = [];
    for await (const chunk of stream)
      chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
    const bytes = Buffer.concat(chunks);
    expect(bytes.subarray(0, 3)).toEqual(Buffer.from([0xef, 0xbb, 0xbf]));
    const rowCount = csvRowCount(bytes);
    expect(exportRequests).toEqual(entry.section === 'items' ? ['', query] : ['']);
    const logout = await context.request.post(`${fixture.proxyOrigin}/api/hrm/auth/logout`, {
      headers: scope, failOnStatusCode: false,
    });
    expect(logout.status()).toBe(200);
    const record = { schemaVersion: 1, status: 'BROWSER_UI_COOKIE_EXPORT_ONLY_OWNER_PENDING',
      database: fixture.databaseName, actor: entry.actor, section: entry.section,
      exportId: response.headers()['x-export-id'], rowCount, payloadSha256: sha(bytes), query,
      sourceFixtureSha256: sha(fixtureBytes), productionTouched: false };
    writeFileSync(`${resultPath}.${entry.section}`, JSON.stringify(record) + '\n',
      { encoding: 'utf8', flag: 'wx', mode: 0o600 });
  });
}
