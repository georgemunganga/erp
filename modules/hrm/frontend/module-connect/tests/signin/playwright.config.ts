import { defineConfig } from '@playwright/test';
export default defineConfig({testDir:'.',testMatch:'*.spec.ts',workers:1,retries:0,reporter:'list',use:{baseURL:process.env.PLAYWRIGHT_BASE_URL || 'http://127.0.0.1:3302',browserName:'chromium',screenshot:'only-on-failure',trace:'retain-on-failure'},outputDir:process.env.SIGNIN_TEST_OUTPUT || 'test-results/signin'});
