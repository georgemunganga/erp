import { defineConfig, devices } from '@playwright/test';
import path from 'node:path';

if (process.env.DEBUG || process.env.PWDEBUG || process.env.NODE_DEBUG)
  throw Error('Debug logging forbidden for private CSV cookie proof');
const result = process.env.BE01_PRIVATE_BROWSER_RESULT;
if (!result || !path.isAbsolute(result)) throw Error('Protected browser result prefix required');

export default defineConfig({
  testDir: '.',
  testMatch: 'browser-export.spec.ts',
  testIgnore: '**/superseded*/**',
  workers: 1, fullyParallel: false, retries: 0, maxFailures: 1,
  timeout: 180_000, expect: { timeout: 20_000 }, reporter: 'list',
  outputDir: path.join(path.dirname(result), 'pw-artifacts-be01'),
  use: { ignoreHTTPSErrors: true, trace: 'off', screenshot: 'off', video: 'off',
    actionTimeout: 25_000, acceptDownloads: true },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
